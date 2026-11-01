using static Orblit.Motion.Tests.Rigs;

namespace Orblit.Motion.Tests;

public class BoneNamesTests
{
    [Fact]
    public void MatchesTheSameNameThenTheSameNameOnceCleaned()
    {
        var from = Chain(
            "mixamorig:Hips",
            "mixamorig:Spine",
            "mixamorig:LeftArm",
            "mixamorig:RightArm",
            "mixamorig:LeftHand",
            "Tail");
        var to = Chain("Hips", "spine", "arm.L", "arm.R", "hand_L", "Tail", "Wing.L");

        Same(
            new Dictionary<string, string>
            {
                ["Hips"] = "mixamorig:Hips",
                ["spine"] = "mixamorig:Spine",
                ["arm.L"] = "mixamorig:LeftArm",
                ["arm.R"] = "mixamorig:RightArm",
                ["hand_L"] = "mixamorig:LeftHand",
                ["Tail"] = "Tail",
            },
            BoneNames.Match(from, to));
    }

    [Fact]
    public void KeepsLeftAndRightApart()
    {
        var from = Chain("hand.L", "hand.R");
        var to = Chain("RightHand", "LeftHand");

        Same(
            new Dictionary<string, string> { ["RightHand"] = "hand.R", ["LeftHand"] = "hand.L" },
            BoneNames.Match(from, to));
    }

    [Fact]
    public void LeavesOutANameThatTwoBonesShareOnceCleaned()
    {
        var from = Chain("DEF-arm.L", "ORG-arm.L", "hips");
        var to = Chain("arm.L", "hips");

        Same(new Dictionary<string, string> { ["hips"] = "hips" }, BoneNames.Match(from, to));
    }

    private static void Same(Dictionary<string, string> expected, OrderedDictionary<string, string> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        foreach (var (name, source) in expected)
        {
            Assert.Equal(source, actual[name]);
        }
    }
}
