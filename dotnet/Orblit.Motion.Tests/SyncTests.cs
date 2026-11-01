using static Orblit.Motion.Tests.BlendSamples;

namespace Orblit.Motion.Tests;

public class SyncTests
{
    private static readonly Dictionary<string, ClipDocument> Clips = new()
    {
        ["walk"] = Stride("Walk", 2, 0.2, 1.6),
        ["run"] = Stride("Run", 1, 0.3, 0.6),
    };

    private static readonly BlendLine Line = new("speed", [new LinePoint(0, new BlendClip("walk")), new LinePoint(1, new BlendClip("run"))]);

    /// <summary>A loop whose left foot is down at <paramref name="left"/> and whose right foot is down at <paramref name="right"/>.</summary>
    private static ClipDocument Stride(string name, double duration, double left, double right) => new(
        name,
        duration,
        WhenDone.Loop,
        channels:
        [
            new ClipChannel<double>(
                string.Empty,
                "foot.left",
                ChannelKind.Number,
                [
                    new Key<double>(0, -1.0, Hold.Linear),
                    new Key<double>(left, 0.0, Hold.Linear),
                    new Key<double>(right, 1.0, Hold.Linear),
                    new Key<double>(duration, -1.0),
                ]),
        ],
        marks: [new Mark(left, "left"), new Mark(right, "right")]);

    private static BlendDocument Locomotion(BlendSource source, IReadOnlyList<string>? sync = null) => new(
        "Locomotion",
        [new BlendState("move", source, sync: sync ?? ["left", "right"])],
        inputs: Inputs(("speed", 0.5), ("x", 0.5), ("y", 0)));

    private static double FootAt(BlendDocument graph, BlendPlace place, IReadOnlyDictionary<string, double> values) =>
        (double)graph.SampleAt(place, values, Clips).ValueOf(string.Empty, "foot.left")!;

    [Fact]
    public void RootMotionFollowsTheSameContactClockAsThePose()
    {
        ClipDocument Walk(string name, double duration, double distance, double left, double right) => new(
            name,
            duration,
            WhenDone.Loop,
            rootMotion: new RootMotion(bone: "hips"),
            marks: [new Mark(left, "left"), new Mark(right, "right")],
            channels:
            [
                new ClipChannel<Vector3>(
                    string.Empty,
                    "position",
                    ChannelKind.Vector,
                    [new Key<Vector3>(0, Vector3.Zero(), Hold.Linear), new Key<Vector3>(duration, new Vector3(0, 0, distance))],
                    bone: "hips"),
            ]);
        var graph = Locomotion(new BlendLine("speed", [new LinePoint(0, new BlendClip("walk")), new LinePoint(1, new BlendClip("run"))]));
        var player = new BlendPlayer(
            graph,
            new Dictionary<string, ClipDocument> { ["walk"] = Walk("Walk", 2, 2, 0.2, 1.6), ["run"] = Walk("Run", 1, 4, 0.3, 0.6) });

        var step = player.Advance(0.75);

        Assert.Equal(1.3, step.Moved.Position.Z, 1e-9);
        Assert.Equal(0, step.Frame.BoneOf(string.Empty, "hips")!.Position!.Z);
        Assert.Equal(["left", "right"], step.Marks.Select(static mark => mark.Name));
    }

    [Fact]
    public void WalkRunFeetMatchAtBothContactsDespiteDifferentMarkerTimes()
    {
        var graph = Locomotion(Line);

        foreach (var speed in new[] { 0.0, 0.2, 0.5, 0.8, 1.0 })
        {
            Assert.Equal(0, FootAt(graph, new BlendPlace("move"), Inputs(("speed", speed))), 1e-9);
            Assert.Equal(1, FootAt(graph, new BlendPlace("move", lap: 0.5), Inputs(("speed", speed))), 1e-9);
        }
    }

    [Fact]
    public void ChangingBlendWeightsMidStrideKeepsTheSharedContactPhase()
    {
        var player = new BlendPlayer(Locomotion(Line), Clips);
        player.Advance(0.75);

        Assert.Equal(0.5, player.Place.Lap);
        player.Inputs["speed"] = 1;
        Assert.Equal(1, (double)player.Sample().ValueOf(string.Empty, "foot.left")!, 1e-9);
        player.Inputs["speed"] = 0;
        Assert.Equal(1, (double)player.Sample().ValueOf(string.Empty, "foot.left")!, 1e-9);
    }

    [Fact]
    public void OneContactEventPerStepComesFromTheDominantCyclicClip()
    {
        var player = new BlendPlayer(Locomotion(Line), Clips);
        var marks = new List<string>();

        for (var i = 0; i < 60; i++)
        {
            marks.AddRange(player.Advance(1.5 / 60).Marks.Select(static mark => mark.Name));
        }

        Assert.Equal(["left", "right", "left"], marks);
    }

    [Fact]
    public void SyncAppliesToPlanesAsWellAsLines()
    {
        var graph = Locomotion(new BlendPlane("x", "y", [new PlanePoint(0, 0, new BlendClip("walk")), new PlanePoint(1, 0, new BlendClip("run"))]));

        Assert.Equal(1, FootAt(graph, new BlendPlace("move", lap: 0.5), NoInputs), 1e-9);
    }

    [Fact]
    public void MissingSyncContactsFallBackToOrdinaryLaps()
    {
        var graph = Locomotion(Line, ["left", "missing"]);
        var plain = Locomotion(Line, []);

        Assert.Equal(
            FootAt(plain, new BlendPlace("move", lap: 0.5), NoInputs),
            FootAt(graph, new BlendPlace("move", lap: 0.5), NoInputs));
    }

    [Fact]
    public void SyncNamesRoundTripInTheBlendFile()
    {
        var graph = Locomotion(Line);

        var loaded = BlendDocument.Decode(graph.Encode());

        Assert.Empty(loaded.Problems);
        Assert.Equal(["left", "right"], loaded.Blend.States.Single().Sync);
        Assert.Throws<ArgumentException>(() => new BlendState("bad", new BlendClip("walk"), sync: ["left", "left"]));
    }
}
