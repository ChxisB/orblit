namespace Orblit.Motion.Tests;

/// <summary>What the shared fixture cannot say: what a retarget leaves alone, and what a bone map keeps of what it was given.</summary>
public class RetargetAddedTests
{
    [Fact]
    public void ARetargetLeavesTheClipItWasGivenAsItWas()
    {
        var clip = Rigs.Walk();
        var before = clip.Encode();

        ClipRetarget.Of(clip, Rigs.Source().Skeleton, Rigs.Target().Skeleton);

        Assert.Equal(before, clip.Encode());
    }

    [Fact]
    public void NoPairsAndNoUnpairedBonesAreTheSameAsEmptyOnes()
    {
        var from = Rigs.Source().Skeleton;
        var to = Rigs.Target().Skeleton;

        var plain = ClipRetarget.Of(Rigs.Walk(), from, to);
        var empty = ClipRetarget.Of(Rigs.Walk(), from, to, new Dictionary<string, string>(), new HashSet<string>());

        Assert.Equal(plain.Clip.Encode(), empty.Clip.Encode());
        Assert.Equal(plain.Problems, empty.Problems);
    }

    [Fact]
    public void ABoneMapKeepsWhatItWasMadeWithWhateverHappensToTheCollectionsItWasGiven()
    {
        var bones = new Dictionary<string, string> { ["a"] = "b" };
        var unpaired = new HashSet<string> { "c" };
        var map = new BoneMap(bones: bones, unpaired: unpaired);

        bones["d"] = "e";
        unpaired.Add("f");

        Assert.Equal(["a"], map.Bones.Keys);
        Assert.Equal(["c"], map.Unpaired);
    }

    [Fact]
    public void ABoneMapKeepsPairsInTheOrderTheyWereWrittenAndWritesThemSorted()
    {
        var map = BoneMap.Decode("""{"kind":"orblit.bonemap","formatVersion":1,"bones":{"zed":"1","arm":"2"}}""").Map;

        Assert.Equal(["zed", "arm"], map.Bones.Keys);
        Assert.True(map.Encode().IndexOf("\"arm\"", StringComparison.Ordinal) < map.Encode().IndexOf("\"zed\"", StringComparison.Ordinal));
    }

    [Fact]
    public void ABoneMapFileEndsWithOneNewline()
    {
        var text = new BoneMap().Encode();

        Assert.EndsWith("}\n", text, StringComparison.Ordinal);
        Assert.DoesNotContain("\n\n", text, StringComparison.Ordinal);
    }
}
