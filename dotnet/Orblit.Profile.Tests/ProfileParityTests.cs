using System.Text.Json;
using Orblit.Parity;

namespace Orblit.Profile.Tests;

public class ProfileParityTests
{
    private static void Replay(Profiler profiler, JsonElement ops, Action<long> advance, Action<long> setHeap)
    {
        foreach (var op in ops.EnumerateArray())
        {
            switch (op[0].GetString())
            {
                case "frame":
                    profiler.Frame();
                    break;
                case "advance":
                    advance(op[1].GetInt64());
                    break;
                case "span":
                    var started = profiler.Begin();
                    advance(op[2].GetInt64());
                    profiler.End(op[1].GetString()!, started);
                    break;
                case "gauge":
                    profiler.Gauge(op[1].GetString()!, op[2].GetDouble());
                    break;
                case "count":
                    profiler.Count(op[1].GetString()!, op[2].GetInt64());
                    break;
                case "setHeap":
                    setHeap(op[1].GetInt64());
                    break;
                default:
                    throw new InvalidDataException($"unknown op {op}");
            }
        }
    }

    [Fact]
    public void ASnapshotMatchesTheDartTwin()
    {
        var fixture = Fixture.Load("orblit_profile", "profile");

        foreach (var c in fixture.Cases)
        {
            long now = 0;
            long heap = 0;
            var profiler = Profiler.Create(
                frames: c.GetProperty("frames").GetInt32(),
                heapSamples: c.GetProperty("heapSamples").GetInt32(),
                heap: () => heap,
                clock: () => now);

            Replay(profiler, c.GetProperty("ops"), by => now += by, bytes => heap = bytes);

            var actual = JsonSerializer.SerializeToElement(profiler.Snapshot());
            var difference = JsonCompare.FirstDifference(c.GetProperty("expect"), actual);
            Assert.True(difference is null, $"{difference}, from Dart commit {fixture.DartCommit}");
        }

        Assert.True(fixture.Cases.Count > 0, "the fixture has no cases");
    }
}
