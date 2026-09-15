// The half of the conditional export in directory.dart for platforms with a
// file system.
import 'dart:io';
import 'dart:math';
import 'dart:typed_data';

import 'asset_id.dart';
import 'asset_source.dart';
import 'content_hash.dart';
import 'content_store.dart';

/// Assets read from a directory on disk, an id's slashes being its folders.
///
/// What a project uses while it is being worked on, so that saving a texture
/// in an image editor is all it takes for the next read to see it.
///
/// Nothing outside the directory can be read through it. An [AssetId] cannot
/// spell `..` in the first place, but a link inside the directory can still
/// lead out of it, so every file is followed to where it really is and
/// refused unless that is still inside. A link from one place in the
/// directory to another is fine. The check happens just before the read, so
/// a link swapped in between the two could still slip past; closing that
/// needs file handles Dart does not offer, and the check is there to stop a
/// project's files reaching out by accident — or a downloaded project doing it
/// on purpose — not to sandbox a hostile process on the same machine.
class DirectoryAssetSource implements AssetSource {
  DirectoryAssetSource(this.rootPath);

  /// The directory ids are relative to, as it was given.
  final String rootPath;

  @override
  Future<Uint8List> read(AssetId id) async {
    final path = await _locate(id);
    try {
      return await File(path).readAsBytes();
    } on PathNotFoundException {
      throw AssetNotFound(id);
    }
  }

  @override
  Future<bool> exists(AssetId id) async {
    try {
      await _locate(id);
      return true;
    } on AssetNotFound {
      return false;
    }
  }

  /// Where [id] really is on disk, or an [AssetNotFound] saying why it is not
  /// readable from here.
  ///
  /// The root has its own links resolved as well as the file, and fresh on
  /// every call. Otherwise a root that is itself reached through a link —
  /// every temporary directory on macOS is, `/var` being a link to
  /// `/private/var` — would make every file in it look as if it were outside.
  Future<String> _locate(AssetId id) async {
    final String root;
    try {
      root = await Directory(rootPath).resolveSymbolicLinks();
    } on FileSystemException {
      throw AssetNotFound(
        id,
        reason: 'the directory "$rootPath" does not exist',
      );
    }

    final separator = Platform.pathSeparator;
    final inside = root.endsWith(separator) ? root : '$root$separator';
    final path = '$inside${id.toString().split('/').join(separator)}';

    // A folder is not an asset, and a link to nothing is not one either; both
    // come back as something other than a file.
    if (await FileSystemEntity.type(path) != FileSystemEntityType.file) {
      throw AssetNotFound(id);
    }

    final real = await File(path).resolveSymbolicLinks();
    if (!real.startsWith(inside)) {
      throw AssetNotFound(
        id,
        reason: 'it is reached through a link that leads outside "$rootPath"',
      );
    }
    return real;
  }
}

/// A content store in a directory on disk, laid out as `<shard>/<hash>`.
///
/// Safe for several writers at once, in one process or several, because no
/// entry is ever written where it is read. [put] writes into `.incoming` in
/// the same directory and renames the finished file into place, and a rename
/// within one file system is atomic: a reader sees the whole entry or no
/// entry, never half of one. The temporary file lives under the store rather
/// than in the system's temporary directory for exactly that reason — that is
/// often a different file system, and a rename across two is a copy.
///
/// A process killed mid-write leaves its temporary file in `.incoming`.
/// Nothing ever reads from there, so it is untidy rather than wrong, and the
/// folder can be emptied whenever nothing is writing.
class DirectoryContentStore implements ContentStore {
  DirectoryContentStore(this.rootPath);

  /// The directory the store lives in. It need not exist until something is
  /// put.
  final String rootPath;

  static final Random _random = Random();
  static int _written = 0;

  String _pathOf(ContentHash hash) =>
      [rootPath, hash.shard, hash.hex].join(Platform.pathSeparator);

  /// The bytes under [hash], checked against it on the way out.
  ///
  /// A file on disk can be changed by things that are not this store — a disk
  /// going bad, a sync tool, somebody curious with an editor — and bytes that
  /// no longer match their hash are worse than none, because everything above
  /// trusts the hash. So what is read is hashed again, and an entry that fails
  /// is deleted and reported as absent. Deleting it is what lets the next
  /// [put] of the right bytes write it back rather than finding it already
  /// there.
  @override
  Future<Uint8List?> get(ContentHash hash) async {
    final file = File(_pathOf(hash));
    final Uint8List bytes;
    try {
      bytes = await file.readAsBytes();
    } on PathNotFoundException {
      return null;
    }

    if (ContentHash.of(bytes) != hash) {
      try {
        await file.delete();
      } on PathNotFoundException {
        // Something else noticed first and already took it away.
      }
      return null;
    }
    return bytes;
  }

  /// Files [bytes] under their hash.
  ///
  /// Bytes already stored are not written again. The name of an entry is its
  /// contents, so a file already under that name already holds them — and
  /// if it has been damaged since, [get] is where that is caught. Git treats
  /// its objects the same way.
  ///
  /// Two puts of the same bytes racing each other are fine: each writes its
  /// own temporary file, and whichever rename lands second replaces an entry
  /// with an identical one. Should that second rename fail instead, as it can
  /// on Windows while the entry is open for reading, the entry being there is
  /// all that was wanted, and that is not reported as an error.
  @override
  Future<ContentHash> put(List<int> bytes) async {
    final hash = ContentHash.of(bytes);
    final target = File(_pathOf(hash));
    if (await target.exists()) return hash;

    final separator = Platform.pathSeparator;
    final incoming = await Directory(
      '$rootPath$separator.incoming',
    ).create(recursive: true);
    // Unique between processes by the pid, between puts in one isolate by the
    // count, and between isolates in one process — which share a pid and
    // each have their own count — by the random part. `exclusive` makes a
    // collision fail loudly rather than two writers sharing one file.
    final name =
        '${hash.hex}.$pid.${_written++}.${_random.nextInt(1 << 32).toRadixString(16)}';
    final temporary = File('${incoming.path}$separator$name');

    try {
      await temporary.create(exclusive: true);
      await temporary.writeAsBytes(bytes, flush: true);
      await Directory(
        '$rootPath$separator${hash.shard}',
      ).create(recursive: true);
      await temporary.rename(target.path);
    } on FileSystemException {
      try {
        await temporary.delete();
      } on FileSystemException {
        // Never created, or already renamed away.
      }
      if (await target.exists()) return hash;
      rethrow;
    }
    return hash;
  }

  /// Whether there is an entry under [hash].
  ///
  /// Not checked against the hash the way [get] is, since that means reading
  /// all of it. An entry this says is there can still turn out damaged when
  /// it is read.
  @override
  Future<bool> contains(ContentHash hash) => File(_pathOf(hash)).exists();

  @override
  Future<void> remove(ContentHash hash) async {
    try {
      await File(_pathOf(hash)).delete();
    } on PathNotFoundException {
      // Already not there, which is what was asked for.
    }
  }
}
