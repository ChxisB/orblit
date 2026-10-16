// The half of the conditional export in pack_bundle.dart for platforms with a
// file system.
import 'dart:io';
import 'dart:typed_data';

import '../asset_id.dart';
import '../asset_manifest.dart';
import '../content_hash.dart';
import '../importer.dart';
import 'asset_pack.dart';
import 'mounted_packs.dart';
import 'pack_format.dart';
import 'pack_summary.dart';

/// Writes the cooked bundle in [bundlePath] as one pack at [outPath].
///
/// Each blob is copied and hashed on the way, and a file that is not what the
/// bundle's manifest says it is refuses the whole pack. That check is how a
/// stale bundle is caught. Assets with the same bytes share one blob. The
/// pack is written beside [outPath] and renamed into place, so a failure
/// leaves no half of one behind.
///
/// With [base], the packs already shipped, in the order they were mounted,
/// the result is a patch. It holds the assets whose hash differs from what
/// the chain serves or that the chain lacks, and names the ids the bundle has
/// dropped. Nothing in [base] is read for its bytes. The comparison is by
/// hash. The patch's own base is the hash of the last pack in [base].
Future<PackSummary> packBundle({
  required String bundlePath,
  required String outPath,
  required CookTarget target,
  List<AssetPack> base = const [],
}) async {
  final bundle = await _readManifest(bundlePath);
  final chain = _mount(target, base);
  final manifest = _changedFrom(bundle, chain);
  final removed = _droppedFrom(bundle, chain);
  final added = _newTo(manifest, chain);
  final layout = _Layout.of(
    manifest,
    target.name,
    base: chain?.chainHash,
    removed: removed,
  );
  final temporary = File('$outPath.partial');
  try {
    await _copyBlobs(bundlePath, temporary, layout);
    await temporary.rename(outPath);
  } on Object {
    if (temporary.existsSync()) temporary.deleteSync();
    rethrow;
  }
  return PackSummary(
    hash: ContentHash.of(layout.directoryBytes),
    bytes: layout.directoryAt + layout.directoryBytes.length,
    assets: manifest.entries.length,
    added: added,
    replaced: manifest.entries.length - added,
    blobs: layout.firstUse.length,
    sharedBytes: layout.sharedBytes,
    removed: removed.length,
  );
}

MountedPacks? _mount(CookTarget target, List<AssetPack> base) {
  if (base.isEmpty) return null;
  final chain = MountedPacks(target: target)..start(base.first);
  for (final pack in base.skip(1)) {
    chain.patch(pack);
  }
  return chain;
}

/// The bundle's assets that the chain does not already serve, by hash.
AssetManifest _changedFrom(AssetManifest bundle, MountedPacks? chain) {
  if (chain == null) return bundle;
  return AssetManifest(
    entries: {
      for (final MapEntry(:key, :value) in bundle.entries.entries)
        if (chain.manifest.entries[key]?.hash != value.hash) key: value,
    },
  );
}

/// How many of the [held] assets the chain has no answer for at all.
int _newTo(AssetManifest held, MountedPacks? chain) {
  if (chain == null) return held.entries.length;
  return held.entries.keys
      .where((id) => !chain.manifest.entries.containsKey(id))
      .length;
}

/// What the chain serves that the bundle no longer has, in id order.
List<AssetId> _droppedFrom(AssetManifest bundle, MountedPacks? chain) {
  if (chain == null) return const [];
  return [
    for (final id in chain.manifest.entries.keys)
      if (!bundle.entries.containsKey(id)) id,
  ]..sort((a, b) => a.toString().compareTo(b.toString()));
}

Future<AssetManifest> _readManifest(String bundlePath) async {
  final file = File(_inside(bundlePath, 'manifest.json'));
  final String text;
  try {
    text = await file.readAsString();
  } on PathNotFoundException {
    throw FormatException(
      '"$bundlePath" has no manifest.json. It is not a cooked bundle. Cook '
      'the project first.',
    );
  }
  final load = AssetManifest.read(text);
  if (load.hasProblems) {
    throw FormatException(
      'The manifest in "$bundlePath" has problems, so the bundle cannot be '
      'packed: ${load.problems.join(' ')}',
    );
  }
  return load.manifest;
}

/// Where every blob goes, worked out from the manifest alone.
///
/// The sizes and hashes are in the manifest, so the header and the directory
/// are known before a byte is copied and the file is written front to back.
final class _Layout {
  _Layout._({
    required this.firstUse,
    required this.blobs,
    required this.sharedBytes,
    required this.directoryAt,
    required this.directoryBytes,
  });

  factory _Layout.of(
    AssetManifest manifest,
    String target, {
    required ContentHash? base,
    required List<AssetId> removed,
  }) {
    final ids = manifest.entries.keys.toList()
      ..sort((a, b) => a.toString().compareTo(b.toString()));
    final blobs = <ContentHash, PackBlob>{};
    final firstUse = <ContentHash, AssetId>{};
    var at = PackHeader.size;
    var shared = 0;
    for (final id in ids) {
      final entry = manifest.entries[id]!;
      if (blobs.containsKey(entry.hash)) {
        shared += entry.bytes;
        continue;
      }
      blobs[entry.hash] = PackBlob(at: at, bytes: entry.bytes);
      firstUse[entry.hash] = id;
      at = alignedOffset(at + entry.bytes);
    }
    final directory = PackDirectory(
      target: target,
      base: base,
      removed: removed,
      assets: {
        for (final MapEntry(:key, :value) in manifest.entries.entries)
          key: value.hash,
      },
      blobs: blobs,
      directoryAt: at,
    );
    return _Layout._(
      firstUse: firstUse,
      blobs: blobs,
      sharedBytes: shared,
      directoryAt: at,
      directoryBytes: directory.encode(),
    );
  }

  /// The first id, in id order, that uses each blob, in file order.
  final Map<ContentHash, AssetId> firstUse;
  final Map<ContentHash, PackBlob> blobs;
  final int sharedBytes;
  final int directoryAt;
  final Uint8List directoryBytes;
}

Future<void> _copyBlobs(
  String bundlePath,
  File temporary,
  _Layout layout,
) async {
  final header = PackHeader(
    directoryAt: layout.directoryAt,
    directoryBytes: layout.directoryBytes.length,
  );
  final sink = temporary.openWrite();
  try {
    sink.add(header.encode());
    for (final MapEntry(key: hash, value: id) in layout.firstUse.entries) {
      final blob = layout.blobs[hash]!;
      final bytes = await _readChecked(bundlePath, id, hash, blob.bytes);
      sink.add(bytes);
      final padding = alignedOffset(blob.end) - blob.end;
      if (padding > 0) sink.add(Uint8List(padding));
    }
    sink.add(layout.directoryBytes);
    await sink.flush();
  } finally {
    await sink.close();
  }
}

Future<Uint8List> _readChecked(
  String bundlePath,
  AssetId id,
  ContentHash expected,
  int expectedBytes,
) async {
  final Uint8List bytes;
  try {
    bytes = await File(_inside(bundlePath, '$id')).readAsBytes();
  } on PathNotFoundException {
    throw FormatException(
      'The manifest lists "$id" and "$bundlePath" has no such file. The '
      'bundle is stale. Cook the project again.',
    );
  }
  if (bytes.length != expectedBytes || ContentHash.of(bytes) != expected) {
    throw FormatException(
      '"$id" in "$bundlePath" is not what its manifest says. The bundle is '
      'stale or was changed by hand. Cook the project again.',
    );
  }
  return bytes;
}

String _inside(String root, String id) =>
    [root, ...id.split('/')].join(Platform.pathSeparator);
