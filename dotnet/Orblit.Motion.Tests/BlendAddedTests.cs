using Orblit.Json;

namespace Orblit.Motion.Tests;

/// <summary>
/// Checks the C# adds to the ported blend tests: the refusals a constructor makes, the edges of a place, and
/// the parts of a snapshot that the ported tests never leave out.
/// </summary>
public class BlendAddedTests
{
    private static BlendDocument TwoStates() => new(
        "Two",
        [new BlendState("a", new BlendClip("a")), new BlendState("b", new BlendClip("b"))]);

    private static Dictionary<string, ClipDocument> TwoClips() => new()
    {
        ["a"] = new ClipDocument("a", 1, whenDone: WhenDone.Loop),
        ["b"] = new ClipDocument("b", 1, whenDone: WhenDone.Loop),
    };

    [Fact]
    public void AFadeNeedsBothNamesAndASecondsThatIsFiniteAndNotBelowNought()
    {
        Assert.Throws<ArgumentException>(() => new BlendFade(string.Empty, "b", 0.1));
        Assert.Throws<ArgumentException>(() => new BlendFade("a", string.Empty, 0.1));
        Assert.Throws<ArgumentException>(() => new BlendFade("a", "b", -0.1));
        Assert.Throws<ArgumentException>(() => new BlendFade("a", "b", double.NaN));
        Assert.Throws<ArgumentException>(() => new BlendFade("a", "b", double.PositiveInfinity));
        Assert.Equal(0, new BlendFade("a", "b", 0).Seconds);
    }

    [Fact]
    public void AStateCannotTakeTheNameOfAGraphPathOrOfAGraphItself()
    {
        var inner = new BlendDocument("In", [new BlendState("a", new BlendClip("a"))]);
        var middle = new BlendDocument("Mid", [new BlendState("g", new BlendGraph(inner))]);

        Assert.Throws<ArgumentException>(() => new BlendDocument(
            "Out",
            [new BlendState("g", new BlendGraph(inner)), new BlendState("g/a", new BlendClip("a"))]));
        Assert.Throws<ArgumentException>(() => new BlendDocument(
            "Out",
            [new BlendState("h", new BlendGraph(middle)), new BlendState("h/g", new BlendClip("a"))]));
    }

    [Fact]
    public void ASyncListNeedsTwoDistinctNamedMarkers()
    {
        var plays = new BlendClip("walk");

        Assert.Throws<ArgumentException>(() => new BlendState("a", plays, sync: ["left"]));
        Assert.Throws<ArgumentException>(() => new BlendState("a", plays, sync: ["left", "left"]));
        Assert.Throws<ArgumentException>(() => new BlendState("a", plays, sync: ["left", string.Empty]));
        Assert.Equal(["left", "right"], new BlendState("a", plays, sync: ["left", "right"]).Sync);
    }

    [Fact]
    public void APlanePointWithOneCoordinateThatIsNotFiniteIsRefused()
    {
        var plays = new BlendClip("walk");

        Assert.Throws<ArgumentException>(() => new BlendPlane("x", "y", [new PlanePoint(double.NaN, 0, plays)]));
        Assert.Throws<ArgumentException>(() => new BlendPlane("x", "y", [new PlanePoint(0, double.PositiveInfinity, plays)]));
    }

    [Fact]
    public void ARunningFadeWeighsOneWhenItHasNothingToFadeFromOrNoTime()
    {
        Assert.Equal(1, new BlendPlace("a", faded: 0.1, fade: 0.2).Weight);
        Assert.Equal(1, new BlendPlace("a", from: new BlendPlace("b"), faded: -5, fade: 0).Weight);
        Assert.Equal(1, new BlendPlace("a", from: new BlendPlace("b"), faded: 0.1, fade: -1).Weight);
    }

    [Fact]
    public void AFadeWithNoTimeLeavesNothingToFadeFromAfterAStep()
    {
        var place = new BlendPlace("b", from: new BlendPlace("a"), faded: -5, fade: 0);
        var player = new BlendPlayer(TwoStates(), TwoClips(), place);

        player.Advance(0.1);

        Assert.Null(player.Place.From);
    }

    [Fact]
    public void ARouteThatIsEmptyLeavesNoJourney()
    {
        var blend = TwoStates();

        Assert.Null(new BlendPlace("a").Enter("b", route: []).Journey);
        Assert.Null(BlendPlace.FromJson(Tree.Map(("state", "a"), ("route", Tree.List())), blend)!.Journey);
    }

    [Fact]
    public void OnlyTheFourDeepestPlacesAreWrittenAsNumbers()
    {
        var place = new BlendPlace("a");
        for (var i = 0; i < 5; i++)
        {
            place = new BlendPlace("b", from: place, faded: 0.1, fade: 1);
        }

        Assert.Equal(6, place.Depth);
        Assert.Equal(BlendPlace.Deepest, place.ToNumbers(TwoStates())[0]);
    }

    [Fact]
    public void TheOnlyMigrationUpgradesFormatOneToFormatTwo()
    {
        var migration = Assert.Single(BlendDocument.Migrations);

        Assert.Equal(1, migration.From);
        Assert.Equal(2, migration.To);
    }

    [Fact]
    public void AnEnterKeepsNoFrozenPoseUnlessOneIsAskedFor()
    {
        var player = new BlendPlayer(TwoStates(), TwoClips());
        player.Enter("b", fade: 0.5);
        Assert.Null(player.Place.Frozen);

        var frozen = new BlendPlayer(TwoStates(), TwoClips());
        frozen.Enter("b", fade: 0.5, fromPose: true);
        Assert.NotNull(frozen.Place.Frozen);
    }

    [Fact]
    public void ASnapshotWithoutBonesOrWithBonesThatAreNotAMapIsNotOne()
    {
        Assert.Null(PoseSnapshot.FromJson(Tree.Map(("at", 0L), ("values", Tree.Map()))));
        Assert.Null(PoseSnapshot.FromJson(Tree.Map(("at", 0L), ("values", Tree.Map()), ("bones", Tree.List()))));
        Assert.NotNull(PoseSnapshot.FromJson(Tree.Map(("at", 0L), ("values", Tree.Map()), ("bones", Tree.Map()))));
    }
}
