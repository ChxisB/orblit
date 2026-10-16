// The web half of the conditional export in pack_file.dart.
import 'dart:typed_data';

import 'pack_bytes.dart';

/// A pack read from a file, which needs `dart:io` and is not available on this
/// platform. The web loads a pack whole into a [MemoryPackBytes].
final class FilePackBytes implements PackBytes {
  FilePackBytes(this.path, {this.start = 0, this.bytes}) {
    throw UnsupportedError(
      'FilePackBytes needs a file system, and this platform has none.',
    );
  }

  final String path;
  final int start;
  final int? bytes;

  Future<void> close() =>
      throw UnsupportedError('FilePackBytes needs a file system.');

  @override
  Future<int> length() =>
      throw UnsupportedError('FilePackBytes needs a file system.');

  @override
  Future<Uint8List> read(int at, int bytes) =>
      throw UnsupportedError('FilePackBytes needs a file system.');
}
