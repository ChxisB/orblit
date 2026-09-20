import 'dart:async';
import 'dart:math';
import 'dart:typed_data';

import '../asset_id.dart';
import '../asset_manifest.dart';
import '../content_hash.dart';
import '../content_store.dart';
import 'picture_size.dart';
import 'policy.dart';
import 'records.dart';
import 'transport.dart';

/// How badly something is wanted.
///
/// The queue takes [onScreen] work before [soon] and [soon] before
/// [eventually], and within one level it takes what was asked for first. That
/// last part is not a detail: without it, a level with more work than slots
/// starves whatever asked earliest, and the thing nobody is waiting for
/// finishes before the thing somebody is.
enum FetchUrgency {
  /// Something being looked at now. A frame is worse for the want of it.
  onScreen,

  /// The next room, the next level, the thing behind the door.
  soon,

  /// Warming the cache. Should never delay either of the others.
  eventually,
}

/// How much of an asset has arrived.
class FetchProgress {
  const FetchProgress({required this.received, this.total});

  final int received;

  /// The whole size, when the server says. Null when it does not, which no
  /// progress bar likes and which happens anyway on a chunked reply.
  final int? total;

  /// Between nought and one, or null when the total is unknown.
  double? get fraction =>
      total == null || total == 0 ? null : (received / total!).clamp(0.0, 1.0);
}

/// An asset that was fetched and is not what it should be.
///
/// Its own failure rather than a general one, because the answer is different:
/// a connection failure is retried and a wrong hash is not. Bytes that do not
/// match the manifest are either a corrupted transfer or somebody else's
/// bytes, and both of those are reasons to stop rather than to try again more
/// slowly.
class FetchCorrupt implements Exception {
  const FetchCorrupt(this.url, {required this.wanted, required this.got});

  final Uri url;
  final ContentHash wanted;
  final ContentHash got;

  @override
  String toString() =>
      'What came back from $url is not what the manifest says it should be. '
      'Expected $wanted, got $got.';
}

/// A fetch that ran out of attempts, carrying the last thing that went wrong.
class FetchFailed implements Exception {
  const FetchFailed(this.url, this.attempts, this.cause);

  final Uri url;
  final int attempts;

  /// The last failure: an exception thrown by the transport, or an [int]
  /// status code the server answered with.
  final Object cause;

  @override
  String toString() {
    final why = cause is int ? 'the server answered $cause' : '$cause';
    return 'Could not fetch $url after $attempts '
        '${attempts == 1 ? 'attempt' : 'attempts'}: $why.';
  }
}

/// One caller's hold on a download.
///
/// Several callers asking for the same asset share one download and each get
/// one of these. [cancel] gives up this caller's interest in it; the download
/// itself stops only when the last interested caller has gone, because
/// cancelling a fetch somebody else is still waiting for is not a saving.
class FetchJob {
  FetchJob._(this._download, this._completer, this.onProgress);

  final _Download _download;
  final Completer<Uint8List> _completer;

  /// Called as bytes arrive, for this caller only.
  final void Function(FetchProgress)? onProgress;

  /// Where the asset is being fetched from.
  Uri get url => _download.url;

  /// The bytes, when they have all arrived and been checked.
  Future<Uint8List> get bytes => _completer.future;

  var _given = false;

  /// Gives up waiting.
  ///
  /// [bytes] completes with a [FetchCancelled]. Awaiting it afterwards is
  /// safe; ignoring it is too, since the error is delivered to whoever asked
  /// and nowhere else.
  void cancel() {
    if (_given) return;
    _given = true;
    _download._drop(this);
    _giveUp();
  }

  /// Completes [bytes] with a [FetchCancelled] that nobody has to await.
  ///
  /// The future is marked handled as well as completed. Giving up on a fetch
  /// and then not awaiting the thing you gave up on is the ordinary way to
  /// use this, and an error with no listener is otherwise reported as
  /// unhandled — which in an app means the fetch that was cancelled on
  /// purpose takes the frame down with it. Awaiting it afterwards still
  /// delivers the error.
  void _giveUp() {
    if (_completer.isCompleted) return;
    _completer.completeError(FetchCancelled(_download.url));
    _completer.future.ignore();
  }
}

/// A fetch somebody gave up on.
class FetchCancelled implements Exception {
  const FetchCancelled(this.url);

  final Uri url;

  @override
  String toString() => 'The fetch of $url was cancelled.';
}

/// Fetches assets over the network, once each, in the order they are wanted.
///
/// Everything here exists because a download is slow, unreliable and somebody
/// else's: it is **deduplicated**, so two parts of a scene asking for the same
/// texture cost one download; **queued**, so what is on screen goes first;
/// **revalidated**, so a warm start costs a round trip rather than the bytes;
/// **resumed**, so a connection that drops at ninety per cent does not start
/// again; **verified** against the manifest, so a corrupted or substituted
/// file is caught before anything decodes it; and **cached**, so the second
/// launch and the offline launch both work.
///
/// It never writes a temporary file. Bytes go from the socket to memory to the
/// content store, and an asset is handed over as the bytes it is. That is
/// partly because the web has no files to write, and partly because a
/// half-written temporary that outlives the process is the oldest way to serve
/// a truncated asset and swear it is fine.
class AssetFetcher {
  AssetFetcher({
    required this.origin,
    required this.transport,
    ContentStore? store,
    FetchRecords? records,
    this.manifest,
    this.concurrent = 4,
    this.attempts = 3,
    this.backoff = const Duration(milliseconds: 300),
    Random? jitter,
  }) : store = store ?? MemoryContentStore(),
       records = records ?? MemoryFetchRecords(),
       _jitter = jitter ?? Random(),
       assert(concurrent > 0),
       assert(attempts > 0);

  /// Where assets are served from, and what is allowed.
  final AssetOrigin origin;

  final AssetTransport transport;

  /// Where fetched bytes are kept, by hash.
  final ContentStore store;

  /// What each URL was, last time it was fetched.
  final FetchRecords records;

  /// What each asset should hash to. Without one, nothing is verified, and
  /// the fetcher says so rather than pretending the bytes are vouched for.
  final AssetManifest? manifest;

  /// How many downloads run at once.
  ///
  /// Four. More connections do not make a link faster once it is full, and
  /// they do make the first thing finish later — which on a loading screen is
  /// the number anybody actually sees.
  final int concurrent;

  /// How many times a failure that might pass is tried.
  final int attempts;

  /// The wait before the second attempt, doubling after that.
  final Duration backoff;

  final Random _jitter;

  final Map<Uri, _Download> _live = {};
  final List<_Download> _waiting = [];
  var _running = 0;
  var _arrived = 0;
  var _closed = false;

  /// How many downloads are queued but not started.
  int get waiting => _waiting.length;

  /// How many are running.
  int get running => _running;

  /// Fetches [id], returning a handle that can be cancelled.
  FetchJob fetch(
    AssetId id, {
    FetchUrgency urgency = FetchUrgency.soon,
    void Function(FetchProgress)? onProgress,
  }) => fetchUrl(
    origin.urlOf(id),
    id: id,
    urgency: urgency,
    onProgress: onProgress,
  );

  /// Fetches a URL directly, for a file named from inside another one.
  ///
  /// [id] is what the manifest would call it, when there is such a name; a
  /// buffer a glTF names beside itself usually has none, and then nothing
  /// verifies it beyond its length. Refuses anything [origin] would not allow,
  /// before a byte is sent.
  FetchJob fetchUrl(
    Uri url, {
    AssetId? id,
    FetchUrgency urgency = FetchUrgency.soon,
    void Function(FetchProgress)? onProgress,
  }) {
    if (_closed) {
      throw StateError('This fetcher is closed.');
    }
    if (!origin.allows(url)) {
      // Asked again so that the refusal says which rule, rather than "no".
      origin.beside(url, url.toString());
    }

    final download = _live[url] ??= _Download(this, url, id, _arrived++);

    // The caller is recorded before the download is queued, because the queue
    // skips whatever nobody is waiting for — and until this job is on the
    // list, that is every download, including this one.
    final job = FetchJob._(download, Completer<Uint8List>(), onProgress);
    download._jobs.add(job);

    // Something already queued that is suddenly on screen jumps the queue
    // rather than waiting its turn behind work nobody is looking at.
    download._wantedBy(urgency);
    return job;
  }

  /// The bytes of [id], waiting for them.
  ///
  /// The plain form, for code that has nothing to cancel with and no progress
  /// to show — which is most code, and is what [NetworkAssetSource] is built
  /// on.
  Future<Uint8List> read(
    AssetId id, {
    FetchUrgency urgency = FetchUrgency.soon,
  }) => fetch(id, urgency: urgency).bytes;

  /// Stops everything and lets go of the transport.
  ///
  /// Every job in flight fails with [FetchCancelled]. Fetching afterwards is
  /// a [StateError] rather than a hang.
  void close() {
    if (_closed) return;
    _closed = true;
    for (final download in [..._live.values]) {
      download._abandon();
    }
    _live.clear();
    _waiting.clear();
    transport.close();
  }

  void _sort() {
    // By urgency, then by the order they were first asked for. A plain list
    // sorted on demand rather than a heap: the queue is short — it is bounded
    // by what a scene is loading — and this way the ordering rule is one line
    // that can be read.
    _waiting.sort((a, b) {
      final by = a._urgency.index.compareTo(b._urgency.index);
      return by != 0 ? by : a._arrived.compareTo(b._arrived);
    });
  }

  void _queue(_Download download) {
    download._queued = true;
    _waiting.add(download);
    _sort();
    _pump();
  }

  void _pump() {
    while (_running < concurrent && _waiting.isNotEmpty) {
      final next = _waiting.removeAt(0);
      next._queued = false;
      if (next._jobs.isEmpty) {
        // Everyone gave up while it waited. Forget it too: the map exists so
        // that callers share a download, and there is no longer one to share.
        next._forget();
        continue;
      }
      _running++;
      next._run().whenComplete(() {
        _running--;
        next._forget();
        _pump();
      });
    }
  }

  /// Waits, longer each time, with a little noise.
  ///
  /// The noise matters more than the doubling. A hundred devices that lost the
  /// same server come back at the same moment without it, and knock it over
  /// again the instant it recovers.
  Duration _waitBefore(int attempt) {
    final grown = backoff * pow(2, attempt - 1).toDouble();
    final noise = 1 + (_jitter.nextDouble() - 0.5) * 0.4;
    return Duration(microseconds: (grown.inMicroseconds * noise).round());
  }
}

/// One asset being fetched, however many callers are waiting for it.
class _Download {
  _Download(this._fetcher, this.url, this.id, this._arrived);

  final AssetFetcher _fetcher;
  final Uri url;
  final AssetId? id;
  final int _arrived;

  final List<FetchJob> _jobs = [];
  var _urgency = FetchUrgency.eventually;
  var _queued = false;
  var _started = false;
  var _over = false;
  StreamSubscription<List<int>>? _reading;
  Completer<Uint8List>? _body;

  void _wantedBy(FetchUrgency urgency) {
    final sooner = urgency.index < _urgency.index;
    if (sooner) _urgency = urgency;
    if (!_started && !_queued) {
      _fetcher._queue(this);
    } else if (_queued && sooner) {
      // Already in line, and now wanted more: reorder rather than requeue,
      // which would lose the place it earned by being asked for first.
      _fetcher._sort();
      _fetcher._pump();
    }
  }

  void _drop(FetchJob job) {
    _jobs.remove(job);
    // The last caller has gone. Stop reading — cancelling the subscription is
    // what closes the connection — and let the slot go to something wanted.
    if (_jobs.isEmpty && !_over) _abandon();
  }

  /// Takes this download out of the live map, if it is still the one there.
  ///
  /// Guarded on identity because a later caller may already have started a
  /// fresh download for the same URL, and that one is not ours to forget.
  void _forget() {
    if (identical(_fetcher._live[url], this)) _fetcher._live.remove(url);
  }

  void _abandon() {
    _over = true;
    // Out of the map at once, or the next caller for this URL joins a
    // download that has given up and waits for bytes that will never come.
    _forget();
    _reading?.cancel();
    _reading = null;
    // Whoever is awaiting the body has to be told, or _run never returns and
    // the slot this download holds in the queue is never given back.
    if (_body != null && !_body!.isCompleted) {
      _body!.completeError(FetchCancelled(url));
    }
    _body = null;
    for (final job in [..._jobs]) {
      job._giveUp();
    }
    _jobs.clear();
  }

  void _finish(Uint8List bytes) {
    if (_over) return;
    _over = true;
    for (final job in [..._jobs]) {
      if (!job._completer.isCompleted) job._completer.complete(bytes);
    }
    _jobs.clear();
  }

  void _fail(Object error, StackTrace stack) {
    if (_over) return;
    _over = true;
    for (final job in [..._jobs]) {
      if (!job._completer.isCompleted) {
        job._completer.completeError(error, stack);
      }
    }
    _jobs.clear();
  }

  void _tell(int received, int? total) {
    final progress = FetchProgress(received: received, total: total);
    for (final job in [..._jobs]) {
      job.onProgress?.call(progress);
    }
  }

  ContentHash? get _wanted =>
      id == null ? null : _fetcher.manifest?.entries[id]?.hash;

  Future<void> _run() async {
    _started = true;
    final policy = _fetcher.origin.policy;
    final known = await _fetcher.records.get(url);

    // Already held and still current? A hash the manifest names is a promise
    // about the bytes, not about the URL, so a cached copy that matches it
    // needs no server at all — not even a round trip to ask.
    final wanted = _wanted;
    if (wanted != null) {
      final held = await _fetcher.store.get(wanted);
      if (held != null) {
        await _fetcher.records.put(
          url,
          FetchRecord(
            hash: wanted,
            bytes: held.length,
            etag: known?.etag,
            fetched: DateTime.now(),
          ),
        );
        _finish(held);
        return;
      }
    }

    // Starts as what was on disk and is cleared if it turns out to be no use,
    // so that a later attempt does not offer the server a tag we have already
    // learned we cannot honour.
    var held = known;
    var partial = BytesBuilder(copy: false);
    String? tag = known?.etag;
    Object? last;
    StackTrace? where;

    for (var attempt = 1; attempt <= _fetcher.attempts; attempt++) {
      if (_over) return;
      if (attempt > 1) {
        await Future<void>.delayed(_fetcher._waitBefore(attempt - 1));
        if (_over) return;
      }

      try {
        final resuming = partial.length > 0;
        final reply = await _fetcher.transport.send(
          FetchRequest(
            url,
            // Only on a first, whole request: asking a server both to resume
            // and to tell us whether the file changed is asking two questions
            // whose answers contradict each other.
            ifNoneMatch: resuming ? null : held?.etag,
            from: resuming ? partial.length : null,
          ),
        );
        if (_over) return;

        if (reply.isNotModified) {
          final cached = held == null
              ? null
              : await _fetcher.store.get(held.hash);
          if (cached != null) {
            await _fetcher.records.put(url, held!.confirmedAt(DateTime.now()));
            _finish(cached);
            return;
          }
          // The server says the copy we named is current and we no longer
          // have it — the system emptied the cache, most likely. Forget the
          // record, here and in memory, so that the attempt that follows asks
          // for the whole thing instead of being told 304 all over again.
          await _fetcher.records.remove(url);
          held = null;
          tag = null;
          last = reply.status;
          partial = BytesBuilder(copy: false);
          continue;
        }

        if (reply.status == 200 || reply.status == 206) {
          // A server that ignored the range, or one whose copy changed under
          // us, answers 200 to a resumed request. Either way what is held is
          // the start of a different file.
          if (resuming && reply.status != 206) {
            partial = BytesBuilder(copy: false);
          }
          if (reply.etag != null && tag != null && reply.etag != tag) {
            partial = BytesBuilder(copy: false);
          }
          tag = reply.etag ?? tag;

          final total = reply.length;
          if (total != null && total > policy.maxBytes) {
            throw FetchRefused(
              '$url',
              'it is $total bytes and the policy allows ${policy.maxBytes}',
            );
          }

          final bytes = await _read(reply, partial, total, policy);
          if (_over) return;

          final hash = ContentHash.of(bytes);
          final promised = _wanted;
          if (promised != null && hash != promised) {
            throw FetchCorrupt(url, wanted: promised, got: hash);
          }

          await _fetcher.store.put(bytes);
          await _fetcher.records.put(
            url,
            FetchRecord(
              hash: hash,
              bytes: bytes.length,
              etag: tag,
              fetched: DateTime.now(),
            ),
          );
          _finish(bytes);
          return;
        }

        if (!reply.mayWorkLater) {
          _fail(FetchFailed(url, attempt, reply.status), StackTrace.current);
          return;
        }
        last = reply.status;
      } on FetchCorrupt catch (error, stack) {
        // Not retried. See FetchCorrupt.
        _fail(error, stack);
        return;
      } on FetchRefused catch (error, stack) {
        _fail(error, stack);
        return;
      } on FetchCancelled {
        return;
      } catch (error, stack) {
        last = error;
        where = stack;
      }
    }

    if (_over) return;

    // Out of attempts. An old copy is better than a blank screen, and being
    // offline is the ordinary reason to be here.
    if (held != null) {
      final cached = await _fetcher.store.get(held.hash);
      if (cached != null) {
        _finish(cached);
        return;
      }
    }
    _fail(
      FetchFailed(url, _fetcher.attempts, last ?? 'no answer'),
      where ?? StackTrace.current,
    );
  }

  /// Reads a reply's body into [partial], stopping early where it can.
  ///
  /// Two limits are enforced while the bytes are still arriving rather than
  /// after. A length that passes what the policy allows stops the download at
  /// the byte that passes it; and a picture whose header says it is bigger
  /// than the policy allows stops it as soon as the header has arrived —
  /// which for every format here is within the first few dozen bytes. A
  /// sixteen-gigapixel PNG is a small download and an enormous decode, so the
  /// only useful place to catch it is before the rest of it is even sent.
  Future<Uint8List> _read(
    FetchReply reply,
    BytesBuilder partial,
    int? total,
    FetchPolicy policy,
  ) {
    final done = _body = Completer<Uint8List>();
    var looked = false;

    void stop(Object error, StackTrace stack) {
      _reading?.cancel();
      _reading = null;
      if (!done.isCompleted) done.completeError(error, stack);
    }

    _reading = reply.body.listen(
      (chunk) {
        partial.add(chunk);
        if (partial.length > policy.maxBytes) {
          stop(
            FetchRefused(
              '$url',
              'it passed the ${policy.maxBytes} bytes the policy allows',
            ),
            StackTrace.current,
          );
          return;
        }

        if (!looked && partial.length >= 64) {
          looked = true;
          // toBytes leaves the builder alone, unlike takeBytes, so this is a
          // look rather than a read. It costs one copy of what has arrived so
          // far, once per download, and what has arrived so far at this point
          // is the first chunk.
          final size = pictureSizeOf(partial.toBytes());
          if (size != null && size.pixels > policy.maxPixels) {
            stop(
              FetchRefused(
                '$url',
                'it is a $size picture, which is ${size.pixels} pixels and '
                    'the policy allows ${policy.maxPixels}',
              ),
              StackTrace.current,
            );
            return;
          }
        }

        _tell(partial.length, total);
      },
      onError: (Object error, StackTrace stack) => stop(error, stack),
      onDone: () {
        _reading = null;
        if (!done.isCompleted) done.complete(partial.toBytes());
        if (identical(_body, done)) _body = null;
      },
      cancelOnError: true,
    );
    return done.future;
  }
}
