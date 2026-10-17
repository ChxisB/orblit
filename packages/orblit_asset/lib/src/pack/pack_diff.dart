import 'dart:convert';

import '../asset_id.dart';
import 'mounted_packs.dart';

/// An asset with the same name in both packs and other bytes.
final class ChangedAsset {
  const ChangedAsset(this.id, this.before, this.after);

  final AssetId id;

  /// Its size in the older pack.
  final int before;

  /// Its size in the newer pack.
  final int after;
}

/// A document that names an asset the newer pack no longer holds.
final class DanglingReference {
  const DanglingReference(this.document, this.missing);

  /// The scene, prefab, clip, material or other document.
  final AssetId document;

  /// The asset it names.
  final AssetId missing;
}

/// What differs between two packs, by asset name.
///
/// Added, removed and changed come from comparing what each side serves, so
/// no blob is read for them. When something was removed, the documents of the
/// newer side are read for its name, because a removed texture is a blank
/// model and nothing else says so before a player does.
final class PackDiff {
  PackDiff._({
    required List<AssetId> added,
    required List<AssetId> removed,
    required List<ChangedAsset> changed,
    required List<DanglingReference> dangling,
    required List<AssetId> unreadable,
  }) : added = List.unmodifiable(added),
       removed = List.unmodifiable(removed),
       changed = List.unmodifiable(changed),
       dangling = List.unmodifiable(dangling),
       unreadable = List.unmodifiable(unreadable);

  /// Compares what [before] and [after] each serve.
  ///
  /// Throws [PackMountRefused] when they are for different targets, because
  /// every cooked asset would then show as changed.
  static Future<PackDiff> between({
    required MountedPacks before,
    required MountedPacks after,
  }) async {
    if (before.target != after.target) {
      throw PackMountRefused(
        'The old pack is for ${before.target} and the new one is for '
        '${after.target}, so every cooked asset would show as changed.',
      );
    }
    final was = before.manifest.entries;
    final now = after.manifest.entries;
    final removed = _byName(was.keys.where((id) => !now.containsKey(id)));
    final scan = await _scan(after, removed.toSet());
    return PackDiff._(
      added: _byName(now.keys.where((id) => !was.containsKey(id))),
      removed: removed,
      changed: [
        for (final id in now.keys)
          if (was[id] case final old? when old.hash != now[id]!.hash)
            ChangedAsset(id, old.bytes, now[id]!.bytes),
      ]..sort((a, b) => a.id.toString().compareTo(b.id.toString())),
      dangling: scan.dangling,
      unreadable: scan.unreadable,
    );
  }

  final List<AssetId> added;
  final List<AssetId> removed;
  final List<ChangedAsset> changed;

  /// Every removed name that a document of the newer side still uses, by
  /// document and then by name.
  ///
  /// A name that was never in the older pack is not here. Cooking refuses
  /// that when the new pack is built, and counting it would make the first
  /// stray string in a scene fail every later diff.
  final List<DanglingReference> dangling;

  /// Documents that are not JSON, so what they name is not known. Only read
  /// when something was removed.
  final List<AssetId> unreadable;

  /// Whether a build should stop on this: a removed name is still used or a
  /// document could not be read.
  bool get isBroken => dangling.isNotEmpty || unreadable.isNotEmpty;
}

/// The extensions of the text documents Orblit writes. Each is JSON.
const _documents = {
  'oscene',
  'oprefab',
  'oclip',
  'oblend',
  'ocutscene',
  'obonemap',
  'omat',
  'oterrain',
  'oui',
  'opath',
};

/// Reads each document of [after] for the names in [gone].
Future<({List<DanglingReference> dangling, List<AssetId> unreadable})> _scan(
  MountedPacks after,
  Set<AssetId> gone,
) async {
  final dangling = <DanglingReference>[];
  final unreadable = <AssetId>[];
  if (gone.isEmpty) return (dangling: dangling, unreadable: unreadable);
  final documents = after.manifest.entries.keys.where(
    (id) => _documents.contains(id.extension),
  );
  for (final document in _byName(documents)) {
    final Object? json;
    try {
      json = jsonDecode(utf8.decode(await after.read(document)));
    } on FormatException {
      unreadable.add(document);
      continue;
    }
    final named = <AssetId>{};
    _collect(document, json, gone, named);
    for (final missing in _byName(named)) {
      dangling.add(DanglingReference(document, missing));
    }
  }
  return (dangling: dangling, unreadable: unreadable);
}

/// Adds to [into] every name in [gone] that appears in [node], as a value or
/// as a key.
///
/// Any string is tried. A name has to be exactly an asset that was removed
/// to count, so free text and entity names are never mistaken for one, and a
/// field added to a format next year is found without being told about it.
void _collect(
  AssetId document,
  Object? node,
  Set<AssetId> gone,
  Set<AssetId> into,
) {
  switch (node) {
    case final List<Object?> list:
      for (final child in list) {
        _collect(document, child, gone, into);
      }
    case final Map<String, Object?> map:
      for (final MapEntry(:key, :value) in map.entries) {
        _collect(document, key, gone, into);
        _collect(document, value, gone, into);
      }
    case final String text:
      // Ids are read from the project root. Only a leading "./" or "../"
      // means "near this document", as in a scene.
      final id = text.startsWith('./') || text.startsWith('../')
          ? document.resolve(text)
          : AssetId.tryParse(text);
      if (id != null && gone.contains(id)) into.add(id);
    default:
  }
}

List<AssetId> _byName(Iterable<AssetId> ids) =>
    ids.toList()..sort((a, b) => a.toString().compareTo(b.toString()));
