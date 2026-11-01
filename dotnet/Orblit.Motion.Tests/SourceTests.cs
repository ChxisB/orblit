using static Orblit.Motion.Tests.BlendSamples;

namespace Orblit.Motion.Tests;

public class SourceTests
{
    private static BlendLine WalkToRun() => new("speed",
    [
        new LinePoint(0, new BlendClip("idle")),
        new LinePoint(1.5, new BlendClip("walk")),
        new LinePoint(4, new BlendClip("run")),
    ]);

    /// <summary>Idle in the middle and a walk each way, over a velocity's two parts.</summary>
    private static BlendPlane Directions() => new("sideways", "forwards",
    [
        new PlanePoint(0, 0, new BlendClip("idle")),
        new PlanePoint(0, 1, new BlendClip("forwards")),
        new PlanePoint(0, -1, new BlendClip("backwards")),
        new PlanePoint(1, 0, new BlendClip("right")),
        new PlanePoint(-1, 0, new BlendClip("left")),
    ]);

    private static BlendLine IdleOrDirections() => new("speed",
    [
        new LinePoint(0, new BlendClip("idle")),
        new LinePoint(2, Directions()),
    ]);

    [Fact]
    public void AClipHasTheWholeSay() =>
        Weights(new BlendClip("idle").Weigh(Reading(Inputs())), ("idle", 1));

    [Fact]
    public void ALineIsTheNearerClipPastEitherEnd()
    {
        var line = WalkToRun();
        Weights(line.Weigh(Reading(Inputs(("speed", -1)))), ("idle", 1));
        Weights(line.Weigh(Reading(Inputs(("speed", 0)))), ("idle", 1));
        Weights(line.Weigh(Reading(Inputs(("speed", 9)))), ("run", 1));
    }

    [Fact]
    public void ALineIsExactlyAClipAtItsPoint() =>
        Weights(WalkToRun().Weigh(Reading(Inputs(("speed", 1.5)))), ("walk", 1));

    [Fact]
    public void ALineMixesTheTwoEitherSideInProportion() =>
        Weights(WalkToRun().Weigh(Reading(Inputs(("speed", 2)))), ("walk", 0.8), ("run", 0.2));

    [Fact]
    public void ALineIsPutInOrderKeepingTheOrderOfTwoAtOnePlace()
    {
        var line = new BlendLine("speed",
        [
            new LinePoint(4, new BlendClip("run")),
            new LinePoint(1, new BlendClip("walk")),
            new LinePoint(1, new BlendClip("stride")),
        ]);
        Assert.Equal([1.0, 1.0, 4.0], line.Points.Select(static point => point.At));
        // Two at one place are a step: the first below, the second from there up.
        Weights(line.Weigh(Reading(Inputs(("speed", 1)))), ("stride", 1));
        Weights(line.Weigh(Reading(Inputs(("speed", 0.5)))), ("walk", 1));
        Weights(line.Weigh(Reading(Inputs(("speed", 2.5)))), ("stride", 0.5), ("run", 0.5));
    }

    [Fact]
    public void ALineOfOnePointIsThatPointAnywhere()
    {
        var line = new BlendLine("speed", [new LinePoint(3, new BlendClip("only"))]);
        Weights(line.Weigh(Reading(Inputs(("speed", -10)))), ("only", 1));
        Weights(line.Weigh(Reading(Inputs(("speed", 10)))), ("only", 1));
    }

    [Fact]
    public void ALineNeedsAPointSomewhere()
    {
        Assert.Throws<ArgumentException>(static () => new BlendLine("speed", []));
        Assert.Throws<ArgumentException>(static () =>
            new BlendLine("speed", [new LinePoint(double.NaN, new BlendClip("x"))]));
    }

    [Fact]
    public void APlaneIsExactlyAClipAtItsPoint()
    {
        var plane = Directions();
        foreach (var point in plane.Points)
        {
            var clip = ((BlendClip)point.Plays).Clip;
            Weights(
                plane.Weigh(Reading(Inputs(("sideways", point.X), ("forwards", point.Y)))),
                (clip, 1));
        }
    }

    [Fact]
    public void APlaneSharesBetweenTheTwoPointsAPlaceIsBetween() =>
        Weights(
            Directions().Weigh(Reading(Inputs(("forwards", 0.5)))),
            ("idle", 0.5),
            ("forwards", 0.5));

    [Fact]
    public void APlaneSharesASquareEvenlyAtItsMiddle()
    {
        var plane = new BlendPlane("x", "y",
        [
            new PlanePoint(0, 0, new BlendClip("a")),
            new PlanePoint(1, 0, new BlendClip("b")),
            new PlanePoint(0, 1, new BlendClip("c")),
            new PlanePoint(1, 1, new BlendClip("d")),
        ]);
        Weights(
            plane.Weigh(Reading(Inputs(("x", 0.5), ("y", 0.5)))),
            ("a", 0.25),
            ("b", 0.25),
            ("c", 0.25),
            ("d", 0.25));
    }

    [Fact]
    public void APlaneSettlesOnTheNearestOutsideThemAll() =>
        Weights(
            Directions().Weigh(Reading(Inputs(("sideways", 5), ("forwards", 0)))),
            ("right", 1));

    [Fact]
    public void APlaneAlwaysAddsToOne()
    {
        var plane = Directions();
        var random = new Random(7);
        for (var i = 0; i < 500; i++)
        {
            var x = (random.NextDouble() * 4) - 2;
            var y = (random.NextDouble() * 4) - 2;
            var weights = plane.WeightsAt(x, y);
            Assert.Equal(1, weights.Sum(), 1e-9);
            Assert.All(weights, static one => Assert.True(one >= 0));
        }
    }

    [Fact]
    public void APlaneChangesSmoothlyWithNoJumpAnywhere()
    {
        var plane = Directions();
        for (var i = 0; i <= 400; i++)
        {
            var angle = i / 400.0 * 2 * Math.PI;
            var here = plane.WeightsAt(0.7 * Math.Cos(angle), 0.7 * Math.Sin(angle));
            var next = plane.WeightsAt(0.7 * Math.Cos(angle + 1e-4), 0.7 * Math.Sin(angle + 1e-4));
            for (var j = 0; j < here.Count; j++)
            {
                Assert.True(Math.Abs(here[j] - next[j]) < 1e-3);
            }
        }
    }

    [Fact]
    public void APlaneSharesOnePlaceBetweenTwoPointsAtIt()
    {
        var plane = new BlendPlane("x", "y",
        [
            new PlanePoint(0, 0, new BlendClip("a")),
            new PlanePoint(0, 0, new BlendClip("b")),
            new PlanePoint(1, 0, new BlendClip("c")),
        ]);
        Weights(plane.Weigh(Reading(Inputs())), ("a", 0.5), ("b", 0.5));
    }

    [Fact]
    public void APlaneNeedsAPoint() =>
        Assert.Throws<ArgumentException>(static () => new BlendPlane("x", "y", []));

    [Fact]
    public void NestingMultipliesASayThroughEachLevel()
    {
        var line = IdleOrDirections();
        Weights(
            line.Weigh(Reading(Inputs(("speed", 1), ("sideways", 0), ("forwards", 1)))),
            ("idle", 0.5),
            ("forwards", 0.5));
        Weights(
            line.Weigh(Reading(Inputs(("speed", 1), ("sideways", 0), ("forwards", 0.5)))),
            ("idle", 0.75),
            ("forwards", 0.25));
    }

    [Fact]
    public void NestingAddsTogetherAClipNamedTwice()
    {
        var line = new BlendLine("speed",
        [
            new LinePoint(0, new BlendClip("walk")),
            new LinePoint(1, new BlendClip("walk")),
        ]);
        Weights(line.Weigh(Reading(Inputs(("speed", 0.3)))), ("walk", 1));
    }

    [Fact]
    public void NestingListsEveryClipAndEveryInput()
    {
        var line = IdleOrDirections();
        Assert.Equal(["idle", "idle", "forwards", "backwards", "right", "left"], line.Clips);
        Assert.Equal(
            new HashSet<string> { "speed", "sideways", "forwards" },
            line.Inputs.ToHashSet());
    }

    [Fact]
    public void InAFileReadsBackWhatItWrites()
    {
        var line = IdleOrDirections();
        var problems = new List<string>();
        var back = BlendSource.FromJson(line.ToJson(), "here", problems);
        Assert.Empty(problems);
        Tree.Same(line.ToJson(), back!.ToJson());
    }

    [Fact]
    public void InAFileNotesWhatItCannotReadAndKeepsTheRest()
    {
        var problems = new List<string>();
        var back = BlendSource.FromJson(
            Tree.Map(("line", Tree.Map(
                ("input", "speed"),
                ("points", Tree.List(
                    Tree.Map(("at", 0L), ("clip", "idle")),
                    Tree.Map(("at", "fast"), ("clip", "run")),
                    Tree.Map(("at", 2L))))))),
            "the state \"move\"",
            problems);
        Assert.Single(Assert.IsType<BlendLine>(back).Points);
        Assert.Equal(2, problems.Count);
        Assert.Contains("the line in the state \"move\"", problems[0], StringComparison.Ordinal);
    }

    [Fact]
    public void InAFileIsNothingWhenItNamesNothing()
    {
        var problems = new List<string>();
        Assert.Null(BlendSource.FromJson(Tree.Map(), "the state \"x\"", problems));
        Assert.StartsWith("The state \"x\" plays nothing", Assert.Single(problems), StringComparison.Ordinal);
        Assert.Null(BlendSource.FromJson(
            Tree.Map(("plane", Tree.Map(("x", "a")))),
            "the state \"y\"",
            problems));
    }
}
