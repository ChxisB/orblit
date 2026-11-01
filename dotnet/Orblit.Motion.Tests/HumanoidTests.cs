using static Orblit.Motion.Tests.People;

namespace Orblit.Motion.Tests;

public class HumanoidTests
{
    [Fact]
    public void FindsEachPartOfAPersonWhateverTheBonesAreCalled()
    {
        foreach (var naming in new Naming[] { PlainName, Mannequin, Capture })
        {
            var who = Build(naming: naming);
            var map = HumanoidMap.Of(who.Skeleton);
            Same(who.Slots, map.Bones);
            Assert.Empty(map.Problems);
        }
    }

    [Fact]
    public void TellsLeftFromRightByWhereTheyStandNotByTheirOrder()
    {
        var who = Build(rightFirst: true);
        var map = HumanoidMap.Of(who.Skeleton);
        Same(who.Slots, map.Bones);
        var hand = who.IndexOf(map[HumanoidBone.LeftHand]!);
        Assert.True(who.World()[hand].GetTranslation().X > 0);
    }

    [Fact]
    public void BelievesANameThatSaysASideOverTheWayItIsFacing()
    {
        // Facing -Z, a person's left is at -X, and their names say so.
        var who = Build(naming: Mannequin);
        var names = who.Names.Select(static name => name switch
        {
            _ when name.EndsWith("_l", StringComparison.Ordinal) => $"{name[..^2]}_r",
            _ when name.EndsWith("_r", StringComparison.Ordinal) => $"{name[..^2]}_l",
            _ => name,
        }).ToList();
        var turned = new RestSkeleton(
            names,
            who.Parents,
            [.. who.Locals.Select(static local => Matrix4.Compose(local.Position, local.Turn, Vector3.All(1)))]);

        var map = HumanoidMap.Of(turned);

        Assert.Equal("hand_l", map[HumanoidBone.LeftHand]);
        Assert.Equal("hand_r", map[HumanoidBone.RightHand]);
    }

    [Fact]
    public void GoesByWhichWayThePersonFacesWhenNoNameSays()
    {
        var who = Build();
        var map = HumanoidMap.Of(who.Skeleton, forward: new Vector3(0, 0, -1));
        var left = who.Slots[HumanoidBone.LeftHand];
        Assert.Equal(left, map[HumanoidBone.RightHand]);
    }

    [Fact]
    public void PutsASkeletonWithOneTwoOrThreeSpineBonesInOrder()
    {
        foreach (var count in new[] { 1, 2, 3 })
        {
            var who = Build(spine: count);
            Same(who.Slots, HumanoidMap.Of(who.Skeleton).Bones);
        }
    }

    [Fact]
    public void HasNoShoulderWhereTheArmHangsFromTheSpine()
    {
        var who = Build(shoulders: false);
        var map = HumanoidMap.Of(who.Skeleton);
        Same(who.Slots, map.Bones);
        Assert.Null(map[HumanoidBone.LeftShoulder]);
    }

    [Fact]
    public void LeavesOffAnEndSiteThatOnlyMarksWhereABoneStops()
    {
        var who = Build(naming: Capture, endSites: true);
        Same(who.Slots, HumanoidMap.Of(who.Skeleton).Bones);
    }

    [Fact]
    public void SaysNothingForASkeletonThatIsNotAPerson()
    {
        // Four legs under a body, and a head: no spine with arms at its top.
        var locals = new List<Matrix4> { At(0, 1, 0), At(0, 0.2, 0.5), At(0, 0.2, 0.2) };
        foreach (var x in new[] { -0.2, 0.2 })
        {
            foreach (var z in new[] { -0.4, 0.4 })
            {
                locals.Add(At(x, -0.1, z));
                locals.Add(At(0, -0.9, 0));
            }
        }

        var animal = new RestSkeleton(
            ["body", "neck", "head", "a1", "a2", "b1", "b2", "c1", "c2", "d1", "d2"],
            [-1, 0, 1, 0, 3, 0, 5, 0, 7, 0, 9],
            locals);

        var map = HumanoidMap.Of(animal);

        Assert.Empty(map.Bones);
        Assert.NotEmpty(map.Problems);
    }

    [Fact]
    public void SaysWhyWhenItHasNoHeightToGoBy()
    {
        var flat = new RestSkeleton(["a", "b"], [-1, 0], [Matrix4.Identity(), Matrix4.Identity()]);

        Assert.Contains("no height", Assert.Single(HumanoidMap.Of(flat).Problems));
    }

    [Fact]
    public void PairsTwoPeopleByThePartEachBoneIs()
    {
        var from = Build(naming: Capture, rightFirst: true);
        var to = Build(naming: Mannequin, height: 1.2);

        var pairs = HumanoidMap.Match(from.Skeleton, to.Skeleton);

        Assert.Equal(to.Slots.Count, pairs.Count);
        foreach (var (slot, bone) in to.Slots)
        {
            Assert.Equal(from.Slots[slot], pairs[bone]);
        }
    }

    [Fact]
    public void PairsOnlyTheSlotsBothPeopleHave()
    {
        var from = Build(spine: 3, naming: Capture);
        var to = Build(spine: 1, shoulders: false, naming: Mannequin);

        var pairs = HumanoidMap.Match(from.Skeleton, to.Skeleton);

        Assert.Equal("mixamorig:Spine", pairs["spine_01"]);
        Assert.DoesNotContain("mixamorig:Spine1", pairs.Values);
        Assert.False(pairs.ContainsKey("clavicle_l"));
    }

    [Fact]
    public void AFoxIsNotReadAsAPerson()
    {
        var skeleton = GltfImport.Skeletons(GltfSamples.Sample("Fox"))[0];
        var map = HumanoidMap.Of(skeleton);
        Assert.Empty(map.Bones);
        Assert.NotEmpty(map.Problems);
    }

    private static Matrix4 At(double x, double y, double z) => Matrix4.TranslationValues(x, y, z);

    private static void Same(Dictionary<HumanoidBone, string> expected, IReadOnlyDictionary<HumanoidBone, string> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        foreach (var (slot, bone) in expected)
        {
            Assert.Equal(bone, actual[slot]);
        }
    }
}
