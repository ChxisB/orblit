import 'dart:convert';

import 'document.dart';
import 'entity.dart';
import 'migration.dart';
import 'values.dart';

/// The extension a prefab file carries.
const String prefabExtension = '.oprefab';

/// A prefab read back off disk, with anything that could not be read.
class PrefabLoad {
  const PrefabLoad({required this.prefab, this.problems = const []});

  final PrefabDocument prefab;

  final List<String> problems;
}

/// Something built once and used many times: an entity and everything under
/// it, saved as a file of its own.
///
/// The point of one is that the thing exists in the project rather than in a
/// scene. A lamp post made once can stand down a street a hundred times, and
/// when the lamp changes, the street changes — because each of the hundred is
/// a link to this file and a note of what is different about that one, never
/// a copy of it.
///
/// Its entities are a scene's entities, written the way a scene writes them,
/// so a prefab is readable, diffable and mergeable, and a scene and a prefab
/// cannot drift into two encodings of one thing. One may hold instances of
/// others, folded exactly as a scene holds them.
class PrefabDocument {
  PrefabDocument({
    required this.name,
    required this.root,
    required this.document,
  });

  static const String marker = 'orblit.prefab';

  /// The shape of the file around the entities. The entities have their own
  /// version, [SceneDocument.formatVersion], written beside it.
  static const int formatVersion = 1;

  static const JsonEncoder _encoder = JsonEncoder.withIndent('  ');

  /// What the thing is called. The file's name is what identifies it; this
  /// is what somebody reads.
  final String name;

  /// The id, inside [document], of the entity everything else hangs off.
  final String root;

  /// The root and everything under it. Ids are the prefab's own: an instance
  /// puts its own id in front of them, so two instances never argue over one.
  final SceneDocument document;

  SceneEntity get rootEntity => document[root]!;

  String encode() =>
      '${_encoder.convert({
        'kind': marker,
        'formatVersion': formatVersion,
        'sceneFormatVersion': SceneDocument.formatVersion,
        'name': name,
        'root': root,
        'objects': [for (final entity in document.entities) entity.toJson()],
      })}\n';

  /// A prefab out of a file's text, with whatever could not be read.
  ///
  /// Throws [SceneFormatException] when the text is not a prefab at all, or
  /// is one from a newer Orblit, for the reasons [SceneDocument.decode] does.
  static PrefabLoad decode(String text) {
    final Object? parsed;
    try {
      parsed = jsonDecode(text);
    } on FormatException catch (error) {
      throw SceneFormatException('This is not a prefab file: ${error.message}');
    }
    if (parsed is! Map<String, Object?> || parsed['kind'] != marker) {
      throw const SceneFormatException('This is not a prefab file.');
    }

    final version = parsed['formatVersion'];
    final sceneVersion = parsed['sceneFormatVersion'];
    if ((version is int && version > formatVersion) ||
        (sceneVersion is int && sceneVersion > SceneDocument.formatVersion)) {
      throw const SceneFormatException(
        'This prefab was written by a newer Orblit.',
      );
    }

    final raw = parsed['objects'];
    if (raw is! List) {
      throw const SceneFormatException('This prefab has nothing in it.');
    }

    final problems = <String>[];
    final entities = <SceneEntity>[];
    final seen = <String>{};
    for (final entry in raw) {
      if (entry is! Map<String, Object?>) continue;
      final migrated = SceneMigrations.entity(
        entry,
        version: sceneVersion is int
            ? sceneVersion
            : SceneDocument.formatVersion,
        notes: problems,
      );
      final entity = migrated == null ? null : SceneEntity.fromJson(migrated);
      if (entity == null || !seen.add(entity.id)) continue;
      entities.add(entity);
    }
    if (entities.isEmpty) {
      throw const SceneFormatException('This prefab has nothing in it.');
    }

    final tree = rooted(entities, problems, folded: true);
    final named = Values.text(parsed, 'root');
    final root = named != null && seen.contains(named)
        ? named
        // A file whose root is missing still has one: the first thing with
        // nothing above it. Better than refusing something recoverable.
        : tree.firstWhere((e) => e.parent == null, orElse: () => tree.first).id;

    final name = Values.text(parsed, 'name') ?? 'Prefab';
    return PrefabLoad(
      prefab: PrefabDocument(
        name: name,
        root: root,
        document: SceneDocument(name: name, entities: tree),
      ),
      problems: problems,
    );
  }
}
