import 'dart:convert';

import '../asset_id.dart';
import '../content_hash.dart';
import '../importer.dart';
import 'asset_pack.dart';
import 'mounted_packs.dart';

/// Mounts [top] with the packs it was built on, found by hash in [pool].
///
/// A patch holds only what it changes, so it means nothing without the packs
/// under it. [pool] may hold more than the chain needs and may list its packs
/// in any order. Throws [PackMountRefused] when a pack in the chain names a
/// base that [pool] does not hold.
MountedPacks mountChain(AssetPack top, Iterable<AssetPack> pool) {
  final byHash = {for (final pack in pool) pack.hash: pack};
  final chain = [top];
  for (var base = top.base; base != null; base = chain.last.base) {
    final under = byHash[base];
    if (under == null) {
      throw PackMountRefused(
        'A pack in this chain was built on ${_short(base)} and that pack '
        'was not given.',
      );
    }
    chain.add(under);
  }
  final packs = MountedPacks(target: CookTarget(name: top.target))
    ..start(chain.last);
  chain.reversed.skip(1).forEach(packs.patch);
  return packs;
}

/// An asset with the same name in both packs and other bytes.
final class ChangedAsset {
  const ChangedAsset(this.id, this.before, this.after);

  final AssetId id;

  /// Its size in the older pack.
  final int before;

  /// Its size in the newer pack.
  final int after;
}

/// A document that names an asset the newer pack does not hold.
final class DanglingReference {
  const DanglingReference(this.document, this.missing);

  /// The scene, prefab, clip or blend.
  final AssetId document;

  /// The asset it names.
  final AssetId missing;
}

/// What differs between two packs, by asset name.
///
/// Added, removed and changed come from comparing what each side serves, so
/// no blob is read for them. The documents of the newer side are read for the
/// assets they name, because a removed texture is a blank model and nothing
/// else says so before a player does.
final class PackDiff {
  const PackDiff._({
    required this.added,
    required this.removed,
    required this.changed,
    required this.dangling,
    required this.unreadable,
  });

  /// Compares what [before] and [after] each serve.
  static Future<PackDiff> between({
    required MountedPacks before,
    required MountedPacks after,
  }) async {
    final was = before.manifest.entries;
    final now = after.manifest.entries;
    final scan = await _scan(after, was.keys.followedBy(now.keys));
    return PackDiff._(
      added: _byName([
        for (final id in now.keys)
          if (!was.containsKey(id)) id,
      ]),
      removed: _byName([
        for (final id in was.keys)
          if (!now.containsKey(id)) id,
      ]),
      changed: [
        for (final id in _byName(now.keys.where(was.containsKey)))
          if (was[id]!.hash != now[id]!.hash)
            ChangedAsset(id, was[id]!.bytes, now[id]!.bytes),
      ],
      dangling: scan.dangling,
      unreadable: scan.unreadable,
    );
  }

  final List<AssetId> added;
  final List<AssetId> removed;
  final List<ChangedAsset> changed;

  /// Every name a document of the newer side uses that it does not hold, by
  /// document and then by name. A name that never existed counts, because it
  /// breaks the game the same way.
  final List<DanglingReference> dangling;

  /// Documents that are not JSON, so what they name is not known.
  final List<AssetId> unreadable;

  /// Whether a build should stop on this: a name is missing or a document
  /// could not be read.
  bool get isBroken => dangling.isNotEmpty || unreadable.isNotEmpty;
}

const _documents = {'oscene', 'oprefab', 'oclip', 'oblend'};

/// Reads each document of [after] and checks what it names against [after].
///
/// A string is a reference when it is an asset id whose extension is one that
/// some asset in either pack has. The schemas of scenes and clips grow, so
/// this finds a new field without being told about it, and `"Hero.1"` or a
/// line of dialogue is left alone because nothing in the pack ends that way.
Future<({List<DanglingReference> dangling, List<AssetId> unreadable})> _scan(
  MountedPacks after,
  Iterable<AssetId> everyName,
) async {
  final held = after.manifest.entries;
  final kinds = {for (final id in everyName) id.extension}..remove('');
  final dangling = <DanglingReference>[];
  final unreadable = <AssetId>[];
  for (final document in _byName(held.keys.where(_isDocument))) {
    final Object? json;
    try {
      json = jsonDecode(utf8.decode(await after.read(document)));
    } on FormatException {
      unreadable.add(document);
      continue;
    }
    final named = <AssetId>{};
    _collect(document, json, kinds, named);
    for (final missing in _byName(named.where((id) => !held.containsKey(id)))) {
      dangling.add(DanglingReference(document, missing));
    }
  }
  return (dangling: dangling, unreadable: unreadable);
}

bool _isDocument(AssetId id) => _documents.contains(id.extension);

void _collect(
  AssetId document,
  Object? node,
  Set<String> kinds,
  Set<AssetId> into,
) {
  switch (node) {
    case final List<Object?> list:
      for (final child in list) {
        _collect(document, child, kinds, into);
      }
    case final Map<String, Object?> map:
      for (final child in map.values) {
        _collect(document, child, kinds, into);
      }
    case final String text:
      // Ids are read from the project root. Only a leading "./" or "../"
      // means "near this document", as in a scene.
      final id = text.startsWith('./') || text.startsWith('../')
          ? document.resolve(text)
          : AssetId.tryParse(text);
      if (id != null && kinds.contains(id.extension)) into.add(id);
    default:
  }
}

List<AssetId> _byName(Iterable<AssetId> ids) =>
    ids.toList()..sort((a, b) => a.toString().compareTo(b.toString()));

String _short(ContentHash hash) => hash.hex.substring(0, 12);
