using System.Text;
using static Orblit.Motion.Tests.ClipSamples;
using static Orblit.Motion.Tests.GltfSamples;

namespace Orblit.Motion.Tests;

public class GltfImportTests
{
    private static readonly ClipsImported Imported = GltfImport.Clips(Armature().Bytes);

    private static ClipDocument Walk => Imported.Clips[0];

    private static void ExpectRoundTrip(ClipDocument clip)
    {
        var again = ClipDocument.Decode(clip.Encode());
        Assert.Empty(again.Problems);
        Assert.Equal(clip.Encode(), again.Clip.Encode());
    }

    [Fact]
    public void GivesAClipForEachAnimationInOrderNamed() =>
        Assert.Equal(["Walk Loop", "cycle run", "Animation 3"], Imported.Clips.Select(static clip => clip.Name));

    [Fact]
    public void MovesAJointAsABoneByTheSkeletonsNameForIt()
    {
        var hips = Tree.Some(Walk.ChannelFor(string.Empty, "rotation", "hips"));
        Assert.Equal(ChannelKind.Rotation, hips.Kind);
        var unnamed = Tree.Some(Walk.ChannelFor(string.Empty, "position", "<unknown>"));
        Assert.Equal(ChannelKind.Vector, unnamed.Kind);
    }

    [Fact]
    public void MovesAnyOtherNodeAsAnEntityByTheIdASceneGivesIt()
    {
        var door = Tree.Some(Walk.ChannelFor("house/door", "transform.position"));
        Assert.Null(door.Bone);
        Assert.Equal(new Vector3(0.5, 0, 0), door.ValueAt(0.25));
        Assert.NotNull(Walk.ChannelFor("node-0", "transform.scale"));
    }

    [Fact]
    public void KeepsEachKeyWhereTheFileHasItEasedAsTheFileHasIt()
    {
        var hips = Assert.IsType<ClipChannel<Quaternion>>(Walk.ChannelFor(string.Empty, "rotation", "hips"));
        Assert.Equal([Hold.Curve, Hold.Curve], hips.Keys.Select(static key => key.Hold));
        Assert.Equal(new Quaternion(0, 0.5, 0, 0), hips.Keys[0].SlopeOut!.Value.Value);
        Assert.Equal(new Quaternion(0, 0.25, 0, 0), hips.Keys[1].SlopeIn!.Value.Value);

        // A value is made a rotation that does not scale; a slope is not.
        Assert.Equal(new Quaternion(0, 1, 0, 0), hips.Keys[1].Value);

        var unnamed = Tree.Some(Walk.ChannelFor(string.Empty, "position", "<unknown>"));
        Assert.Equal(Hold.Step, unnamed.BoxedKeys[0].Hold);
        Assert.Equal(Vector3.Zero(), unnamed.ValueAt(0.2));
        var door = Tree.Some(Walk.ChannelFor("house/door", "transform.position"));
        Assert.Equal(Hold.Linear, door.BoxedKeys[0].Hold);
    }

    [Fact]
    public void SaysWhatItCouldNotCarryAndCarriesTheRest()
    {
        Assert.Equal(4, Walk.Channels.Count);
        bool Noted(string words) => Imported.Problems.Any(problem => problem.Contains(words, StringComparison.Ordinal));
        Assert.True(Noted("Morph target weights in \"Walk Loop\""));
        Assert.True(Noted("Two channels of \"Walk Loop\""));
        Assert.True(Noted("not a node"));
        Assert.True(Noted("has no sampler"));
        Assert.True(Noted("moves \"pointer\""));
        Assert.True(Noted("fewer values than times"));
        Assert.Single(Imported.Clips[2].Channels);
    }

    [Fact]
    public void GuessesTheRateEveryKeySitsOnAndThirtyForNone()
    {
        Assert.Equal(24, Walk.Rate);
        Assert.Equal(24, Imported.Clips[1].Rate);
        Assert.Equal(30, Imported.Clips[2].Rate);
    }

    [Fact]
    public void LoopsWhatIsNamedAsALoopOrACycle()
    {
        Assert.Equal(WhenDone.Loop, Walk.WhenDone);
        Assert.Equal(WhenDone.Loop, Imported.Clips[1].WhenDone);
        Assert.Equal(WhenDone.Hold, Imported.Clips[2].WhenDone);
    }

    [Fact]
    public void LastsAsLongAsItsLastKey()
    {
        Assert.Equal(0.5, Walk.Duration);
        Assert.Equal(2.0 / 24, Imported.Clips[1].Duration, 1e-7);
    }

    [Fact]
    public void WritesAFloatAsShortAsStillReadsBackAsIt()
    {
        var third = Imported.Clips[2];
        var door = Tree.Some(third.ChannelFor("house/door", "transform.position"));
        Assert.Equal(0.1, door.BoxedKeys[^1].At);
        Assert.Contains("\"at\":0.1,", third.Encode(), StringComparison.Ordinal);
    }

    [Fact]
    public void ReadsTheDataFromBesideTheFileToo()
    {
        var apart = Armature("walk%20cycle.bin");
        var beside = GltfImport.Clips(apart.Bytes, new Dictionary<string, byte[]> { ["walk cycle.bin"] = apart.Data });
        Assert.Equal(Walk.Encode(), beside.Clips[0].Encode());
    }

    [Fact]
    public void GoesToAClipFileAndComesBackTheSame()
    {
        foreach (var clip in Imported.Clips)
        {
            ExpectRoundTrip(clip);
        }
    }

    [Fact]
    public void RefusesWhatIsNotGltfAtAll() =>
        Assert.Throws<ClipFormatException>(() => GltfImport.Clips(Encoding.UTF8.GetBytes("not a model")));

    [Fact]
    public void TheFoxSurveysWalksAndRunsOnItsBones()
    {
        var fox = Sample("Fox");
        var imported = GltfImport.Clips(fox);
        Assert.Empty(imported.Problems);
        Assert.Equal(["Survey", "Walk", "Run"], imported.Clips.Select(static clip => clip.Name));
        foreach (var clip in imported.Clips)
        {
            Assert.NotEmpty(clip.Channels);
            foreach (var channel in clip.Channels)
            {
                Assert.Equal(string.Empty, channel.Target);
                Assert.StartsWith("b_", channel.Bone, StringComparison.Ordinal);
            }

            ExpectRoundTrip(clip);
        }

        var walk = imported.Clips[1];
        Assert.Equal(24, walk.Rate);
        Assert.Equal(17.0 / 24, walk.Duration, 1e-6);
        Assert.True(walk.SampleAt(0.3, false).Bones[string.Empty].Count > 10);
    }

    [Fact]
    public void AnUnnamedAnimationIsNamedForWhereItIs()
    {
        var cesium = Sample("CesiumMan");
        var man = Assert.Single(GltfImport.Clips(cesium).Clips);
        Assert.Equal("Animation 1", man.Name);
        Assert.Equal(2, man.Duration);
        ExpectRoundTrip(man);
    }

    [Fact]
    public void ASkinMovesTheBonesItMovesAndNoOthers()
    {
        var rigged = Sample("RiggedSimple");
        var simple = Assert.Single(GltfImport.Clips(rigged).Clips);
        Assert.Equal(new HashSet<string?> { "Bone.001" }, simple.Channels.Select(static channel => channel.Bone).ToHashSet());
        ExpectRoundTrip(simple);
    }

    [Fact]
    public void ReadsASkinAsBonesParentsFirst()
    {
        var bytes = Plain(Tree.Map(
            ("nodes", Tree.List(
                Tree.Map(
                    ("name", "Armature"),
                    ("rotation", Tree.List(-0.7071068, 0, 0, 0.7071068)),
                    ("scale", Tree.List(0.01, 0.01, 0.01)),
                    ("children", Tree.List(1))),
                Tree.Map(("name", "Hips"), ("translation", Tree.List(0, 100, 0)), ("children", Tree.List(2))),
                Tree.Map(("name", "Spine"), ("matrix", Tree.List(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 20, 0, 1))))),

            // The child comes first, which a file is free to do.
            ("skins", Tree.List(Tree.Map(("joints", Tree.List(2, 1)))))));
        var skeleton = Assert.Single(GltfImport.Skeletons(bytes));
        Assert.Equal(["Hips", "Spine"], skeleton.Names);
        Assert.Equal([-1, 0], skeleton.Parents);
        Assert.Equal(100, skeleton.PositionOf(0).Y, 1e-9);
        Assert.Equal(20, skeleton.PositionOf(1).Y, 1e-9);
        Assert.Equal(1.2, skeleton.Reach, 1e-9);
        var up = skeleton.ParentWorldRotationOf(0).AsRotationMatrix();
        Assert.Equal(-1, up.Transformed(new Vector3(0, 1, 0)).Z, 1e-6);
    }

    [Fact]
    public void NamesSkeletonBonesAsTheClipsDo()
    {
        var bytes = Plain(Tree.Map(
            ("nodes", Tree.List(Tree.Map(("name", "A"), ("children", Tree.List(1))), Tree.Map(("name", "A")))),
            ("skins", Tree.List(Tree.Map(("joints", Tree.List(0, 1)))))));
        Assert.Equal(["A", "A.001"], Assert.Single(GltfImport.Skeletons(bytes)).Names);
    }

    [Fact]
    public void HasNothingToSayAboutAFileWithNoSkin() =>
        Assert.Empty(GltfImport.Skeletons(Plain(Tree.Map(("nodes", Tree.List(Tree.Map()))))));

    [Fact]
    public void RefusesBytesThatAreNotGltf() =>
        Assert.Throws<ClipFormatException>(() => GltfImport.Skeletons([1, 2, 3]));
}
