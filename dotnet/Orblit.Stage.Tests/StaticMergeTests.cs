using System.Collections.Concurrent;
using Orblit.Filament;
using Orblit.VectorMath;

namespace Orblit.Stage.Tests;

public class StaticMergeTests
{
    private const long MergedFrom = 0x4d470000;

    private static RenderObject Rock(long key, double x = 0, double z = 0) => new()
    {
        Key = key,
        Transform = Matrix4.TranslationValues(x, 0, z),
        Colour = new Vector3(0.5, 0.25, 0.125),
        Mesh = "rock.glb",
        Material = 1,
        CastShadows = true,
    };

    private static RenderScene SceneOf(IReadOnlyList<RenderObject> objects) => new()
    {
        Objects = objects,
        Camera = new RenderCamera { Position = new Vector3(0, 5, 10), Target = Vector3.Zero() },
    };

    private static List<RenderObject> Rocks(int count, double x = 0) =>
        [.. Enumerable.Range(0, count).Select(i => Rock(i, x + i))];

    private static List<double> Xs(IReadOnlyList<Matrix4> placements) => [.. placements.Select(m => m[12])];

    private static bool IsMerged(RenderObject one) => one.Key >= MergedFrom;

    private static StaticMergeOptions Options() => new()
    {
        Bake = (_, _) => Task.FromResult<byte[]?>(null),
    };

    // A merge wired to a fake baker and a fake resource store.
    private sealed class Rig
    {
        private int _parked;
        private int _ready;

        public Rig(int settle = 2, int minimum = 3, int maxBakes = 8)
        {
            Merge = new StaticMerge(new StaticMergeOptions
            {
                Bake = BakeAsync,
                Provide = ProvideAsync,
                Release = ReleaseAsync,
                OnReady = () => Interlocked.Increment(ref _ready),
                Settle = settle,
                Minimum = minimum,
                CellSize = 10,
                MaxBakes = maxBakes,
            });
        }

        public StaticMerge Merge { get; }

        public List<(string Mesh, IReadOnlyList<Matrix4> Placements)> Calls { get; } = [];

        public ConcurrentDictionary<string, byte[]> Provided { get; } = new();

        public ConcurrentQueue<string> Released { get; } = new();

        public int Ready => Volatile.Read(ref _ready);

        // Makes the baker wait, or the provide call wait, until Open.
        public TaskCompletionSource? BakeGate { get; init; }

        public TaskCompletionSource? ProvideGate { get; init; }

        public Exception? Failure { get; init; }

        public bool Declines { get; init; }

        // Observes the objects for some frames, then waits until every bake has landed or is parked
        // at a gate.
        public async Task Watch(IReadOnlyList<RenderObject> objects, int frames = 1)
        {
            var scene = SceneOf(objects);
            for (var i = 0; i < frames; i++)
            {
                Merge.Observe(scene);
            }

            await Waits.Until(() => Merge.InFlight == Volatile.Read(ref _parked), "the bakes to land or wait");
        }

        public IReadOnlyList<RenderObject> Drawn(IReadOnlyList<RenderObject> objects) =>
            Merge.Replace(SceneOf(objects)).Objects;

        public async Task Open(TaskCompletionSource gate)
        {
            gate.SetResult();
            await Waits.Until(() => Merge.InFlight == 0, "the bakes to land");
            Interlocked.Exchange(ref _parked, 0);
        }

        private async Task<byte[]?> BakeAsync(string mesh, IReadOnlyList<Matrix4> placements)
        {
            Calls.Add((mesh, placements));
            await Park(BakeGate);
            return Failure is not null ? throw Failure : Declines ? null : new byte[4];
        }

        private async Task ProvideAsync(string name, byte[] bytes)
        {
            await Park(ProvideGate);
            Provided[name] = bytes;
        }

        private Task<bool> ReleaseAsync(string name)
        {
            Released.Enqueue(name);
            return Task.FromResult(Provided.TryRemove(name, out _));
        }

        private async Task Park(TaskCompletionSource? gate)
        {
            if (gate is null)
            {
                return;
            }

            Interlocked.Increment(ref _parked);
            await gate.Task;
        }
    }

    private static TaskCompletionSource Gate() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    [Fact]
    public async Task AGroupBelowTheMinimumIsLeftAlone()
    {
        var rig = new Rig(minimum: 6);
        var objects = Rocks(5);

        await rig.Watch(objects, 5);

        Assert.Empty(rig.Calls);
        Assert.Equal(5, rig.Drawn(objects).Count);
    }

    [Fact]
    public async Task ASettledGroupBecomesOneObjectWithTheGroupSettings()
    {
        var rig = new Rig();
        var objects = Enumerable.Range(0, 5).Select(i => Rock(i, i) with { CastShadows = false, Layer = 2 }).ToList();

        await rig.Watch(objects, 2);
        Assert.Empty(rig.Calls);

        await rig.Watch(objects);
        var call = Assert.Single(rig.Calls);
        Assert.Equal("rock.glb", call.Mesh);
        Assert.Equal([0.0, 1, 2, 3, 4], Xs(call.Placements));
        Assert.Single(rig.Provided);
        Assert.Equal(1, rig.Ready);

        var merged = Assert.Single(rig.Drawn(objects));
        Assert.True(IsMerged(merged));
        Assert.Equal(rig.Provided.Keys.Single(), merged.Mesh);
        Assert.StartsWith(RenderResources.Scheme, merged.Mesh);
        for (var n = 0; n < 16; n++)
        {
            Assert.Equal(n % 5 == 0 ? 1.0 : 0.0, merged.Transform[n]);
        }

        Assert.Equal(1L, merged.Material);
        Assert.Equal(new Vector3(0.5, 0.25, 0.125), merged.Colour);
        Assert.False(merged.CastShadows);
        Assert.Equal(2, merged.Layer);
    }

    [Fact]
    public async Task ObjectsAreDrawnSinglyUntilTheBakeLands()
    {
        var gate = Gate();
        var rig = new Rig { BakeGate = gate };
        var objects = Rocks(4);

        await rig.Watch(objects, 3);

        Assert.Single(rig.Calls);
        Assert.Equal(4, rig.Drawn(objects).Count);
        Assert.Equal(0, rig.Ready);

        await rig.Open(gate);
        Assert.Single(rig.Drawn(objects));
        Assert.Equal(1, rig.Ready);
    }

    [Fact]
    public async Task AMemberThatMovesPutsTheGroupBackAndBakesWithoutIt()
    {
        var rig = new Rig();
        var objects = Rocks(5);
        await rig.Watch(objects, 3);
        Assert.Single(rig.Drawn(objects));

        List<RenderObject> moved = [Rock(0, 6.5), .. objects.Skip(1)];
        Assert.Equal(5, rig.Drawn(moved).Count);

        await rig.Watch(moved);
        Assert.Equal(2, rig.Calls.Count);
        Assert.Equal([1.0, 2, 3, 4], Xs(rig.Calls[^1].Placements));
        var drawn = rig.Drawn(moved);
        Assert.Equal(2, drawn.Count);
        Assert.Single(drawn, IsMerged);
        Assert.Equal(0, drawn.First(one => !IsMerged(one)).Key);

        await rig.Watch(moved, 2);
        Assert.Equal(3, rig.Calls.Count);
        Assert.Equal([6.5, 1, 2, 3, 4], Xs(rig.Calls[^1].Placements));
        Assert.Single(rig.Drawn(moved));
    }

    [Fact]
    public async Task HidingOneObjectLeavesTheOthersDrawn()
    {
        var rig = new Rig();
        var objects = Rocks(5);
        await rig.Watch(objects, 3);

        List<RenderObject> hidden = [.. objects.Take(2), Rock(2, 2) with { Visible = false }, .. objects.Skip(3)];
        Assert.Equal(5, rig.Drawn(hidden).Count);

        await rig.Watch(hidden);
        Assert.Equal([0.0, 1, 3, 4], Xs(rig.Calls[^1].Placements));
        var drawn = rig.Drawn(hidden);
        Assert.Equal(2, drawn.Count);
        Assert.False(drawn.First(one => !IsMerged(one)).Visible);
    }

    [Fact]
    public async Task AnObjectThatLeavesTheScenePutsItsGroupBack()
    {
        var rig = new Rig();
        var objects = Rocks(5);
        await rig.Watch(objects, 3);

        var fewer = objects.Take(4).ToList();
        Assert.Equal(4, rig.Drawn(fewer).Count);
        await rig.Watch(fewer);

        Assert.Equal([0.0, 1, 2, 3], Xs(rig.Calls[^1].Placements));
        Assert.Single(rig.Drawn(fewer));
    }

    [Fact]
    public async Task AGroupThatFallsBelowTheMinimumGoesBackForGood()
    {
        var rig = new Rig(minimum: 4);
        var objects = Rocks(4);
        await rig.Watch(objects, 3);
        Assert.Single(rig.Drawn(objects));

        var fewer = objects.Take(3).ToList();
        await rig.Watch(fewer, 3);

        Assert.Single(rig.Calls);
        Assert.Equal(3, rig.Drawn(fewer).Count);
    }

    [Fact]
    public async Task ObjectsThatKeepMovingNeverStartABake()
    {
        var rig = new Rig();

        for (var frame = 0; frame < 8; frame++)
        {
            await rig.Watch([.. Enumerable.Range(0, 5).Select(i => Rock(i, i + frame))]);
        }

        Assert.Empty(rig.Calls);
    }

    [Fact]
    public async Task AnObjectWithNoPositionIsLeftAlone()
    {
        var rig = new Rig();
        var lost = Rock(9);
        lost.Transform[12] = double.NaN;
        List<RenderObject> objects = [.. Rocks(3), lost];

        await rig.Watch(objects, 4);

        Assert.Equal(3, Assert.Single(rig.Calls).Placements.Count);
        Assert.Single(rig.Drawn(objects), one => one.Key == 9);
    }

    [Fact]
    public async Task AnObjectWithNoColourIsLeftAlone()
    {
        var rig = new Rig();
        List<RenderObject> objects = [.. Rocks(3), Rock(9) with { Colour = new Vector3(double.NaN, 0, 0) }];

        await rig.Watch(objects, 4);

        Assert.Equal(3, Assert.Single(rig.Calls).Placements.Count);
        Assert.Single(rig.Drawn(objects), one => one.Key == 9);
    }

    [Fact]
    public async Task TwoSquaresOfGroundAreTwoMerges()
    {
        var rig = new Rig();
        List<RenderObject> objects =
        [
            .. Enumerable.Range(0, 3).Select(i => Rock(i, 1 + i)),
            .. Enumerable.Range(0, 3).Select(i => Rock(10 + i, 11 + i)),
        ];

        await rig.Watch(objects, 3);

        Assert.Equal(2, rig.Calls.Count);
        Assert.Equal([3, 3], rig.Calls.Select(call => call.Placements.Count));
        var drawn = rig.Drawn(objects);
        Assert.Equal(2, drawn.Count);
        Assert.Equal(2, drawn.Select(one => one.Key).Distinct().Count());
    }

    [Fact]
    public async Task AnotherMeshAnotherMerge()
    {
        var rig = new Rig();
        List<RenderObject> objects =
        [
            .. Enumerable.Range(0, 3).Select(i => Rock(i, i)),
            .. Enumerable.Range(0, 3).Select(i => Rock(10 + i, i) with { Mesh = "tree.glb" }),
        ];

        await rig.Watch(objects, 3);

        Assert.Equal(["rock.glb", "tree.glb"], rig.Calls.Select(call => call.Mesh).Distinct().Order());
        Assert.Equal(2, rig.Drawn(objects).Count);
    }

    [Fact]
    public async Task WhatMovesByItselfIsNotMerged()
    {
        var rig = new Rig();
        List<RenderObject> alone =
        [
            Rock(10) with { Animation = new ModelAnimation { Clip = 0 } },
            Rock(11) with { MorphWeights = [0.5] },
            Rock(12) with { Joints = [] },
            Rock(13) with { Variant = 1 },
            Rock(14) with { Visible = false },
            Rock(15) with { Mesh = null },
        ];
        List<RenderObject> objects = [.. Rocks(3), .. alone];

        await rig.Watch(objects, 3);

        Assert.Equal([0.0, 1, 2], Xs(Assert.Single(rig.Calls).Placements));
        Assert.Equal([10L, 11, 12, 13, 14, 15], rig.Drawn(objects).Where(one => !IsMerged(one)).Select(one => one.Key));
    }

    [Fact]
    public async Task ABakerThatSaysNoIsNotAskedAgainForThatModel()
    {
        var rig = new Rig { Declines = true };
        var objects = Rocks(5);
        await rig.Watch(objects, 3);

        Assert.Single(rig.Calls);
        var declined = Assert.Single(rig.Merge.Declined);
        Assert.Equal("rock.glb", declined.Key);
        Assert.Null(declined.Value);
        Assert.Equal(5, rig.Drawn(objects).Count);

        await rig.Watch([Rock(0, 7), .. objects.Skip(1)], 4);
        Assert.Single(rig.Calls);
    }

    [Fact]
    public async Task ABakerThatThrowsLeavesTheObjectsAndKeepsTheError()
    {
        var rig = new Rig { Failure = new InvalidOperationException("no luck") };
        var objects = Rocks(5);

        await rig.Watch(objects, 3);

        Assert.IsType<InvalidOperationException>(rig.Merge.Declined["rock.glb"]);
        Assert.Equal(5, rig.Drawn(objects).Count);
        Assert.Equal(0, rig.Ready);
    }

    [Fact]
    public async Task AnErrorInOnReadyIsNotBlamedOnTheModel()
    {
        await using var merge = new StaticMerge(Options() with
        {
            Bake = (_, _) => Task.FromResult<byte[]?>(new byte[4]),
            Provide = (_, _) => Task.CompletedTask,
            Release = _ => Task.FromResult(true),
            OnReady = () => throw new InvalidOperationException("the caller"),
            Settle = 0,
            Minimum = 3,
        });

        // The error goes nowhere, as an error in a task nobody awaits does, and the merge stands.
        merge.Observe(SceneOf(Rocks(3)));
        await Waits.Until(() => merge.InFlight == 0, "the bake to land");

        Assert.Empty(merge.Declined);
        Assert.Single(merge.Replace(SceneOf(Rocks(3))).Objects);
    }

    [Fact]
    public async Task AGroupStopsRebuildingAfterMaxBakes()
    {
        var rig = new Rig(maxBakes: 2);
        var objects = Rocks(5);
        await rig.Watch(objects, 3);
        List<RenderObject> first = [Rock(0, 6.5), .. objects.Skip(1)];
        await rig.Watch(first);
        List<RenderObject> second = [first[0], Rock(1, 7.5), .. objects.Skip(2)];
        await rig.Watch(second);

        Assert.Equal(2, rig.Calls.Count);
        Assert.Equal(5, rig.Drawn(second).Count);
    }

    [Fact]
    public async Task ABakeThatWasOvertakenIsDropped()
    {
        var gate = Gate();
        var rig = new Rig { BakeGate = gate };
        var objects = Rocks(5);
        await rig.Watch(objects, 3);
        List<RenderObject> moved = [Rock(0, 6.5), .. objects.Skip(1)];
        await rig.Watch(moved);
        Assert.Equal(2, rig.Calls.Count);

        await rig.Open(gate);

        Assert.Single(rig.Provided);
        Assert.Equal(1, rig.Ready);
        Assert.Single(rig.Drawn(moved), IsMerged);
    }

    [Fact]
    public async Task WithNoSettleAMoveInPlaceStillBakesAgain()
    {
        var rig = new Rig(settle: 0);
        var objects = Rocks(4);
        await rig.Watch(objects);
        Assert.Single(rig.Drawn(objects));

        List<RenderObject> moved = [Rock(0, 6.5), .. objects.Skip(1)];
        await rig.Watch(moved);

        Assert.Equal(2, rig.Calls.Count);
        Assert.Equal([6.5, 1, 2, 3], Xs(rig.Calls[^1].Placements));
        Assert.Single(rig.Drawn(moved));
    }

    [Fact]
    public async Task AMergeIsNotDrawnWithoutTheMembersItWasMadeOf()
    {
        var rig = new Rig();
        await rig.Watch(Rocks(5), 3);

        Assert.Equal(5, rig.Drawn(Rocks(5, 3)).Count);
        Assert.Empty(rig.Merge.Replace(SceneOf([])).Objects);
    }

    [Fact]
    public async Task DisposeReleasesWhatWasProvided()
    {
        var rig = new Rig();
        await rig.Watch(Rocks(5), 3);
        var name = rig.Provided.Keys.Single();

        await rig.Merge.DisposeAsync();

        Assert.Equal([name], rig.Released.ToArray());
        Assert.Empty(rig.Provided);
        Assert.Equal(5, rig.Drawn(Rocks(5)).Count);
    }

    [Fact]
    public async Task AFileThatLandsAfterDisposeIsReleasedAtOnce()
    {
        var gate = Gate();
        var rig = new Rig { ProvideGate = gate };
        await rig.Watch(Rocks(5), 3);
        await rig.Merge.DisposeAsync();

        await rig.Open(gate);

        Assert.Single(rig.Released);
        Assert.Empty(rig.Provided);
        Assert.Equal(0, rig.Ready);
    }

    [Fact]
    public async Task TwoMergesNeverShareAFileName()
    {
        var a = new Rig();
        var b = new Rig();
        await a.Watch(Rocks(5), 3);
        await b.Watch(Rocks(5), 3);

        Assert.NotEqual(a.Provided.Keys.Single(), b.Provided.Keys.Single());
    }

    [Fact]
    public void SettingsThatCannotWorkAreRefused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new StaticMerge(Options() with { Settle = -1 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new StaticMerge(Options() with { Minimum = 1 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new StaticMerge(Options() with { CellSize = 0 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new StaticMerge(Options() with { MaxBakes = 0 }));
    }
}
