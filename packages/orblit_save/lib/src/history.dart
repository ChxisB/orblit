import 'dart:convert';
import 'dart:io';
import 'dart:typed_data';

import 'package:crypto/crypto.dart';

import 'file_io.dart';
import 'reason.dart';
import 'record.dart';
import 'store.dart';

/// How far apart the kept versions of a save must be once they are [age]
/// old. A zero keeps every version. Null drops anything that old.
typedef Spacing = Duration? Function(Duration age);

/// Every version for a day, one an hour for a week, one a day for a month,
/// and nothing older.
Duration? defaultSpacing(Duration age) {
  if (age < const Duration(days: 1)) return Duration.zero;
  if (age < const Duration(days: 7)) return const Duration(hours: 1);
  if (age < const Duration(days: 30)) return const Duration(days: 1);
  return null;
}

/// The SHA-256 of [bytes] as hex, which names a stored copy.
String hashOf(List<int> bytes) => sha256.convert(bytes).toString();

/// One kept version of a save. The bytes are in the history under [hash].
final class HistoryEntry {
  const HistoryEntry({
    required this.key,
    required this.at,
    required this.reason,
    required this.hash,
  });

  final String key;
  final DateTime at;
  final SaveReason reason;
  final String hash;
}

const int _entryFormat = 1;
const String _entrySuffix = '.json';
const String _blobSuffix = '.gz';

/// A blob younger than this is kept even when no entry names it, because the
/// write that made it may not have written its entry yet.
const Duration _blobGrace = Duration(minutes: 1);

/// The versions of every save, in a directory. Bytes are kept once for each
/// distinct content, gzipped under their SHA-256, and an entry for each
/// version names the blob, so an identical save adds an entry and no copy.
final class SaveHistory {
  SaveHistory(this.directory);

  final String directory;

  String _entryDir(String key) {
    checkKey(key);
    return '$directory/entries/$key';
  }

  String _blobPath(String hash) => '$directory/blobs/$hash$_blobSuffix';

  String _entryPath(String key, DateTime at, String hash) =>
      '${_entryDir(key)}/${at.millisecondsSinceEpoch}-${hash.substring(0, 12)}'
      '$_entrySuffix';

  Future<void> record(String key, SaveRecord record) async {
    final hash = hashOf(record.bytes);
    final blob = File(_blobPath(hash));
    // shortcut: a prune that deletes this old, unnamed copy after this check
    // leaves the new version without bytes, which load reports. Run prune on
    // a timer and not per save, and add a lock if that ever races.
    if (!await blob.exists()) {
      await writeWhole(blob.path, gzip.encode(record.bytes));
    }
    await writeWhole(
      _entryPath(key, record.at, hash),
      utf8.encode(
        jsonEncode({
          'format': _entryFormat,
          'at': record.at.millisecondsSinceEpoch,
          'reason': record.reason.label,
          'hash': hash,
        }),
      ),
    );
  }

  /// The keys that have a version.
  Future<List<String>> keys() async {
    final dir = Directory('$directory/entries');
    if (!await dir.exists()) return [];
    return [
      await for (final entry in dir.list())
        if (entry is Directory)
          entry.uri.pathSegments[entry.uri.pathSegments.length - 2],
    ];
  }

  /// The versions of [key], newest first. Throws [FormatException] naming
  /// the file when an entry is damaged.
  Future<List<HistoryEntry>> list(String key) async {
    final dir = Directory(_entryDir(key));
    if (!await dir.exists()) return [];
    final entries = [
      await for (final file in dir.list())
        if (file is File && file.path.endsWith(_entrySuffix))
          await _readEntry(key, file),
    ];
    return entries..sort(
      (a, b) => a.at != b.at ? b.at.compareTo(a.at) : a.hash.compareTo(b.hash),
    );
  }

  Future<HistoryEntry> _readEntry(String key, File file) async {
    try {
      final json = jsonDecode(await file.readAsString());
      if (json case {
        'format': _entryFormat,
        'at': final int at,
        'reason': final String reason,
        'hash': final String hash,
      }) {
        return HistoryEntry(
          key: key,
          at: DateTime.fromMillisecondsSinceEpoch(at, isUtc: true),
          reason: SaveReason.parse(reason),
          hash: hash,
        );
      }
      throw const FormatException('not a history entry');
    } on FormatException catch (e) {
      throw FormatException(
        'The history entry ${file.path} is damaged: ${e.message}',
      );
    }
  }

  /// The version [entry] names. Throws [FormatException] when its blob is
  /// missing or does not hold what its name says.
  Future<SaveRecord> load(HistoryEntry entry) async {
    final Uint8List bytes;
    try {
      bytes = Uint8List.fromList(
        gzip.decode(await File(_blobPath(entry.hash)).readAsBytes()),
      );
    } on PathNotFoundException {
      throw FormatException('The copy ${entry.hash} is missing');
    } on FormatException catch (e) {
      throw FormatException('The copy ${entry.hash} is damaged: ${e.message}');
    }
    if (hashOf(bytes) != entry.hash) {
      throw FormatException('The copy ${entry.hash} is damaged: wrong hash');
    }
    return SaveRecord(bytes: bytes, reason: entry.reason, at: entry.at);
  }

  /// Removes the versions [spacing] drops, then the copies nothing names.
  /// Returns how many versions it removed.
  Future<int> prune(Spacing spacing, DateTime now) async {
    var removed = 0;
    final named = <String>{};
    for (final key in await keys()) {
      HistoryEntry? kept;
      for (final entry in await list(key)) {
        final gap = spacing(now.difference(entry.at));
        if (gap != null &&
            (kept == null || kept.at.difference(entry.at) >= gap)) {
          kept = entry;
          named.add(entry.hash);
        } else {
          await _remove(entry);
          removed++;
        }
      }
    }
    await _removeUnnamed(named, now);
    return removed;
  }

  Future<void> _remove(HistoryEntry entry) =>
      File(_entryPath(entry.key, entry.at, entry.hash)).delete();

  Future<bool> _isUnnamedAndOld(
    File file,
    Set<String> named,
    DateTime now,
  ) async {
    final name = file.uri.pathSegments.last;
    if (!name.endsWith(_blobSuffix)) return false;
    final hash = name.substring(0, name.length - _blobSuffix.length);
    return !named.contains(hash) &&
        now.difference(await file.lastModified()) >= _blobGrace;
  }

  Future<void> _removeUnnamed(Set<String> named, DateTime now) async {
    final dir = Directory('$directory/blobs');
    if (!await dir.exists()) return;
    await for (final file in dir.list()) {
      if (file is File && await _isUnnamedAndOld(file, named, now)) {
        await file.delete();
      }
    }
  }
}
