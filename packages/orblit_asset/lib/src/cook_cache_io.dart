// The half of the conditional export in cook_cache_directory.dart for
// platforms with a file system.
import 'dart:async';
import 'dart:convert';
import 'dart:io';
import 'dart:math';
import 'dart:typed_data';

import 'content_hash.dart';
import 'cook_cache.dart';
import 'cook_key.dart';
import 'directory_io.dart';

/// A cook cache in a directory, shared by every build on the machine.
///
/// Laid out as an index of what was cooked and a content store of the bytes:
///
///     index.json          key -> outputs, and when each was last wanted;
///                         and the keys that would not cook, with why
///     index.lock          held while the index is read and written
///     content/<shard>/…   the bytes, filed by hash
///
/// The split is what makes it cheap. Bytes are content-addressed, so two
/// targets whose cooks happen to agree — the same PNG cooked for two devices
/// that both want ASTC — share one copy, and writing bytes needs no lock at
/// all: the name of an entry is its contents, so two writers racing write the
/// same thing. Only the index is mutable, and only the index is locked.
///
/// The lock is a file lock, so it holds across processes: two builds running
/// at once, a cook and an editor, CI running two jobs on one machine. Inside
/// one process it is backed up by a queue shared by every cache on the same
/// directory, because a POSIX file lock belongs to the *process* rather than
/// to whoever took it — a second cache in the same process asks for a lock it
/// already holds and is given it at once. The queue is what actually
/// serialises them, and the file lock is what holds against everyone else.
///
/// What is left uncovered is two *isolates* of one process: they share the
/// file lock, and, since isolates share no memory, not the queue. A cook that
/// fans work out to isolates should let one of them own the cache rather than
/// handing the same directory to all of them.
class DirectoryCookCache implements CookCache {
  DirectoryCookCache(
    this.rootPath, {
    this.limitBytes,
    DateTime Function()? clock,
  }) : _clock = clock ?? DateTime.now {
    if (limitBytes != null && limitBytes! <= 0) {
      throw ArgumentError.value(
        limitBytes,
        'limitBytes',
        'A cache limit of nothing is a cache that evicts every entry as it '
            'writes it. Leave it out for no limit.',
      );
    }
  }

  /// The directory the cache lives in. It need not exist yet.
  final String rootPath;

  /// About how many bytes of cooked output to keep, or null to keep
  /// everything.
  ///
  /// About, not exactly: the entry just written is never the one evicted, so
  /// a single cook larger than the limit is kept rather than thrown away the
  /// instant it is made. A limit is there to stop a cache growing without
  /// bound over months, and a build that cannot use what it just cooked is a
  /// worse failure than a directory briefly over its budget.
  final int? limitBytes;

  final DateTime Function() _clock;

  /// Bumped when the index's shape changes. An index this cannot read is
  /// treated as an empty one, so an older build meeting a newer cache misses
  /// and recooks rather than misreading.
  ///
  /// Remembered failures did not bump it, although they added a key. An older
  /// build ignores a key it does not know, and the worst it can do is drop the
  /// failures when it writes the index back — which costs a mark in an editor
  /// and nothing else. Bumping would have thrown away every machine's cooked
  /// bytes to add a note beside the broken ones.
  static const int formatVersion = 1;

  /// How many remembered failures to keep, newest first.
  ///
  /// A bound rather than a limit in bytes, because these are a line of text
  /// each and the thing worth stopping is a build loop that fails on ten
  /// thousand assets a night writing an index nobody can read. What falls off
  /// the end is only a mark in a browser, and the asset beneath it still reads
  /// as needing a cook.
  static const int _keptFailures = 4096;

  /// How stale a last-used time is allowed to get before a hit rewrites it.
  ///
  /// Every hit bumping the time would mean rewriting the whole index on every
  /// hit — for a project of ten thousand assets, ten thousand rewrites of a
  /// file that grows with the project, which is quadratic work to record
  /// something eviction only needs to the nearest day.
  static const Duration _usedAtResolution = Duration(hours: 1);

  late final DirectoryContentStore _content = DirectoryContentStore(
    '$rootPath${Platform.pathSeparator}content',
  );

  /// One queue for each directory, shared by every cache on it in this
  /// isolate; see the class comment for why the file lock alone is not
  /// enough.
  ///
  /// Keyed on the absolute path, so that a relative spelling and an absolute
  /// one of the same directory queue together. Two spellings that only a
  /// symbolic link makes equal still get a queue each, which is the same
  /// narrow gap as the isolate one and has the same answer.
  static final Map<String, Future<void>> _turns = {};

  static final Random _random = Random();
  static int _writes = 0;

  late final String _queueKey = Directory(rootPath).absolute.path;

  String get _indexPath => '$rootPath${Platform.pathSeparator}index.json';

  String get _lockPath => '$rootPath${Platform.pathSeparator}index.lock';

  @override
  Future<CookedAsset?> lookUp(CookKey key) => _guarded(() async {
    final index = await _readIndex();
    final entry = index.entries[key.hash.hex];
    if (entry == null) return null;

    for (final output in entry.asset.outputs) {
      if (!await _content.contains(output.hash)) {
        // The bytes have been evicted, or removed by something that is not
        // this cache. The entry is a promise the cache can no longer keep, so
        // it goes, and the caller cooks.
        index.entries.remove(key.hash.hex);
        await _writeIndex(index);
        return null;
      }
    }

    final now = _clock();
    if (now.difference(entry.usedAt) > _usedAtResolution) {
      index.entries[key.hash.hex] = _Entry(entry.asset, now);
      await _writeIndex(index);
    }
    return entry.asset;
  });

  @override
  Future<CookedAsset> store(CookKey key, Map<String, List<int>> outputs) async {
    // Outside the lock on purpose. An entry's name is its contents, so two
    // writers storing the same bytes write the same file, and a long cook of
    // a large texture holding the index shut would serialise every build on
    // the machine behind it.
    final stored = <CookOutput>[];
    for (final output in outputs.entries) {
      final hash = await _content.put(output.value);
      stored.add(
        CookOutput(name: output.key, hash: hash, bytes: output.value.length),
      );
    }
    final asset = CookedAsset(stored);

    await _guarded(() async {
      final index = await _readIndex();
      index.entries[key.hash.hex] = _Entry(asset, _clock());
      // A key that cooks is a key that no longer failed: the tool that was
      // missing has been installed, or the disk has been cleared.
      index.failures.remove(key.hash.hex);
      await _evict(index.entries, keeping: key.hash.hex);
      await _writeIndex(index);
    });
    return asset;
  }

  @override
  Future<Uint8List?> read(ContentHash hash) => _content.get(hash);

  @override
  Future<CookFailure?> lookUpFailure(CookKey key) =>
      _guarded(() async => (await _readIndex()).failures[key.hash.hex]);

  @override
  Future<void> recordFailure(CookKey key, String reason) => _guarded(() async {
    final index = await _readIndex();
    index.failures[key.hash.hex] = CookFailure(
      reason: reason,
      at: _clock().toUtc(),
    );

    if (index.failures.length > _keptFailures) {
      final oldest = index.failures.entries.toList()
        ..sort((a, b) => a.value.at.compareTo(b.value.at));
      for (final candidate in oldest) {
        if (index.failures.length <= _keptFailures) break;
        if (candidate.key == key.hash.hex) continue;
        index.failures.remove(candidate.key);
      }
    }

    await _writeIndex(index);
  });

  /// Drops entries, oldest first, until what is held is within [limitBytes].
  ///
  /// Sizes are counted per distinct hash rather than per output, because that
  /// is what the disk actually holds: two entries sharing one cooked texture
  /// share one file. Counting it twice would evict a cache that was never over
  /// its limit.
  ///
  /// Bytes are only removed once no surviving entry names them. Removing them
  /// with the entry would take a shared texture out from under whatever else
  /// still points at it, and that entry would then look like a hit and read
  /// back nothing.
  Future<void> _evict(
    Map<String, _Entry> index, {
    required String keeping,
  }) async {
    final limit = limitBytes;
    if (limit == null) return;

    int held() {
      final sizes = <ContentHash, int>{};
      for (final entry in index.values) {
        for (final output in entry.asset.outputs) {
          sizes[output.hash] = output.bytes;
        }
      }
      return sizes.values.fold(0, (sum, bytes) => sum + bytes);
    }

    if (held() <= limit) return;

    final oldest = index.entries.toList()
      ..sort((a, b) => a.value.usedAt.compareTo(b.value.usedAt));
    final dropped = <ContentHash>{};
    for (final candidate in oldest) {
      if (candidate.key == keeping) continue;
      if (held() <= limit) break;
      for (final output in candidate.value.asset.outputs) {
        dropped.add(output.hash);
      }
      index.remove(candidate.key);
    }

    for (final entry in index.values) {
      for (final output in entry.asset.outputs) {
        dropped.remove(output.hash);
      }
    }
    for (final hash in dropped) {
      await _content.remove(hash);
    }
  }

  /// Runs [body] with the index locked, against other processes and against
  /// this object's own concurrent callers.
  Future<T> _guarded<T>(Future<T> Function() body) {
    final ours = Completer<void>();
    final queue = _turns[_queueKey] ?? Future.value();
    _turns[_queueKey] = ours.future;
    return queue
        .then((_) async {
          await Directory(rootPath).create(recursive: true);
          final lock = await File(_lockPath).open(mode: FileMode.append);
          try {
            // Blocking, not FileLock.exclusive: that one *fails* when the lock
            // is held rather than waiting for it, so a second build cooking at
            // the same moment would crash instead of taking its turn.
            await lock.lock(FileLock.blockingExclusive);
            try {
              return await body();
            } finally {
              await lock.unlock();
            }
          } finally {
            await lock.close();
          }
        })
        .whenComplete(ours.complete);
  }

  /// The index, or an empty one when there is nothing readable there.
  ///
  /// Every failure lands here: no file yet, a half-written file from a machine
  /// that lost power, a newer format, a hash that is not one. All of them mean
  /// the same thing — this cache cannot say what was cooked — and the answer
  /// to that is to cook again. Refusing to start over an index nobody can read
  /// would make a corrupt cache a broken build rather than a slow one.
  Future<_Index> _readIndex() async {
    final String text;
    try {
      text = await File(_indexPath).readAsString();
    } on FileSystemException {
      return _Index.empty();
    }

    final Object? parsed;
    try {
      parsed = jsonDecode(text);
    } on FormatException {
      return _Index.empty();
    }
    if (parsed is! Map<String, Object?>) return _Index.empty();
    if (parsed['formatVersion'] != formatVersion) return _Index.empty();
    final entries = parsed['entries'];
    if (entries is! Map<String, Object?>) return _Index.empty();

    final index = _Index.empty();
    for (final entry in entries.entries) {
      final read = _Entry.tryRead(entry.value);
      if (read != null) index.entries[entry.key] = read;
    }

    // Absent for an index written before failures were recorded, and for one
    // written by a build that does not know about them. Neither is a problem:
    // no failures remembered is the state every cache starts in.
    final failures = parsed['failures'];
    if (failures is Map<String, Object?>) {
      for (final failure in failures.entries) {
        final read = _readFailure(failure.value);
        if (read != null) index.failures[failure.key] = read;
      }
    }
    return index;
  }

  static CookFailure? _readFailure(Object? value) {
    if (value is! Map<String, Object?>) return null;
    final reason = value['reason'];
    final at = DateTime.tryParse(value['at'] as String? ?? '');
    if (reason is! String || at == null) return null;
    return CookFailure(reason: reason, at: at);
  }

  /// Writes the index where a reader sees all of it or none of it.
  ///
  /// Into a temporary file in the same directory and then renamed, because a
  /// rename within one file system is atomic while a write over a live file is
  /// not: a build killed halfway through the second kind leaves an index that
  /// is valid JSON up to the point it stops.
  ///
  /// The temporary file is named uniquely rather than `index.json.incoming`,
  /// for the same reason the content store does it: two writers sharing one
  /// temporary name do not merely lose an update, they delete each other's
  /// file mid-write and fail on the rename. That should not be reachable
  /// while the lock is held, and naming it this way means a hole in the lock
  /// costs a stale index rather than a crashed build.
  Future<void> _writeIndex(_Index index) async {
    final keys = index.entries.keys.toList()..sort();
    final failed = index.failures.keys.toList()..sort();
    final json = {
      'formatVersion': formatVersion,
      'entries': {for (final key in keys) key: index.entries[key]!.toJson()},
      'failures': {
        for (final key in failed)
          key: {
            'reason': index.failures[key]!.reason,
            'at': index.failures[key]!.at.toUtc().toIso8601String(),
          },
      },
    };
    final incoming = File(
      '$_indexPath.$pid.${_writes++}.'
      '${_random.nextInt(1 << 32).toRadixString(16)}.incoming',
    );
    try {
      await incoming.writeAsString(
        '${const JsonEncoder.withIndent('  ').convert(json)}\n',
        flush: true,
      );
      await incoming.rename(_indexPath);
    } on FileSystemException {
      try {
        await incoming.delete();
      } on FileSystemException {
        // Never created, or already renamed away.
      }
      rethrow;
    }
  }
}

/// What `index.json` holds: the keys that cooked, and the keys that would not.
///
/// One file and one lock for both, because they are written together — a key
/// that cooks drops its old failure in the same turn — and because a second
/// file would be a second thing to leave half-written.
class _Index {
  _Index(this.entries, this.failures);

  _Index.empty() : this({}, {});

  final Map<String, _Entry> entries;
  final Map<String, CookFailure> failures;
}

/// One line of the index: what a key cooked to, and when it was last wanted.
class _Entry {
  const _Entry(this.asset, this.usedAt);

  final CookedAsset asset;
  final DateTime usedAt;

  Map<String, Object?> toJson() => {
    'usedAt': usedAt.toUtc().toIso8601String(),
    'outputs': [
      for (final output in asset.outputs)
        {'name': output.name, 'hash': output.hash.hex, 'bytes': output.bytes},
    ],
  };

  /// Reads one entry, or gives null when it cannot be trusted whole.
  ///
  /// A single unreadable entry costs one recook; the rest of the index is
  /// still good. An entry half-read, though, would be a hit that hands back
  /// some of a cook, so anything wrong with any output throws the entry away.
  static _Entry? tryRead(Object? value) {
    if (value is! Map<String, Object?>) return null;
    final usedAt = DateTime.tryParse(value['usedAt'] as String? ?? '');
    if (usedAt == null) return null;
    final outputs = value['outputs'];
    if (outputs is! List) return null;

    final read = <CookOutput>[];
    for (final output in outputs) {
      if (output is! Map<String, Object?>) return null;
      final name = output['name'];
      final bytes = output['bytes'];
      if (name is! String || name.isEmpty || bytes is! int || bytes < 0) {
        return null;
      }
      final ContentHash hash;
      try {
        hash = ContentHash.parse(output['hash'] as String? ?? '');
      } on FormatException {
        return null;
      }
      read.add(CookOutput(name: name, hash: hash, bytes: bytes));
    }
    return _Entry(CookedAsset(read), usedAt);
  }
}
