import 'ring.dart';

/// Microseconds on a clock that only goes forward.
typedef ProfileClock = int Function();

/// Where the frame went on the Dart side.
///
/// A game calls [frame] once at the top of each frame, wraps the parts worth
/// timing in [begin] and [end], and sets what it knows with [gauge] and
/// [count]. [snapshot] is all of it as JSON.
///
/// Spans are timed, not sampled. The engine's own are `scene build` and `diff`
/// in the document view and `upload` and `submit` in the view. A game adds
/// `input`, `update` and whatever else it wants to see.
abstract interface class Profiler {
  /// Records. [frames] is how many frame times, and how many frames of each
  /// span, are kept. [heap] reads the resident size in bytes, and is asked at
  /// most once in [heapEvery], so one sample a second by default. Without it
  /// there is no heap line. [clock] is for a test.
  ///
  /// shortcut: the heap line is the resident size the caller reads, for
  /// instance `ProcessInfo.currentRss`. The Dart heap on its own needs the VM
  /// service, so wire it in a host that has one.
  factory Profiler({
    int frames,
    int heapSamples,
    Duration heapEvery,
    int Function()? heap,
    ProfileClock? clock,
  }) = _Recording;

  /// Takes every call and keeps nothing, so a build that ships with it off
  /// pays a virtual call per span.
  const factory Profiler.off() = _Off;

  /// Marks the start of a frame, and so the end of the one before. The
  /// first call only starts the clock.
  void frame();

  /// The time a span starts, to hand to [end].
  int begin();

  /// Adds the time since [started] to [name] for this frame. A span that runs
  /// many times in a frame is their sum.
  void end(String name, int started);

  /// Sets what [name] is now: entities, draws, bodies awake.
  void gauge(String name, double value);

  /// Adds [by] to the running count under [tag]. For what the code that
  /// allocates knows about, such as meshes built.
  void count(String tag, [int by = 1]);

  /// Everything kept, as values `jsonEncode` takes. Empty when off.
  Map<String, Object?> snapshot();
}

final class _Off implements Profiler {
  const _Off();

  @override
  void frame() {}

  @override
  int begin() => 0;

  @override
  void end(String name, int started) {}

  @override
  void gauge(String name, double value) {}

  @override
  void count(String tag, [int by = 1]) {}

  @override
  Map<String, Object?> snapshot() => const {};
}

/// One span's running sum for the frame in progress, and its past frames.
final class _Span {
  _Span(int frames) : ring = Ring(frames);

  final Ring ring;
  int sum = 0;
}

final class _Recording implements Profiler {
  _Recording({
    int frames = 600,
    int heapSamples = 600,
    Duration heapEvery = const Duration(seconds: 1),
    this.heap,
    ProfileClock? clock,
  }) : _frameCapacity = frames,
       _heapEvery = heapEvery.inMicroseconds,
       _clock = clock ?? _wallClock(),
       _frames = Ring(frames),
       _heapTimes = Ring(heapSamples),
       _heapBytes = Ring(heapSamples);

  final int Function()? heap;
  final int _frameCapacity;
  final int _heapEvery;
  final ProfileClock _clock;

  final Ring _frames;
  final Map<String, _Span> _spans = {};
  final Map<String, double> _gauges = {};
  final Map<String, int> _counts = {};
  final Ring _heapTimes;
  final Ring _heapBytes;

  int? _frameAt;
  int? _heapAt;

  static ProfileClock _wallClock() {
    final watch = Stopwatch()..start();
    return () => watch.elapsedMicroseconds;
  }

  @override
  void frame() {
    final now = _clock();
    final before = _frameAt;
    _frameAt = now;
    if (before != null) _frames.add((now - before).toDouble());
    for (final span in _spans.values) {
      // The first call has no frame behind it, so what ran before it is not
      // a frame's worth.
      if (before != null) span.ring.add(span.sum.toDouble());
      span.sum = 0;
    }
    _sampleHeap(now);
  }

  @override
  int begin() => _clock();

  @override
  void end(String name, int started) {
    (_spans[name] ??= _newSpan()).sum += _clock() - started;
  }

  // A span that first runs on frame 300 was nothing on the 299 before it, so
  // its mean covers the same frames as the frame times do.
  _Span _newSpan() {
    final span = _Span(_frameCapacity);
    for (var i = 0; i < _frames.length; i++) {
      span.ring.add(0);
    }
    return span;
  }

  @override
  void gauge(String name, double value) => _gauges[name] = value;

  @override
  void count(String tag, [int by = 1]) =>
      _counts[tag] = (_counts[tag] ?? 0) + by;

  void _sampleHeap(int now) {
    final read = heap;
    if (read == null) return;
    final last = _heapAt;
    if (last != null && now - last < _heapEvery) return;
    _heapAt = now;
    _heapTimes.add(now / 1e6);
    _heapBytes.add(read().toDouble());
  }

  @override
  Map<String, Object?> snapshot() {
    final mean = _frames.mean;
    return {
      'frames': {
        'count': _frames.length,
        'fps': mean <= 0 ? null : 1e6 / mean,
        'meanMs': mean / 1000,
        'p95Ms': _frames.percentile(0.95) / 1000,
        'maxMs': _frames.max / 1000,
        'recentMs': [for (final micros in _frames.values) micros / 1000],
      },
      'spans': {
        for (final MapEntry(:key, :value) in _spans.entries)
          key: {
            'meanMs': value.ring.mean / 1000,
            'maxMs': value.ring.max / 1000,
            'lastMs': value.ring.last / 1000,
          },
      },
      'gauges': Map<String, Object?>.of(_gauges),
      'counts': Map<String, Object?>.of(_counts),
      'heap': _heapSnapshot(),
    };
  }

  Map<String, Object?> _heapSnapshot() {
    final seconds = _heapTimes.values;
    final bytes = _heapBytes.values;
    final perSecond = _slope(seconds, bytes);
    return {
      'samples': [
        for (var i = 0; i < seconds.length; i++)
          [seconds[i] - seconds.first, bytes[i]],
      ],
      'bytesPerMinute': perSecond == null ? null : perSecond * 60,
    };
  }
}

/// How much [ys] rises for each step in [xs], by least squares. Null when
/// there are too few points to draw a line.
double? _slope(List<double> xs, List<double> ys) {
  final n = xs.length;
  if (n < 2) return null;
  final meanX = xs.reduce((a, b) => a + b) / n;
  final meanY = ys.reduce((a, b) => a + b) / n;
  var along = 0.0;
  var spread = 0.0;
  for (var i = 0; i < n; i++) {
    along += (xs[i] - meanX) * (ys[i] - meanY);
    spread += (xs[i] - meanX) * (xs[i] - meanX);
  }
  return spread == 0 ? null : along / spread;
}
