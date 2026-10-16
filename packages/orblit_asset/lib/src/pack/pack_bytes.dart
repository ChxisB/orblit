import 'dart:typed_data';

/// Where a pack's bytes come from.
///
/// Two questions. How long is it, and what are these bytes. A file, a range
/// of a file, a download held in memory and a pack inside a larger file all
/// answer them, and the pack does not care which.
abstract interface class PackBytes {
  /// How many bytes there are.
  Future<int> length();

  /// [bytes] bytes from [at]. Throws when fewer than that exist.
  Future<Uint8List> read(int at, int bytes);
}

/// A pack held in memory.
///
/// For tests, for a pack that arrived whole over the web, and for a small
/// pack loaded through a Flutter bundle. Each read is a copy, as every other
/// asset source hands back fresh bytes.
final class MemoryPackBytes implements PackBytes {
  MemoryPackBytes(this._bytes);

  final Uint8List _bytes;

  @override
  Future<int> length() async => _bytes.length;

  @override
  Future<Uint8List> read(int at, int bytes) async {
    if (at < 0 || bytes < 0 || at + bytes > _bytes.length) {
      throw RangeError(
        'Cannot read $bytes bytes at $at from a pack of ${_bytes.length}.',
      );
    }
    return Uint8List.fromList(Uint8List.sublistView(_bytes, at, at + bytes));
  }
}
