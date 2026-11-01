using System.Collections.Frozen;

namespace Orblit.Profile;

/// <summary>Microseconds on a clock that only goes forward.</summary>
public delegate long ProfileClock();

/// <summary>
/// Where the frame went on the managed side.
/// A game calls <see cref="Frame"/> once at the top of each frame, wraps the
/// parts worth timing in <see cref="Begin"/> and <see cref="End"/>, and sets
/// what it knows with <see cref="Gauge"/> and <see cref="Count"/>.
/// <see cref="Snapshot"/> is all of it as JSON.
/// Spans are timed, not sampled. The engine's own are <c>scene build</c> and
/// <c>diff</c> in the document view and <c>upload</c> and <c>submit</c> in the
/// view. A game adds <c>input</c>, <c>update</c> and whatever else it wants to
/// see.
/// </summary>
public abstract class Profiler
{
    /// <summary>
    /// Takes every call and keeps nothing, so a build that ships with it off
    /// pays a virtual call per span.
    /// </summary>
    public static Profiler Off { get; } = new OffProfiler();

    /// <summary>
    /// Records. <paramref name="frames"/> is how many frame times, and how many
    /// frames of each span, are kept. <paramref name="heap"/> reads the resident
    /// size in bytes, and is asked at most once in
    /// <paramref name="heapEvery"/>, so one sample a second by default. Without
    /// it there is no heap line. <paramref name="clock"/> is for a test.
    /// </summary>
    /// <remarks>
    /// shortcut: the heap line is the resident size the caller reads, for
    /// instance <c>Environment.WorkingSet</c>. The managed heap on its own is
    /// <c>GC.GetTotalMemory</c>, which the caller may pass instead.
    /// </remarks>
    public static Profiler Create(
        int frames = 600,
        int heapSamples = 600,
        TimeSpan? heapEvery = null,
        Func<long>? heap = null,
        ProfileClock? clock = null) =>
        new Recording(frames, heapSamples, heapEvery ?? TimeSpan.FromSeconds(1), heap, clock);

    /// <summary>
    /// Marks the start of a frame, and so the end of the one before. The first
    /// call only starts the clock.
    /// </summary>
    public abstract void Frame();

    /// <summary>The time a span starts, to hand to <see cref="End"/>.</summary>
    public abstract long Begin();

    /// <summary>
    /// Adds the time since <paramref name="started"/> to <paramref name="name"/>
    /// for this frame. A span that runs many times in a frame is their sum.
    /// </summary>
    public abstract void End(string name, long started);

    /// <summary>Sets what <paramref name="name"/> is now: entities, draws, bodies awake.</summary>
    public abstract void Gauge(string name, double value);

    /// <summary>
    /// Adds <paramref name="by"/> to the running count under
    /// <paramref name="tag"/>. For what the code that allocates knows about,
    /// such as meshes built.
    /// </summary>
    public abstract void Count(string tag, long by = 1);

    /// <summary>Everything kept, as values <c>System.Text.Json</c> takes. Empty when off.</summary>
    public abstract IReadOnlyDictionary<string, object?> Snapshot();

    private sealed class OffProfiler : Profiler
    {
        public override void Frame()
        {
        }

        public override long Begin() => 0;

        public override void End(string name, long started)
        {
        }

        public override void Gauge(string name, double value)
        {
        }

        public override void Count(string tag, long by = 1)
        {
        }

        public override IReadOnlyDictionary<string, object?> Snapshot() =>
            FrozenDictionary<string, object?>.Empty;
    }

    /// <summary>One span's running sum for the frame in progress, and its past frames.</summary>
    private sealed class TimedSpan(int frames)
    {
        public Ring Ring { get; } = new(frames);

        public long Sum { get; set; }
    }

    private sealed class Recording : Profiler
    {
        private readonly int _frameCapacity;
        private readonly long _heapEvery;
        private readonly Func<long>? _heap;
        private readonly ProfileClock _clock;

        private readonly Ring _frames;
        private readonly OrderedDictionary<string, TimedSpan> _spans = [];
        private readonly OrderedDictionary<string, double> _gauges = [];
        private readonly OrderedDictionary<string, long> _counts = [];
        private readonly Ring _heapTimes;
        private readonly Ring _heapBytes;

        private long? _frameAt;
        private long? _heapAt;

        public Recording(int frames, int heapSamples, TimeSpan heapEvery, Func<long>? heap, ProfileClock? clock)
        {
            _frameCapacity = frames;
            _heapEvery = heapEvery.Ticks / TimeSpan.TicksPerMicrosecond;
            _heap = heap;
            _clock = clock ?? WallClock();
            _frames = new Ring(frames);
            _heapTimes = new Ring(heapSamples);
            _heapBytes = new Ring(heapSamples);
        }

        public override void Frame()
        {
            var now = _clock();
            var before = _frameAt;
            _frameAt = now;
            if (before is { } started)
            {
                _frames.Add(now - started);
            }

            foreach (var span in _spans.Values)
            {
                // The first call has no frame behind it, so what ran before it is not
                // a frame's worth.
                if (before is not null)
                {
                    span.Ring.Add(span.Sum);
                }

                span.Sum = 0;
            }

            SampleHeap(now);
        }

        public override long Begin() => _clock();

        public override void End(string name, long started)
        {
            if (!_spans.TryGetValue(name, out var span))
            {
                span = NewSpan();
                _spans[name] = span;
            }

            span.Sum += _clock() - started;
        }

        public override void Gauge(string name, double value) => _gauges[name] = value;

        public override void Count(string tag, long by = 1) =>
            _counts[tag] = (_counts.TryGetValue(tag, out var current) ? current : 0) + by;

        public override IReadOnlyDictionary<string, object?> Snapshot()
        {
            var mean = _frames.Mean;
            var spans = new OrderedDictionary<string, object?>();
            foreach (var (name, span) in _spans)
            {
                spans[name] = new Dictionary<string, object?>
                {
                    ["meanMs"] = span.Ring.Mean / 1000,
                    ["maxMs"] = span.Ring.Max / 1000,
                    ["lastMs"] = span.Ring.Last / 1000,
                };
            }

            return new Dictionary<string, object?>
            {
                ["frames"] = new Dictionary<string, object?>
                {
                    ["count"] = _frames.Length,
                    ["fps"] = mean <= 0 ? null : 1e6 / mean,
                    ["meanMs"] = mean / 1000,
                    ["p95Ms"] = _frames.Percentile(0.95) / 1000,
                    ["maxMs"] = _frames.Max / 1000,
                    ["recentMs"] = _frames.Values.Select(micros => micros / 1000).ToList(),
                },
                ["spans"] = spans,
                ["gauges"] = Boxed(_gauges),
                ["counts"] = Boxed(_counts),
                ["heap"] = HeapSnapshot(),
            };
        }

        // A snapshot holds objects, and a dictionary of doubles is not one.
        private static OrderedDictionary<string, object?> Boxed<T>(OrderedDictionary<string, T> from)
        {
            var boxed = new OrderedDictionary<string, object?>();
            foreach (var (key, value) in from)
            {
                boxed[key] = value;
            }

            return boxed;
        }

        private static ProfileClock WallClock()
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            return () => watch.Elapsed.Ticks / TimeSpan.TicksPerMicrosecond;
        }

        // A span that first runs on frame 300 was nothing on the 299 before it, so
        // its mean covers the same frames as the frame times do.
        private TimedSpan NewSpan()
        {
            var span = new TimedSpan(_frameCapacity);
            for (var i = 0; i < _frames.Length; i++)
            {
                span.Ring.Add(0);
            }

            return span;
        }

        private void SampleHeap(long now)
        {
            if (_heap is null)
            {
                return;
            }

            if (_heapAt is { } last && now - last < _heapEvery)
            {
                return;
            }

            _heapAt = now;
            _heapTimes.Add(now / 1e6);
            _heapBytes.Add(_heap());
        }

        private Dictionary<string, object?> HeapSnapshot()
        {
            var seconds = _heapTimes.Values;
            var bytes = _heapBytes.Values;
            var perSecond = Slope(seconds, bytes);
            return new Dictionary<string, object?>
            {
                ["samples"] = seconds.Select((s, i) => new[] { s - seconds[0], bytes[i] }).ToList(),
                ["bytesPerMinute"] = perSecond is null ? null : perSecond * 60,
            };
        }

        /// <summary>
        /// How much <paramref name="ys"/> rises for each step in
        /// <paramref name="xs"/>, by least squares. Null when there are too few
        /// points to draw a line.
        /// </summary>
        private static double? Slope(List<double> xs, List<double> ys)
        {
            var n = xs.Count;
            if (n < 2)
            {
                return null;
            }

            // Summed in order, so the total is the same to the last digit.
            var sumX = 0.0;
            var sumY = 0.0;
            for (var i = 0; i < n; i++)
            {
                sumX += xs[i];
                sumY += ys[i];
            }

            var meanX = sumX / n;
            var meanY = sumY / n;
            var along = 0.0;
            var spread = 0.0;
            for (var i = 0; i < n; i++)
            {
                along += (xs[i] - meanX) * (ys[i] - meanY);
                spread += (xs[i] - meanX) * (xs[i] - meanX);
            }

            return spread == 0 ? null : along / spread;
        }
    }
}
