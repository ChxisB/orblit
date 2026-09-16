import 'dart:convert';

import 'package:orblit_light/orblit_light.dart' show Tint;

import 'entity.dart';
import 'migration.dart';
import 'values.dart';

/// The extension a scene file carries.
const String sceneExtension = '.oscene';

/// Something that made a file unreadable as a whole.
///
/// Thrown, unlike everything else in here, because there is no partial answer
/// to give: a file that is not JSON, or that is JSON but is not a scene, has
/// nothing in it to salvage.
class SceneFormatException implements Exception {
  const SceneFormatException(this.message);

  final String message;

  @override
  String toString() => message;
}

/// A scene read back off disk, with anything that could not be read.
///
/// Problems are returned rather than thrown. A scene with one broken object is
/// still worth opening — losing the other ninety-nine because of it is the
/// worse outcome, and whoever opened it can be told what was dropped.
class SceneLoad {
  const SceneLoad({required this.document, this.problems = const []});

  final SceneDocument document;

  final List<String> problems;

  bool get hasProblems => problems.isNotEmpty;
}

/// The scene's own settings: the things that belong to it rather than to
/// anything in it.
class SceneSettings {
  const SceneSettings({
    this.sky = const Tint.hex(0x1A2029),
    this.ambient = 28000,
    this.timeOfDay = 10,
    this.dayCycle = false,
    this.hoursPerSecond = 0.5,
  });

  static SceneSettings fromJson(Map<String, Object?> json) {
    final time = Values.object(json['time']);
    return SceneSettings(
      // Absent and unreadable are different values here, and always have been.
      // A file that says nothing about its sky gets the one a new scene has;
      // a file that says something we cannot read gets the lighter grey the
      // reader has always fallen back to. Collapsing the two would re-light
      // every scene in the second case.
      sky: json['sky'] == null
          ? const Tint.hex(0x1A2029)
          : Values.tint(json['sky'], fallback: const Tint.hex(0x59616F)),
      ambient: Values.number(json, 'ambient', 28000),
      timeOfDay: Values.number(time, 'hour', 10),
      dayCycle: Values.flag(time, 'cycle', fallback: false),
      hoursPerSecond: Values.number(time, 'hoursPerSecond', 0.5),
    );
  }

  /// The sky, and by the same setting the light it casts.
  final Tint sky;

  /// How much light the sky casts, in lux.
  final double ambient;

  /// The hour the scene is set at, from zero to twenty-four.
  ///
  /// What was authored, not wherever a running cycle has carried it to. A
  /// clock left going should not rewrite somebody's scene every time it is
  /// saved.
  final double timeOfDay;

  final bool dayCycle;
  final double hoursPerSecond;

  SceneSettings copyWith({
    Tint? sky,
    double? ambient,
    double? timeOfDay,
    bool? dayCycle,
    double? hoursPerSecond,
  }) => SceneSettings(
    sky: sky ?? this.sky,
    ambient: ambient ?? this.ambient,
    timeOfDay: timeOfDay ?? this.timeOfDay,
    dayCycle: dayCycle ?? this.dayCycle,
    hoursPerSecond: hoursPerSecond ?? this.hoursPerSecond,
  );

  Map<String, Object?> toJson() => {
    'sky': Values.tintToJson(sky),
    'ambient': ambient,
    'time': {
      'hour': timeOfDay,
      'cycle': dayCycle,
      'hoursPerSecond': hoursPerSecond,
    },
  };
}

/// A whole scene, as the thing that is saved and loaded.
///
/// JSON with indentation and a stable key order, because a scene file lives in
/// somebody's repository: a format that reorders itself between saves turns
/// every commit into a diff nobody can review.
///
/// The entities are a flat list, and their order in it is the document's
/// order — siblings draw and list in the order they appear. A tree would say
/// the same thing, and would make every operation that addresses an entity by
/// id walk it first; this way a lookup is a map and a reorder is a move.
class SceneDocument {
  SceneDocument({
    this.name = 'Scene',
    this.settings = const SceneSettings(),
    List<SceneEntity> entities = const [],
  }) : entities = List.unmodifiable(entities) {
    for (final entity in entities) {
      _byId[entity.id] = entity;
    }
  }

  /// Bumped when the shape changes in a way an older editor could misread.
  ///
  /// Two was a light's power changing units. Three moved the air out of the
  /// scene and into an object. Four replaced an object's kind with the set of
  /// components it has.
  static const int formatVersion = 4;

  static const JsonEncoder _encoder = JsonEncoder.withIndent('  ');

  final String name;

  final SceneSettings settings;

  /// Every entity, in document order.
  final List<SceneEntity> entities;

  final Map<String, SceneEntity> _byId = {};

  SceneEntity? operator [](String id) => _byId[id];

  bool contains(String id) => _byId.containsKey(id);

  int get length => entities.length;

  /// Where an entity sits in the flat list, or -1 when it is not in it.
  int indexOf(String id) {
    for (var i = 0; i < entities.length; i++) {
      if (entities[i].id == id) return i;
    }
    return -1;
  }

  List<SceneEntity> get roots => [
    for (final entity in entities)
      if (entity.parent == null) entity,
  ];

  List<SceneEntity> childrenOf(String id) => [
    for (final entity in entities)
      if (entity.parent == id) entity,
  ];

  /// The entities under [parent], in the order they are in.
  List<SceneEntity> siblingsOf(String? parent) => [
    for (final entity in entities)
      if (entity.parent == parent) entity,
  ];

  /// An entity and everything under it, the entity first.
  ///
  /// The unit a move works on: reparenting something takes its children with
  /// it, and so does deleting it.
  List<SceneEntity> subtreeOf(String id) {
    final wanted = <String>{id};
    final found = <SceneEntity>[];
    // One pass works because nothing is ever its own ancestor — decode breaks
    // any loop before this is reachable — but a child may appear before its
    // parent in the list, so it takes as many passes as the tree is deep.
    var growing = true;
    while (growing) {
      growing = false;
      for (final entity in entities) {
        if (wanted.contains(entity.id)) continue;
        final parent = entity.parent;
        if (parent != null && wanted.contains(parent)) {
          wanted.add(entity.id);
          growing = true;
        }
      }
    }
    for (final entity in entities) {
      if (wanted.contains(entity.id)) found.add(entity);
    }
    return found;
  }

  SceneDocument copyWith({
    String? name,
    SceneSettings? settings,
    List<SceneEntity>? entities,
  }) => SceneDocument(
    name: name ?? this.name,
    settings: settings ?? this.settings,
    entities: entities ?? this.entities,
  );

  /// The same document with one entity replaced, or removed when null.
  SceneDocument withEntity(String id, SceneEntity? entity) {
    final next = <SceneEntity>[];
    var replaced = false;
    for (final one in entities) {
      if (one.id != id) {
        next.add(one);
        continue;
      }
      replaced = true;
      if (entity != null) next.add(entity);
    }
    if (!replaced && entity != null) next.add(entity);
    return copyWith(entities: next);
  }

  Map<String, Object?> toJson() => {
    'formatVersion': formatVersion,
    'name': name,
    ...settings.toJson(),
    'entities': [for (final entity in entities) entity.toJson()],
  };

  /// The document as the bytes that go on disk.
  String encode() => '${_encoder.convert(toJson())}\n';

  /// A document out of a file's text, with whatever could not be read.
  static SceneLoad decode(String text) {
    final Object? parsed;
    try {
      parsed = jsonDecode(text);
    } on FormatException catch (error) {
      throw SceneFormatException('This is not a scene file: ${error.message}');
    }

    if (parsed is! Map<String, Object?>) {
      throw const SceneFormatException(
        'A scene file has to be a JSON object, and this one is not.',
      );
    }

    final version = parsed['formatVersion'];
    if (version is! int) {
      throw const SceneFormatException(
        'This file does not say what format version it is, so it cannot be '
        'read safely.',
      );
    }
    if (version > formatVersion) {
      // Refused rather than half-read: a newer file may mean something
      // different by the same keys, and guessing loses somebody's work.
      throw SceneFormatException(
        'This scene was written by a newer Orblit (format $version; this one '
        'reads up to $formatVersion).',
      );
    }

    final problems = <String>[];
    final json = SceneMigrations.run(parsed, version, problems);

    final raw = json['entities'];
    if (raw is! List) {
      throw const SceneFormatException(
        'This scene has no list of entities in it.',
      );
    }

    final entities = <SceneEntity>[];
    final seen = <String>{};
    for (final (index, entry) in raw.indexed) {
      if (entry is! Map<String, Object?>) {
        problems.add('Entity $index is not an object, and was left out.');
        continue;
      }
      final entity = SceneEntity.fromJson(entry);
      if (entity == null) {
        problems.add('Entity $index has no id, and was left out.');
        continue;
      }
      if (!seen.add(entity.id)) {
        problems.add(
          'Two entities share the id "${entity.id}"; the second was dropped.',
        );
        continue;
      }
      entities.add(entity);
    }

    final tree = _rooted(entities, seen, problems);

    return SceneLoad(
      document: SceneDocument(
        name: Values.text(json, 'name') ?? 'Scene',
        settings: SceneSettings.fromJson(json),
        entities: tree,
      ),
      problems: problems,
    );
  }

  /// Makes the parent links into a tree.
  ///
  /// Two things can be wrong with them, and both come from files people hand
  /// edit or merge. A parent that is not in the file would leave its child
  /// unreachable, so the child becomes a root instead of disappearing. And a
  /// loop would hang the outliner while it drew — which is the worst possible
  /// place to find one — so it is cut.
  static List<SceneEntity> _rooted(
    List<SceneEntity> entities,
    Set<String> present,
    List<String> problems,
  ) {
    final parents = {for (final entity in entities) entity.id: entity.parent};
    final orphaned = <String>{};

    for (final entity in entities) {
      final parent = entity.parent;
      if (parent == null) continue;
      if (!present.contains(parent)) {
        problems.add(
          '"${entity.name}" belonged to something that is not in this file, '
          'and is now at the top level.',
        );
        orphaned.add(entity.id);
        parents[entity.id] = null;
      }
    }

    for (final entity in entities) {
      final walked = <String>{entity.id};
      var current = parents[entity.id];
      while (current != null) {
        if (!walked.add(current)) {
          problems.add(
            '"${entity.name}" was inside itself, and is now at the top level.',
          );
          orphaned.add(entity.id);
          parents[entity.id] = null;
          break;
        }
        current = parents[current];
      }
    }

    if (orphaned.isEmpty) return entities;
    return [
      for (final entity in entities)
        if (orphaned.contains(entity.id))
          entity.copyWith(clearParent: true)
        else
          entity,
    ];
  }
}
