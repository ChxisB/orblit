using static Orblit.Motion.Tests.GltfSamples;

namespace Orblit.Motion.Tests;

public class GltfImportAddedTests
{
    [Fact]
    public void KeepsNineDigitsWhenAFloatNeedsThemToReadBackAsItself()
    {
        // Eight digits of this float32 read back as a different float32, so only nine will do.
        var built = new Built();
        var times = built.Floats([0, 0.1104801744222641], "SCALAR");
        var moves = built.Floats([0, 0, 0, 1, 0, 0], "VEC3");
        var bytes = built.File(Tree.Map(
            ("nodes", Tree.List(Tree.Map(("name", "Door")))),
            ("animations", Tree.List(Tree.Map(
                ("samplers", Tree.List(Sampler(times, moves))),
                ("channels", Tree.List(Channel(0, 0, "translation"))))))));
        var clip = Assert.Single(GltfImport.Clips(bytes).Clips);
        Assert.Contains("\"at\":0.110480174,", clip.Encode(), StringComparison.Ordinal);
    }

    [Fact]
    public void RefusesAJointThatIsNotANode()
    {
        foreach (var joint in new[] { -1, 1 })
        {
            var bytes = Plain(Tree.Map(
                ("nodes", Tree.List(Tree.Map(("name", "A")))),
                ("skins", Tree.List(Tree.Map(("joints", Tree.List(0, joint)))))));
            Assert.Throws<ClipFormatException>(() => GltfImport.Skeletons(bytes));
        }
    }
}
