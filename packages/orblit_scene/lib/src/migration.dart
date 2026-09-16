import 'dart:math' as math;

import 'package:orblit_light/orblit_light.dart' show Tint;
import 'package:orblit_weather/orblit_weather.dart'
    show WeatherCondition, WeatherState;

import 'component.dart';
import 'components/staging.dart';
import 'document.dart';
import 'values.dart';

/// One step from an older scene format to the next.
///
/// A list of these rather than conversions scattered through the reader,
/// which is what this started as. The difference shows the third time the
/// format moves: inline conversions have to be read in the reverse order they
/// were written, each one guarding on a version, and a field touched by two of
/// them is only correct if they happen to be in the right order in the
/// function. As steps they compose by construction — a version-one file runs
/// every one of them in turn and arrives at the same place a version-three
/// file does after running the last.
///
/// Each works on decoded JSON rather than on objects. That is deliberate: a
/// migration's whole job is to read a shape this version of the code does not
/// have a type for, and giving it one would mean keeping every retired type
/// alive forever.
abstract class SceneMigration {
  const SceneMigration();

  /// The version this reads.
  int get from;

  /// The version it writes, always [from] + 1.
  int get to => from + 1;

  /// The file, moved on one version.
  ///
  /// Anything worth telling somebody about goes in [notes]. A migration that
  /// silently changes what a scene looks like is worse than one that refuses:
  /// people believe what they see, and a scene that quietly re-lit itself gets
  /// re-authored around the change.
  Map<String, Object?> apply(Map<String, Object?> json, List<String> notes);
}

/// Every migration there is, oldest first.
abstract final class SceneMigrations {
  static const List<SceneMigration> ordered = [
    _PowerInWatts(),
    _FogOnTheScene(),
    _ObjectsWithKinds(),
  ];

  /// A file at [version], brought up to [SceneDocument.formatVersion].
  ///
  /// The shape that predates versioning is normalised first, because it has no
  /// honest version of its own — it declares whatever it was written beside.
  static Map<String, Object?> run(
    Map<String, Object?> json,
    int version,
    List<String> notes,
  ) {
    final start = _beforeVersions(json, version, notes);
    var current = start.json;
    for (final migration in ordered) {
      if (migration.from < start.version) continue;
      current = migration.apply(current, notes);
    }
    return current;
  }

  /// Scenes written before objects carried transforms.
  ///
  /// The shape only existed briefly and it holds names but no positions, so it
  /// is converted rather than refused — and the conversion says what it could
  /// not recover instead of leaving somebody to wonder why everything is
  /// stacked at the origin.
  ///
  /// It takes the name and nothing else. The scene's own settings did not
  /// exist yet, and a file from then carrying a `sky` key is carrying one that
  /// meant something else.
  ///
  /// Gated on the version because version four calls its list `entities` too,
  /// and means something entirely different by it. The two can only be told
  /// apart by what the file says it is.
  ///
  /// It reports version three back, whatever the file claimed. The claim is
  /// not trustworthy — this shape predates the versions and files carrying it
  /// declare whatever number was current when they were written — and what it
  /// produces is already the version-three shape: ids, names and kinds, no fog
  /// block and no light stated in watts. Running the earlier migrations over
  /// it would divide a default sun's power by a sphere it was never spread on
  /// and hand somebody a scene twelve times darker than the one they saved.
  static ({Map<String, Object?> json, int version}) _beforeVersions(
    Map<String, Object?> json,
    int version,
    List<String> notes,
  ) {
    if (version >= 4) return (json: json, version: version);
    if (json['objects'] != null || json['entities'] is! List) {
      return (json: json, version: version);
    }

    final objects = <Map<String, Object?>>[];
    for (final (index, entry) in (json['entities']! as List).indexed) {
      if (entry is! Map<String, Object?>) continue;
      final components = entry['components'];
      final named = components is List ? components.join(' ') : '';

      objects.add({
        'id': 'legacy$index',
        'name': Values.text(entry, 'name') ?? 'Object',
        // The old shape said what components a thing had, which is enough to
        // tell a light from a camera from everything else.
        'kind': named.contains('Light')
            ? 'light'
            : (named.contains('Camera') ? 'camera' : 'mesh'),
      });
    }

    if (objects.isNotEmpty) {
      notes.add(
        'This scene was written before Orblit stored positions, so its '
        '${objects.length} objects are all at the origin.',
      );
    }

    return (
      json: {
        if (json['name'] != null) 'name': json['name'],
        'objects': objects,
      },
      version: 3,
    );
  }
}

/// Version one stated every light's power in watts.
///
/// And converted them all as if they were bulbs — a wattage spread over a
/// sphere. A sun is not a bulb a long way off: it is a sheet of light arriving
/// parallel, stated in watts per square metre. Version two tells them apart,
/// so the same look is the old number over that sphere.
class _PowerInWatts extends SceneMigration {
  const _PowerInWatts();

  @override
  int get from => 1;

  @override
  Map<String, Object?> apply(Map<String, Object?> json, List<String> notes) {
    final objects = json['objects'];
    if (objects is! List) return json;

    var converted = 0;
    for (final entry in objects) {
      if (entry is! Map<String, Object?>) continue;
      if (entry['kind'] != 'light') continue;
      // Every light in a version-one file was drawn as a sun, whatever it
      // called itself, so that is what an unnamed one is read back as.
      final kind = Values.text(entry, 'lightType') ?? 'sun';
      if (kind != 'sun') continue;

      entry['power'] = Values.number(entry, 'power', 1000) / (4 * math.pi);
      converted++;
    }

    if (converted > 0) {
      notes.add(
        converted == 1
            ? 'One sun in this scene was stated in watts, and is now stated in '
                  'watts per square metre. It is the same brightness.'
            : '$converted suns in this scene were stated in watts, and are now '
                  'stated in watts per square metre. They are the same '
                  'brightness.',
      );
    }

    return json;
  }
}

/// Version two kept the air in a block on the scene itself.
///
/// Weather is a thing that changes, and a change needs somewhere to live that
/// can hold both ends of it — a set of fields on the scene can only hold one.
/// So the old fog becomes an object, which version four then makes a component.
class _FogOnTheScene extends SceneMigration {
  const _FogOnTheScene();

  @override
  int get from => 2;

  @override
  Map<String, Object?> apply(Map<String, Object?> json, List<String> notes) {
    final fog = Values.object(json['fog']);
    final density = Values.number(fog, 'density', 0);
    json.remove('fog');
    if (density <= 0) return json;

    final objects = json['objects'];
    if (objects is! List) return json;

    final taken = {
      for (final entry in objects)
        if (entry is Map<String, Object?>) Values.text(entry, 'id'),
    };
    final mist = Values.number(fog, 'mist', 0);

    final air = WeatherState(
      // Nothing in the old shape said anything about cloud, so a converted
      // scene starts with a clear sky over its fog.
      cloudCover: 0,
      fogColour: Values.tint(fog['colour'], fallback: const Tint.hex(0x7D8794)),
      fogDensity: density,
      fogHeight: Values.number(fog, 'height', 0),
      fogFalloff: Values.number(fog, 'falloff', 0.2),
      mist: mist,
      mistSize: Values.number(fog, 'mistSize', 30),
      // The old drift was a rate the layer breathed at rather than a speed
      // across the ground. Four metres a second per unit of it is what makes a
      // scene look about as windy as it did.
      windSpeed: Values.number(fog, 'mistSpeed', 0.08) * 4,
    );

    objects.add({
      'id': freeId(taken, 'weather'),
      'name': 'Weather',
      'kind': 'weather',
      'condition':
          (mist > 0 ? WeatherCondition.misty : WeatherCondition.hazy).name,
      'air': WeatherComponent.airToJson(air),
    });

    notes.add(
      'The fog in this scene is now a Weather object, which can also do cloud '
      'and wind.',
    );

    return json;
  }

  /// An id nothing is already using, for an object a conversion invents.
  static String freeId(Set<String?> taken, String wanted) {
    if (!taken.contains(wanted)) return wanted;
    var attempt = 2;
    while (taken.contains('$wanted$attempt')) {
      attempt++;
    }
    return '$wanted$attempt';
  }
}

/// Version three said what an object *was*, and carried every kind's fields on
/// every object.
///
/// A light held a mesh path it would never use; a camera held a shadow flag.
/// Worse, a thing that was two of them at once could not be said at all — a
/// lamp is a mesh and a light, and the old shape made somebody build it out of
/// two objects and keep them in step by hand. Version four gives an entity a
/// set of components and drops the kind.
class _ObjectsWithKinds extends SceneMigration {
  const _ObjectsWithKinds();

  /// The kinds version three had, and what each one becomes.
  static const Set<String> kinds = {
    'scene',
    'mesh',
    'light',
    'camera',
    'group',
    'weather',
    'canvas',
    'shape',
  };

  @override
  int get from => 3;

  @override
  Map<String, Object?> apply(Map<String, Object?> json, List<String> notes) {
    final objects = json['objects'];
    if (objects is! List) {
      throw const SceneFormatException(
        'This scene has no list of objects in it.',
      );
    }

    final entities = <Map<String, Object?>>[];
    final seen = <String>{};

    for (final (index, entry) in objects.indexed) {
      if (entry is! Map<String, Object?>) {
        notes.add('Object $index is not an object, and was left out.');
        continue;
      }

      final id = Values.text(entry, 'id');
      if (id == null || id.isEmpty) {
        notes.add('Object $index has no id, and was left out.');
        continue;
      }
      if (!seen.add(id)) {
        notes.add('Two objects share the id "$id"; the second was dropped.');
        continue;
      }

      final kind = entry['kind'];
      if (kind is! String || !kinds.contains(kind)) {
        notes.add(
          'Object "$id" is a "$kind", which this editor does not know about. '
          'It was left out.',
        );
        continue;
      }

      entities.add(_entity(entry, id: id, kind: kind));
    }

    return Values.pruned({
      'name': json['name'],
      'sky': json['sky'],
      'ambient': json['ambient'],
      'time': json['time'],
      'entities': entities,
    });
  }

  /// One version-three object as an entity with components.
  static Map<String, Object?> _entity(
    Map<String, Object?> json, {
    required String id,
    required String kind,
  }) {
    final components = <String, Object?>{
      // Every object in version three carried a transform, including the ones
      // that had no use for one, so every entity out of it gets one. Reading
      // the defaults in rather than leaving them out keeps a converted scene
      // identical to what it was, which matters more here than tidiness.
      SceneComponents.transform: {
        'position': Values.vectorToJson(Values.vector(json['position'])),
        'rotation': Values.vectorToJson(Values.vector(json['rotation'])),
        'scale': Values.vectorToJson(Values.vector(json['scale'], fallback: 1)),
      },
    };

    final drawable = kind == 'mesh' || kind == 'shape';
    if (drawable) {
      components[SceneComponents.mesh] = Values.pruned({
        'asset': json['mesh'],
        'shape': json['shape'],
        'geometry': json['geometry'],
        'outline': json['outline'],
        'boundary': json['boundary'],
        'surfaces': json['surfaces'],
        'colour': Values.tintToJson(Values.tint(json['colour'])),
        'castShadows': Values.flag(json, 'castShadows', fallback: true),
        'receiveShadows': Values.flag(json, 'receiveShadows', fallback: true),
        'sway': Values.number(json, 'sway', 0) > 0
            ? Values.number(json, 'sway', 0)
            : null,
      });
    }

    if (kind == 'light') {
      components[SceneComponents.light] = {
        // Version three called this `lightType`; a component is already a
        // light, so the field says which sort rather than repeating itself.
        'kind': Values.text(json, 'lightType') ?? 'sun',
        'power': Values.number(json, 'power', 1000),
        'colour': Values.tintToJson(Values.tint(json['colour'])),
        'spotSize': Values.number(json, 'spotSize', 45),
        'spotBlend': Values.number(json, 'spotBlend', 0.15),
        'sourceRadius': Values.number(json, 'sourceRadius', 0.1),
        'sunAngle': Values.number(json, 'sunAngle', 0.526),
        'body': Values.text(json, 'body') ?? 'sun',
        'castShadows': Values.flag(json, 'castShadows', fallback: true),
      };
    }

    if (kind == 'camera') {
      components[SceneComponents.camera] = const CameraComponent().toJson();
    }

    if (kind == 'weather') {
      final condition = Values.text(json, 'condition') ?? 'clear';
      components[SceneComponents.weather] = Values.pruned({
        'condition': condition,
        'cloudKind': json['cloudKind'],
        'windDirection': Values.number(json, 'windDirection', 135),
        'transition': Values.number(json, 'transition', 8),
        'air': WeatherComponent.airToJson(
          WeatherComponent.airFromJson(
            json['air'],
            Values.named(WeatherCondition.values, condition) ??
                WeatherCondition.clear,
          ),
        ),
      });
    }

    // A material, an interface and data files were never tied to a kind in
    // version three, so they are carried across for whatever object had them
    // rather than only for the kind that usually did.
    if (json['material'] is String) {
      components[SceneComponents.material] = {'asset': json['material']};
    }
    if (kind == 'canvas' || json['interface'] is String) {
      components[SceneComponents.canvas] = Values.pruned({
        'asset': json['interface'],
        'scale': 1.0,
      });
    }
    if (json['prefab'] is String) {
      components[SceneComponents.prefab] = {'asset': json['prefab']};
    }
    final data = Values.texts(json['data']);
    if (data.isNotEmpty) {
      components[SceneComponents.data] = {'paths': data};
    }

    return Values.pruned({
      'id': id,
      'name': Values.text(json, 'name') ?? id,
      'parent': json['parent'],
      'visible': Values.flag(json, 'visible', fallback: true) ? null : false,
      'components': components,
    });
  }
}
