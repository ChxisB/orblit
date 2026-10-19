import '../asset_id.dart';
import '../asset_source.dart';
import '../content_hash.dart';
import '../pack/asset_pack.dart';
import 'kit_manifest.dart';
import 'kit_rules.dart';

/// An opened kit: a pack made for the target `source`, with a `kit.json`.
///
/// A kit holds files for a project's own folders. It is not mounted by a game,
/// so no game reads it by accident, and installing one copies its files out.
final class Kit {
  Kit._(this.pack, this.manifest);

  /// Throws a [FormatException] when [pack] is not a kit: it was made for
  /// another target, it is a patch, or its `kit.json` is missing, too big,
  /// not the bytes the pack lists for it, or wrong.
  static Future<Kit> open(AssetPack pack) async {
    if (pack.target != kitTarget) {
      throw FormatException(
        'This pack is for "${pack.target}", not "$kitTarget", so it is not a '
        'kit.',
      );
    }
    if (pack.base != null || pack.removed.isNotEmpty) {
      throw const FormatException('A kit is a whole pack, not a patch.');
    }
    final entry = pack.manifest.entries[kitManifestId];
    if (entry == null) {
      throw FormatException('This pack has no "$kitManifestId".');
    }
    if (entry.bytes > kitManifestLimit) {
      throw FormatException(
        '"$kitManifestId" is over $kitManifestLimit bytes.',
      );
    }
    final bytes = await pack.read(kitManifestId);
    if (ContentHash.of(bytes) != entry.hash) {
      throw FormatException('"$kitManifestId" does not match its hash.');
    }
    return Kit._(pack, KitManifest.read(bytes));
  }

  final AssetPack pack;
  final KitManifest manifest;

  /// Each file a person gets, with its size. The manifest is not one.
  Map<AssetId, int> get files => {
    for (final MapEntry(:key, :value) in pack.manifest.entries.entries)
      if (key != kitManifestId) key: value.bytes,
  };

  /// What is wrong with the kit, in words. Empty when nothing is.
  ///
  /// Reads and hashes every blob, so it is the place a flipped byte in a
  /// download shows up.
  Future<List<String>> check() async => [
    ...kitProblems(manifest, files),
    for (final id in await pack.findCorrupt())
      '"${kitShow('$id')}" does not match its hash.',
  ];

  /// The files [project] already has with other bytes than the kit's.
  ///
  /// A file that is there and identical is not one, because writing it would
  /// change nothing. Only the project is read, never a blob of the kit.
  ///
  /// shortcut: every file the project has is read and hashed. Compare sizes
  /// first when kits get large.
  Future<List<AssetId>> conflictsIn(AssetSource project) async => [
    for (final MapEntry(:key, :value) in pack.manifest.entries.entries)
      if (key != kitManifestId &&
          await project.exists(key) &&
          ContentHash.of(await project.read(key)) != value.hash)
        key,
  ];
}
