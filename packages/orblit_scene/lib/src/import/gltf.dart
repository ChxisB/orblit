import 'dart:math' as math;
import 'dart:typed_data';

import 'package:orblit_light/orblit_light.dart';
import 'package:orblit_mesh/orblit_mesh.dart';
import 'package:vector_math/vector_math_64.dart';

import '../component.dart';
import '../components/drawing.dart';
import '../components/staging.dart';
import '../components/transform.dart';
import '../document.dart';
import '../entity.dart';
import '../migration.dart';

/// A scene read out of a glTF document.
///
/// [problems] is returned rather than thrown for the reason every other reader
/// in this package returns it: a document with one node nobody could make
/// sense of is still a scene worth opening, and the caller is the one who
/// knows whether to show the list to somebody or fail a build over it.
class SceneImported {
  const SceneImported({
    required this.document,
    this.problems = const [],
    this.wasWrittenHere = false,
  });

  final SceneDocument document;

  /// What could not be carried across, in the order it was met.
  final List<String> problems;

  /// Whether the document carried Orblit's own record of the scene.
  ///
  /// True means this is the scene that was exported, with its ids, its
  /// components and its settings — not an interpretation of one. False means
  /// the document came from somewhere else and what is here was worked out
  /// from the parts of glTF that have an equivalent, which is worth saying out
  /// loud before somebody saves it over the original.
  final bool wasWrittenHere;

  bool get hasProblems => problems.isNotEmpty;
}

/// Reads [json] back into a scene.
///
/// [binary] is the buffer the document refers to — a GLB's `BIN` chunk, or the
/// `.bin` beside a `.gltf`. It is only read for a document written elsewhere,
/// since one written here states its geometry in the scene's own terms and
/// does not need the accessors decoded to get it back.
///
/// The exporter writes every entity's id, visibility and components onto the
/// node's `extras.orblit`, so a document that came from `sceneToGltf` comes
/// back as the scene it was rather than as a flattened photograph of one. A
/// document from anywhere else is read from the parts of glTF that have an
/// equivalent here: the tree, the names, the transforms, the lights, the
/// cameras, and triangles as editable geometry.
SceneImported gltfToScene(
  Map<String, Object?> json, {
  Uint8List? binary,
  String name = 'Scene',
}) => _Reader(json, binary, name).read();

/// Where this package writes its own record, and where it looks for it.
const String _mark = 'orblit';

class _Reader {
  _Reader(this.json, this.binary, this.fallbackName);

  final Map<String, Object?> json;
  final Uint8List? binary;
  final String fallbackName;

  final List<String> problems = [];

  /// The document's bytes, read into [problems] so a bad accessor is noted
  /// alongside everything else.
  late final GltfAccessors _numbers = GltfAccessors(
    json,
    binary,
    problems: problems,
  );

  late final List<Map<String, Object?>> nodes = _maps(json['nodes']);
  late final List<Map<String, Object?>> materials = _maps(json['materials']);
  late final List<Map<String, Object?>> cameras = _maps(json['cameras']);
  late final List<Map<String, Object?>> meshes = _maps(json['meshes']);

  /// The node each node hangs from, by index.
  final Map<int, int> parentOf = {};

  /// Which nodes became entities, and what each one's id is.
  final Map<int, String> idOf = {};

  SceneImported read() {
    for (var at = 0; at < nodes.length; at++) {
      for (final child in _ints(nodes[at]['children'])) {
        if (child >= 0 &&
            child < nodes.length &&
            child != at &&
            !parentOf.containsKey(child)) {
          parentOf[child] = at;
        }
      }
    }

    final scene = _scene();
    final ours = nodes.any((node) => _orblit(node) != null);

    // Which nodes are entities is settled before any of them is built,
    // because a parent has to be known to be an entity before anything can be
    // attached to it — and in a document we wrote, the grafted copy of an
    // imported model is a subtree of nodes that are not entities at all and
    // must not become any. The component that names the model is already on
    // the node above them.
    for (var at = 0; at < nodes.length; at++) {
      if (ours && _orblit(nodes[at]) == null) continue;
      idOf[at] = ours ? _ourId(at) : 'node-$at';
    }

    if (!ours && nodes.isNotEmpty) {
      problems.add(
        'This glTF was not written by Orblit, so the scene was worked out '
        'from the parts of the format that have an equivalent here. '
        'Materials, textures, skins and animation were not carried across.',
      );
    }

    final entities = <SceneEntity>[];
    for (var at = 0; at < nodes.length; at++) {
      final id = idOf[at];
      if (id == null) continue;
      entities.add(ours ? _restore(at, id) : _interpret(at, id));
    }

    return SceneImported(
      document: SceneDocument(
        name: _text(scene?['name']) ?? fallbackName,
        settings: _settings(scene),
        entities: entities,
      ),
      problems: problems,
      wasWrittenHere: ours,
    );
  }

  /// The scene the document points at, or its first, or none.
  Map<String, Object?>? _scene() {
    final all = _maps(json['scenes']);
    if (all.isEmpty) return null;
    final at = json['scene'];
    if (at is int && at >= 0 && at < all.length) return all[at];
    return all.first;
  }

  SceneSettings _settings(Map<String, Object?>? scene) {
    final settings = scene == null ? null : _orblit(scene)?['settings'];
    if (settings is Map<String, Object?>) {
      return SceneSettings.fromJson(settings);
    }
    return const SceneSettings();
  }

  // --- A document this package wrote ---------------------------------------

  String _ourId(int at) {
    final id = _text(_orblit(nodes[at])?['id']);
    // A node of ours with no id is a document somebody edited by hand. It
    // still has its components, so it is worth keeping under a made-up id
    // rather than dropping — what it loses is anything that pointed at it.
    if (id == null || id.isEmpty) {
      problems.add('Node $at carried Orblit components but no id.');
      return 'node-$at';
    }
    return id;
  }

  /// The scene format the components on each node were written in.
  ///
  /// A document from before the exporter wrote one down was written at
  /// version four, the one the exporter arrived at.
  late final int _version = () {
    final scene = _scene();
    final stated = scene == null ? null : _orblit(scene)?['formatVersion'];
    return stated is int ? stated : 4;
  }();

  SceneEntity _restore(int at, String id) {
    final node = nodes[at];
    final ours = _orblit(node)!;

    final components = <String, SceneComponent>{};
    final raw = SceneMigrations.entity(
      {'id': id, 'components': ours['components']},
      version: _version,
      notes: problems,
    )?['components'];
    if (raw is Map<String, Object?>) {
      for (final entry in raw.entries) {
        final value = entry.value;
        if (value is Map<String, Object?>) {
          components[entry.key] = SceneComponents.read(entry.key, value);
        }
      }
    }

    return SceneEntity(
      id: id,
      name: _text(node['name']) ?? id,
      parent: _parentId(at),
      visible: ours['visible'] != false,
      components: components,
    );
  }

  /// The id of the nearest ancestor that is an entity.
  ///
  /// Nearest rather than immediate, because a document somebody else has been
  /// through may have nodes between two of ours — a tool that wrapped every
  /// object in an axis-correcting parent is the common one — and dropping the
  /// child out of the tree over it would be worse than reattaching it a level
  /// up.
  String? _parentId(int at) {
    var walk = parentOf[at];
    final seen = <int>{at};
    while (walk != null && seen.add(walk)) {
      final id = idOf[walk];
      if (id != null) return id;
      walk = parentOf[walk];
    }
    return null;
  }

  // --- A document from somewhere else --------------------------------------

  SceneEntity _interpret(int at, String id) {
    final node = nodes[at];
    final components = <String, SceneComponent>{};

    final placed = _transform(node);
    if (placed != null) components[SceneComponents.transform] = placed;

    final mesh = _mesh(node);
    if (mesh != null) components[SceneComponents.mesh] = mesh;

    final light = _light(node);
    if (light != null) components[SceneComponents.light] = light;

    final camera = _camera(node);
    if (camera != null) components[SceneComponents.camera] = camera;

    return SceneEntity(
      id: id,
      name: _text(node['name']) ?? 'Node $at',
      parent: _parentId(at),
      components: components,
    );
  }

  TransformComponent? _transform(Map<String, Object?> node) {
    final matrix = _doubles(node['matrix']);
    if (matrix.length == 16) {
      // Column-major, which is how glTF states it and how vector_math reads a
      // flat list, so the two need no rearranging between them.
      final m = Matrix4.fromList(matrix);
      final scale = Vector3(
        m.getColumn(0).xyz.length,
        m.getColumn(1).xyz.length,
        m.getColumn(2).xyz.length,
      );
      final rotation = m.getRotation();
      for (var column = 0; column < 3; column++) {
        final by = scale[column];
        if (by == 0) continue;
        for (var row = 0; row < 3; row++) {
          rotation.setEntry(row, column, rotation.entry(row, column) / by);
        }
      }
      return TransformComponent(
        position: m.getTranslation(),
        rotation: TransformComponent.anglesOf(rotation),
        scale: scale,
      );
    }

    final translation = _doubles(node['translation']);
    final quaternion = _doubles(node['rotation']);
    final scale = _doubles(node['scale']);
    if (translation.length != 3 &&
        quaternion.length != 4 &&
        scale.length != 3) {
      return null;
    }

    return TransformComponent(
      position: translation.length == 3
          ? Vector3(translation[0], translation[1], translation[2])
          : null,
      rotation: quaternion.length == 4
          ? TransformComponent.anglesOf(
              Quaternion(
                quaternion[0],
                quaternion[1],
                quaternion[2],
                quaternion[3],
              ).asRotationMatrix(),
            )
          : null,
      scale: scale.length == 3 ? Vector3(scale[0], scale[1], scale[2]) : null,
    );
  }

  LightComponent? _light(Map<String, Object?> node) {
    final on = _object(_object(node['extensions'])['KHR_lights_punctual']);
    final at = _index(on['light']);
    if (at == null) return null;

    final all = _list(
      _object(_object(json['extensions'])['KHR_lights_punctual'])['lights'],
    );
    if (at >= all.length) {
      problems.add('A node names light $at, which the document does not have.');
      return null;
    }
    final light = _object(all[at]);
    final ours = _orblit(light);

    final kind = switch (_text(light['type'])) {
      'directional' => LightType.sun,
      'spot' => LightType.spot,
      // An area light is written as a point, because glTF has no word for a
      // shape that emits. What says it was one is the note the exporter left.
      _ => _text(ours?['kind']) == 'area' ? LightType.area : LightType.point,
    };

    // The exporter's conversion, run backwards. A sun's strength is an
    // illuminance and everything else's is an intensity, which is the one
    // place the two units part company.
    final intensity = _number(light['intensity']) ?? 1;
    final power = kind == LightType.sun
        ? intensity / Photometry.luminousEfficacy
        : Photometry.candelaToLumens(intensity) / Photometry.luminousEfficacy;

    final spot = _object(light['spot']);
    final outer = _number(spot['outerConeAngle']) ?? math.pi / 4;
    final inner = _number(spot['innerConeAngle']) ?? 0;
    final colour = _doubles(light['color']);

    return LightComponent(
      kind: kind,
      power: power,
      colour: colour.length == 3
          ? Tint.fromLinear(Vector3(colour[0], colour[1], colour[2]))
          : Tint.white,
      spotSize: degrees(outer) * 2,
      spotBlend: outer <= 0 ? 0 : (1 - inner / outer).clamp(0.0, 1.0),
      sourceRadius: _number(ours?['sourceRadius']) ?? 0.1,
    );
  }

  CameraComponent? _camera(Map<String, Object?> node) {
    final at = _index(node['camera']);
    if (at == null || at >= cameras.length) return null;

    final camera = cameras[at];
    if (_text(camera['type']) == 'orthographic') {
      problems.add(
        'Camera $at is orthographic, which this engine has no equivalent for; '
        'it was read as a perspective one.',
      );
    }

    // The angle the exporter wrote down, when it is there. glTF states the
    // vertical angle and Orblit states the angle across the shorter side, so
    // the two agree on a landscape frame and part company on a portrait one —
    // and what was authored is worth more than a conversion either way.
    final lens = _object(camera['perspective']);
    final stated = _number(_orblit(camera)?['fieldOfView']);

    return CameraComponent(
      fieldOfView: stated ?? degrees(_number(lens['yfov']) ?? radians(50)),
      near: _number(lens['znear']) ?? 0.1,
      far: _number(lens['zfar']) ?? 1000,
    );
  }

  MeshComponent? _mesh(Map<String, Object?> node) {
    final at = _index(node['mesh']);
    if (at == null || at >= meshes.length) return null;

    final positions = <Vector3>[];
    final faces = <Face>[];
    var colour = const Tint.hex(0xD9634F);
    var toldAboutModes = false;

    for (final primitive in _maps(meshes[at]['primitives'])) {
      // 4 is a triangle list. A strip, a fan or a line is geometry this
      // engine's editable mesh has no way to hold, and quietly dropping a
      // third of a model is worse than saying which third went.
      final mode = _index(primitive['mode']) ?? 4;
      if (mode != 4) {
        if (!toldAboutModes) {
          problems.add(
            'Mesh $at has primitives that are not triangle lists; those were '
            'not read.',
          );
          toldAboutModes = true;
        }
        continue;
      }

      final corners = _vectors(
        _index(_object(primitive['attributes'])['POSITION']),
      );
      if (corners.isEmpty) continue;

      final first = positions.length;
      positions.addAll(corners);

      final indices = _index(primitive['indices']);
      final order = indices == null
          ? [for (var i = 0; i < corners.length; i++) i]
          : _numbers.indices(indices);
      for (var i = 0; i + 2 < order.length; i += 3) {
        faces.add(
          Face([first + order[i], first + order[i + 1], first + order[i + 2]]),
        );
      }

      final wears = _index(primitive['material']);
      if (wears != null && wears < materials.length) {
        final base = _doubles(
          _object(materials[wears]['pbrMetallicRoughness'])['baseColorFactor'],
        );
        if (base.length >= 3) {
          colour = Tint.fromLinear(Vector3(base[0], base[1], base[2]));
        }
      }
    }

    if (positions.isEmpty) return null;
    return MeshComponent(
      geometry: Mesh(positions: positions, faces: faces),
      colour: colour,
      // Geometry that arrived as corners and faces rather than as a file is
      // geometry this document owns, and something somebody can still pull a
      // face off.
      authored: true,
    );
  }

  // --- Accessors -----------------------------------------------------------

  List<Vector3> _vectors(int? accessor) {
    final numbers = _numbers.floats(accessor, 3);
    return [
      for (var i = 0; i + 2 < numbers.length; i += 3)
        Vector3(numbers[i], numbers[i + 1], numbers[i + 2]),
    ];
  }

  // --- Reading JSON that may be anything at all ----------------------------

  Map<String, Object?>? _orblit(Map<String, Object?> holder) {
    final extras = holder['extras'];
    if (extras is! Map<String, Object?>) return null;
    final ours = extras[_mark];
    return ours is Map<String, Object?> ? ours : null;
  }

  List<Map<String, Object?>> _maps(Object? raw) => [
    for (final item in _list(raw))
      if (item is Map<String, Object?>) item,
  ];

  List<Object?> _list(Object? raw) => raw is List ? raw : const [];

  Map<String, Object?> _object(Object? raw) =>
      raw is Map<String, Object?> ? raw : const {};

  String? _text(Object? raw) => raw is String ? raw : null;

  double? _number(Object? raw) => raw is num ? raw.toDouble() : null;

  int? _index(Object? raw) => raw is int && raw >= 0 ? raw : null;

  List<int> _ints(Object? raw) => [
    for (final item in _list(raw))
      if (item is int) item,
  ];

  List<double> _doubles(Object? raw) => [
    for (final item in _list(raw))
      if (item is num) item.toDouble(),
  ];
}
