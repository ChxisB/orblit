import 'dart:typed_data';

import '../asset_id.dart';
import '../asset_manifest.dart';
import '../asset_source.dart';
import '../content_hash.dart';
import 'pack_bytes.dart';
import 'pack_format.dart';

/// One opened `.opack` file.
///
/// A pack is one cooked bundle for one target. The blobs come first and the
/// directory last, so a writer streams and a reader needs only the header
/// and the directory to know what is inside. A pack is an [AssetSource], so
/// nothing that reads assets changes when the assets move into one.
///
/// Blob bytes are not hashed on every read. They are checked when the pack is
/// written and once after a download. [findCorrupt] checks them on demand.
final class AssetPack implements AssetSource {
  AssetPack._({
    required PackBytes bytes,
    required PackDirectory directory,
    required this.hash,
  }) : _bytes = bytes,
       _directory = directory,
       manifest = AssetManifest(
         entries: {
           for (final MapEntry(:key, :value) in directory.assets.entries)
             key: AssetEntry(value, directory.blobs[value]!.bytes),
         },
       );

  /// Opens a pack, throwing a [FormatException] that says what is wrong with
  /// it when it cannot be read whole.
  static Future<AssetPack> open(PackBytes bytes) async {
    final total = await bytes.length();
    if (total < PackHeader.size) {
      throw FormatException(
        'This is not an Orblit pack. It is $total bytes long and a pack '
        'header alone is ${PackHeader.size}.',
      );
    }
    final header = PackHeader.read(await bytes.read(0, PackHeader.size));
    final end = header.directoryAt + header.directoryBytes;
    if (end > total) {
      throw FormatException(
        'This pack is short by ${end - total} bytes. It was probably cut '
        'off while it was being copied or downloaded.',
      );
    }
    if (header.directoryAt < PackHeader.size) {
      throw FormatException(
        'The directory of this pack starts at byte ${header.directoryAt}, '
        'inside the header.',
      );
    }
    final directoryBytes = await bytes.read(
      header.directoryAt,
      header.directoryBytes,
    );
    return AssetPack._(
      bytes: bytes,
      directory: PackDirectory.decode(
        directoryBytes,
        directoryAt: header.directoryAt,
      ),
      hash: ContentHash.of(directoryBytes),
    );
  }

  final PackBytes _bytes;
  final PackDirectory _directory;

  /// The hash of the directory. It holds every blob's hash and place, so it
  /// stands for the whole pack, and a patch names its base by it.
  final ContentHash hash;

  /// Every asset in this pack and which bytes it is.
  final AssetManifest manifest;

  /// The cook target this pack was made for.
  String get target => _directory.target;

  /// The hash of the pack a patch was built against, or null.
  ContentHash? get base => _directory.base;

  /// Ids this pack takes away from the packs under it.
  List<AssetId> get removed => _directory.removed;

  @override
  Future<Uint8List> read(AssetId id) async {
    final blob = _directory.blobs[_directory.assets[id]];
    if (blob == null) throw AssetNotFound(id);
    return _bytes.read(blob.at, blob.bytes);
  }

  @override
  Future<bool> exists(AssetId id) async => _directory.assets.containsKey(id);

  /// The ids whose bytes no longer match their hash, in order.
  ///
  /// Reads and hashes every blob. A flipped byte in a download or on a disk
  /// shows up here and nowhere else.
  Future<List<AssetId>> findCorrupt() async {
    final bad = <ContentHash>{};
    for (final MapEntry(:key, :value) in _directory.blobs.entries) {
      final bytes = await _bytes.read(value.at, value.bytes);
      if (ContentHash.of(bytes) != key) bad.add(key);
    }
    return [
      for (final MapEntry(:key, :value) in _directory.assets.entries)
        if (bad.contains(value)) key,
    ]..sort((a, b) => a.toString().compareTo(b.toString()));
  }
}
