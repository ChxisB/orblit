using Orblit.Json;
using static Orblit.Motion.Tests.ClipSamples;

namespace Orblit.Motion.Tests;

/// <summary>Checks the C# adds to the ported clip tests: the pieces that have no test of their own there.</summary>
public class ClipAddedTests
{
    private static readonly FileFormat Format = new(
        "orblit.test",
        "test",
        3,
        message => new InvalidOperationException(message),
        [new Step(1, "one"), new Step(2, "two")]);

    [Fact]
    public void AFileFormatRunsEveryStepFromTheVersionWrittenUp()
    {
        var problems = new List<string>();

        var json = Format.Open(Text(1), problems);

        Assert.Equal(["one", "two"], problems);
        Assert.Equal("one,two", json["steps"]);
    }

    [Fact]
    public void AFileFormatSkipsTheStepsBelowTheVersionWritten()
    {
        var problems = new List<string>();

        var json = Format.Open(Text(2), problems);

        Assert.Equal(["two"], problems);
        Assert.Equal("two", json["steps"]);
    }

    [Fact]
    public void AFileFormatLeavesAFileAtTheCurrentVersionAlone()
    {
        var problems = new List<string>();

        var json = Format.Open(Text(3), problems);

        Assert.Empty(problems);
        Assert.False(json.ContainsKey("steps"));
    }

    [Fact]
    public void AFileFormatReadsAFileWithNoVersionAsItIs()
    {
        var problems = new List<string>();

        var json = Format.Open("{\"kind\": \"orblit.test\"}", problems);

        Assert.Empty(problems);
        Assert.Single(json);
    }

    [Fact]
    public void AFileFormatSaysWhatIsWrongWithTheText()
    {
        var notJson = Assert.Throws<InvalidOperationException>(() => Format.Open("{", []));
        Assert.StartsWith("This is not a test file: ", notJson.Message);

        var wrongKind = Assert.Throws<InvalidOperationException>(() => Format.Open("{\"kind\": \"other\"}", []));
        Assert.Equal("This is not a test file.", wrongKind.Message);

        var newer = Assert.Throws<InvalidOperationException>(() => Format.Open(Text(4), []));
        Assert.Equal("This test was written by a newer Orblit.", newer.Message);
    }

    [Fact]
    public void EveryKindMakesAChannelOfItsOwnType()
    {
        var carried = new Dictionary<ChannelKind, object>
        {
            [ChannelKind.Number] = 1.5,
            [ChannelKind.Vector] = new Vector3(1, 2, 3),
            [ChannelKind.Rotation] = Quaternion.Identity(),
            [ChannelKind.Flag] = true,
        };

        Assert.Equal(carried.Count, ChannelKind.All.Count);
        foreach (var kind in ChannelKind.All)
        {
            var channel = ClipChannel.OfKind("", "a.b", kind, [new Key<object>(0, carried[kind])]);

            Assert.Same(kind, channel.Kind);
            Assert.Same(kind, ChannelKind.Named(kind.Name));
            Assert.Equal(carried[kind], channel.ValueAt(0));
        }
    }

    [Fact]
    public void AKindIsNamedOnlyByTheTextOfItsName()
    {
        Assert.Null(ChannelKind.Named("colour"));
        Assert.Null(ChannelKind.Named(1));
        Assert.Null(ChannelKind.Named(null));
    }

    [Fact]
    public void AnEnumMemberIsSpeltAsAFileSpellsIt()
    {
        Assert.Equal("linear", FileNames.Of(Hold.Linear));
        Assert.Equal("bounce", FileNames.Of(Easing.Bounce));

        // A file writes the easing In with a trailing underscore.
        Assert.Equal("in_", FileNames.Of(Easing.In));
        Assert.Equal(Easing.In, FileNames.Named<Easing>("in_"));
        Assert.Null(FileNames.Named<Easing>("in"));
        Assert.Null(FileNames.Named<Hold>(3));
    }

    [Fact]
    public void AnAddressIsDescribedByWhatItHas()
    {
        Assert.Equal("bulb › hips › rotation", ChannelAddress.Describe("bulb", "hips", "rotation"));
        Assert.Equal("hips › rotation", ChannelAddress.Describe(string.Empty, "hips", "rotation"));
        Assert.Equal("light.power", ChannelAddress.Describe(string.Empty, null, "light.power"));
        Assert.Equal(string.Empty, ChannelAddress.Describe(string.Empty, null, null));
    }

    [Fact]
    public void FileTextWritesEmptyThingsAsBrackets()
    {
        Assert.Equal("{}\n", FileText.Of(Tree.Map()));
        Assert.Equal("{\n  \"a\": [],\n  \"b\": {}\n}\n", FileText.Of(Tree.Map(("a", Tree.List()), ("b", Tree.Map()))));
    }

    [Fact]
    public void FileTextWritesAListOfPlainValuesOnOneLine()
    {
        var text = FileText.Of(Tree.Map(("value", Tree.List(1, 2, 3))));

        Assert.Equal($"{{\n  \"value\": {JsonText.Encode(Tree.List(1, 2, 3))}\n}}\n", text);
    }

    [Fact]
    public void FileTextWritesAKeyAMarkAShotAndASoundToALine()
    {
        var item = Tree.Map(("at", 1), ("name", "x"));
        foreach (var name in new[] { "keys", "marks", "shots", "sounds" })
        {
            var text = FileText.Of(Tree.Map((name, Tree.List(item))));

            Assert.Equal($"{{\n  \"{name}\": [\n    {JsonText.Encode(item)}\n  ]\n}}\n", text);
        }
    }

    [Fact]
    public void FileTextBreaksOtherListsOfMapsOverLines()
    {
        var text = FileText.Of(Tree.Map(("channels", Tree.List(Tree.Map(("a", 1))))));

        Assert.Equal("{\n  \"channels\": [\n    {\n      \"a\": 1\n    }\n  ]\n}\n", text);
    }

    [Fact]
    public void ASlopeSurvivesBoxingAndComingBack()
    {
        var channel = new ClipChannel<Vector3>(
            "",
            "transform.position",
            ChannelKind.Vector,
            [new Key<Vector3>(0, Vector3.Zero(), Hold.Curve, slopeIn: new Slope<Vector3>(new Vector3(1, 2, 3)))]);

        var boxed = Assert.Single(channel.BoxedKeys);
        var back = Assert.IsType<ClipChannel<Vector3>>(channel.Rekeyed(channel.BoxedKeys));

        Assert.Equal(new Vector3(1, 2, 3), boxed.SlopeIn!.Value.Value);
        Assert.Null(boxed.SlopeOut);
        Assert.Equal(new Vector3(1, 2, 3), back.Keys[0].SlopeIn!.Value.Value);
        Assert.Null(back.Keys[0].SlopeOut);
    }

    [Fact]
    public void AClipWithoutAnythingWritesAndReadsBack()
    {
        var empty = new ClipDocument("Empty", 0);

        var loaded = ClipDocument.Decode(empty.Encode());

        Assert.Empty(loaded.Problems);
        Assert.Equal(empty.Encode(), loaded.Clip.Encode());
        Assert.Empty(loaded.Clip.Channels);
        Assert.Null(loaded.Clip.RootMotion);
    }

    [Fact]
    public void ARootMotionKeepsItsUpAndWritesOnlyWhatIsNotUsual()
    {
        var plain = new RootMotion(bone: "hips").ToJson();
        Assert.False(plain.ContainsKey("turns"));
        Assert.False(plain.ContainsKey("up"));

        var tilted = new RootMotion(rises: true, up: new Vector3(0, 0, 4));
        var back = RootMotion.FromJson(tilted.ToJson())!;
        Assert.True(back.Rises);
        Assert.Equal(new Vector3(0, 0, 1), back.Up);
        Assert.Equal(new Vector3(0, 1, 0), new RootMotion(up: Vector3.Zero()).Up);
    }

    [Fact]
    public void ARootMotionOwnsTheChannelsOnItsTargetAndBone()
    {
        var clip = Populated();
        var motion = Assert.IsType<RootMotion>(clip.RootMotion);

        Assert.Equal([false, false, false, true], clip.Channels.Select(motion.Owns));
    }

    [Fact]
    public void ARootMotionFromSomethingThatIsNotAMapIsNone() => Assert.Null(RootMotion.FromJson("hips"));

    [Fact]
    public void ACopyChangesOnlyWhatItIsToldTo()
    {
        var clip = Populated();

        var renamed = clip.CopyWith(name: "Other");

        Assert.Equal("Other", renamed.Name);
        Assert.Equal(clip.Duration, renamed.Duration);
        Assert.Same(clip.Channels[0], renamed.Channels[0]);
        Assert.Same(clip.RootMotion, renamed.RootMotion);
        Assert.Null(clip.CopyWith(clearRootMotion: true).RootMotion);
    }

    [Fact]
    public void ASampledFrameWithNoMotionOnItsRootStaysAsAuthored()
    {
        var clip = Populated().CopyWith(clearRootMotion: true);

        Assert.Equal(
            Assert.IsType<Vector3>(Populated().SampleAt(1).ValueOf(string.Empty, "transform.position")),
            Assert.IsType<Vector3>(clip.SampleAt(1, inPlace: true).ValueOf(string.Empty, "transform.position")));
    }

    [Fact]
    public void AStatedDurationOfNothingIsKept()
    {
        var loaded = Load(
            ("duration", 0),
            ("channels", Tree.List(Tree.Map(
                ("property", "light.power"),
                ("kind", "number"),
                ("keys", Tree.List(Tree.Map(("at", 1), ("value", 1))))))));

        Assert.Empty(loaded.Problems);
        Assert.Equal(0, loaded.Clip.Duration);
    }

    [Fact]
    public void APropertyThatStartsWithADotNamesNoComponent()
    {
        var loaded = Load(("channels", Tree.List(Tree.Map(
            ("property", ".power"),
            ("kind", "number"),
            ("keys", Tree.List(Tree.Map(("at", 0), ("value", 1))))))));

        Assert.Empty(loaded.Clip.Channels);
        Assert.Contains("names no component", Assert.Single(loaded.Problems));
    }

    [Fact]
    public void AnAddressSplitsAtItsFirstDot()
    {
        var keys = new[] { new Key<double>(0, 1) };

        var entity = new ClipChannel<double>(string.Empty, "light.power.max", ChannelKind.Number, keys);
        var bone = new ClipChannel<double>(string.Empty, "position", ChannelKind.Number, keys, "hips");
        var bare = new ClipChannel<double>(string.Empty, "power", ChannelKind.Number, keys);
        var leading = new ClipChannel<double>(string.Empty, ".power", ChannelKind.Number, keys);

        Assert.Equal("light", entity.Component);
        Assert.Equal("power.max", entity.Field);
        Assert.Null(bone.Component);
        Assert.Equal("position", bone.Field);
        Assert.Null(bare.Component);
        Assert.Null(leading.Component);
    }

    [Fact]
    public void ARotationIsRefusedBelowANanometreAndKeptAtOne()
    {
        // The root of 1e-18 is exactly 1e-9 in a double, so this is the shortest rotation that is read.
        Assert.False(ChannelKind.Rotation.TryRead(Tree.List(5e-10, 0.0, 0.0, 0.0), out _));
        Assert.True(ChannelKind.Rotation.TryRead(Tree.List(1e-9, 0.0, 0.0, 0.0), out var tiny));
        Assert.Equal(1, tiny.X, 1e-9);
    }

    [Fact]
    public void ARotationIsMadeUnitLengthUnlessItIsAlreadyNearly()
    {
        Assert.True(ChannelKind.Rotation.TryRead(Tree.List(0.0, 0.0, 0.0, 1 + 5e-7), out var near));
        Assert.Equal(1 + 5e-7, near.W);

        Assert.True(ChannelKind.Rotation.TryRead(Tree.List(0.0, 0.0, 0.0, 1 + 2e-6), out var far));
        Assert.Equal(1, far.W, 1e-12);

        Assert.True(ChannelKind.Rotation.TryRead(Tree.List(0.0, 0.0, 0.0, 0.5), out var half));
        Assert.Equal(1, half.W, 1e-12);
    }

    [Fact]
    public void AStepIsNothingOnlyBelowTheSmallestMoveItNotices()
    {
        // 1e-9 squared is exactly 1e-18 in a double, so this move is the smallest one that counts.
        Assert.True(new RootStep(new Vector3(1e-10, 0, 0)).IsNone);
        Assert.False(new RootStep(new Vector3(1e-9, 0, 0)).IsNone);
        Assert.True(new RootStep(rotation: new Quaternion(0, 0, 0, 1 - 1e-13)).IsNone);
        Assert.False(new RootStep(rotation: new Quaternion(0, 0, 0, 1 - 1e-11)).IsNone);
    }

    [Fact]
    public void AStepRepeatedIsTheStepTakenThatManyTimes()
    {
        var step = new RootStep(new Vector3(0, 0, 1), Quaternion.AxisAngle(new Vector3(0, 1, 0), 0.3));
        var expected = new RootStep();

        for (var times = 0; times < 10; times++)
        {
            var got = RootStep.Repeated(step, times);

            Assert.Equal(expected.Position.X, got.Position.X, 1e-9);
            Assert.Equal(expected.Position.Y, got.Position.Y, 1e-9);
            Assert.Equal(expected.Position.Z, got.Position.Z, 1e-9);
            Assert.All(
                Numbers(expected.Rotation).Zip(Numbers(got.Rotation)),
                pair => Assert.Equal(pair.First, pair.Second, 1e-9));
            expected = expected.Then(step);
        }
    }

    [Fact]
    public void ARootMotionUpIsOnlyReplacedWhenItPointsNowhere()
    {
        // 1e-6 squared is exactly 1e-12 in a double, so this is the shortest up that is kept.
        Assert.Equal(new Vector3(1, 0, 0), new RootMotion(up: new Vector3(1e-6, 0, 0)).Up);
        Assert.Equal(new Vector3(0, 1, 0), new RootMotion(up: new Vector3(1e-7, 0, 0)).Up);
    }

    [Fact]
    public void ARootTurnAMicrometreLongIsStillATurn()
    {
        // 6e-7 and 8e-7 make a turn of exactly 1e-6 in a double, the shortest one that is kept.
        var lean = 0.7071067811865476;
        var clip = new ClipDocument(
            "Turn",
            1,
            WhenDone.Hold,
            30,
            channels:
            [
                new ClipChannel<Quaternion>(
                    string.Empty,
                    "rotation",
                    ChannelKind.Rotation,
                    [new Key<Quaternion>(0, Quaternion.Identity()), new Key<Quaternion>(1, new Quaternion(lean, 6e-7, lean, 8e-7))],
                    "hips"),
            ],
            rootMotion: new RootMotion(bone: "hips", turns: true));

        Assert.Equal(0.6, Math.Abs(clip.RootStepBetween(0, 1).Rotation.Y), 1e-9);
    }

    [Fact]
    public void FileTextBreaksAListInsideAListOverLines()
    {
        var text = FileText.Of(Tree.Map(("xs", Tree.List(new object?[] { Tree.List(Tree.Map(("a", 1))) }))));

        Assert.Equal("{\n  \"xs\": [\n    [\n      {\n        \"a\": 1\n      }\n    ]\n  ]\n}\n", text);
    }

    private static string Text(int version) =>
        JsonText.Encode(Tree.Map(("kind", "orblit.test"), ("formatVersion", version)));

    private sealed class Step(int from, string note) : IFormatStep
    {
        public int From => from;

        public int To => from + 1;

        public JsonMap Apply(JsonMap json, List<string> notes)
        {
            notes.Add(note);
            var seen = json.GetValueOrDefault("steps") as string;
            return Tree.With(json, ("steps", seen is null ? note : $"{seen},{note}"));
        }
    }
}
