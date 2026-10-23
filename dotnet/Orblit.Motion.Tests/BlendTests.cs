using Orblit.Json;
using static Orblit.Motion.Tests.BlendSamples;

namespace Orblit.Motion.Tests;

public class BlendTests
{
    private static readonly BlendChange ToMove = new(
        "move", "idle", BlendCondition.Above("speed", 0.1), 0.2, inStep: true);

    private static readonly BlendChange ToIdle = new("idle", "move", BlendCondition.Below("speed", 0.1), 0.3);

    private static readonly BlendChange ToJump = new(
        "jump", when: BlendCondition.On("jump"), fade: 0.1, shape: Easing.Out);

    private static readonly BlendChange ToLand = new("land", "jump", BlendCondition.Through(1));

    private static readonly BlendChange Landed = new("idle", "land", BlendCondition.Through(1), 0.2);

    /// <summary>A pose held still: <c>pose.lean</c> at <paramref name="lean"/> throughout.</summary>
    // F2: five parameters, all but the first two optional and called by name.
    private static ClipDocument Still(
        string name,
        double lean,
        double duration = 1,
        WhenDone whenDone = WhenDone.Loop,
        IReadOnlyList<Mark>? marks = null) => new(
        name,
        duration,
        whenDone,
        channels:
        [
            new ClipChannel<double>(string.Empty, "pose.lean", ChannelKind.Number, [new Key<double>(0, lean)]),
        ],
        marks: marks);

    /// <summary><paramref name="metres"/> along Z in <paramref name="duration"/> seconds, looping, carrying the character.</summary>
    private static ClipDocument Walking(
        string name,
        double metres = 1,
        double duration = 1,
        IReadOnlyList<Mark>? marks = null) => new(
        name,
        duration,
        WhenDone.Loop,
        channels:
        [
            new ClipChannel<Vector3>(
                "body",
                "transform.position",
                ChannelKind.Vector,
                [new Key<Vector3>(0, Vector3.Zero(), Hold.Linear), new Key<Vector3>(duration, new Vector3(0, 0, metres))]),
        ],
        marks: marks,
        rootMotion: new RootMotion("body"));

    /// <summary>Standing, moving along a line from a walk to a run, jumping and landing.</summary>
    private static BlendDocument Moves() => new(
        "Moves",
        [
            new BlendState("idle", new BlendClip("idle")),
            new BlendState(
                "move",
                new BlendLine("speed", [new LinePoint(1, new BlendClip("walk")), new LinePoint(3, new BlendClip("run"))])),
            new BlendState("jump", new BlendClip("jump")),
            new BlendState("land", new BlendClip("land"), whenDone: WhenDone.Hold),
        ],
        inputs: Inputs(("speed", 0), ("jump", 0)),
        changes: [ToMove, ToIdle, ToJump, ToLand, Landed]);

    /// <summary>The clips <see cref="Moves"/> names. A walk goes a metre a second and a run three.</summary>
    private static Dictionary<string, ClipDocument> MovesClips() => new()
    {
        ["idle"] = Still("idle", 0, duration: 2, marks: [new Mark(0.5, "breath")]),
        ["walk"] = Walking("walk", marks: [new Mark(0.25, "walk-step"), new Mark(0.75, "walk-step")]),
        ["run"] = Walking("run", metres: 3, marks: [new Mark(0.25, "run-step"), new Mark(0.75, "run-step")]),
        ["jump"] = Still("jump", 1, duration: 0.5, whenDone: WhenDone.Hold),
        ["land"] = Still("land", -1, duration: 0.25),
    };

    private static List<string> Names(BlendStep step) => step.Marks.Select(static mark => mark.Name).ToList();

    private static double Lean(ClipFrame frame) => (double)frame.ValueOf(string.Empty, "pose.lean")!;

    private static BlendDocument OneStateOfClip(string name, string state, string clip, WhenDone? whenDone = null) =>
        new(name, [new BlendState(state, new BlendClip(clip), whenDone: whenDone)]);

    [Fact]
    public void ABlendStartsInItsFirstStateUnlessItSaysAnother()
    {
        Assert.Equal("idle", Moves().Start);
        var blend = new BlendDocument(
            "Two",
            [new BlendState("a", new BlendClip("a")), new BlendState("b", new BlendClip("b"))],
            start: "b");
        Assert.Equal("b", blend.Start);
    }

    [Fact]
    public void ABlendNeedsAStateEachWithItsOwnName()
    {
        Assert.Throws<ArgumentException>(static () => new BlendDocument("None", []));
        Assert.Throws<ArgumentException>(static () => new BlendDocument(
            "Twice",
            [new BlendState("a", new BlendClip("a")), new BlendState("a", new BlendClip("b"))]));
        Assert.Throws<ArgumentException>(static () => new BlendState(string.Empty, new BlendClip("a")));
    }

    [Fact]
    public void ABlendStartsInAndChangesBetweenStatesItHas()
    {
        BlendState[] states = [new BlendState("a", new BlendClip("a"))];
        Assert.Throws<ArgumentException>(() => new BlendDocument("x", states, start: "b"));
        Assert.Throws<ArgumentException>(() => new BlendDocument("x", states, changes: [new BlendChange("b")]));
        Assert.Throws<ArgumentException>(() => new BlendDocument("x", states, changes: [new BlendChange("a", "b")]));
    }

    [Fact]
    public void ABlendPlaysForwardsOrNotAtAll()
    {
        Assert.Throws<ArgumentException>(static () => new BlendState("a", new BlendClip("a"), speed: -1));
        Assert.Throws<ArgumentException>(static () => new BlendState("a", new BlendClip("a"), speed: double.NaN));
    }

    [Fact]
    public void ABlendListsEveryClipItNamesAndEveryInputItReads()
    {
        var blend = Moves();
        Assert.Equal(["idle", "walk", "run", "jump", "land"], blend.ClipNames);
        Assert.Equal(new HashSet<string> { "speed", "jump" }, blend.InputsRead.ToHashSet());
        Assert.Equal(2, blend.IndexOf("jump"));
        Assert.Equal(-1, blend.IndexOf("swim"));
        Assert.IsType<BlendLine>(blend.StateNamed("move")!.Plays);
    }

    [Fact]
    public void AStateOrAChangeWithSomethingChangedAStateKeepsWhatItIsNotGiven()
    {
        var state = new BlendState(
            "move",
            new BlendClip("walk"),
            speed: 1.5,
            whenDone: WhenDone.Hold,
            sync: ["left", "right"]);
        Tree.Same(state.ToJson(), state.CopyWith().ToJson());
        var renamed = state.CopyWith(name: "run", speed: 2);
        Assert.Equal("run", renamed.Name);
        Assert.Equal(2, renamed.Speed);
        Assert.Equal(WhenDone.Hold, renamed.WhenDone);
        Assert.Equal(["left", "right"], renamed.Sync);
    }

    [Fact]
    public void AStateOrAChangeWithSomethingChangedAStateCanGoBackToEachClipEndingItsOwnWay()
    {
        var state = new BlendState("a", new BlendClip("a"), whenDone: WhenDone.Hold);
        Assert.Null(state.CopyWith(clearWhenDone: true).WhenDone);
    }

    [Fact]
    public void AStateOrAChangeWithSomethingChangedAChangeKeepsWhatItIsNotGiven()
    {
        Tree.Same(ToMove.ToJson(), ToMove.CopyWith().ToJson());
        var slower = ToMove.CopyWith(fade: 0.5, shape: Easing.Linear);
        Assert.Equal("idle", slower.From);
        Assert.True(slower.InStep);
        Assert.Equal(0.5, slower.Fade);
        Assert.Equal(Easing.Linear, slower.Shape);
    }

    [Fact]
    public void AStateOrAChangeWithSomethingChangedAChangeCanBeMadeOneFromAnywhere()
    {
        Assert.Null(ToMove.CopyWith(clearFrom: true).From);
        Assert.Equal("idle", ToJump.CopyWith(from: "idle").From);
    }

    [Fact]
    public void ABlendWithSomethingChangedKeepsEverythingItIsNotGiven()
    {
        var blend = Moves();
        var same = blend.CopyWith();
        Assert.Equal(blend.Encode(), same.Encode());
        Assert.Equal(blend.Start, same.Start);
    }

    [Fact]
    public void ABlendWithSomethingChangedTakesNewStatesChangesAStartAndAName()
    {
        var edited = Moves().CopyWith(
            name: "Fewer",
            states: [new BlendState("idle", new BlendClip("idle")), new BlendState("jump", new BlendClip("jump"))],
            changes: [ToJump],
            start: "jump");
        Assert.Equal("Fewer", edited.Name);
        Assert.Equal("jump", edited.Start);
        Assert.Equal(["idle", "jump"], edited.States.Select(static state => state.Name));
        Assert.Same(ToJump, Assert.Single(edited.Changes));
    }

    [Fact]
    public void ABlendWithSomethingChangedRefusesAChangeLeftPointingAtAStateThatIsGone() =>
        Assert.Throws<ArgumentException>(static () =>
            Moves().CopyWith(states: [new BlendState("idle", new BlendClip("idle"))]));

    [Fact]
    public void ABlendWithSomethingChangedWritesANestedGraphOnceNotOnceForEachOfItsLeaves()
    {
        var inner = new BlendDocument(
            "Inner",
            [new BlendState("a", new BlendClip("a")), new BlendState("b", new BlendClip("b"))],
            inputs: Inputs(("speed", 0)),
            changes: [new BlendChange("b", "a", BlendCondition.Above("speed", 1))],
            fades: [new BlendFade("a", "b", 0.3)]);
        var outer = new BlendDocument(
            "Outer",
            [new BlendState("ground", new BlendGraph(inner)), new BlendState("air", new BlendClip("air"))],
            changes: [new BlendChange("air", "ground")]);
        var edited = outer.CopyWith(name: "Renamed");
        Assert.Equal(["ground", "air"], edited.WrittenStates.Select(static state => state.Name));
        Assert.Equal(["ground/a", "ground/b", "air"], edited.States.Select(static state => state.Name));
        Assert.Single(edited.Fades);
        Assert.Empty(edited.WrittenFades);
        Assert.Equal(outer.Encode().Replace("Outer", "Renamed", StringComparison.Ordinal), edited.Encode());
    }

    [Fact]
    public void WhichChangeComesNextIsNoneWhileNothingHolds() =>
        Assert.Null(Moves().ChangeFor(new BlendPlace("idle"), NoInputs));

    [Fact]
    public void WhichChangeComesNextIsOneFromTheStateBeingPlayed()
    {
        var blend = Moves();
        Assert.Same(ToMove, blend.ChangeFor(new BlendPlace("idle"), Inputs(("speed", 2))));
        Assert.Same(ToIdle, blend.ChangeFor(new BlendPlace("move"), Inputs(("speed", 0))));
        Assert.Null(blend.ChangeFor(new BlendPlace("move"), Inputs(("speed", 2))));
    }

    [Fact]
    public void WhichChangeComesNextIsOneFromAnywhereButNeverIntoTheStateBeingPlayed()
    {
        var blend = Moves();
        Assert.Same(ToJump, blend.ChangeFor(new BlendPlace("move"), Inputs(("speed", 2), ("jump", 1))));
        Assert.Null(blend.ChangeFor(new BlendPlace("jump"), Inputs(("jump", 1))));
    }

    [Fact]
    public void WhichChangeComesNextIsTheFirstThatHoldsInTheOrderTheyAreWritten() =>
        Assert.Same(ToMove, Moves().ChangeFor(new BlendPlace("idle"), Inputs(("speed", 2), ("jump", 1))));

    [Fact]
    public void WhichChangeComesNextWaitsForAStateToPlayThroughWhenToldTo()
    {
        var blend = Moves();
        Assert.Null(blend.ChangeFor(new BlendPlace("jump", lap: 0.9), Inputs()));
        Assert.Same(ToLand, blend.ChangeFor(new BlendPlace("jump", lap: 1), Inputs()));
    }

    [Fact]
    public void WhichChangeComesNextReadsAnInputItIsNotGivenAsTheBlendSaysItStarts()
    {
        var blend = new BlendDocument(
            "Grounded",
            [new BlendState("stand", new BlendClip("stand")), new BlendState("fall", new BlendClip("fall"))],
            inputs: Inputs(("grounded", 1)),
            changes: [new BlendChange("fall", when: BlendCondition.Not(BlendCondition.On("grounded")))]);
        Assert.Null(blend.ChangeFor(new BlendPlace("stand"), Inputs()));
        Assert.Null(blend.ChangeFor(new BlendPlace("stand"), Inputs(("grounded", double.NaN))));
        Assert.NotNull(blend.ChangeFor(new BlendPlace("stand"), Inputs(("grounded", 0))));
    }

    [Fact]
    public void WhichClipsHaveASaySharesAFadeBetweenTheStatesInIt()
    {
        var place = new BlendPlace(
            "move",
            lap: 0.5,
            from: new BlendPlace("idle", lap: 3.2),
            fade: 0.4,
            faded: 0.2,
            shape: Easing.Linear);
        var weights = Moves().WeightsAt(place, Inputs(("speed", 2)));
        Assert.Equal(
            [("move", "walk", 0.5), ("move", "run", 0.5), ("idle", "idle", 3.2)],
            weights.Select(static one => (one.State, one.Clip, one.Lap)));
        Assert.Collection(
            weights.Select(static one => one.Weight),
            static one => Assert.Equal(0.25, one, 1e-12),
            static one => Assert.Equal(0.25, one, 1e-12),
            static one => Assert.Equal(0.5, one, 1e-12));
    }

    [Fact]
    public void WhichClipsHaveASayLeavesOutAStateWithNoSayYet()
    {
        var place = new BlendPlace("idle").Enter("jump", fade: 0.1);
        Assert.Equal(["idle"], Moves().WeightsAt(place, NoInputs).Select(static one => one.Clip));
    }

    [Fact]
    public void HowLongAStateIsIsItsClipsMixedAsTheyAre()
    {
        var blend = Moves();
        var clips = new Dictionary<string, ClipDocument>
        {
            ["walk"] = Walking("walk", duration: 1.2),
            ["run"] = Walking("run", duration: 0.8),
        };
        Assert.Equal(1, blend.LengthOf("move", Inputs(("speed", 2)), clips), 1e-12);
        Assert.Equal(0.8, blend.LengthOf("move", Inputs(("speed", 3)), clips), 1e-12);
    }

    [Fact]
    public void HowLongAStateIsIsNoughtWithNoClipsToMeasure()
    {
        Assert.Equal(0, Moves().LengthOf("move", NoInputs, new Dictionary<string, ClipDocument>()));
        Assert.Equal(0, Moves().LengthOf("swim", NoInputs, MovesClips()));
    }

    [Fact]
    public void ThePoseMixesAFadeAsItHasGotTo()
    {
        var place = new BlendPlace("jump", from: new BlendPlace("idle"), faded: 0.03, fade: 0.1, shape: Easing.Linear);
        Assert.Equal(0.3, Lean(Moves().SampleAt(place, NoInputs, MovesClips())), 1e-12);
    }

    [Fact]
    public void ThePoseHoldsRootMotionInPlace()
    {
        var frame = Moves().SampleAt(new BlendPlace("move", lap: 0.5), Inputs(("speed", 2)), MovesClips());
        var body = (Vector3)frame.ValueOf("body", "transform.position")!;
        Assert.True(body.Length < 1e-12);
    }

    [Fact]
    public void ThePoseSharesOutWhatAMissingClipWouldHaveHad()
    {
        var place = new BlendPlace("jump", from: new BlendPlace("idle"), faded: 0.03, fade: 0.1);
        var clips = MovesClips();
        clips.Remove("idle");
        Assert.Equal(1, Lean(Moves().SampleAt(place, NoInputs, clips)));
    }

    [Fact]
    public void ThePoseIsWhereTheLoudestClipIs()
    {
        var frame = Moves().SampleAt(new BlendPlace("move", lap: 1.25), Inputs(("speed", 2.5)), MovesClips());
        Assert.Equal(0.25, frame.At, 1e-12);
    }

    [Fact]
    public void PlayingOnGoesThroughAStateByTheTimeItsClipsTake()
    {
        var step = Moves().Advance(new BlendPlace("idle"), NoInputs, 0.5, MovesClips());
        Assert.Equal(new BlendPlace("idle", lap: 0.25), step.Place);
        Assert.Null(step.Change);
    }

    [Fact]
    public void PlayingOnGoesAtTheStatesOwnSpeed()
    {
        var blend = new BlendDocument(
            "Fast",
            [
                new BlendState("idle", new BlendClip("idle"), speed: 2),
                new BlendState("frozen", new BlendClip("idle"), speed: 0),
            ]);
        var clips = MovesClips();
        Assert.Equal(new BlendPlace("idle", lap: 0.5), blend.Advance(new BlendPlace("idle"), NoInputs, 0.5, clips).Place);
        Assert.Equal(
            new BlendPlace("frozen", lap: 0.3),
            blend.Advance(new BlendPlace("frozen", lap: 0.3), NoInputs, 0.5, clips).Place);
    }

    [Fact]
    public void PlayingOnCarriesTheCharacterAsFarAsTheMixOfClipsGoes()
    {
        var step = Moves().Advance(new BlendPlace("move"), Inputs(("speed", 2)), 0.5, MovesClips());
        Assert.Equal(0.5, step.Place.Lap, 1e-12);
        Assert.True((step.Moved.Position - new Vector3(0, 0, 1)).Length < 1e-9);
    }

    [Fact]
    public void PlayingOnCarriesOnThroughAFadeTheStateFadingOutIncluded()
    {
        var blend = Moves();
        var clips = MovesClips();
        var fast = Inputs(("speed", 2));
        var place = blend.Advance(new BlendPlace("idle", lap: 0.5), fast, 0, clips).Place;
        Assert.Equal("move", place.State);
        // In step with the idle, as the change says.
        Assert.Equal(0.5, place.Lap);
        Assert.Equal(new BlendPlace("idle", lap: 0.5), place.From);

        var step = blend.Advance(place, fast, 0.1, clips);
        place = step.Place;
        Assert.Equal(0.1, place.Faded, 1e-12);
        Assert.Equal(0.6, place.Lap, 1e-12);
        Assert.Equal(0.55, place.From!.Lap, 1e-12);
        // Half faded, smoothly, so half standing still and half moving at two metres a second.
        Assert.Equal(0.5 * 0.2, step.Moved.Position.Z, 1e-9);

        step = blend.Advance(place, fast, 0.15, clips);
        Assert.Null(step.Place.From);
        Assert.Equal(0.75, step.Place.Lap, 1e-12);
    }

    [Fact]
    public void PlayingOnKeepsInStepWhenTheChangeSaysSo()
    {
        var place = Moves().Advance(new BlendPlace("idle", lap: 1.75), Inputs(("speed", 2)), 0, MovesClips()).Place;
        Assert.Equal(0.75, place.Lap);
    }

    [Fact]
    public void PlayingOnTakesAChangeAsSoonAsTheStateHasPlayedThrough()
    {
        var blend = Moves();
        var clips = MovesClips();
        var step = blend.Advance(new BlendPlace("jump"), NoInputs, 0.4, clips);
        Assert.Equal(new BlendPlace("jump", lap: 0.8), step.Place);
        step = blend.Advance(step.Place, NoInputs, 0.2, clips);
        Assert.Same(ToLand, step.Change);
        Assert.Equal(new BlendPlace("land"), step.Place);
        Assert.Equal(-1, Lean(step.Frame));
    }

    [Fact]
    public void PlayingOnTakesOneChangeAStepHoweverTheyChain()
    {
        var blend = new BlendDocument(
            "Round",
            [
                new BlendState("a", new BlendClip("idle")),
                new BlendState("b", new BlendClip("idle")),
                new BlendState("c", new BlendClip("idle")),
            ],
            changes: [new BlendChange("b", "a"), new BlendChange("c", "b"), new BlendChange("a", "c")]);
        var clips = MovesClips();
        var place = new BlendPlace("a");
        var visited = new List<string>();
        for (var i = 0; i < 4; i++)
        {
            place = blend.Advance(place, NoInputs, 1.0 / 60, clips).Place;
            visited.Add(place.State);
        }

        Assert.Equal(["b", "c", "a", "b"], visited);
    }

    [Fact]
    public void PlayingOnKeepsNoMoreThanTheDeepestFewFades()
    {
        var blend = Moves();
        var clips = MovesClips();
        var place = new BlendPlace("idle");
        Dictionary<string, double>[] sequence =
        [
            Inputs(("speed", 2)),
            Inputs(("speed", 2), ("jump", 1)),
            Inputs(("speed", 0)),
        ];
        foreach (var values in sequence)
        {
            place = blend.Advance(place, values, 0.01, clips).Place;
        }

        place = place.Enter("move", fade: 1).Enter("idle", fade: 1);
        Assert.Equal(BlendPlace.Deepest, place.Depth);
        var step = blend.Advance(place, NoInputs, 0.01, clips);
        Assert.True(step.Place.Depth <= BlendPlace.Deepest);
    }

    [Fact]
    public void MarksFireEveryLapOfALoopTheFirstFrameIncluded()
    {
        var blend = OneStateOfClip("Walk", "walk", "walk");
        var clips = new Dictionary<string, ClipDocument>
        {
            ["walk"] = Walking("walk", marks: [new Mark(0, "start"), new Mark(0.5, "middle")]),
        };
        var step = blend.Advance(new BlendPlace("walk"), NoInputs, 2, clips);
        Assert.Equal(["start", "middle", "start", "middle"], Names(step));
    }

    [Fact]
    public void MarksComeFromTheClipWithTheMostSayAndOnlyIt()
    {
        var blend = Moves();
        var clips = MovesClips();
        Assert.Equal(["walk-step"], Names(blend.Advance(new BlendPlace("move"), Inputs(("speed", 1.5)), 0.5, clips)));
        Assert.Equal(["run-step"], Names(blend.Advance(new BlendPlace("move"), Inputs(("speed", 2.5)), 0.5, clips)));
    }

    [Fact]
    public void MarksComeFromTheStateBeingPlayedNotOneFadingOut()
    {
        var place = new BlendPlace("move", from: new BlendPlace("idle"), fade: 5, shape: Easing.Linear);
        var step = Moves().Advance(place, Inputs(("speed", 1.5)), 1, MovesClips());
        Assert.Equal(["walk-step", "walk-step"], Names(step));
    }

    [Fact]
    public void MarksFireOnceForAClipThatHolds()
    {
        var blend = OneStateOfClip("Once", "hit", "hit");
        var clips = new Dictionary<string, ClipDocument>
        {
            ["hit"] = Still("hit", 0, whenDone: WhenDone.Hold, marks: [new Mark(0, "swing"), new Mark(1, "land")]),
        };
        var step = blend.Advance(new BlendPlace("hit"), NoInputs, 0.5, clips);
        Assert.Equal(["swing"], Names(step));
        step = blend.Advance(step.Place, NoInputs, 5, clips);
        Assert.Equal(["land"], Names(step));
        step = blend.Advance(step.Place, NoInputs, 5, clips);
        Assert.Empty(Names(step));
    }

    [Fact]
    public void OddStepsGetThroughAStallWithoutWalkingEveryLap()
    {
        var blend = OneStateOfClip("Walk", "walk", "walk");
        var clips = new Dictionary<string, ClipDocument> { ["walk"] = Walking("walk") };
        var step = blend.Advance(new BlendPlace("walk"), NoInputs, 1e6, clips);
        Assert.Equal(1e6, step.Place.Lap, 1e-6);
        Assert.Equal(1e6, step.Moved.Position.Z, 1e-3);
    }

    [Fact]
    public void OddStepsComeBackToWhereTheyStartedForABounceThereAndBack()
    {
        var blend = OneStateOfClip("Pace", "pace", "walk", WhenDone.Bounce);
        var clips = new Dictionary<string, ClipDocument> { ["walk"] = Walking("walk") };
        var there = blend.Advance(new BlendPlace("pace"), NoInputs, 1.5, clips);
        Assert.Equal(0.5, there.Moved.Position.Z, 1e-9);
        var back = blend.Advance(there.Place, NoInputs, 0.5, clips);
        Assert.Equal(-0.5, back.Moved.Position.Z, 1e-9);
        Assert.Equal(0, blend.Advance(new BlendPlace("pace"), NoInputs, 2, clips).Moved.Position.Z, 1e-9);
    }

    [Fact]
    public void OddStepsPlayOnWithoutAClipThatIsMissing()
    {
        var clips = MovesClips();
        clips.Remove("run");
        var step = Moves().Advance(new BlendPlace("move"), Inputs(("speed", 2)), 0.5, clips);
        Assert.Equal(0.5, step.Place.Lap, 1e-12);
        Assert.Equal(0.5, step.Moved.Position.Z, 1e-9);
        Assert.Equal(
            ["walk", "run"],
            Moves().WeightsAt(step.Place, Inputs(("speed", 2))).Select(static one => one.Clip));
    }

    [Fact]
    public void OddStepsAreThroughAtOnceForAStateWithNothingToPlay()
    {
        var blend = new BlendDocument(
            "Empty",
            [new BlendState("gone", new BlendClip("nothing")), new BlendState("idle", new BlendClip("idle"))],
            changes: [new BlendChange("idle", "gone", BlendCondition.Through(1))]);
        var clips = MovesClips();
        Assert.Equal(new BlendPlace("gone"), blend.Advance(new BlendPlace("gone"), NoInputs, 0, clips).Place);
        Assert.Equal(new BlendPlace("idle"), blend.Advance(new BlendPlace("gone"), NoInputs, 0.1, clips).Place);
    }

    [Fact]
    public void OddStepsPlayNothingForAStepThatIsNotALengthOfTime()
    {
        double[] steps = [-1, double.NaN, double.PositiveInfinity];
        foreach (var seconds in steps)
        {
            var step = Moves().Advance(new BlendPlace("idle", lap: 0.5), NoInputs, seconds, MovesClips());
            Assert.Equal(new BlendPlace("idle", lap: 0.5), step.Place);
        }
    }

    [Fact]
    public void InAFileReadsBackWhatItWritesWithNothingToNote()
    {
        var blend = new BlendDocument(
            "Moves",
            [
                new BlendState(
                    "move",
                    new BlendLine(
                        "speed",
                        [
                            new LinePoint(0, new BlendClip("idle")),
                            new LinePoint(
                                2,
                                new BlendPlane(
                                    "sideways",
                                    "speed",
                                    [
                                        new PlanePoint(0, 2, new BlendClip("walk")),
                                        new PlanePoint(1, 0, new BlendClip("strafe")),
                                    ])),
                        ]),
                    speed: 1.25),
                new BlendState("idle", new BlendClip("idle")),
                new BlendState("jump", new BlendClip("jump"), whenDone: WhenDone.Hold),
            ],
            inputs: Inputs(("speed", 0), ("sideways", 0), ("jump", 0)),
            changes:
            [
                new BlendChange("move", "idle", BlendCondition.Above("speed", 0.1), 0.2, inStep: true),
                new BlendChange(
                    "jump",
                    when: BlendCondition.AllOf([BlendCondition.On("jump"), BlendCondition.Not(BlendCondition.Through(0.5))]),
                    fade: 0.1,
                    shape: Easing.Out),
                new BlendChange("idle", "jump"),
            ],
            start: "idle");
        var text = blend.Encode();
        var load = BlendDocument.Decode(text);
        Assert.Empty(load.Problems);
        Assert.Equal(text, load.Blend.Encode());
        Assert.Equal("idle", load.Blend.Start);
        Assert.Equal(1.25, load.Blend.States[0].Speed);
        Assert.Equal(Easing.Out, load.Blend.Changes[1].Shape);
        Assert.True(load.Blend.Changes[0].InStep);
    }

    [Fact]
    public void InAFilePutsAnythingShortOnALineOfItsOwn()
    {
        var text = Moves().Encode();
        Assert.Contains("\n    {\"name\":\"idle\",\"clip\":\"idle\"},\n", text, StringComparison.Ordinal);
        Assert.Contains("\n    {\"from\":\"jump\",\"to\":\"land\",\"when\":{\"through\":1.0}},\n", text, StringComparison.Ordinal);
        Assert.Contains("\n      \"inStep\": true\n", text, StringComparison.Ordinal);
        Assert.StartsWith("{\n  \"kind\": \"orblit.blend\",\n", text, StringComparison.Ordinal);
    }

    [Fact]
    public void InAFileIsNotABlendUnlessItSaysSo()
    {
        Assert.Throws<BlendFormatException>(static () => BlendDocument.Decode("not json"));
        Assert.Throws<BlendFormatException>(static () => BlendDocument.Decode("{\"kind\": \"orblit.clip\"}"));
        var wrongVersion = Moves().ToJson();
        wrongVersion["formatVersion"] = 99L;
        Assert.Throws<BlendFormatException>(() => BlendDocument.Decode(JsonText.Encode(wrongVersion)));
        var noStates = Moves().ToJson();
        noStates["states"] = new List<object?>();
        Assert.Throws<BlendFormatException>(() => BlendDocument.Decode(JsonText.Encode(noStates)));
    }

    [Fact]
    public void InAFileLeavesOutWhatItCannotReadAndSaysSo()
    {
        var load = BlendDocument.Decode(JsonText.Encode(Tree.Map(
            ("kind", "orblit.blend"),
            ("formatVersion", 1L),
            ("name", "Rough"),
            ("inputs", Tree.Map(("speed", "fast"))),
            ("start", "swim"),
            ("states", Tree.List(
                Tree.Map(("name", "idle"), ("clip", "idle"), ("speed", -2L)),
                Tree.Map(("clip", "nameless")),
                Tree.Map(("name", "idle"), ("clip", "again")),
                Tree.Map(("name", "empty")),
                Tree.Map(("name", "walk"), ("clip", "walk")))),
            ("changes", Tree.List(
                Tree.Map(("from", "idle"), ("to", "swim")),
                Tree.Map(
                    ("from", "idle"),
                    ("to", "walk"),
                    ("when", Tree.Map(("input", "speed"), ("above", "slow")))),
                Tree.Map(("from", "walk"), ("to", "idle"), ("fade", -1L)),
                Tree.Map(("to", "walk"), ("when", Tree.Map(("input", "hurry")))))))));
        var blend = load.Blend;
        Assert.Equal(["idle", "walk"], blend.States.Select(static state => state.Name));
        Assert.Equal(1, blend.States[0].Speed);
        Assert.Equal("idle", blend.Start);
        var input = Assert.Single(blend.Inputs);
        Assert.Equal(("speed", 0.0), (input.Key, input.Value));
        Assert.Equal(2, blend.Changes.Count);
        Assert.Equal(0, blend.Changes[0].Fade);
        Assert.Equal(
            [
                "The input \"speed\" starts at something that is not a number, so it starts at nought.",
                "The state \"idle\" has a speed it cannot play at, so it plays at one.",
                "A state with no name was left out.",
                "Two states are called \"idle\"; the second was left out.",
                "The state \"empty\" plays nothing: it names no clip, line or plane.",
                "The blend starts in \"swim\", which is not one of its states, so it starts in \"idle\".",
                "The change from \"idle\" to \"swim\" is not between two of the states, so it was left out.",
                "The change from \"idle\" to \"walk\" compares \"speed\" with something that is not a number.",
                "The change from \"idle\" to \"walk\" was left out, since its condition could not be read.",
                "The change from \"walk\" to \"idle\" fades for a time that is not one, so it cuts.",
                "The blend reads \"hurry\", which it does not declare, so it is nought until something sets it.",
            ],
            load.Problems);
    }
}
