import 'dart:async';
import 'dart:convert';

import '../content_hash.dart';
import '../content_store.dart';

/// What was learned last time a URL was fetched.
///
/// Small on purpose. The bytes themselves live in a [ContentStore], filed
/// under their hash, and this is only the note that says which hash a URL
/// currently is and what to say to the server to find out whether that is
/// still true. Keeping them apart is what lets two URLs that serve the same
/// bytes share one copy, and what lets the byte cache be emptied by the
/// system without these notes becoming lies — a record whose bytes have gone
/// is a miss, not a wrong answer.
class FetchRecord {
  const FetchRecord({
    required this.hash,
    required this.bytes,
    this.etag,
    this.fetched,
  }) : assert(bytes >= 0);

  final ContentHash hash;

  /// How many bytes, for showing progress before any have arrived.
  final int bytes;

  /// What the server called this version, to hand back as `If-None-Match`.
  ///
  /// Null when the server did not say. Without one, a revalidation is a
  /// download, which is why a server not sending ETags costs a project more
  /// than it looks like it should.
  final String? etag;

  /// When it was last fetched or confirmed current.
  final DateTime? fetched;

  FetchRecord confirmedAt(DateTime when) =>
      FetchRecord(hash: hash, bytes: bytes, etag: etag, fetched: when);

  Map<String, Object?> toJson() => {
    'hash': hash.hex,
    'bytes': bytes,
    if (etag != null) 'etag': etag,
    if (fetched != null) 'fetched': fetched!.toUtc().toIso8601String(),
  };

  /// Reads back what [toJson] writes, or null for anything that is not one.
  ///
  /// Null rather than throwing: this is a cache, read at startup, and one
  /// corrupt line in it should cost that one entry rather than every asset
  /// the app has ever downloaded.
  static FetchRecord? fromJson(Object? json) {
    if (json is! Map<String, Object?>) return null;
    final hash = json['hash'];
    final bytes = json['bytes'];
    if (hash is! String || bytes is! int || bytes < 0) return null;
    final ContentHash parsed;
    try {
      parsed = ContentHash.parse(hash);
    } on FormatException {
      return null;
    }
    final etag = json['etag'];
    final fetched = json['fetched'];
    return FetchRecord(
      hash: parsed,
      bytes: bytes,
      etag: etag is String ? etag : null,
      fetched: fetched is String ? DateTime.tryParse(fetched) : null,
    );
  }

  @override
  bool operator ==(Object other) =>
      other is FetchRecord &&
      other.hash == hash &&
      other.bytes == bytes &&
      other.etag == etag;

  @override
  int get hashCode => Object.hash(hash, bytes, etag);
}

/// Which bytes each URL was, last anybody looked.
abstract interface class FetchRecords {
  /// What is known about [url], or null when nothing is.
  Future<FetchRecord?> get(Uri url);

  Future<void> put(Uri url, FetchRecord record);

  Future<void> remove(Uri url);

  /// Forgets everything. For a "clear cache" button, and for tests.
  Future<void> clear();
}

/// Records held in memory, which is the right answer for a tool that runs
/// once and for every test that is not about persistence.
class MemoryFetchRecords implements FetchRecords {
  MemoryFetchRecords([Map<Uri, FetchRecord> records = const {}])
    : _records = {...records};

  final Map<Uri, FetchRecord> _records;

  @override
  Future<FetchRecord?> get(Uri url) async => _records[url];

  @override
  Future<void> put(Uri url, FetchRecord record) async {
    _records[url] = record;
  }

  @override
  Future<void> remove(Uri url) async {
    _records.remove(url);
  }

  @override
  Future<void> clear() async => _records.clear();
}

/// Reads and writes a whole set of records as one JSON object.
///
/// Shared by every store that keeps them in a single blob — a file on disk, an
/// entry in the browser's cache — because the encoding should not be written
/// twice and then differ by a field.
class FetchRecordsCodec {
  const FetchRecordsCodec._();

  static const int formatVersion = 1;

  static String encode(Map<Uri, FetchRecord> records) {
    // Sorted by URL so the file is the same every time for the same records,
    // and carried as entries rather than looked up again by a reparsed key:
    // a URL that does not survive the round trip should cost nothing, not
    // throw while writing the cache.
    final entries = records.entries.toList()
      ..sort((a, b) => a.key.toString().compareTo(b.key.toString()));
    return jsonEncode({
      'formatVersion': formatVersion,
      'records': {
        for (final entry in entries) entry.key.toString(): entry.value.toJson(),
      },
    });
  }

  /// Reads back what [encode] writes, dropping anything unreadable.
  ///
  /// An empty map for text that is not a record set at all, including one
  /// written by a newer version. Everything here can be fetched again; losing
  /// it costs a download, and guessing at a format that has changed costs
  /// correctness.
  static Map<Uri, FetchRecord> decode(String text) {
    final Object? parsed;
    try {
      parsed = jsonDecode(text);
    } on FormatException {
      return {};
    }
    if (parsed is! Map<String, Object?>) return {};
    if (parsed['formatVersion'] != formatVersion) return {};
    final records = parsed['records'];
    if (records is! Map<String, Object?>) return {};

    final out = <Uri, FetchRecord>{};
    for (final MapEntry(:key, :value) in records.entries) {
      final url = Uri.tryParse(key);
      final record = FetchRecord.fromJson(value);
      if (url != null && record != null) out[url] = record;
    }
    return out;
  }
}

/// Records kept as one blob somewhere that is slow to write.
///
/// Every persistent store here wants the same two things and neither is about
/// where the bytes end up, so they are written once: the whole set is read on
/// first use and kept in memory, and changes are coalesced into one write
/// [settle] after the last of them.
///
/// The coalescing is the point. A scene loading five hundred assets produces
/// five hundred records within a second or two of each other, and a store
/// that wrote on every change would rewrite a file — or a cache entry — five
/// hundred times to answer a question that is only asked at startup.
///
/// Losing a write costs a revalidation, not a correct answer: the bytes live
/// in a [ContentStore] under their hash, and a record that never landed means
/// the next launch downloads rather than asking whether it needs to.
abstract class BufferedFetchRecords implements FetchRecords {
  BufferedFetchRecords({this.settle = const Duration(milliseconds: 250)});

  /// How long after the last change the set is written.
  final Duration settle;

  Future<Map<Uri, FetchRecord>>? _reading;
  Map<Uri, FetchRecord>? _records;
  Timer? _due;
  Future<void> _writing = Future<void>.value();
  var _changed = false;
  var _closed = false;

  /// The stored text, or null when nothing has been stored yet.
  ///
  /// Throwing is treated as nothing stored: this is a cache, and a store that
  /// cannot be read should cost downloads rather than a startup failure.
  Future<String?> readText();

  /// Replaces the stored text. Must not leave a half-written blob behind for
  /// [readText] to find.
  Future<void> writeText(String text);

  @override
  Future<FetchRecord?> get(Uri url) async => (await _load())[url];

  @override
  Future<void> put(Uri url, FetchRecord record) async {
    (await _load())[url] = record;
    _touch();
  }

  @override
  Future<void> remove(Uri url) async {
    (await _load()).remove(url);
    _touch();
  }

  @override
  Future<void> clear() async {
    (await _load()).clear();
    _touch();
  }

  /// Writes now, if anything is pending, and waits for it.
  ///
  /// A set nothing has changed is not written again. Closing a fetcher that
  /// only ever read should cost nothing, and on the web that would otherwise
  /// be a whole round trip into the browser's storage to store what is
  /// already there.
  Future<void> flush() async {
    _due?.cancel();
    _due = null;
    if (_changed) _schedule();
    await _writing;
  }

  /// Writes what is pending and stops scheduling. Reading still works.
  Future<void> close() async {
    await flush();
    _closed = true;
  }

  Future<Map<Uri, FetchRecord>> _load() =>
      _reading ??= _first().then((records) => _records = records);

  Future<Map<Uri, FetchRecord>> _first() async {
    try {
      final text = await readText();
      return text == null ? {} : FetchRecordsCodec.decode(text);
    } on Object {
      return {};
    }
  }

  void _touch() {
    if (_closed) return;
    _changed = true;
    _due?.cancel();
    _due = Timer(settle, () {
      _due = null;
      _schedule();
    });
  }

  /// Queues a write behind whatever is already writing.
  ///
  /// Chained rather than started at once, so that two bursts cannot overlap
  /// and leave the later set underneath the earlier one.
  void _schedule() {
    _changed = false;
    final text = FetchRecordsCodec.encode(_records ?? const {});
    _writing = _writing.then((_) => writeText(text)).catchError((Object _) {
      // A cache that cannot be written is a cache that does not help, which
      // is not a reason to take an app down. The next change tries again.
    });
  }
}
