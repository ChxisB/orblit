// The web half of the conditional export in pack_folder.dart.
import 'dart:math';

import '../content_hash.dart';
import '../net/fetcher.dart' show FetchProgress;
import '../net/policy.dart';
import '../net/transport.dart';
import 'asset_pack.dart';
import 'pack_listing.dart';
import 'pack_offer.dart';

/// A folder of packs downloaded after install, which needs a file system and
/// is not available on this platform.
///
/// A web app reads a pack from its server in ranges with `RangePackBytes`, and
/// the browser keeps what it fetched.
final class PackFolder {
  PackFolder(this.path) {
    throw UnsupportedError(
      'PackFolder needs a file system, and this platform has none.',
    );
  }

  final String path;

  Future<PackListing> list() =>
      throw UnsupportedError('PackFolder needs a file system.');

  Future<bool> remove(ContentHash hash) =>
      throw UnsupportedError('PackFolder needs a file system.');

  Future<AssetPack> download(
    PackOffer offer, {
    required AssetTransport transport,
    required AssetOrigin origin,
    int attempts = 5,
    Duration backoff = const Duration(seconds: 1),
    Random? jitter,
    void Function(FetchProgress progress)? onProgress,
  }) => throw UnsupportedError('PackFolder needs a file system.');
}
