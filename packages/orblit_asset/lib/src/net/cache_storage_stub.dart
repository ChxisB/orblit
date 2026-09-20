// The native half of the conditional export in cache_storage.dart.
//
// The same classes as cache_storage_web.dart, so that code naming them
// compiles everywhere, but with no browser behind them: opening one fails
// where the choice was made rather than on first use.
import 'dart:typed_data';

import '../content_hash.dart';
import '../content_store.dart';
import 'records.dart';

/// A content store in the browser's Cache Storage, which this platform has
/// none of. Use `DirectoryContentStore` where there is a file system.
class CacheStorageContentStore implements ContentStore {
  static Future<CacheStorageContentStore> open({
    String name = 'orblit-assets',
  }) => throw UnsupportedError(
    'CacheStorageContentStore needs a browser, and this platform is not one.',
  );

  @override
  Future<Uint8List?> get(ContentHash hash) =>
      throw UnsupportedError('CacheStorageContentStore needs a browser.');

  @override
  Future<ContentHash> put(List<int> bytes) =>
      throw UnsupportedError('CacheStorageContentStore needs a browser.');

  @override
  Future<bool> contains(ContentHash hash) =>
      throw UnsupportedError('CacheStorageContentStore needs a browser.');

  @override
  Future<void> remove(ContentHash hash) =>
      throw UnsupportedError('CacheStorageContentStore needs a browser.');
}

/// Fetch records in the browser's Cache Storage, which this platform has none
/// of. Use `DirectoryFetchRecords` where there is a file system.
class CacheStorageFetchRecords extends BufferedFetchRecords {
  CacheStorageFetchRecords._();

  static Future<CacheStorageFetchRecords> open({
    String name = 'orblit-assets',
    Duration settle = const Duration(milliseconds: 250),
  }) => throw UnsupportedError(
    'CacheStorageFetchRecords needs a browser, and this platform is not one.',
  );

  @override
  Future<String?> readText() =>
      throw UnsupportedError('CacheStorageFetchRecords needs a browser.');

  @override
  Future<void> writeText(String text) =>
      throw UnsupportedError('CacheStorageFetchRecords needs a browser.');
}
