import '../content_hash.dart';

/// What writing a pack produced.
final class PackSummary {
  const PackSummary({
    required this.hash,
    required this.bytes,
    required this.assets,
    required this.added,
    required this.replaced,
    required this.blobs,
    required this.sharedBytes,
    required this.removed,
  });

  /// The pack's hash, which is the hash of its directory.
  final ContentHash hash;

  /// The size of the file.
  final int bytes;

  /// How many assets it holds.
  final int assets;

  /// How many of [assets] the base did not serve. Every asset of a pack
  /// with no base.
  final int added;

  /// How many of [assets] the base served with other bytes. Always 0 for a
  /// pack with no base, and `added + replaced == assets`.
  final int replaced;

  /// How many blobs it holds. Fewer than [assets] when some share bytes.
  final int blobs;

  /// The bytes left out because two assets had the same content.
  final int sharedBytes;

  /// How many ids the pack takes away from the one it was built against.
  final int removed;
}
