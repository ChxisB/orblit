using Orblit.Json;
using static Orblit.Motion.Tests.BlendSamples;

namespace Orblit.Motion.Tests;

public class BlendPlayerTests
{
    private static readonly Dictionary<string, ClipDocument> Clips = new()
    {
        ["idle"] = Leaning("idle", 0, 0.1),
        ["walk"] = Leaning("walk", 0, 1),
        ["run"] = Leaning("run", 0, 3),
        ["jump"] = Leaning("jump", 5, 6),
    };

    /// <summary>A second of leaning from <paramref name="from"/> to <paramref name="to"/> and back, looping.</summary>
    private static ClipDocument Leaning(string name, double from, double to) => new(
        name,
        1,
        WhenDone.Loop,
        channels:
        [
            new ClipChannel<double>(
                string.Empty,
                "pose.lean",
                ChannelKind.Number,
                [new Key<double>(0, from, Hold.Linear), new Key<double>(0.5, to, Hold.Linear), new Key<double>(1, from)]),
            new ClipChannel<Vector3>(
                string.Empty,
                "position",
                ChannelKind.Vector,
                [
                    new Key<Vector3>(0, new Vector3(0, 1, 0), Hold.Linear),
                    new Key<Vector3>(0.5, new Vector3(0, 1.1, 0), Hold.Linear),
                    new Key<Vector3>(1, new Vector3(0, 1, 0)),
                ],
                bone: "hips"),
        ],
        marks: [new Mark(0.25, "step"), new Mark(0.75, "step")]);

    private static BlendDocument Moves() => new(
        "Moves",
        [
            new BlendState("idle", new BlendClip("idle")),
            new BlendState(
                "move",
                new BlendLine("speed", [new LinePoint(1, new BlendClip("walk")), new LinePoint(3, new BlendClip("run"))])),
            new BlendState("jump", new BlendClip("jump")),
        ],
        inputs: Inputs(("speed", 0)),
        changes:
        [
            new BlendChange("move", "idle", BlendCondition.Above("speed", 0.1), 0.25, inStep: true),
            new BlendChange("idle", "move", BlendCondition.Below("speed", 0.1), 0.4),
            new BlendChange("jump", when: BlendCondition.On("jump"), fade: 0.1),
        ]);

    private static void ExpectSameFrame(ClipFrame actual, ClipFrame expected)
    {
        Assert.Equal(expected.At, actual.At);
        Assert.Equal(expected.ValueOf(string.Empty, "pose.lean"), actual.ValueOf(string.Empty, "pose.lean"));
        var hips = expected.BoneOf(string.Empty, "hips")!.Position!;
        var other = actual.BoneOf(string.Empty, "hips")!.Position!;
        Assert.Equal((hips.X, hips.Y, hips.Z), (other.X, other.Y, other.Z));
    }

    /// <summary>Plays through a few seconds of starting, speeding up and slowing down, the way a frame loop would.</summary>
    private static void Play(BlendPlayer player, int frames)
    {
        for (var i = 0; i < frames; i++)
        {
            player.Inputs["speed"] = i < 20 ? 2.5 : 0;
            player.Advance(1.0 / 60);
        }
    }

    [Fact]
    public void StartsWhereTheBlendSaysWithTheBlendsInputs()
    {
        var player = new BlendPlayer(Moves(), Clips, inputs: Inputs(("jump", 0)));
        Assert.Equal("idle", player.State);
        Assert.Equal(new BlendPlace("idle"), player.Place);
        Assert.Equal(Inputs(("speed", 0), ("jump", 0)), player.Inputs);
    }

    [Fact]
    public void MovesItsPlaceOnAsItPlays()
    {
        var player = new BlendPlayer(Moves(), Clips);
        player.Advance(0.25);
        Assert.Equal(new BlendPlace("idle", lap: 0.25), player.Place);
        player.Inputs["speed"] = 2;
        var step = player.Advance(0.25);
        Assert.Equal("move", step.Change!.To);
        Assert.Equal("move", player.State);
        Assert.Equal("idle", player.Place.From!.State);
        Assert.NotNull(player.Sample().ValueOf(string.Empty, "pose.lean"));
    }

    [Fact]
    public void SamplesWhereItIsWithoutMoving()
    {
        var player = new BlendPlayer(
            Moves(),
            Clips,
            place: new BlendPlace("move", lap: 0.25),
            inputs: Inputs(("speed", 2)));
        var frame = player.Sample();
        Assert.Equal(1, (double)frame.ValueOf(string.Empty, "pose.lean")!, 1e-12);
        Assert.Equal(0.25, player.Place.Lap);
    }

    [Fact]
    public void GoesIntoAStateWhenToldWhateverTheChangesSay()
    {
        var player = new BlendPlayer(Moves(), Clips);
        player.Enter("jump", fade: 0.2);
        Assert.Equal("jump", player.Place.State);
        Assert.Equal(new BlendPlace("idle"), player.Place.From);
        Assert.Throws<ArgumentException>(() => player.Enter("swim"));
    }

    [Fact]
    public void APlaceAssertsAChangeWithoutPlayingUpToIt()
    {
        var blend = Moves();
        var place = new BlendPlace(
            "move",
            lap: 3.4,
            from: new BlendPlace("idle", lap: 12.5),
            faded: 0.2,
            fade: 0.25);
        var step = blend.Advance(place, Inputs(("speed", 2), ("jump", 1)), 0, Clips);
        Assert.Equal("jump", step.Change!.To);
        Assert.Equal("jump", step.Place.State);
        Assert.Equal(place, step.Place.From);
        Assert.Equal(0.1, step.Place.Fade);
    }

    [Fact]
    public void APlaceSavedMidFadeRestoresTheSamePoseAndPlaysOnTheSame()
    {
        var blend = Moves();
        var player = new BlendPlayer(blend, Clips);
        Play(player, 30);
        Assert.NotNull(player.Place.From);

        var saved = JsonText.Encode(Tree.Map(
            ("place", player.Place.ToJson()),
            ("inputs", player.Inputs.ToDictionary(static one => one.Key, static one => (object?)one.Value))));
        var back = (OrderedDictionary<string, object?>)JsonText.Decode(saved)!;
        var inputs = ((OrderedDictionary<string, object?>)back["inputs"]!)
            .ToDictionary(static one => one.Key, static one => Convert.ToDouble(one.Value, System.Globalization.CultureInfo.InvariantCulture));
        var restored = new BlendPlayer(blend, Clips, BlendPlace.FromJson(back["place"], blend), inputs);
        Assert.Equal(player.Place, restored.Place);
        ExpectSameFrame(restored.Sample(), player.Sample());

        for (var i = 0; i < 10; i++)
        {
            var one = player.Advance(1.0 / 60);
            var other = restored.Advance(1.0 / 60);
            Assert.Equal(one.Place, other.Place);
            Assert.Equal(
                (one.Moved.Position.X, one.Moved.Position.Y, one.Moved.Position.Z),
                (other.Moved.Position.X, other.Moved.Position.Y, other.Moved.Position.Z));
            Assert.Equal(one.Marks.Select(static mark => (mark.At, mark.Name)), other.Marks.Select(static mark => (mark.At, mark.Name)));
            ExpectSameFrame(other.Frame, one.Frame);
        }
    }

    [Fact]
    public void APlaceSentAsNumbersStandsTheSameOnTheOtherEnd()
    {
        var blend = Moves();
        var here = new BlendPlayer(blend, Clips);
        Play(here, 30);
        var numbers = here.Place.ToNumbers(blend);
        Assert.Equal(BlendPlace.Width, numbers.Length);

        var there = new BlendPlayer(blend, Clips, BlendPlace.FromNumbers(blend, numbers), here.Inputs);
        Assert.Equal(here.Place, there.Place);
        ExpectSameFrame(there.Sample(), here.Sample());
    }
}
