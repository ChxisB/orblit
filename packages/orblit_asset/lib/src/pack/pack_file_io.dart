// The half of the conditional export in pack_file.dart for platforms with a
// file system.
import 'dart:async';
import 'dart:io';
import 'dart:typed_data';

import 'pack_bytes.dart';

/// A pack read from a file, or from a range of one.
///
/// [start] and [bytes] describe a pack that sits inside another file, which
/// is how an APK holds an uncompressed asset. Offsets a reader asks for are
/// relative to [start].
///
/// One handle serves every read and the reads run one after another. A
/// `RandomAccessFile` takes one operation at a time and throws on a second,
/// and a game asking for a thousand assets at once would run out of handles
/// if each read opened its own.
final class FilePackBytes implements PackBytes {
  FilePackBytes(this.path, {this.start = 0, this.bytes})
    : assert(start >= 0),
      assert(bytes == null || bytes >= 0);

  final String path;

  /// Where the pack begins in the file.
  final int start;

  /// How long the pack is, or null for the rest of the file.
  final int? bytes;

  RandomAccessFile? _handle;
  Future<void> _queue = Future<void>.value();

  @override
  Future<int> length() => _enqueue((file) async {
    final fileLength = await file.length();
    final available = fileLength - start;
    if (available < 0) return 0;
    final wanted = bytes;
    return wanted == null || wanted > available ? available : wanted;
  });

  @override
  Future<Uint8List> read(int at, int count) => _enqueue((file) async {
    await file.setPosition(start + at);
    final data = await file.read(count);
    if (data.length != count) {
      throw RangeError(
        'Cannot read $count bytes at $at from "$path", which gave '
        '${data.length}.',
      );
    }
    return data;
  });

  /// Closes the file. A later read opens it again.
  Future<void> close() => _enqueue((_) async {
    await _handle?.close();
    _handle = null;
  });

  Future<T> _enqueue<T>(Future<T> Function(RandomAccessFile file) action) {
    final result = _queue.then((_) async {
      final file = _handle ??= await File(path).open();
      return action(file);
    });
    // A failed read must not stop the reads behind it.
    _queue = result.then<void>((_) {}, onError: (Object _) {});
    return result;
  }
}
