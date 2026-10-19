import 'dart:convert';
import 'dart:io';
import 'dart:typed_data';

import 'file_io.dart';
import 'reason.dart';
import 'record.dart';

/// Where saves are kept. A [SaveQueue] writes through it and reads through it.
abstract interface class SaveStore {
  /// The save under [key], or null when there is none. Throws
  /// [FormatException] when what is stored is damaged.
  Future<SaveRecord?> read(String key);

  /// Replaces the save under [key], whole or not at all.
  Future<void> write(String key, SaveRecord record);

  Future<bool> exists(String key);

  Future<List<String>> keys();
}

final RegExp _keyPattern = RegExp(r'^[A-Za-z0-9_-][A-Za-z0-9._-]{0,127}$');

/// Throws unless [key] is a name a file store can use: letters, digits and
/// `._-`, 128 at most, not starting with a dot. A key comes from a game, but
/// it often starts as a player's name.
void checkKey(String key) {
  if (!_keyPattern.hasMatch(key)) {
    throw ArgumentError.value(key, 'key', 'is not a save key');
  }
}

/// Saves in memory, for tests.
final class MemorySaveStore implements SaveStore {
  final Map<String, SaveRecord> _records = {};

  @override
  Future<SaveRecord?> read(String key) async {
    checkKey(key);
    return _records[key];
  }

  @override
  Future<void> write(String key, SaveRecord record) async {
    checkKey(key);
    _records[key] = record;
  }

  @override
  Future<bool> exists(String key) async {
    checkKey(key);
    return _records.containsKey(key);
  }

  @override
  Future<List<String>> keys() async => _records.keys.toList();
}

const List<int> _magic = [0x4F, 0x53, 0x41, 0x56]; // OSAV
const int _fileFormat = 1;
const String _suffix = '.save';

/// One file for each key in a directory. A write goes to a temporary file,
/// is flushed, and replaces the real file by a rename, so a crash leaves the
/// old save whole.
final class FileSaveStore implements SaveStore {
  FileSaveStore(this.directory);

  final String directory;

  String _path(String key) {
    checkKey(key);
    return '$directory/$key$_suffix';
  }

  @override
  Future<SaveRecord?> read(String key) async {
    final Uint8List bytes;
    try {
      bytes = await File(_path(key)).readAsBytes();
    } on PathNotFoundException {
      return null;
    }
    try {
      return _decode(bytes);
    } on FormatException catch (e) {
      throw FormatException('The save "$key" is damaged: ${e.message}');
    }
  }

  @override
  Future<void> write(String key, SaveRecord record) async =>
      writeWhole(_path(key), _encode(record));

  @override
  Future<bool> exists(String key) async => File(_path(key)).exists();

  @override
  Future<List<String>> keys() async {
    final dir = Directory(directory);
    if (!await dir.exists()) return [];
    return [
      await for (final entry in dir.list())
        if (entry is File && entry.path.endsWith(_suffix))
          entry.uri.pathSegments.last.substring(
            0,
            entry.uri.pathSegments.last.length - _suffix.length,
          ),
    ];
  }
}

Uint8List _encode(SaveRecord record) {
  final header = utf8.encode(
    jsonEncode({
      'format': _fileFormat,
      'reason': record.reason.label,
      'at': record.at.millisecondsSinceEpoch,
    }),
  );
  final out = Uint8List(8 + header.length + record.bytes.length);
  out.setRange(0, 4, _magic);
  ByteData.sublistView(out).setUint32(4, header.length, Endian.little);
  out.setRange(8, 8 + header.length, header);
  out.setRange(8 + header.length, out.length, record.bytes);
  return out;
}

SaveRecord _decode(Uint8List file) {
  if (file.length < 8 || !_hasMagic(file)) {
    throw const FormatException('not a save file');
  }
  final headerLength = ByteData.sublistView(file).getUint32(4, Endian.little);
  if (8 + headerLength > file.length) {
    throw const FormatException('the header is cut short');
  }
  final header = jsonDecode(
    utf8.decode(Uint8List.sublistView(file, 8, 8 + headerLength)),
  );
  if (header case {
    'format': _fileFormat,
    'reason': final String reason,
    'at': final int at,
  }) {
    return SaveRecord(
      bytes: Uint8List.sublistView(file, 8 + headerLength),
      reason: SaveReason.parse(reason),
      at: DateTime.fromMillisecondsSinceEpoch(at, isUtc: true),
    );
  }
  throw const FormatException('the header is not one this version wrote');
}

bool _hasMagic(Uint8List file) {
  for (var i = 0; i < _magic.length; i++) {
    if (file[i] != _magic[i]) return false;
  }
  return true;
}
