using System.Text.Json;

namespace Orblit.Profile.Tests;

public class ProfilerTests
{
    // A clock the test moves by hand, in microseconds.
    private sealed class FakeClock
    {
        public long Now { get; set; }

        public long Call() => Now;
    }

    private readonly FakeClock _clock = new();

    private static IReadOnlyDictionary<string, object?> Section(IReadOnlyDictionary<string, object?> from, string name) =>
        (IReadOnlyDictionary<string, object?>)from[name]!;

    private static double Number(object? value) => (double)value!;

    [Fact]
    public void FramesAreTheTimeBetweenOneFrameCallAndTheNext()
    {
        var profiler = Profiler.Create(clock: _clock.Call);
        profiler.Frame();
        foreach (var ms in new[] { 16, 17, 33 })
        {
            _clock.Now += ms * 1000;
            profiler.Frame();
        }

        var frames = Section(profiler.Snapshot(), "frames");
        Assert.Equal(3, frames["count"]);
        Assert.Equal(33.0, Number(frames["maxMs"]));
        Assert.Equal(22.0, Number(frames["meanMs"]), 1e-9);
        Assert.Equal(1000.0 / 22, Number(frames["fps"]), 1e-9);
        Assert.Equal(new[] { 16.0, 17.0, 33.0 }, (List<double>)frames["recentMs"]!);
    }

    [Fact]
    public void FramesKeepOnlyTheLastFewSoMemoryDoesNotGrow()
    {
        var profiler = Profiler.Create(frames: 4, clock: _clock.Call);
        profiler.Frame();
        for (var ms = 1; ms <= 10; ms++)
        {
            _clock.Now += ms * 1000;
            profiler.Frame();
        }

        Assert.Equal(new[] { 7.0, 8.0, 9.0, 10.0 }, (List<double>)Section(profiler.Snapshot(), "frames")["recentMs"]!);
    }

    [Fact]
    public void FramesSayNothingAboutSpeedBeforeThereIsAFrameToMeasure()
    {
        var frames = Section(Profiler.Create(clock: _clock.Call).Snapshot(), "frames");

        Assert.Equal(0, frames["count"]);
        Assert.Null(frames["fps"]);
    }

    [Fact]
    public void SpansAddUpWithinAFrameAndAreReadPerFrame()
    {
        var profiler = Profiler.Create(clock: _clock.Call);
        profiler.Frame();
        for (var frame = 0; frame < 2; frame++)
        {
            for (var i = 0; i < 3; i++)
            {
                var started = profiler.Begin();
                _clock.Now += 2000;
                profiler.End("update", started);
            }

            _clock.Now += 10000;
            profiler.Frame();
        }

        var update = Section(Section(profiler.Snapshot(), "spans"), "update");
        Assert.Equal(6.0, Number(update["lastMs"]));
        Assert.Equal(6.0, Number(update["meanMs"]));
    }

    [Fact]
    public void SpansCountAFrameWhereTheSpanDidNotRunAsNothing()
    {
        var profiler = Profiler.Create(clock: _clock.Call);
        profiler.Frame();
        var started = profiler.Begin();
        _clock.Now += 4000;
        profiler.End("diff", started);
        profiler.Frame();
        _clock.Now += 1000;
        profiler.Frame();

        var diff = Section(Section(profiler.Snapshot(), "spans"), "diff");
        Assert.Equal(2.0, Number(diff["meanMs"]));
        Assert.Equal(0.0, Number(diff["lastMs"]));
        Assert.Equal(4.0, Number(diff["maxMs"]));
    }

    [Fact]
    public void SpansCountTheFramesBeforeASpanFirstRanAsNothing()
    {
        var profiler = Profiler.Create(clock: _clock.Call);
        profiler.Frame();
        for (var i = 0; i < 3; i++)
        {
            _clock.Now += 1000;
            profiler.Frame();
        }

        var started = profiler.Begin();
        _clock.Now += 8000;
        profiler.End("late", started);
        profiler.Frame();

        var late = Section(Section(profiler.Snapshot(), "spans"), "late");
        Assert.Equal(2.0, Number(late["meanMs"]));
        Assert.Equal(8.0, Number(late["maxMs"]));
    }

    [Fact]
    public void SpansIgnoreWhatRanBeforeTheFirstFrame()
    {
        var profiler = Profiler.Create(clock: _clock.Call);
        var started = profiler.Begin();
        _clock.Now += 9000;
        profiler.End("load", started);
        profiler.Frame();
        _clock.Now += 1000;
        profiler.Frame();

        Assert.Equal(0.0, Number(Section(Section(profiler.Snapshot(), "spans"), "load")["maxMs"]));
    }

    [Fact]
    public void AGaugeHoldsTheLastValueAndACountAddsUp()
    {
        var profiler = Profiler.Create(clock: _clock.Call);
        profiler.Gauge("entities", 12);
        profiler.Gauge("entities", 30);
        profiler.Count("mesh");
        profiler.Count("mesh", 4);

        var snapshot = profiler.Snapshot();
        var gauges = Section(snapshot, "gauges");
        var counts = Section(snapshot, "counts");
        Assert.Equal(30.0, Number(Assert.Single(gauges).Value));
        Assert.Equal(5L, Assert.Single(counts).Value);
    }

    [Fact]
    public void HeapIsReadOnceInTheIntervalNotEveryFrame()
    {
        var reads = 0;
        var profiler = Profiler.Create(heap: () => ++reads, clock: _clock.Call);
        for (var i = 0; i < 300; i++)
        {
            profiler.Frame();
            _clock.Now += 16667;
        }

        // 300 frames of 16.7 ms are five seconds.
        Assert.InRange(reads, 5, 6);
    }

    [Fact]
    public void HeapNamesALeakAsBytesAMinute()
    {
        var bytes = 100000000L;
        var profiler = Profiler.Create(heap: () => bytes, clock: _clock.Call);
        for (var second = 0; second < 120; second++)
        {
            profiler.Frame();
            _clock.Now += 1000000;
            bytes += 50000;
        }

        var heap = Section(profiler.Snapshot(), "heap");
        Assert.Equal(3000000.0, Number(heap["bytesPerMinute"]), 1);
    }

    [Fact]
    public void HeapIsFlatWhenNothingGrows()
    {
        var profiler = Profiler.Create(heap: () => 80000000, clock: _clock.Call);
        for (var second = 0; second < 30; second++)
        {
            profiler.Frame();
            _clock.Now += 1000000;
        }

        Assert.Equal(0.0, Number(Section(profiler.Snapshot(), "heap")["bytesPerMinute"]), 1e-6);
    }

    [Fact]
    public void HeapHasNoLineWithoutAReaderOrWithOneSample()
    {
        var none = Profiler.Create(clock: _clock.Call);
        none.Frame();
        var one = Profiler.Create(heap: () => 1, clock: _clock.Call);
        one.Frame();

        Assert.Empty((List<double[]>)Section(none.Snapshot(), "heap")["samples"]!);
        Assert.Null(Section(one.Snapshot(), "heap")["bytesPerMinute"]);
    }

    [Fact]
    public void HeapKeepsTheLastSamplesInSecondsFromTheFirst()
    {
        var profiler = Profiler.Create(heapSamples: 3, heap: () => 7, clock: _clock.Call);
        for (var second = 0; second < 6; second++)
        {
            profiler.Frame();
            _clock.Now += 1000000;
        }

        var samples = (List<double[]>)Section(profiler.Snapshot(), "heap")["samples"]!;
        Assert.Equal(3, samples.Count);
        Assert.Equal(new[] { 0.0, 7.0 }, samples[0]);
        Assert.Equal(new[] { 1.0, 7.0 }, samples[1]);
        Assert.Equal(new[] { 2.0, 7.0 }, samples[2]);
    }

    [Fact]
    public void ASnapshotIsPlainJson()
    {
        var profiler = Profiler.Create(heap: () => 5, clock: _clock.Call);
        profiler.Frame();
        var started = profiler.Begin();
        _clock.Now += 1000;
        profiler.End("update", started);
        profiler.Gauge("draws", 4);
        profiler.Frame();

        Assert.Null(Record.Exception(() => JsonSerializer.Serialize(profiler.Snapshot())));
    }

    [Fact]
    public void OffTakesEveryCallAndKeepsNothing()
    {
        var profiler = Profiler.Off;

        profiler.Frame();
        profiler.End("update", profiler.Begin());
        profiler.Gauge("draws", 4);
        profiler.Count("mesh");

        Assert.Empty(profiler.Snapshot());
    }

    [Fact]
    public void ARingWithNoRoomIsRefused() =>
        Assert.ThrowsAny<ArgumentException>(() => Profiler.Create(frames: 0));
}
