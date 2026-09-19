// The web half of the conditional export in cook_cache_directory.dart.
//
// The same class as cook_cache_io.dart, so that code naming it compiles
// everywhere, but with no file system behind it: constructing one throws
// rather than handing back something that fails on first use, far from where
// the choice to cache in a directory was made.
//
// The web is not left without a cache — MemoryCookCache works anywhere, and
// Phase 8's browser cache is Cache Storage rather than a directory.
import 'dart:typed_data';

import 'content_hash.dart';
import 'cook_cache.dart';
import 'cook_key.dart';

/// A cook cache in a directory, which needs `dart:io` and is not available on
/// this platform.
class DirectoryCookCache implements CookCache {
  DirectoryCookCache(
    this.rootPath, {
    this.limitBytes,
    DateTime Function()? clock,
  }) {
    throw UnsupportedError(
      'DirectoryCookCache needs a file system, and this platform has none.',
    );
  }

  final String rootPath;
  final int? limitBytes;

  @override
  Future<CookedAsset?> lookUp(CookKey key) =>
      throw UnsupportedError('DirectoryCookCache needs a file system.');

  @override
  Future<CookedAsset> store(CookKey key, Map<String, List<int>> outputs) =>
      throw UnsupportedError('DirectoryCookCache needs a file system.');

  @override
  Future<Uint8List?> read(ContentHash hash) =>
      throw UnsupportedError('DirectoryCookCache needs a file system.');
}
