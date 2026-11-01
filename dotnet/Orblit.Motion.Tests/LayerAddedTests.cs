using Orblit.Sequence;
using static Orblit.Motion.Tests.LayerSamples;

namespace Orblit.Motion.Tests;

/// <summary>What the shared fixture cannot say: which parameter a refusal names, and what a result shares.</summary>
public class LayerAddedTests
{
    private static ClipDocument Shot() => new("Shot", 1, marks: [new Mark(0.5, "mid")], channels: [MovingBone("arm", 8, 10)]);

    [Fact]
    public void RefusalsNameTheParameterAtFault()
    {
        Assert.Equal("weight", Assert.Throws<ArgumentException>(() => FrameLayers.Layer(Pose(), Pose(), Pose(), weight: 2)).ParamName);
        Assert.Equal("weight", Assert.Throws<ArgumentException>(() => FrameLayers.Add(Pose(), Pose(), Pose(), weight: -1)).ParamName);
        Assert.Equal("weights", Assert.Throws<ArgumentException>(() => new BoneMask(new Dictionary<string, double> { ["arm"] = 2 })).ParamName);
        Assert.Equal("bone", Assert.Throws<ArgumentException>(() => BoneMask.Below(Skeleton(), "nope")).ParamName);
        Assert.Equal("at", Assert.Throws<ArgumentException>(() => new LayerPlace(-1)).ParamName);
        Assert.Equal("influence", Assert.Throws<ArgumentException>(() => new ClipLayer(Shot(), influence: 2)).ParamName);
        Assert.Equal("fadeIn", Assert.Throws<ArgumentException>(() => new ClipLayer(Shot(), fadeIn: -1)).ParamName);
        Assert.Equal("fadeOut", Assert.Throws<ArgumentException>(() => new ClipLayer(Shot(), fadeOut: double.NaN)).ParamName);
    }

    [Fact]
    public void ALayerStartsWithShortFadesAndFullSayOverTheWholePose()
    {
        var layer = new ClipLayer(Shot());

        Assert.Equal(0.1, layer.FadeIn);
        Assert.Equal(0.1, layer.FadeOut);
        Assert.Equal(1, layer.Influence);
        Assert.False(layer.Additive);
        Assert.Null(layer.Mask);
    }

    [Fact]
    public void AStepKeepsItsMarksWhateverHappensToTheListItWasGiven()
    {
        var given = new List<Mark> { new(0.5, "mid") };
        var step = new LayerStep(new LayerPlace(0.5), given, finished: false);

        given.Clear();

        Assert.Equal(["mid"], step.Marks.Select(static mark => mark.Name));
        Assert.Throws<NotSupportedException>(() => ((IList<Mark>)step.Marks).Add(new Mark(0, "late")));
    }

    [Fact]
    public void AStepFromTheEndOfAShotPassesNoMarksAndIsFinished()
    {
        var step = new ClipLayer(Shot()).Advance(new LayerPlace(1), 0.5);

        Assert.Equal(1, step.Place.At);
        Assert.True(step.Finished);
        Assert.Empty(step.Marks);
    }

    [Fact]
    public void AMaskKeepsTheWeightsItWasMadeWith()
    {
        var weights = new Dictionary<string, double> { ["arm"] = 0.5 };
        var mask = new BoneMask(weights);

        weights["arm"] = 1;
        weights["leg"] = 1;

        Assert.Equal(0.5, mask.WeightOf(string.Empty, "arm"));
        Assert.Equal(0, mask.WeightOf(string.Empty, "leg"));
    }

    [Fact]
    public void LayeringLeavesBothInputsAsTheyWere()
    {
        var @base = Pose(position: 2);
        var overlay = Pose(position: 6);
        var before = (BoneX(@base, "arm"), BoneX(overlay, "arm"));

        var layered = FrameLayers.Layer(@base, overlay, Pose(), weight: 0.5);
        layered.BoneOf(string.Empty, "arm")!.Position!.X = 100;

        Assert.Equal(before, (BoneX(@base, "arm"), BoneX(overlay, "arm")));
    }

    [Fact]
    public void AnOverlayBoneTheBaseLacksIsCopiedNotShared()
    {
        var overlay = Pose(position: 6);

        var layered = FrameLayers.Layer(new ClipFrame(0), overlay, new ClipFrame(0));
        layered.BoneOf(string.Empty, "arm")!.Position!.X = 100;

        Assert.Equal(6, BoneX(overlay, "arm"));
    }

    [Fact]
    public void AddingLeavesBothInputsAsTheyWere()
    {
        var @base = Pose(position: 2);
        var overlay = Pose(position: 6);

        var added = FrameLayers.Add(@base, overlay, Pose(position: 1));
        added.BoneOf(string.Empty, "arm")!.Position!.X = 100;

        Assert.Equal(2, BoneX(@base, "arm"));
        Assert.Equal(6, BoneX(overlay, "arm"));
    }

    [Fact]
    public void AReferenceIsCopiedWhenTheLayerIsMade()
    {
        var reference = Pose(position: 4);
        var layer = new ClipLayer(new ClipDocument("Recoil", 1, channels: [MovingBone("arm", 10, 10)]), fadeIn: 0, additive: true, reference: reference);

        reference.BoneOf(string.Empty, "arm")!.Position!.X = 100;

        Assert.Equal(11, BoneX(layer.SampleAt(Pose(position: 5), new LayerPlace(0.5), Pose()), "arm"));
    }
}
