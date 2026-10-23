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

    public RestSkeleton Skeleton => new(
        Names,
        parents,
        [.. rest.Select(static bone => Matrix4.Compose(bone.Position, bone.Turn, Vector3.All(1)))],
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

    /// <summary>Every bone in the world at rest.</summary>
    public List<Matrix4> Pose()
    {
        var world = new List<Matrix4>();
        for (var i = 0; i < Names.Count; i++)
        {
            var local = Matrix4.Compose(rest[i].Position, rest[i].Turn, Vector3.All(1));
            world.Add((parents[i] < 0 ? _above : world[parents[i]]).Multiplied(local));
        }

        return world;
    }
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
}
