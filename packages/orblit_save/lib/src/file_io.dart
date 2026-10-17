import 'dart:io';

/// Writes [bytes] to [path] whole or not at all. The bytes go to a temporary
/// file beside it, are flushed, and replace the real file by a rename, so a
/// crash leaves the old file.
// shortcut: the directory is not synced after the rename, so a power cut can
// lose the newest write and keep the one before. Upgrade with a native fsync.
Future<void> writeWhole(String path, List<int> bytes) async {
  await File(path).parent.create(recursive: true);
  final temp = File('$path.tmp');
  final file = await temp.open(mode: FileMode.write);
  try {
    await file.writeFrom(bytes);
    await file.flush();
  } finally {
    await file.close();
  }
  await temp.rename(path);
}
