using Orblit.Json;
using static Orblit.Motion.Tests.LayerSamples;

namespace Orblit.Motion.Tests;

public class LayerTests
{
    [Fact]
    public void AdditiveEntityValuesUseTheirReferenceAndPreserveFlags()
    {
        var rest = Entity(2.0, new Vector3(1, 0, 0), false);
        var @base = Entity(5.0, new Vector3(4, 0, 0), false);
        var overlay = Entity(6.0, new Vector3(3, 0, 0), true);

        var added = FrameLayers.Add(@base, overlay, rest, weight: 0.5);

        Assert.Equal(7.0, added.ValueOf(string.Empty, "pose.value"));
        Assert.Equal(new Vector3(5, 0, 0), added.ValueOf(string.Empty, "pose.point"));
        Assert.Equal(false, added.ValueOf(string.Empty, "pose.flag"));
        ((Vector3)added.ValueOf(string.Empty, "pose.point")!).X = 20;
        Assert.Equal(new Vector3(4, 0, 0), @base.ValueOf(string.Empty, "pose.point"));
        var masked = FrameLayers.Add(@base, overlay, rest, mask: new BoneMask(new Dictionary<string, double> { ["arm"] = 1 }));
        Assert.Equal(5.0, masked.ValueOf(string.Empty, "pose.value"));
    }

    [Fact]
    public void AZeroReferenceScaleAddsItsOffsetWithoutDivision()
    {
        var added = FrameLayers.Add(Pose(scale: 3), Pose(scale: 2), Pose(scale: 0), weight: 0.5);

        Assert.Equal(Vector3.All(4), added.BoneOf(string.Empty, "arm")!.Scale);
    }

    [Fact]
    public void ARunningCharacterAttacksWithItsUpperBodyAndKeepsItsLegs()
    {
        var rest = FrameLayers.RestOf(new Dictionary<string, RestSkeleton> { [string.Empty] = Skeleton() });
        var run = new ClipDocument("Run", 1, WhenDone.Loop, channels: [MovingBone("leg", 0, 4), MovingBone("arm", 0, 1)]);
        var attack = new ClipDocument(
            "Attack",
            1,
            channels: [MovingBone("arm", 8, 10), MovingBone("leg", 100, 200)],
            marks: [new Mark(0.5, "fire")]);
        var graph = new BlendDocument("Run", [new BlendState("run", new BlendClip("run"))]);
        var player = new BlendPlayer(graph, new Dictionary<string, ClipDocument> { ["run"] = run }, rest: rest);
        var layer = new ClipLayer(attack, BoneMask.Below(Skeleton(), "spine"), fadeIn: 0.2, fadeOut: 0.2);
        var place = new LayerPlace();
        var legPositions = new List<double>();
        var marks = new List<string>();

        for (var i = 0; i < 60; i++)
        {
            var @base = player.Advance(1.0 / 60).Frame;
            var step = layer.Advance(place, 1.0 / 60);
            place = step.Place;
            marks.AddRange(step.Marks.Select(static mark => mark.Name));
            var result = layer.SampleAt(@base, place, rest);
            legPositions.Add(BoneX(result, "leg"));
            Assert.Equal(BoneX(@base, "leg"), BoneX(result, "leg"));
            if (i == 29)
            {
                Assert.Equal(9, BoneX(result, "arm"), 1e-9);
            }

            if (i == 59)
            {
                Assert.Equal(BoneX(@base, "arm"), BoneX(result, "arm"));
            }
        }

        Assert.Equal(60, legPositions.Distinct().Count());
        Assert.Equal(["fire"], marks);
    }

    [Fact]
    public void SubtreeMasksIncludeDescendantsAndLeaveOtherTargetsAlone()
    {
        var mask = BoneMask.Below(Skeleton(), "spine", weight: 0.5);

        Assert.Equal([("spine", 0.5), ("arm", 0.5)], mask.Weights.Select(static one => (one.Key, one.Value)));
        Assert.Equal(0, mask.WeightOf("other", "arm"));
        Assert.Equal(0, mask.WeightOf(string.Empty, "leg"));
        Assert.Throws<ArgumentException>(() => BoneMask.Below(Skeleton(), "missing"));
        Assert.Throws<ArgumentException>(() => new BoneMask(new Dictionary<string, double> { ["arm"] = double.NaN }));
        Assert.Throws<NotSupportedException>(() => ((IDictionary<string, double>)mask.Weights)["arm"] = 0);
    }

    [Fact]
    public void PartialOverlaysPreserveEveryUnauthoredBaseChannel()
    {
        var @base = Pose(position: 2, rotation: 0.4, scale: 3);
        var overlay = new ClipFrame(0);
        overlay.Bones[string.Empty] = new OrderedDictionary<string, BoneLocal> { ["arm"] = new BoneLocal(new Vector3(10, 0, 0)) };

        var layered = FrameLayers.Layer(@base, overlay, Pose(), weight: 0.5);

        Assert.Equal(6, BoneX(layered, "arm"));
        Assert.Equal(0.4, Radians(layered.BoneOf(string.Empty, "arm")!.Rotation!), 1e-9);
        Assert.Equal(Vector3.All(3), layered.BoneOf(string.Empty, "arm")!.Scale);
        Assert.Equal(2, BoneX(@base, "arm"));
    }

    [Fact]
    public void AdditiveTranslationRotationAndScaleAreRelativeToRest()
    {
        var added = FrameLayers.Add(
            Pose(position: 5, rotation: 0.4, scale: 3),
            Pose(position: 6, rotation: 1.2, scale: 4),
            Pose(position: 2, rotation: 0.2, scale: 2),
            weight: 0.5);

        Assert.Equal(7, BoneX(added, "arm"));
        Assert.Equal(0.9, Radians(added.BoneOf(string.Empty, "arm")!.Rotation!), 1e-9);
        Assert.Equal(Vector3.All(4.5), added.BoneOf(string.Empty, "arm")!.Scale);
    }

    [Fact]
    public void AdditiveRotationsComposeInLocalOrderForNoncommutingAxes()
    {
        var rest = Pose();
        rest.BoneOf(string.Empty, "arm")!.Rotation = Quaternion.AxisAngle(new Vector3(1, 0, 0), 0.5);
        var delta = Quaternion.AxisAngle(new Vector3(0, 1, 0), 0.8);
        var overlay = Pose();
        overlay.BoneOf(string.Empty, "arm")!.Rotation = rest.BoneOf(string.Empty, "arm")!.Rotation! * delta;
        var @base = Pose(rotation: 0.4);

        var added = FrameLayers.Add(@base, overlay, rest);

        var expected = @base.BoneOf(string.Empty, "arm")!.Rotation! * delta;
        var along = new Vector3(1, 0, 0);
        var actualAlong = added.BoneOf(string.Empty, "arm")!.Rotation!.AsRotationMatrix().Transformed(along);
        Assert.True(actualAlong.DistanceTo(expected.AsRotationMatrix().Transformed(along)) < 1e-9);
    }

    [Fact]
    public void AnAdditiveReferenceCanDifferFromTheRestPose()
    {
        var added = FrameLayers.Add(Pose(position: 5), Pose(position: 6), Pose(position: 1), reference: Pose(position: 4));

        Assert.Equal(7, BoneX(added, "arm"));
    }

    [Fact]
    public void ARemovedChannelReturnsToRestDuringAGraphFade()
    {
        var graph = new BlendDocument("Arm", [new BlendState("up", new BlendClip("up")), new BlendState("empty", new BlendClip("empty"))]);
        var player = new BlendPlayer(
            graph,
            new Dictionary<string, ClipDocument>
            {
                ["up"] = new ClipDocument("Up", 1, channels: [MovingBone("arm", 10, 10)]),
                ["empty"] = new ClipDocument("Empty", 1, channels: []),
            },
            rest: Pose(position: 2));

        player.Enter("empty", fade: 1, shape: Easing.Linear);

        Assert.Equal(6, BoneX(player.Advance(0.5).Frame, "arm"));
        Assert.Equal(2, BoneX(player.Advance(0.5).Frame, "arm"));
    }

    [Fact]
    public void OneShotEnvelopeFadesInAndOutAndReleasesAtItsEnd()
    {
        var layer = new ClipLayer(new ClipDocument("Shot", 1, channels: []), fadeIn: 0.2, fadeOut: 0.4);

        Assert.Equal(0, layer.WeightAt(new LayerPlace()));
        Assert.Equal(0.5, layer.WeightAt(new LayerPlace(0.1)), 1e-9);
        Assert.Equal(1, layer.WeightAt(new LayerPlace(0.4)));
        Assert.Equal(0.5, layer.WeightAt(new LayerPlace(0.8)), 1e-9);
        Assert.Equal(0, layer.WeightAt(new LayerPlace(1)));
        Assert.True(layer.Advance(new LayerPlace(), 5).Finished);
        Assert.Empty(layer.Advance(new LayerPlace(1), 1).Marks);
    }

    [Fact]
    public void LayerClockSurvivesJsonAndSamplesTheSameAfterRestoring()
    {
        var place = new LayerPlace(0.4);

        var restored = LayerPlace.FromJson(JsonText.Decode(JsonText.Encode(place.ToJson())))!;

        Assert.Equal(place.At, restored.At);
        Assert.Null(LayerPlace.FromJson(JsonText.Decode("""{"at": -1}""")));
        Assert.Throws<ArgumentException>(() => new LayerPlace(double.PositiveInfinity));
    }

    [Fact]
    public void ZeroWeightAndCompletedAdditiveShotsReturnTheBasePose()
    {
        var clip = new ClipDocument("Recoil", 1, channels: [MovingBone("arm", 8, 10)]);
        var layer = new ClipLayer(clip, additive: true);

        Assert.Equal(4, BoneX(layer.SampleAt(Pose(position: 4), new LayerPlace(1), Pose()), "arm"));
        Assert.Equal(4, BoneX(FrameLayers.Add(Pose(position: 4), Pose(position: 10), Pose(), weight: 0), "arm"));
        Assert.Throws<ArgumentException>(() => new ClipLayer(clip, fadeIn: -1));
        Assert.Throws<ArgumentException>(() => FrameLayers.Layer(Pose(), Pose(), Pose(), weight: 2));
    }

    private static ClipFrame Entity(double value, Vector3 point, bool flag)
    {
        var frame = new ClipFrame(0);
        frame.Values[string.Empty] = new OrderedDictionary<string, object>
        {
            ["pose.value"] = value,
            ["pose.point"] = point,
            ["pose.flag"] = flag,
        };
        return frame;
    }
}
