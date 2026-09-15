// The web half of the conditional export in directory.dart.
//
// The same classes as directory_io.dart, so that code naming them compiles
// everywhere, but with no file system behind them: constructing one throws
// rather than handing back something that fails on first use, far from where
// the choice to read a directory was made.
import 'dart:typed_data';

import 'asset_id.dart';
import 'asset_source.dart';
import 'content_hash.dart';
import 'content_store.dart';

/// Assets read from a directory, which needs `dart:io` and is not available
/// on this platform.
class DirectoryAssetSource implements AssetSource {
  DirectoryAssetSource(this.rootPath) {
    throw UnsupportedError(
      'DirectoryAssetSource needs a file system, and this platform has none.',
    );
  }

  final String rootPath;

  @override
  Future<Uint8List> read(AssetId id) =>
      throw UnsupportedError('DirectoryAssetSource needs a file system.');

  @override
  Future<bool> exists(AssetId id) =>
      throw UnsupportedError('DirectoryAssetSource needs a file system.');
}

/// A content store in a directory, which needs `dart:io` and is not available
/// on this platform.
class DirectoryContentStore implements ContentStore {
  DirectoryContentStore(this.rootPath) {
    throw UnsupportedError(
      'DirectoryContentStore needs a file system, and this platform has none.',
    );
  }

  final String rootPath;

  @override
  Future<Uint8List?> get(ContentHash hash) =>
      throw UnsupportedError('DirectoryContentStore needs a file system.');

  @override
  Future<ContentHash> put(List<int> bytes) =>
      throw UnsupportedError('DirectoryContentStore needs a file system.');

  @override
  Future<bool> contains(ContentHash hash) =>
      throw UnsupportedError('DirectoryContentStore needs a file system.');

  @override
  Future<void> remove(ContentHash hash) =>
      throw UnsupportedError('DirectoryContentStore needs a file system.');
}
