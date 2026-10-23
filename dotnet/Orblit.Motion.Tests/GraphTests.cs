using Orblit.Json;

namespace Orblit.Motion.Tests;

public class GraphTests
{
    private static ClipDocument Moving(string name, double from, double to) => new(
        name,
        1,
        whenDone: WhenDone.Loop,
        channels:
        [
            new ClipChannel<double>(
                string.Empty,
                "pose.value",
                ChannelKind.Number,
                [new Key<double>(0, from, Hold.Linear), new Key<double>(1, to)]),
        ]);

    private static BlendDocument Graph(bool fromPose = false) => new(
        "Three",
        new[] { "a", "b", "c", "alone" }.Select(static name => new BlendState(name, new BlendClip(name))).ToList(),
        inputs: new Dictionary<string, double> { ["go"] = 0 },
        changes:
        [
            new BlendChange("b", "a", BlendCondition.On("go"), 0.4, Easing.Linear, fromPose: fromPose),
            new BlendChange("c", "b", BlendCondition.On("go"), 0.2, Easing.Linear),
        ]);

    private static double Value(ClipFrame frame) => (double)frame.ValueOf(string.Empty, "pose.value")!;

    private static Dictionary<string, ClipDocument> Clips() => new()
    {
        ["a"] = Moving("A", 0, 10),
        ["b"] = Moving("B", 20, 30),
        ["c"] = Moving("C", 40, 50),
        ["alone"] = Moving("Alone", 60, 70),
    };

    private static BlendDocument Nested() => new(
        "Outer",
        [
            new BlendState(
                "ground",
                new BlendGraph(new BlendDocument(
                    "Ground",
                    [new BlendState("a", new BlendClip("a")), new BlendState("b", new BlendClip("b"))],
                    inputs: new Dictionary<string, double> { ["speed"] = 0.5 },
                    changes: [new BlendChange("b", "a", BlendCondition.Above("speed", 1))]))),
            new BlendState("c", new BlendClip("c")),
        ],
        inputs: new Dictionary<string, double> { ["jump"] = 0, ["land"] = 0 },
        changes:
        [
            new BlendChange("c", "ground", BlendCondition.On("jump")),
            new BlendChange("ground", "c", BlendCondition.On("land")),
        ]);

    private static ClipFrame ArmFrame(Quaternion? rotation = null, Vector3? position = null)
    {
        var frame = new ClipFrame(0);
        frame.Bones[string.Empty] = new() { ["arm"] = new BoneLocal(position: position, rotation: rotation) };
        return frame;
    }

    [Fact]
    public void PairFadesChooseTheDominantClipThatIsActuallyAvailable()
    {
        var blend = new BlendDocument(
            "Missing",
            [
                new BlendState(
                    "mixed",
                    new BlendLine("speed", [new LinePoint(0, new BlendClip("missing")), new LinePoint(1, new BlendClip("a"))])),
                new BlendState("b", new BlendClip("b")),
            ],
            inputs: new Dictionary<string, double> { ["speed"] = 0.2 },
            fades: [new BlendFade("a", "b", 0.75)]);
        var player = new BlendPlayer(blend, Clips());
        player.Enter("b", fade: 0.2);
        Assert.Equal(0.75, player.Place.Fade);
    }

    [Fact]
    public void SnapshotConstructionAndReadingRejectMalformedPoseValues()
    {
        var invalid = ArmFrame(rotation: new Quaternion(0, 0, 0, 0));
        Assert.Throws<ArgumentException>(() => new PoseSnapshot(invalid));
        Assert.Null(PoseSnapshot.FromJson(Tree.Map(
            ("at", 0L),
            ("values", Tree.Map()),
            ("bones", Tree.Map((string.Empty, Tree.Map(("arm", Tree.Map(("position", Tree.List(1L, 2L)))))))))));
    }

    [Fact]
    public void AmbiguousParentNamesCannotShadowExpandedLeaves()
    {
        var inner = new BlendDocument("Inner", [new BlendState("a", new BlendClip("a"))]);
        Assert.Throws<ArgumentException>(() => new BlendDocument(
            "Ambiguous",
            [new BlendState("root", new BlendGraph(inner)), new BlendState("root/a", new BlendGraph(inner))]));
    }

    [Fact]
    public void SavedRoutesAreCanceledWhenAnEdgeDisappears()
    {
        var blend = Graph();
        var player = new BlendPlayer(blend, Clips());
        player.Travel("c");
        var saved = JsonText.Decode(JsonText.Encode(player.Place.ToJson()));
        var edited = new BlendDocument("Edited", blend.States, changes: [new BlendChange("b", "a")]);
        Assert.Empty(BlendPlace.FromJson(saved, edited)!.Route);

        var supplied = new List<string> { "b", "c" };
        var place = new BlendPlace("a").WithRoute(supplied);
        supplied.Clear();
        Assert.Equal(["b", "c"], place.Route);
        Assert.Throws<NotSupportedException>(() => ((IList<string>)place.Route).Clear());
    }

    [Fact]
    public void NestedWildcardChangesStayInsideTheirGraph()
    {
        var inner = new BlendDocument(
            "Inner",
            [new BlendState("a", new BlendClip("a")), new BlendState("b", new BlendClip("b"))],
            inputs: new Dictionary<string, double> { ["hit"] = 0 },
            changes: [new BlendChange("b", when: BlendCondition.On("hit"))]);
        var outer = new BlendDocument(
            "Outer",
            [new BlendState("inside", new BlendGraph(inner), speed: 0.5), new BlendState("c", new BlendClip("c"))],
            inputs: new Dictionary<string, double> { ["hit"] = 1 });
        var player = new BlendPlayer(outer, Clips());
        player.Advance(0.4);
        Assert.Equal("inside/b", player.State);
        Assert.Equal(0, player.Place.Lap);
        player.Advance(0.4);
        Assert.Equal(0.2, player.Place.Lap, 1e-12);
        player.Enter("c");
        player.Advance(0);
        Assert.Equal("c", player.State);
    }

    [Fact]
    public void MalformedSyncAndPairDataIsReportedWithoutLosingAState()
    {
        var raw = Graph().ToJson();
        var first = (OrderedDictionary<string, object?>)((List<object?>)raw["states"]!)[0]!;
        first["sync"] = new List<object?> { "left" };
        raw["fades"] = new List<object?> { Tree.Map(("from", "a"), ("to", "b"), ("seconds", -1L)) };
        var loaded = BlendDocument.Decode(JsonText.Encode(raw));
        Assert.Equal(4, loaded.Blend.States.Count);
        Assert.Empty(loaded.Blend.Fades);
        Assert.Empty(loaded.Blend.States[0].Sync);
        Assert.Equal(2, loaded.Problems.Count);
    }

    [Fact]
    public void TravelFindsADirectedShortestRouteAndLeavesUnreachableTargetsAlone()
    {
        var blend = Graph();
        Assert.Equal(["b", "c"], blend.RouteTo("a", "c"));
        Assert.Empty(blend.RouteTo("a", "a")!);
        Assert.Null(blend.RouteTo("c", "a"));
        var player = new BlendPlayer(blend, Clips());
        var before = player.Place;
        Assert.Throws<ArgumentException>(() => player.Travel("alone"));
        Assert.Same(before, player.Place);
    }

    [Fact]
    public void TravelOverridesConditionsAndWaitsForFadesBetweenEdges()
    {
        var player = new BlendPlayer(Graph(), Clips());
        player.Travel("c");
        Assert.Equal(["b", "c"], player.Place.Route);
        player.Advance(0);
        Assert.Equal("b", player.State);
        Assert.Equal(["c"], player.Place.Route);
        player.Advance(0.2);
        Assert.Equal("b", player.State);
        player.Advance(0.2);
        Assert.Equal("c", player.State);
        Assert.Equal(0.2, player.Place.Fade);
        Assert.Empty(player.Place.Route);
    }

    [Fact]
    public void ACurrentPoseFadeHoldsItsSourceStillWhileTheDestinationMoves()
    {
        var player = new BlendPlayer(Graph(), Clips());
        player.Advance(0.2);
        player.Enter("b", fade: 1, shape: Easing.Linear, fromPose: true);
        Assert.Equal(2, Value(player.Sample()), 1e-9);
        Assert.Equal(13.5, Value(player.Advance(0.5).Frame), 1e-9);
        Assert.NotNull(player.Place.Frozen);
    }

    [Fact]
    public void InterruptingAFadeFreezesTheVisibleMixedPoseWithoutAJump()
    {
        var player = new BlendPlayer(Graph(), Clips());
        player.Enter("b", fade: 1, shape: Easing.Linear);
        player.Advance(0.2);
        var before = Value(player.Sample());
        player.Enter("c", fade: 1, shape: Easing.Linear, fromPose: true);
        Assert.Equal(before, Value(player.Sample()), 1e-9);
        Assert.Equal((before + 45) / 2, Value(player.Advance(0.5).Frame), 1e-9);
    }

    [Fact]
    public void AutomaticTransitionsCanFreezeTheCurrentPose()
    {
        var player = new BlendPlayer(
            Graph(fromPose: true),
            Clips(),
            inputs: new Dictionary<string, double> { ["go"] = 1 });
        player.Advance(0.2);
        Assert.Equal("b", player.State);
        Assert.Equal(2, Value(player.Sample()), 1e-9);
        player.Inputs["go"] = 0;
        Assert.Equal(12, Value(player.Advance(0.2).Frame), 1e-9);
    }

    [Fact]
    public void AClipPairFadeOverridesTheEdgeDurationInEitherDirectionOfEntry()
    {
        var plain = Graph();
        var blend = new BlendDocument(
            "Pair",
            plain.States,
            inputs: plain.Inputs,
            changes: plain.Changes,
            fades: [new BlendFade("a", "b", 0.75)]);
        var player = new BlendPlayer(blend, Clips(), inputs: new Dictionary<string, double> { ["go"] = 1 });
        player.Advance(0);
        Assert.Equal(0.75, player.Place.Fade);
        player.Enter("a");
        player.Enter("b", fade: 2);
        Assert.Equal(0.75, player.Place.Fade);
        var loaded = BlendDocument.Decode(blend.Encode());
        Assert.Empty(loaded.Problems);
        Assert.Equal(0.75, Assert.Single(loaded.Blend.Fades).Seconds);
    }

    [Fact]
    public void NestedGraphsPlayChildrenEnterTheirStartAndExitFromAnyChild()
    {
        var player = new BlendPlayer(Nested(), Clips());
        Assert.Equal("ground/a", player.State);
        player.Inputs["speed"] = 2;
        player.Advance(0);
        Assert.Equal("ground/b", player.State);
        player.Inputs["jump"] = 1;
        player.Advance(0);
        Assert.Equal("c", player.State);
        player.Inputs["land"] = 1;
        player.Advance(0);
        Assert.Equal("ground/a", player.State);
        player.Enter("ground");
        Assert.Equal("ground/a", player.State);
        Assert.Equal(["c"], player.Blend.RouteTo("ground/a", "c"));
    }

    [Fact]
    public void NestedAssetsRoundTripWithoutLosingGraphBoundariesOrInputs()
    {
        var original = Nested();
        var loaded = BlendDocument.Decode(original.Encode());
        Assert.Empty(loaded.Problems);
        Tree.Same(original.ToJson(), loaded.Blend.ToJson());
        Assert.Equal(0.5, loaded.Blend.Inputs["speed"]);
        Assert.Superset(new HashSet<string> { "a", "b", "c" }, loaded.Blend.ClipNames.ToHashSet());
        Assert.Equal(["ground/a", "ground/b", "c"], loaded.Blend.States.Select(static state => state.Name));
    }

    [Fact]
    public void MultipleGraphLevelsKeepTheirStartsAndNumericPlaces()
    {
        var blend = new BlendDocument("Root", [new BlendState("outer", new BlendGraph(Nested()))]);
        var player = new BlendPlayer(blend, Clips());
        Assert.Equal("outer/ground/a", player.State);
        player.Enter("outer/ground");
        Assert.Equal("outer/ground/a", player.State);
        Assert.Equal(player.Place, BlendPlace.FromNumbers(blend, player.Place.ToNumbers(blend)));
        Assert.Equal(player.State, BlendDocument.Decode(blend.Encode()).Blend.Start);
    }

    [Fact]
    public void FrozenPosesAndRoutesRoundTripAndAdvanceIdentically()
    {
        var blend = Graph();
        var player = new BlendPlayer(blend, Clips());
        player.Advance(0.2);
        player.Enter("b", fade: 1, fromPose: true);
        player.Travel("c");
        player.Advance(0.2);
        var saved = JsonText.Decode(JsonText.Encode(player.Place.ToJson()));
        var numbers = player.Place.ToNumbers(blend);
        Assert.True(numbers.Length > BlendPlace.Width);
        BlendPlace?[] restoredPlaces = [BlendPlace.FromJson(saved, blend), BlendPlace.FromNumbers(blend, numbers)];
        foreach (var restored in restoredPlaces)
        {
            Assert.Equal(player.Place, restored);
            var copy = new BlendPlayer(blend, Clips(), place: restored);
            Assert.Equal(Value(player.Sample()), Value(copy.Sample()));
            Assert.Equal(
                Value(blend.Advance(player.Place, player.Inputs, 0.2, Clips()).Frame),
                Value(copy.Advance(0.2).Frame));
        }

        numbers[^1] = 999;
        Assert.Null(BlendPlace.FromNumbers(blend, numbers));
    }

    [Fact]
    public void FrozenVectorsCannotBeChangedThroughSourceOrSampledFrames()
    {
        var frame = ArmFrame(position: new Vector3(1, 2, 3));
        var snapshot = new PoseSnapshot(frame);
        frame.BoneOf(string.Empty, "arm")!.Position!.X = 20;
        snapshot.Sample().BoneOf(string.Empty, "arm")!.Position!.X = 30;
        Assert.Equal(1, snapshot.Sample().BoneOf(string.Empty, "arm")!.Position!.X);
        Assert.Equal(snapshot, PoseSnapshot.FromJson(JsonText.Decode(JsonText.Encode(snapshot.ToJson()))));
    }

    [Fact]
    public void FormatOneLoadsWithItsOriginalPlaybackDefaults()
    {
        var raw = Graph().ToJson();
        raw["formatVersion"] = 1L;
        var loaded = BlendDocument.Decode(JsonText.Encode(raw));
        Assert.Empty(loaded.Problems);
        Assert.Equal("a", loaded.Blend.Start);
        Assert.False(loaded.Blend.Changes[0].FromPose);
        Assert.Empty(loaded.Blend.States[0].Sync);
    }
}
