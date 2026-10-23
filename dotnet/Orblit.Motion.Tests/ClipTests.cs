using Orblit.Json;
using static Orblit.Motion.Tests.ClipSamples;

namespace Orblit.Motion.Tests;

public class ClipTests
{
    [Fact]
    public void ReadsBackAsTheClipItWasWrittenFrom()
    {
        var text = Populated().Encode();
        var loaded = ClipDocument.Decode(text);

        Assert.Empty(loaded.Problems);
        Assert.Equal(text, loaded.Clip.Encode());

        var clip = loaded.Clip;
        Assert.Equal("Lamp flicker", clip.Name);
        Assert.Equal(2, clip.Duration);
        Assert.Equal(WhenDone.Loop, clip.WhenDone);
        Assert.Equal(24, clip.Rate);
        Assert.Equal("hips", clip.RootMotion?.Bone);
        Assert.True(clip.RootMotion?.Turns);
        Assert.False(clip.RootMotion?.Rises);
        Assert.Equal(4, clip.Channels.Count);
        Assert.Equal(["spark", "buzz"], clip.Marks.Select(static mark => mark.Name));
        Tree.Same(Tree.Map(("volume", 0.5)), clip.Marks[^1].Payload);
    }

    [Fact]
    public void KeepsEachKeysHoldShapeAndSlopes()
    {
        var clip = ClipDocument.Decode(Populated().Encode()).Clip;

        var power = Assert.IsType<ClipChannel<double>>(clip.ChannelFor("bulb", "light.power"));
        Assert.Equal([Hold.Linear, Hold.Linear, Hold.Shaped, Hold.Linear], power.Keys.Select(static key => key.Hold));
        Assert.Equal(Easing.Bounce, power.Keys[2].Shape);

        var hips = Assert.IsType<ClipChannel<Quaternion>>(clip.ChannelFor(string.Empty, "rotation", "hips"));
        Assert.Equal([0, 0.5, 0, 0], Numbers(hips.Keys[0].SlopeOut!.Value.Value));
        Assert.Equal([0, 0.25, 0, -0.1], Numbers(hips.Keys[^1].SlopeIn!.Value.Value));
    }

    [Fact]
    public void WritesOneKeyToALine()
    {
        var keys = Populated().Encode().Split('\n')
            .Where(static line => line.TrimStart().StartsWith("{\"at\"", StringComparison.Ordinal))
            .ToList();

        // Four, two, two and two keys, and two marks.
        Assert.Equal(12, keys.Count);
        foreach (var line in keys)
        {
            Assert.IsAssignableFrom<JsonMap>(JsonText.Decode(line.Trim().TrimEnd(',')));
        }
    }

    [Fact]
    public void SaysAChannelsUsualHoldOnceAndOnlyTheOddKeyItsOwn()
    {
        var json = Populated().ToJson();
        var channels = Assert.IsAssignableFrom<JsonList>(json["channels"]);
        var power = Assert.IsAssignableFrom<JsonMap>(channels[0]);
        Assert.Equal("linear", power["hold"]);

        var holds = Assert.IsAssignableFrom<JsonList>(power["keys"])
            .Select(static key => Assert.IsAssignableFrom<JsonMap>(key).GetValueOrDefault("hold"));
        Assert.Equal([null, null, "shaped", null], holds);

        // Smooth is what a key does when nothing says otherwise.
        var position = Assert.IsAssignableFrom<JsonMap>(channels[1]);
        Assert.False(position.ContainsKey("hold"));
    }

    [Fact]
    public void SortsKeysWrittenOutOfOrderKeepingACutsOrder()
    {
        var clip = Load(("channels", Tree.List(Tree.Map(
            ("property", "light.power"),
            ("kind", "number"),
            ("keys", Tree.List(
                Tree.Map(("at", 1), ("value", 5)),
                Tree.Map(("at", 0), ("value", 1)),
                Tree.Map(("at", 1), ("value", 9)))))))).Clip;

        var channel = Only<double>(clip);
        Assert.Equal([1.0, 5.0, 9.0], channel.Keys.Select(static key => key.Value));

        // The later of two keys at one moment is what it says from then on.
        Assert.Equal(9.0, channel.ValueAt(1));
    }

    [Fact]
    public void DropsAKeyItCannotReadAndSaysSo()
    {
        var loaded = Load(("channels", Tree.List(Tree.Map(
            ("property", "transform.position"),
            ("kind", "vector"),
            ("keys", Tree.List(
                Tree.Map(("at", 0), ("value", Tree.List(0, 0, 0))),
                Tree.Map(("at", 1), ("value", Tree.List(1, 2))),
                Tree.Map(("value", Tree.List(1, 1, 1))),
                Tree.Map(("at", 2), ("value", Tree.List(2, 2, 2)))))))));

        Assert.Equal(2, Only<Vector3>(loaded.Clip).Keys.Count);
        Assert.Contains("2 keys", Assert.Single(loaded.Problems));
    }

    [Fact]
    public void DropsASecondChannelMovingTheSameThing()
    {
        var channel = Tree.Map(
            ("target", "bulb"),
            ("property", "light.power"),
            ("kind", "number"),
            ("keys", Tree.List(Tree.Map(("at", 0), ("value", 1)))));

        var loaded = Load(("channels", Tree.List(channel, channel)));

        Assert.Single(loaded.Clip.Channels);
        Assert.Contains("second was left out", Assert.Single(loaded.Problems));
    }

    [Fact]
    public void DropsAChannelOfAKindItDoesNotKnow()
    {
        var loaded = Load(("channels", Tree.List(Tree.Map(
            ("property", "light.colour"),
            ("kind", "colour"),
            ("keys", Tree.List(Tree.Map(("at", 0), ("value", "#ffffff"))))))));

        Assert.Empty(loaded.Clip.Channels);
        Assert.Contains("kind", Assert.Single(loaded.Problems));
    }

    [Fact]
    public void DropsAChannelMovingSomethingItsTargetDoesNotHave()
    {
        var loaded = Load(("channels", Tree.List(
            Tree.Map(
                ("bone", "hips"),
                ("property", "rotation"),
                ("kind", "vector"),
                ("keys", Tree.List(Tree.Map(("at", 0), ("value", Tree.List(0, 0, 0)))))),
            Tree.Map(
                ("property", "power"),
                ("kind", "number"),
                ("keys", Tree.List(Tree.Map(("at", 0), ("value", 1))))))));

        Assert.Empty(loaded.Clip.Channels);
        Assert.Equal(2, loaded.Problems.Count);
        Assert.Contains("not something a bone has", loaded.Problems[0]);
        Assert.Contains("names no component", loaded.Problems[^1]);
    }

    [Fact]
    public void DropsAMarkWithNoName()
    {
        var loaded = Load(("marks", Tree.List(
            Tree.Map(("at", 1)),
            Tree.Map(("at", 0.5), ("name", "step")))));

        Assert.Equal("step", Assert.Single(loaded.Clip.Marks).Name);
        Assert.Single(loaded.Problems);
    }

    [Fact]
    public void TakesTheLastKeyOrMarkAsTheLengthWhenNoneIsGiven()
    {
        var clip = Load(
            ("channels", Tree.List(Tree.Map(
                ("property", "light.power"),
                ("kind", "number"),
                ("keys", Tree.List(
                    Tree.Map(("at", 0), ("value", 1)),
                    Tree.Map(("at", 1.5), ("value", 2))))))),
            ("marks", Tree.List(Tree.Map(("at", 1.75), ("name", "end"))))).Clip;

        Assert.Equal(1.75, clip.Duration);
        Assert.Equal(30, clip.Rate);
        Assert.Equal(WhenDone.Hold, clip.WhenDone);
    }

    [Fact]
    public void KeepsAStatedLengthShorterThanTheKeys()
    {
        var clip = Load(
            ("duration", 1),
            ("channels", Tree.List(Tree.Map(
                ("property", "light.power"),
                ("kind", "number"),
                ("keys", Tree.List(
                    Tree.Map(("at", 0), ("value", 1)),
                    Tree.Map(("at", 2), ("value", 2)))))))).Clip;

        Assert.Equal(1, clip.Duration);
    }

    [Fact]
    public void RefusesAFileThatIsNotAClip()
    {
        foreach (var text in new[] { "{\"kind\": \"orblit.scene\"}", "not json", "[1, 2]" })
        {
            Assert.Throws<ClipFormatException>(() => ClipDocument.Decode(text));
        }
    }

    [Fact]
    public void RefusesAClipFromANewerOrblit()
    {
        var text = JsonText.Encode(Tree.Map(
            ("kind", ClipDocument.Marker),
            ("formatVersion", ClipDocument.FormatVersion + 1)));

        var error = Assert.Throws<ClipFormatException>(() => ClipDocument.Decode(text));

        Assert.Contains("newer", error.Message);
    }

    [Fact]
    public void RotationsAreMadeUnitLengthOnTheWayIn() =>
        Assert.Equal([0, 0, 0, 1], Numbers(ReadOne([0, 0, 0, 2])));

    [Fact]
    public void RotationsAlreadyUnitLengthAsNearAsAFloatGetsAreLeftAlone()
    {
        // One, as a float, but not as a double.
        double[] value = [0.18257418, 0.36514837, 0.5477226, 0.73029673];

        Assert.Equal(value, Numbers(ReadOne(value.Cast<object?>().ToArray())));
    }

    [Fact]
    public void RotationsOfNoLengthAreRefused()
    {
        var loaded = Rotations(
            null,
            Tree.Map(("at", 0), ("value", Tree.List(0, 0, 0, 0))),
            Tree.Map(("at", 1), ("value", Tree.List(0, 0, 0, 1))));

        Assert.Equal(1, Only<Quaternion>(loaded.Clip).Keys.Single().At);
        Assert.Single(loaded.Problems);
    }

    [Fact]
    public void RotationsKeepTheirSlopesAsWrittenOfAnyLength()
    {
        var keys = Only<Quaternion>(Rotations(
            "curve",
            Tree.Map(("at", 0), ("value", Tree.List(0, 0, 0, 1)), ("out", Tree.List(0, 0, 0, 0))),
            Tree.Map(("at", 1), ("value", Tree.List(0, 0, 0, 1)), ("in", Tree.List(0, 3, 0, 0)))).Clip).Keys;

        Assert.Equal([0, 0, 0, 0], Numbers(keys[0].SlopeOut!.Value.Value));
        Assert.Equal([0, 3, 0, 0], Numbers(keys[^1].SlopeIn!.Value.Value));
        Assert.Equal(Hold.Curve, keys[0].Hold);
    }

    [Fact]
    public void SamplingPutsAnEntitysValuesByTargetAndProperty()
    {
        var frame = Populated().SampleAt(0.25);

        Assert.Equal(0.25, frame.At);
        Assert.Equal(40, Assert.IsType<double>(frame.ValueOf("bulb", "light.power")), 1e-9);
        Assert.Equal(true, frame.ValueOf("bulb/glow", "mesh.visible"));
        Assert.True(Assert.IsType<Vector3>(frame.ValueOf(string.Empty, "transform.position")).Z > 0);
        Assert.Null(frame.ValueOf("bulb", "light.colour"));
    }

    [Fact]
    public void SamplingPutsABonesPartsOnTheBone()
    {
        var frame = Populated().SampleAt(1);

        var hips = Assert.IsType<BoneLocal>(frame.BoneOf(string.Empty, "hips"));
        Assert.Null(hips.Position);
        Assert.Null(hips.Scale);
        var expected = Quaternion.AxisAngle(new Vector3(0, 1, 0), 0.5);
        Assert.Equal(expected.W, hips.Rotation!.W, 1e-12);
        Assert.Equal(expected.Y, hips.Rotation.Y, 1e-12);
        Assert.False(frame.Values.ContainsKey("hips"));
    }

    [Fact]
    public void SamplingHoldsTheFirstAndLastKeysPastEitherEnd()
    {
        var clip = Populated();

        Assert.Equal(60.0, clip.SampleAt(-1).ValueOf("bulb", "light.power"));
        Assert.Equal(false, clip.SampleAt(5).ValueOf("bulb/glow", "mesh.visible"));
    }

    [Fact]
    public void AMarkFiresInTheStepThatReachesItAndOnlyThatOne()
    {
        var clip = Populated();

        Assert.Equal("spark", Assert.Single(clip.MarksBetween(0, 0.25)).Name);
        Assert.Equal("buzz", Assert.Single(clip.MarksBetween(0.25, 1)).Name);
        Assert.Empty(clip.MarksBetween(1, 2));
    }

    [Fact]
    public void AChannelNeedsAKey() =>
        Assert.Throws<ArgumentException>(
            static () => new ClipChannel<double>(string.Empty, "light.power", ChannelKind.Number, []));

    [Fact]
    public void RekeyedTakesKeysOfAnythingAndKeepsWhatTheyCarry()
    {
        var channel = Populated().Channels[1];
        var moved = Assert.IsType<ClipChannel<Vector3>>(channel.Rekeyed(
        [
            new Key<object>(0.5, new Vector3(4, 5, 6), Hold.Curve, slopeOut: new Slope<object>(new Vector3(1, 0, 0))),
            .. channel.BoxedKeys,
        ]));

        Assert.Equal([0, 0.5, 2], moved.Keys.Select(static key => key.At));
        Assert.Equal(Hold.Curve, moved.Keys[1].Hold);
        Assert.Equal(new Vector3(1, 0, 0), moved.Keys[1].SlopeOut!.Value.Value);
        Assert.True(moved.SameAddress(channel));
    }

    [Fact]
    public void AChannelReadFromAFileIsTypedAsWhatItCarries()
    {
        var read = ClipDocument.Decode(Populated().Encode()).Clip.Channels;

        Assert.IsType<ClipChannel<double>>(read[0]);
        Assert.IsType<ClipChannel<Vector3>>(read[1]);
        Assert.IsType<ClipChannel<bool>>(read[2]);
    }

    [Fact]
    public void RekeyedRefusesAValueTheChannelDoesNotCarry()
    {
        var channel = Populated().Channels[0];

        Assert.Throws<InvalidCastException>(() => channel.Rekeyed([new Key<object>(0, Vector3.Zero())]));
    }

    [Fact]
    public void OfKindMakesAChannelOfAKindChosenAtRunTime()
    {
        var kind = ChannelKind.Named("number")!;

        var channel = ClipChannel.OfKind(
            "bulb",
            "light.power",
            kind,
            [new Key<object>(1, 30.0), new Key<object>(0, 10.0)]);

        var typed = Assert.IsType<ClipChannel<double>>(channel);
        Assert.Equal([0, 1], typed.Keys.Select(static key => key.At));
        Assert.Equal(20, Assert.IsType<double>(channel.ValueAt(0.5)), 1e-9);
    }

    private static ClipLoad Rotations(string? hold, params object?[] keys)
    {
        var channel = new List<(string, object?)> { ("bone", "hips"), ("property", "rotation"), ("kind", "rotation") };
        if (hold is not null)
        {
            channel.Add(("hold", hold));
        }

        channel.Add(("keys", keys));
        return Load(("channels", Tree.List(Tree.Map([.. channel]))));
    }

    private static Quaternion ReadOne(object?[] value) =>
        Only<Quaternion>(Rotations(null, Tree.Map(("at", 0), ("value", value))).Clip).Keys.Single().Value;
}
