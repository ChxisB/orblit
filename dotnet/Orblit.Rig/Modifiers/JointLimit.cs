using Orblit.VectorMath;

namespace Orblit.Rig;

/// <summary>
/// Keeps a joint inside a cone of its rest direction. A shoulder or a hip is a ball: the bone swings
/// off its rest direction in any way, and rolls about its own length. The swing is held to
/// <see cref="Swing"/> radians and the roll to <see cref="Twist"/> either side of rest. Both are
/// measured from the bone's rest pose, so a bone that is not axis-aligned is not treated as already
/// bent.
/// Put it after whatever moves the bone. A limit that runs first is undone by the next modifier that
/// turns the bone.
/// </summary>
public sealed class ConeLimit : Modifier
{
    /// <param name="bone">The bone to limit.</param>
    /// <param name="swing">The most the bone may swing off its rest direction, in radians.</param>
    /// <param name="twist">The most it may roll about its own length either way, in radians.</param>
    public ConeLimit(string bone, double swing, double twist = Math.PI)
    {
        if (!double.IsFinite(swing) || swing < 0 || swing > Math.PI)
        {
            throw new ArgumentOutOfRangeException(nameof(swing), swing, "Swing is 0 to pi radians.");
        }

        if (!double.IsFinite(twist) || twist < 0 || twist > Math.PI)
        {
            throw new ArgumentOutOfRangeException(nameof(twist), twist, "Twist is 0 to pi radians.");
        }

        Bone = bone;
        Swing = swing;
        Twist = twist;
    }

    public string Bone { get; }

    public double Swing { get; }

    public double Twist { get; }

    public override void Check(Armature armature) =>
        BoneEdit.RequireBone(armature, Bone, "so there is no joint to limit");

    public override void Solve(Pose pose, ModifierFrame frame)
    {
        var along = BoneEdit.AlongBone;
        var (swing, twist) = Ik.SwingTwist(pose[Bone].Rotation, along);

        var swung = Ik.TurnAngle(swing);
        var kept = swung <= Swing ? swing : Ik.SlerpShortest(Quaternion.Identity(), swing, Swing / swung);

        var rolled = Doubles.Clamp(Ik.TwistAngle(twist, along), -Twist, Twist);
        var limited = kept * Quaternion.AxisAngle(along, rolled);
        limited.Normalize();
        pose[Bone].Rotation = limited;
        pose.Refresh(from: Bone);
    }
}

/// <summary>
/// Lets a joint turn about one axis only, between two angles. A knee or an elbow is a hinge. Whatever
/// the clip or a modifier above asked of it off the axis is dropped, and the turn about the axis is
/// held to <see cref="Min"/> and <see cref="Max"/>. The axis is in the bone's own space, which makes X
/// the default: a limb that bends forwards in the rest pose bends about X.
/// </summary>
public sealed class HingeLimit : Modifier
{
    /// <param name="bone">The bone to limit.</param>
    /// <param name="min">Radians. Zero is the rest pose, and a bend is one side or the other.</param>
    /// <param name="max">Radians.</param>
    /// <param name="axis">The hinge's axis, in the bone's own space. Along X when left out.</param>
    public HingeLimit(string bone, double min, double max, Vector3? axis = null)
    {
        var unit = (axis ?? new Vector3(1, 0, 0)).Normalized();
        if (!double.IsFinite(min) || !double.IsFinite(max) || min > max)
        {
            throw new ArgumentException("A hinge needs a lowest angle that is not past its highest.", nameof(min));
        }

        if (unit.Length2 < 0.5)
        {
            throw new ArgumentException("A hinge needs an axis.", nameof(axis));
        }

        Bone = bone;
        Axis = unit;
        Min = min;
        Max = max;
    }

    public string Bone { get; }

    /// <summary>The hinge's axis, in the bone's own space. Always a unit vector.</summary>
    public Vector3 Axis { get; }

    public double Min { get; }

    public double Max { get; }

    public override void Check(Armature armature) =>
        BoneEdit.RequireBone(armature, Bone, "so there is no joint to limit");

    public override void Solve(Pose pose, ModifierFrame frame)
    {
        var (_, twist) = Ik.SwingTwist(pose[Bone].Rotation, Axis);
        var angle = Doubles.Clamp(Ik.TwistAngle(twist, Axis), Min, Max);
        pose[Bone].Rotation = Quaternion.AxisAngle(Axis, angle);
        pose.Refresh(from: Bone);
    }
}
