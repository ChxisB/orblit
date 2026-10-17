import 'dart:async';
import 'dart:math' as math;
import 'dart:typed_data';

/// How soon a want is needed.
enum RangePriority {
  /// Someone is waiting on it. Sent before anything that can wait.
  now,

  /// Wanted soon, with nobody waiting. Sent when no urgent want is queued.
  later,
}

/// How a [RangeQueue] groups wants into requests.
///
/// The defaults are a guess and have not been timed on a real host. A host
/// with a long round trip wants a larger [mergeGap] and [maxBatch]. One that
/// is quick to answer and slow to send wants smaller ones.
final class RangeBatching {
  const RangeBatching({
    this.mergeGap = 16 * 1024,
    this.maxBatch = 1024 * 1024,
    this.parallel = 4,
    this.delay = const Duration(milliseconds: 2),
  });

  /// Two wants with at most this many bytes between them are one request, and
  /// the bytes between are fetched too.
  final int mergeGap;

  /// The most bytes a joined request grows to. One want larger than this is
  /// still sent whole.
  final int maxBatch;

  /// How many requests may be out at once.
  final int parallel;

  /// How long a want waits for others to join it before it is sent.
  final Duration delay;
}

/// Asks for byte ranges of one file through [fetch], as fewer and larger
/// requests.
///
/// A want waits for [RangeBatching.delay], so wants made close together can
/// join. Wants whose gap is within [RangeBatching.mergeGap] are sent as one
/// request, cut at [RangeBatching.maxBatch]. At most [RangeBatching.parallel]
/// requests are out. The most urgent wait is sent first, and a want that can
/// wait is sent only when no urgent one is queued, and never in the last free
/// request.
///
/// A want for the same bytes as one still queued is the same want. Its askers
/// share the answer, and it takes the more urgent of the two priorities. One
/// already sent is not looked for, so the same bytes can be fetched twice.
///
/// shortcut: a want inside a request that went out for another is not
/// answered from it, and the bytes between two joined wants are thrown away
/// once the wants are answered. Upgrade to a small cache of recent ranges when
/// a pack is read with a lot of overlap.
///
/// shortcut: a request starts at the lowest offset of its priority, so under
/// a steady stream of low offsets a want at a high one waits. Wants of the two
/// priorities are never joined. The queue is scanned in full for each want and
/// each request, which is fine for hundreds of wants at once. Upgrade to
/// starting from the oldest want, and to an ordered structure, when a pack is
/// read that hard.
final class RangeQueue {
  RangeQueue({
    required Future<Uint8List> Function(int at, int bytes) fetch,
    this.batching = const RangeBatching(),
  }) : _fetch = fetch {
    if (batching.mergeGap < 0) {
      throw ArgumentError.value(
        batching.mergeGap,
        'mergeGap',
        'Cannot be negative.',
      );
    }
    if (batching.maxBatch < 1) {
      throw ArgumentError.value(
        batching.maxBatch,
        'maxBatch',
        'Must be at least 1.',
      );
    }
    if (batching.parallel < 1) {
      throw ArgumentError.value(
        batching.parallel,
        'parallel',
        'Must be at least 1.',
      );
    }
    if (batching.delay.isNegative) {
      throw ArgumentError.value(batching.delay, 'delay', 'Cannot be negative.');
    }
  }

  final Future<Uint8List> Function(int at, int bytes) _fetch;
  final RangeBatching batching;

  final List<_Want> _waiting = [];
  Timer? _timer;
  var _running = 0;

  /// [bytes] bytes from [at]. Fails with a [RangeError] for a range that
  /// cannot exist.
  Future<Uint8List> want(
    int at,
    int bytes, {
    RangePriority priority = RangePriority.now,
  }) {
    if (at < 0 || bytes < 1) {
      return Future.error(RangeError('Cannot want $bytes bytes at $at.'));
    }
    for (final queued in _waiting) {
      if (queued.at == at && queued.bytes == bytes) {
        if (priority == RangePriority.now) queued.priority = priority;
        return queued.reply.future.then(Uint8List.fromList);
      }
    }
    final want = _Want(at, bytes, priority);
    _waiting.add(want);
    _timer ??= Timer(batching.delay, _fire);
    return want.reply.future;
  }

  void _fire() {
    _timer = null;
    _pump();
  }

  void _pump() {
    while (_waiting.isNotEmpty) {
      final urgent = _waiting.any((want) => want.priority == RangePriority.now);
      final priority = urgent ? RangePriority.now : RangePriority.later;
      if (_running >= _slotsFor(priority)) return;
      _send(_takeBatch(priority));
    }
  }

  /// A want that can wait never takes the last free request, so a want made
  /// urgent later has somewhere to go. With one request allowed there is
  /// nothing to keep back.
  int _slotsFor(RangePriority priority) => priority == RangePriority.now
      ? batching.parallel
      : math.max(1, batching.parallel - 1);

  /// The next wants of [priority] to send as one request, taken off the queue.
  List<_Want> _takeBatch(RangePriority priority) {
    final pool = _waiting.where((want) => want.priority == priority).toList()
      ..sort((a, b) => a.at.compareTo(b.at));
    final batch = [pool.first];
    var end = pool.first.end;
    for (final want in pool.skip(1)) {
      if (want.at - end > batching.mergeGap) break;
      final grown = math.max(end, want.end);
      if (grown - batch.first.at > batching.maxBatch) break;
      batch.add(want);
      end = grown;
    }
    _waiting.removeWhere(batch.contains);
    return batch;
  }

  void _send(List<_Want> batch) {
    _running++;
    final at = batch.first.at;
    final end = batch.map((want) => want.end).reduce(math.max);
    unawaited(_answer(batch, at, end - at));
  }

  Future<void> _answer(List<_Want> batch, int at, int bytes) async {
    try {
      final got = await _fetch(at, bytes);
      final parts = [
        for (final want in batch)
          Uint8List.fromList(
            Uint8List.sublistView(got, want.at - at, want.end - at),
          ),
      ];
      for (var i = 0; i < batch.length; i++) {
        batch[i].reply.complete(parts[i]);
      }
    } catch (error, stack) {
      for (final want in batch) {
        want.reply.completeError(error, stack);
      }
    } finally {
      _running--;
      _pump();
    }
  }
}

final class _Want {
  _Want(this.at, this.bytes, this.priority);

  final int at;
  final int bytes;
  RangePriority priority;
  final Completer<Uint8List> reply = Completer();

  int get end => at + bytes;
}
