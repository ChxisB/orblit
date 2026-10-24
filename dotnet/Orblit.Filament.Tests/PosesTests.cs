using Orblit.VectorMath;

namespace Orblit.Filament.Tests;

/// <summary>Ported from poses_test.dart.</summary>
public sealed class PosesTests
{
    private static RenderObject One(
        long key,
        ModelAnimation? animation = null,
        int? variant = null,
        IReadOnlyList<JointPose>? joints = null) =>
        new()
        {
            Key = key,
            Transform = Matrix4.Identity(),
            Colour = new Vector3(1, 1, 1),
            Mesh = $"/models/{key}.glb",
            Animation = animation,
            Variant = variant,
            Joints = joints,
        };

    private static OrderedDictionary<string, object> Sent(RenderObject[] objects, double? at = null) =>
        SceneBuilders.Of(objects).ToMessage(at);

    private static JointPose Joint(int skin, int joint, double x, double y, double z) =>
        new(skin, joint, Matrix4.TranslationValues(x, y, z));

    private static long[] Keys(OrderedDictionary<string, object> message) => SceneBuilders.Read<long[]>(message, "poseKeys");

    private static int[] Ints(OrderedDictionary<string, object> message) => SceneBuilders.Read<int[]>(message, "poseInts");

    private static float[] Floats(OrderedDictionary<string, object> message) => SceneBuilders.Read<float[]>(message, "poseFloats");

    private static int[] JointCounts(OrderedDictionary<string, object> message) => SceneBuilders.Read<int[]>(message, "poseJointCounts");

    private static int[] Joints(OrderedDictionary<string, object> message) => SceneBuilders.Read<int[]>(message, "poseJoints");

    private static float[] JointTransforms(OrderedDictionary<string, object> message) => SceneBuilders.Read<float[]>(message, "poseJointTransforms");

    [Fact]
    public void ASceneWithNothingPosedSendsNoPoseKeysAtAll()
    {
        // Every scene without models out of files sends exactly what it sent before poses existed.
        var message = Sent([One(1), One(2)]);

        foreach (var key in new[] { "poseKeys", "poseInts", "poseFloats", "poseJointCounts", "poseJoints", "poseJointTransforms" })
        {
            Assert.False(message.ContainsKey(key), key);
        }
    }

    [Fact]
    public void OnePlayingClipPacksItsRowInTheOrderTheRendererReads()
    {
        // A fade without a from is ignored: the clip is all there is.
        var message = Sent([One(1), One(7, new ModelAnimation { Clip = 2, Seconds = 1.5, Speed = 0.5, Fade = 0.25 })]);

        Assert.Equal([7L], Keys(message));
        // Clip, faded-from clip, flags (1: loops), variant.
        Assert.Equal([2, -1, 1, -1], Ints(message));
        Assert.Equal(ModelAnimation.IntStride, Ints(message).Length);
        // Seconds, speed, the faded-from seconds, its speed, fade.
        Assert.Equal([1.5f, 0.5f, 0f, 0f, 1f], Floats(message));
        Assert.Equal(ModelAnimation.Stride, Floats(message).Length);
        Assert.Equal([0], JointCounts(message));
        Assert.Empty(Joints(message));
        Assert.Empty(JointTransforms(message));
    }

    [Fact]
    public void AFadePacksTheClipItLeavesAndEachClipsOwnLoop()
    {
        var message = Sent(
            [
                One(
                    3,
                    new ModelAnimation
                    {
                        Clip = 1,
                        Seconds = 0.25,
                        Speed = 2,
                        Loop = false,
                        From = new ModelAnimation { Clip = 0, Seconds = 3, Speed = 1 },
                        Fade = 0.5,
                    }),
            ]);

        // This clip does not loop and the one it leaves does: flag 2 alone.
        Assert.Equal([1, 0, 2, -1], Ints(message));
        Assert.Equal([0.25f, 2f, 3f, 1f, 0.5f], Floats(message));
    }

    [Fact]
    public void AVariantAloneAndJointsAloneAreEachAPose()
    {
        var message = Sent(
            [
                One(1),
                One(2, variant: 3),
                One(4),
                One(5, joints: [Joint(0, 6, 1, 2, 3)]),
                // An empty list of joints has nothing to say.
                One(6, joints: []),
            ]);

        Assert.Equal([2L, 5L], Keys(message));
        Assert.Equal([-1, -1, 0, 3, -1, -1, 0, -1], Ints(message));
        Assert.Equal([0f, 0f, 0f, 0f, 1f, 0f, 0f, 0f, 0f, 1f], Floats(message));
        Assert.Equal([0, 1], JointCounts(message));
    }

    [Fact]
    public void JointsAreEndToEndInObjectOrder()
    {
        var message = Sent(
            [
                One(10, joints: [Joint(0, 1, 1, 0, 0), Joint(0, 2, 2, 0, 0)]),
                One(11, variant: 0),
                One(12, new ModelAnimation { Clip = 0 }, joints: [Joint(1, 3, 0, 0, 3)]),
            ]);

        Assert.Equal([10L, 11L, 12L], Keys(message));
        var counts = JointCounts(message);
        Assert.Equal([2, 0, 1], counts);
        var total = counts.Sum();
        Assert.Equal([0, 1, 0, 2, 1, 3], Joints(message));
        Assert.Equal(total * 2, Joints(message).Length);

        var transforms = JointTransforms(message);
        Assert.Equal(total * 16, transforms.Length);
        // Each joint's column-major transform, its translation in 12..14.
        Assert.Equal([1f, 0f, 0f], transforms[12..15]);
        Assert.Equal([2f, 0f, 0f], transforms[(16 + 12)..(16 + 15)]);
        Assert.Equal([0f, 0f, 3f], transforms[(32 + 12)..(32 + 15)]);
    }

    [Fact]
    public void TheHostsSecondsTravelWithThePoses()
    {
        var message = Sent([One(1, new ModelAnimation { Clip = 0, Seconds = 2 })], at: 12.5);

        Assert.Equal(12.5, message["at"]);
        Assert.True(message.ContainsKey("poseKeys"));
    }
}
