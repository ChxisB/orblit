using Orblit.Json;

namespace Orblit.Motion.Tests;

public class PlaceTests
{
    private static BlendDocument Moves() => new(
        "Moves",
        [
            new BlendState("idle", new BlendClip("idle")),
            new BlendState("walk", new BlendClip("walk")),
            new BlendState("run", new BlendClip("run")),
            new BlendState("jump", new BlendClip("jump")),
            new BlendState("land", new BlendClip("land")),
        ]);

    private static void ExpectShares(BlendPlace place, params (string State, double Share)[] expected)
    {
        var actual = place.Shares;
        Assert.Equal(expected.Select(static one => one.State), actual.Select(static one => one.Place.State));
        for (var i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i].Share, actual[i].Share, 1e-12);
        }
    }

    /// <summary>Walk fading in over idle, halfway, and run fading in over that, a quarter of the way.</summary>
    private static BlendPlace TwoFades() => new(
        "run",
        lap: 0.1,
        from: new BlendPlace(
            "walk",
            lap: 1.6,
            from: new BlendPlace("idle", lap: 4.25),
            faded: 0.1,
            fade: 0.2,
            shape: Easing.Linear),
        faded: 0.05,
        fade: 0.2,
        shape: Easing.Linear);

    private static BlendPlace Reload(BlendPlace place, BlendDocument blend) =>
        BlendPlace.FromJson(JsonText.Decode(JsonText.Encode(place.ToJson())), blend)!;

    [Fact]
    public void SayIsAllTheStateBeingPlayedWhenNothingFades()
    {
        var place = new BlendPlace("walk", lap: 0.3);
        Assert.Equal(1, place.Weight);
        ExpectShares(place, ("walk", 1));
        Assert.Equal(1, place.Depth);
    }

    [Fact]
    public void SayGrowsThroughAFadeTheWayItEases()
    {
        var halfway = new BlendPlace("walk", from: new BlendPlace("idle"), faded: 0.1, fade: 0.2, shape: Easing.Linear);
        Assert.Equal(0.5, halfway.Weight, 1e-12);
        var eased = new BlendPlace("walk", from: new BlendPlace("idle"), faded: 0.05, fade: 0.2);
        Assert.Equal(Easings.Ease(Easing.Smooth, 0.25), eased.Weight, 1e-12);
    }

    [Fact]
    public void SaySharesOutNewestFirstAndAddsToOne()
    {
        var place = TwoFades();
        Assert.Equal(3, place.Depth);
        ExpectShares(place, ("run", 0.25), ("walk", 0.375), ("idle", 0.375));
        Assert.Equal(1, place.Shares.Sum(static share => share.Share), 1e-12);
    }

    [Fact]
    public void SayStaysBetweenNoughtAndOneForAnEasingThatOvershoots()
    {
        var place = new BlendPlace("walk", from: new BlendPlace("idle"), faded: 0.7, fade: 1, shape: Easing.Back);
        Assert.InRange(place.Weight, 0, 1);
        Assert.All(place.Shares, static share => Assert.InRange(share.Share, 0, 1));
    }

    [Fact]
    public void EnteringCutsWithNoFadeLeavingNothingBehind() =>
        Assert.Equal(new BlendPlace("jump"), TwoFades().Enter("jump"));

    [Fact]
    public void EnteringFadesInOverEverythingThatWasPlaying()
    {
        var next = new BlendPlace("walk", lap: 2.5).Enter("run", fade: 0.3);
        Assert.Equal("run", next.State);
        Assert.Equal(0, next.Lap);
        Assert.Equal(new BlendPlace("walk", lap: 2.5), next.From);
        Assert.Equal(0.3, next.Fade);
        Assert.Equal(0, next.Faded);
        Assert.Equal(0, next.Weight);
    }

    [Fact]
    public void EnteringKeepsInStepWhenAskedAsFarThroughTheLap() =>
        Assert.Equal(0.25, new BlendPlace("walk", lap: 2.25).Enter("run", fade: 0.3, inStep: true).Lap);

    [Fact]
    public void EnteringKeepsNoMoreThanTheDeepestFewDroppingTheOldest()
    {
        var deep = TwoFades().Enter("jump", fade: 0.1).Enter("land", fade: 0.1);
        Assert.Equal(BlendPlace.Deepest, deep.Depth);
        Assert.Equal(["land", "jump", "run", "walk"], deep.Shares.Select(static one => one.Place.State));
        var oldest = deep.From!.From!.From!;
        Assert.Equal(new BlendPlace("walk", lap: 1.6), oldest);
        // The rest of the fades are as they were.
        Assert.Equal(0.05, deep.From.From.Faded);
    }

    [Fact]
    public void EnteringIsACutForAFadeThatIsNotALengthOfTime()
    {
        Assert.Null(new BlendPlace("walk").Enter("run", fade: double.PositiveInfinity).From);
        Assert.Null(new BlendPlace("walk").Enter("run", fade: double.NaN).From);
    }

    [Fact]
    public void InASaveFileComesBackTheSameFadesAndAll()
    {
        var place = TwoFades();
        Assert.Equal(place, Reload(place, Moves()));
    }

    [Fact]
    public void InASaveFileWritesAPlaceWithNothingFadingAsAStateAndALap() =>
        Tree.Same(Tree.Map(("state", "walk"), ("lap", 1.5)), new BlendPlace("walk", lap: 1.5).ToJson());

    [Fact]
    public void InASaveFileDropsWhatTheBlendNoLongerHas()
    {
        var blend = Moves();
        var json = TwoFades().ToJson();
        var oldest = (OrderedDictionary<string, object?>)((OrderedDictionary<string, object?>)json["from"]!)["from"]!;
        oldest["state"] = "crawl";
        Assert.Equal(
            new BlendPlace(
                "run",
                lap: 0.1,
                from: new BlendPlace("walk", lap: 1.6),
                faded: 0.05,
                fade: 0.2,
                shape: Easing.Linear),
            BlendPlace.FromJson(json, blend));
        json["state"] = "swim";
        Assert.Null(BlendPlace.FromJson(json, blend));
        Assert.Null(BlendPlace.FromJson("walk", blend));
    }

    [Fact]
    public void InASaveFileMendsALapOrAFadeItCannotUse()
    {
        var blend = Moves();
        Assert.Equal(
            new BlendPlace("walk"),
            BlendPlace.FromJson(Tree.Map(("state", "walk"), ("lap", -2L)), blend));
        var fading = BlendPlace.FromJson(
            Tree.Map(
                ("state", "walk"),
                ("fade", 0.2),
                ("faded", 9L),
                ("from", Tree.Map(("state", "idle")))),
            blend);
        Assert.Equal(0.2, fading!.Faded);
    }

    [Fact]
    public void AsNumbersAlwaysTakesTheSameRoom()
    {
        var blend = Moves();
        Assert.Equal(21, BlendPlace.Width);
        Assert.Equal(21, new BlendPlace("idle").ToNumbers(blend).Length);
        Assert.Equal(21, TwoFades().ToNumbers(blend).Length);
    }

    [Fact]
    public void AsNumbersComesBackTheSameFadesAndAll()
    {
        var blend = Moves();
        BlendPlace[] places =
        [
            new BlendPlace("idle"),
            new BlendPlace("run", lap: 7.125),
            TwoFades(),
            TwoFades().Enter("jump", fade: 0.1).Enter("land", fade: 0.1),
        ];
        foreach (var place in places)
        {
            Assert.Equal(place, BlendPlace.FromNumbers(blend, place.ToNumbers(blend)));
        }
    }

    [Fact]
    public void AsNumbersNamesStatesByWhereTheyAreInTheBlend() =>
        Assert.Equal([1.0, 2.0, 0.5], new BlendPlace("run", lap: 0.5).ToNumbers(Moves()).Take(3));

    [Fact]
    public void AsNumbersDropsAStateTheBlendHasNotGotAndAllOlder()
    {
        var blend = Moves();
        var numbers = TwoFades().ToNumbers(blend);
        numbers[1 + (5 * 1)] = 40;
        Assert.Equal(new BlendPlace("run", lap: 0.1), BlendPlace.FromNumbers(blend, numbers));
        numbers[1] = -1;
        Assert.Null(BlendPlace.FromNumbers(blend, numbers));
        Assert.Null(BlendPlace.FromNumbers(blend, [1, 0]));
    }
}
