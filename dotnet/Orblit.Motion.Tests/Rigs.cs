namespace Orblit.Motion.Tests;

/// <summary>
/// A skeleton written out by hand, and the forward kinematics to pose it, so that the checks never go through
/// the code under test.
/// </summary>
internal sealed class Rig(
    List<string> names,
    List<int> parents,
    List<(Vector3 Position, Quaternion Turn)> rest,
    Matrix4? above = null)
{
    private readonly Matrix4 _above = above ?? Matrix4.Identity();

    public List<string> Names { get; } = names;

    public List<int> Parents { get; } = parents;

    public List<(Vector3 Position, Quaternion Turn)> Rest { get; } = rest;

    public RestSkeleton Skeleton => new(
        Names,
        Parents,
        [.. Rest.Select(static bone => Matrix4.Compose(bone.Position, bone.Turn, Vector3.All(1)))],
        _above);

    /// <summary>How far the farthest bone is from where the roots hang, at rest.</summary>
    public double Reach
    {
        get
        {
            var origin = _above.GetTranslation();
            return Pose().Max(world => world.GetTranslation().DistanceTo(origin));
        }
    }

    /// <summary>Every bone in the world with <paramref name="clip"/> at <paramref name="time"/>, or at rest with none.</summary>
    public List<Matrix4> Pose(ClipDocument? clip = null, double time = 0)
    {
        var frame = clip?.SampleAt(time);
        var world = new List<Matrix4>();
        for (var i = 0; i < Names.Count; i++)
        {
            var local = LocalOf(i, frame?.BoneOf(string.Empty, Names[i]));
            world.Add((Parents[i] < 0 ? _above : world[Parents[i]]).Multiplied(local));
        }

        return world;
    }

    private Matrix4 LocalOf(int bone, BoneLocal? posed) => Matrix4.Compose(
        posed?.Position ?? Rest[bone].Position,
        posed?.Rotation ?? Rest[bone].Turn,
        posed?.Scale ?? Vector3.All(1));
}

internal static class Rigs
{
    /// <summary>
    /// A turn tipped about all three axes, so that a wrong order of multiplication shows up. A quarter turn
    /// about one axis hides it.
    /// </summary>
    public static Quaternion Tip(double x, double y, double z) =>
        (Quaternion.AxisAngle(new Vector3(1, 0, 0), Doubles.Radians(x))
            * Quaternion.AxisAngle(new Vector3(0, 1, 0), Doubles.Radians(y))
            * Quaternion.AxisAngle(new Vector3(0, 0, 1), Doubles.Radians(z))).Normalized();

    public static Matrix3 TurnOf(Matrix4 matrix)
    {
        var turn = Quaternion.Identity();
        matrix.Decompose(Vector3.Zero(), turn, Vector3.Zero());
        return turn.AsRotationMatrix();
    }

    public static Rig Source() => new(
        ["hips", "spine", "chest", "head", "armL", "handL"],
        [-1, 0, 1, 2, 2, 4],
        [
            (new Vector3(0, 1, 0), Quaternion.Identity()),
            (new Vector3(0, 0.2, 0), Tip(5, 0, 0)),
            (new Vector3(0, 0.3, 0), Tip(0, 10, 0)),
            (new Vector3(0, 0.25, 0), Quaternion.Identity()),
            (new Vector3(0.2, 0.2, 0), Tip(0, 0, -90)),
            (new Vector3(0, 0.3, 0), Quaternion.Identity()),
        ]);

    /// <summary>
    /// The same body half as big again, with every bone rest-turned another way and an armature above it
    /// standing a Z-up rig upright. <paramref name="spine"/> is the name of its spine bone, or null for a body
    /// with none.
    /// </summary>
    public static Rig Target(string? spine = "spine")
    {
        var bones = new List<(string Name, int Parent, Vector3 Position, Quaternion Turn)>
        {
            ("hips", -1, new Vector3(0, 1.5, 0), Tip(0, 15, 0)),
        };
        if (spine is not null)
        {
            bones.Add((spine, 0, new Vector3(0, 0.3, 0), Tip(-8, 0, 10)));
        }

        var above = spine is null ? 0 : 1;
        bones.Add(("chest", above, new Vector3(0, 0.45, 0), Tip(0, -20, 0)));
        bones.Add(("head", above + 1, new Vector3(0, 0.4, 0), Tip(10, 0, 0)));
        bones.Add(("armL", above + 1, new Vector3(0.3, 0.3, 0), Tip(20, 0, -70)));
        bones.Add(("handL", above + 3, new Vector3(0, 0.45, 0), Tip(0, 30, 0)));
        return new Rig(
            [.. bones.Select(static bone => bone.Name)],
            [.. bones.Select(static bone => bone.Parent)],
            [.. bones.Select(static bone => (bone.Position, bone.Turn))],
            Matrix4.Compose(Vector3.Zero(), Quaternion.AxisAngle(new Vector3(1, 0, 0), -Math.PI / 2), Vector3.All(1)));
    }

    public static RestSkeleton Chain(params string[] names) => new(
        names,
        [.. Enumerable.Range(0, names.Length).Select(static i => i - 1)],
        [.. names.Select(static _ => Matrix4.Identity())]);

    /// <summary>The hips, spine, chest, head and left arm at four moments, and one mark.</summary>
    public static ClipDocument Walk() => new(
        "Walk",
        1.5,
        channels:
        [
            ClipSamples.Turns(
                "hips",
                Hold.Smooth,
                (0, Tip(0, 0, 0)),
                (0.4, Tip(10, 25, -5)),
                (0.9, Tip(-15, 40, 20)),
                (1.5, Tip(0, 0, 0))),
            ClipSamples.Turns(
                "spine",
                (0, Tip(0, 0, 0)),
                (0.4, Tip(20, 40, 0)),
                (0.9, Tip(-10, 15, 30)),
                (1.5, Tip(0, 0, 0))),
            ClipSamples.Turns(
                "chest",
                (0, Tip(0, 0, 0)),
                (0.4, Tip(30, -20, 10)),
                (0.9, Tip(0, 30, -20)),
                (1.5, Tip(0, 0, 0))),
            ClipSamples.Turns("head", (0, Tip(0, 0, 0)), (0.4, Tip(20, 40, 0))),
            ClipSamples.Turns(
                "armL",
                (0, Tip(0, 0, -90)),
                (0.4, Tip(40, 20, -60)),
                (0.9, Tip(-30, 50, -120)),
                (1.5, Tip(0, 0, -90))),
        ],
        marks: [new Mark(0.4, "step")]);

    public static Quaternion TurnAt(ClipDocument clip, string bone, int key) =>
        Assert.IsType<ClipChannel<Quaternion>>(clip.ChannelFor(string.Empty, "rotation", bone)).Keys[key].Value;

    /// <summary>
    /// Every bone of <paramref name="to"/> that <paramref name="from"/> has a bone of the same name for stands
    /// in the world where that bone did, measured from where each rests.
    /// </summary>
    // F2: six parameters, called by name.
    public static void ExpectSamePose(
        Rig from,
        Rig to,
        ClipDocument clip,
        ClipDocument retargeted,
        IReadOnlyList<double> times,
        double within = 1e-9)
    {
        var fromRest = from.Pose();
        var toRest = to.Pose();
        foreach (var time in times)
        {
            var fromPose = from.Pose(clip, time);
            var toPose = to.Pose(retargeted, time);
            for (var i = 0; i < to.Names.Count; i++)
            {
                var s = from.Names.IndexOf(to.Names[i]);
                if (s < 0)
                {
                    continue;
                }

                var offset = TurnOf(fromRest[s]);
                offset.Transpose();
                offset.Multiply(TurnOf(toRest[i]));
                var expected = TurnOf(fromPose[s]);
                expected.Multiply(offset);
                var actual = TurnOf(toPose[i]);
                for (var e = 0; e < 9; e++)
                {
                    Assert.True(Math.Abs(actual[e] - expected[e]) <= within, $"{to.Names[i]} at {time}: {actual[e]} is not {expected[e]}");
                }
            }
        }
    }
}
