using static Orblit.Motion.Tests.GltfSamples;

namespace Orblit.Motion.Tests;

public class GltfPointerTests
{
    /// <summary>A channel the pointer extension aims, which names no node of its own.</summary>
    private static OrderedDictionary<string, object?> Pointing(int sampler, string pointer) =>
        Tree.Map(
            ("sampler", sampler),
            ("target", Tree.Map(
                ("path", "pointer"),
                ("extensions", Tree.Map(("KHR_animation_pointer", Tree.Map(("pointer", pointer))))))));

    /// <summary>A door a scene gave an id, a joint called hips, and one animation holding the given channels.</summary>
    private static ClipsImported Reading(Built built, object?[] samplers, object?[] channels) =>
        GltfImport.Clips(built.File(Tree.Map(
            ("nodes", Tree.List(
                Tree.Map(("name", "Door"), ("extras", Tree.Map(("orblit", Tree.Map(("id", "house/door")))))),
                Tree.Map(("name", "hips")))),
            ("skins", Tree.List(Tree.Map(("joints", Tree.List(1))))),
            ("animations", Tree.List(Tree.Map(
                ("name", "Open"),
                ("samplers", Tree.List(samplers)),
                ("channels", Tree.List(channels))))))));

    private static (Built Built, object?[] Samplers) Slide()
    {
        var built = new Built();
        var times = built.Floats([0, 0.5], "SCALAR");
        var slide = built.Floats([0, 0, 0, 1, 0, 0], "VEC3");
        return (built, [Sampler(times, slide)]);
    }

    // a pointer into a node's own transform

    [Fact]
    public void ATranslationPointerMovesTheNodeAsThePlainFormDoes()
    {
        var (built, samplers) = Slide();

        var pointed = Reading(built, samplers, [Pointing(0, "/nodes/0/translation")]);
        var plain = Reading(built, samplers, [Channel(0, 0, "translation")]);

        var door = Tree.Some(Assert.Single(pointed.Clips).ChannelFor("house/door", "transform.position"));
        Assert.Equal(new Vector3(0.5, 0, 0), door.ValueAt(0.25));
        Assert.Equal(plain.Clips[0].Encode(), pointed.Clips[0].Encode());
        Assert.Empty(pointed.Problems);
    }

    [Fact]
    public void AScalePointerMovesTheScale()
    {
        var (built, samplers) = Slide();

        var read = Reading(built, samplers, [Pointing(0, "/nodes/0/scale")]);

        Assert.NotNull(Assert.Single(read.Clips).ChannelFor("house/door", "transform.scale"));
    }

    [Fact]
    public void ARotationPointerAtAJointMovesTheBoneByItsName()
    {
        var built = new Built();
        var times = built.Floats([0, 0.5], "SCALAR");
        var turn = built.Floats([0, 0, 0, 1, 0, 1, 0, 0], "VEC4");

        var read = Reading(built, [Sampler(times, turn)], [Pointing(0, "/nodes/1/rotation")]);

        var hips = Tree.Some(Assert.Single(read.Clips).ChannelFor(string.Empty, "rotation", "hips"));
        Assert.Equal(ChannelKind.Rotation, hips.Kind);
    }

    [Fact]
    public void ThePlainFormAndThePointerFormAreOneChannelWhenTheyMeetOnANode()
    {
        var (built, samplers) = Slide();

        var read = Reading(built, samplers, [Channel(0, 0, "translation"), Pointing(0, "/nodes/0/translation")]);

        Assert.Single(Assert.Single(read.Clips).Channels);
        var problem = Assert.Single(read.Problems);
        Assert.Contains("Two channels of \"Open\" move translation", problem);
    }

    // a pointer at anything else

    [Fact]
    public void APointerAtAMaterialIsNamedAndNothingIsMoved()
    {
        var (built, samplers) = Slide();

        var read = Reading(built, samplers, [Pointing(0, "/materials/0/pbrMetallicRoughness/baseColorFactor")]);

        Assert.Empty(Assert.Single(read.Clips).Channels);
        var problem = Assert.Single(read.Problems);
        Assert.Contains("\"/materials/0/pbrMetallicRoughness/baseColorFactor\", which is not read", problem);
        Assert.DoesNotContain("not a node", problem);
    }

    [Fact]
    public void APointerAtANodesVisibilityIsNamedForTheSameReason()
    {
        var (built, samplers) = Slide();

        var read = Reading(built, samplers, [Pointing(0, "/nodes/0/extensions/KHR_node_visibility/visible")]);

        Assert.Contains("/nodes/0/extensions/KHR_node_visibility/visible", Assert.Single(read.Problems));
    }

    [Fact]
    public void APointerAtMorphWeightsSaysSoLikeThePlainFormDoes()
    {
        var (built, samplers) = Slide();

        var read = Reading(built, samplers, [Pointing(0, "/nodes/0/weights")]);

        Assert.Contains("Morph target weights in \"Open\"", Assert.Single(read.Problems));
    }

    [Fact]
    public void APointerAtANodeTheDocumentDoesNotHaveIsNotANode()
    {
        var (built, samplers) = Slide();

        var read = Reading(built, samplers, [Pointing(0, "/nodes/9/translation"), Pointing(0, "/nodes/99999999999999999999/scale")]);

        Assert.Empty(Assert.Single(read.Clips).Channels);
        Assert.All(read.Problems, problem => Assert.Contains("not a node", problem));
        Assert.Equal(2, read.Problems.Count);
    }

    [Fact]
    public void AChannelThatSaysPointerWithoutTextToPointWithIsStillOneWithNoNode()
    {
        var (built, samplers) = Slide();
        var odd = Tree.Map(
            ("sampler", 0),
            ("target", Tree.Map(
                ("path", "pointer"),
                ("extensions", Tree.Map(("KHR_animation_pointer", Tree.Map(("pointer", 7))))))));

        var read = Reading(built, samplers, [odd]);

        Assert.Contains("not a node", Assert.Single(read.Problems));
    }
}
