using Orblit.VectorMath;

namespace Orblit.Rig;

/// <summary>
/// Which of a bone's own axes a constraint aims. A bone points along its own Y, so that is what
/// usually wants aiming, but a control that should keep its front towards something aims Z instead.
/// </summary>
public enum TrackAxis
{
    X,
    Y,
    Z,
    NegativeX,
    NegativeY,
    NegativeZ,
}

public static class TrackAxes
{
    /// <summary>Which column of the bone's matrix holds this axis.</summary>
    public static int Column(this TrackAxis axis) => axis switch
    {
        TrackAxis.X or TrackAxis.NegativeX => 0,
        TrackAxis.Y or TrackAxis.NegativeY => 1,
        TrackAxis.Z or TrackAxis.NegativeZ => 2,
        _ => throw new InvalidOperationException($"{axis} is not a track axis."),
    };

    public static bool Negated(this TrackAxis axis) => axis is TrackAxis.NegativeX or TrackAxis.NegativeY or TrackAxis.NegativeZ;

    /// <summary>The axis as a unit vector in the space the matrix puts the bone in.</summary>
    public static Vector3 Of(this TrackAxis axis, Matrix4 matrix)
    {
        var vector = matrix.GetColumn(axis.Column());
        var unit = new Vector3(vector.X, vector.Y, vector.Z);
        unit.Normalize();
        return axis.Negated() ? -unit : unit;
    }
}

/// <summary>
/// Something that adjusts where a bone ended up. Applied after a bone's own transform and before
/// its children are worked out, so a constrained bone carries everything below it with it. That is
/// what makes a rig a mechanism rather than a list of independent parts.
/// </summary>
public abstract class BoneConstraint(double influence = 1, string? influenceProperty = null, bool invertInfluence = false)
{
    /// <summary>
    /// How much of the constraint to apply, from zero to one. Every constraint blends rather than
    /// switching, because the interesting uses are partial: a head that half-follows a target reads
    /// as attention, and one that fully follows reads as a turret.
    /// </summary>
    public double Influence { get; } = influence;

    /// <summary>
    /// A named value on the pose to take the influence from instead. What makes a switch possible.
    /// An inverse kinematics blend is two stacks of constraints reading the same number, one of them
    /// inverted, so moving one slider hands a limb from one to the other.
    /// </summary>
    public string? InfluenceProperty { get; } = influenceProperty;

    /// <summary>Uses one minus the property instead, for the other half of a switch.</summary>
    public bool InvertInfluence { get; } = invertInfluence;

    /// <summary>
    /// Bones this constraint reads, which must be worked out before the bone it is on. A hierarchy is
    /// not the only thing that decides evaluation order. A bone aimed at its cousin depends on that
    /// cousin, and ordering by parents alone would read a matrix that has not been computed yet.
    /// </summary>
    public abstract IReadOnlyList<string> Dependencies { get; }

    /// <summary>The influence to actually use this frame.</summary>
    public double InfluenceIn(Pose pose)
    {
        var value = InfluenceProperty is null ? Influence : pose.Property(InfluenceProperty) ?? Influence;
        var clamped = Doubles.Clamp(value, 0.0, 1.0);
        return InvertInfluence ? 1 - clamped : clamped;
    }

    /// <summary>
    /// <paramref name="bone"/> is the bone being constrained, for the few constraints that need to
    /// know where it rests rather than only where it currently is.
    /// </summary>
    public abstract Matrix4 Apply(Matrix4 world, Pose pose, string bone);

    internal static Quaternion RotationOf(Matrix4 matrix)
    {
        var rotation = Quaternion.Identity();
        matrix.Decompose(Vector3.Zero(), rotation, Vector3.Zero());
        return rotation.Normalized();
    }

    internal static Vector3 ScaleOf(Matrix4 matrix)
    {
        var scale = Vector3.Zero();
        matrix.Decompose(Vector3.Zero(), Quaternion.Identity(), scale);
        return scale;
    }
}

/// <summary>Takes another bone's orientation.</summary>
// F2: five parameters, called by name.
public sealed class CopyRotation(
    string source,
    double influence = 1,
    string? influenceProperty = null,
    bool invertInfluence = false,
    bool invert = false) : BoneConstraint(influence, influenceProperty, invertInfluence)
{
    /// <summary>
    /// The bone to copy from. It must already have been evaluated, which evaluation order guarantees
    /// for a parent and does not for a sibling.
    /// </summary>
    public string Source { get; } = source;

    /// <summary>Turns the opposite way instead. What a counter-rotating mechanism needs.</summary>
    public bool Invert { get; } = invert;

    public override IReadOnlyList<string> Dependencies => [Source];

    public override Matrix4 Apply(Matrix4 world, Pose pose, string bone)
    {
        var target = pose.WorldOf(Source);

        var current = RotationOf(world);
        var wanted = RotationOf(target);
        if (Invert)
        {
            wanted = wanted.Conjugated();
        }

        var blended = Ik.SlerpShortest(current, wanted, InfluenceIn(pose));
        return Matrix4.Compose(world.GetTranslation(), blended, ScaleOf(world));
    }
}

/// <summary>
/// Points one of a bone's axes at a place, and twists no further than it must. Damped because it
/// takes the shortest rotation to the target rather than building a full orientation from an up
/// vector. That leaves the bone's roll alone, which is what stops an aimed bone spinning as it
/// passes overhead, the classic failure of a naive look-at in a rig.
/// </summary>
public sealed class DampedTrack(
    string target,
    TrackAxis axis = TrackAxis.Y,
    double influence = 1,
    string? influenceProperty = null,
    bool invertInfluence = false) : BoneConstraint(influence, influenceProperty, invertInfluence)
{
    /// <summary>The bone to aim at.</summary>
    public string Target { get; } = target;

    public TrackAxis Axis { get; } = axis;

    public override IReadOnlyList<string> Dependencies => [Target];

    public override Matrix4 Apply(Matrix4 world, Pose pose, string bone)
    {
        var amount = InfluenceIn(pose);
        if (amount <= 0)
        {
            return world;
        }

        var origin = world.GetTranslation();
        var toTarget = pose.WorldOf(Target).GetTranslation() - origin;
        if (toTarget.Length2 < 1e-12)
        {
            return world;
        }

        var current = Axis.Of(world);
        var correction = Ik.RotationBetween(current, toTarget.Normalized());

        var rotation = RotationOf(world);
        var wanted = correction * rotation;
        var blended = Ik.SlerpShortest(rotation, wanted, amount);

        return Matrix4.Compose(origin, blended, ScaleOf(world));
    }
}

/// <summary>
/// Stops a bone bending further than a joint can. A cone about the rest direction rather than
/// per-axis minimums and maximums. A cone has no rotation order to get wrong and no gimbal to fall
/// into, and it is what a shoulder or a finger actually is.
/// </summary>
public sealed class LimitRotation(
    double maximumAngle,
    double influence = 1,
    string? influenceProperty = null,
    bool invertInfluence = false) : BoneConstraint(influence, influenceProperty, invertInfluence)
{
    /// <summary>How far from its rest direction the bone may point, in radians.</summary>
    public double MaximumAngle { get; } = maximumAngle;

    /// <summary>Reads nothing but the bone it is on.</summary>
    public override IReadOnlyList<string> Dependencies => [];

    public override Matrix4 Apply(Matrix4 world, Pose pose, string bone)
    {
        var amount = InfluenceIn(pose);
        if (amount <= 0)
        {
            return world;
        }

        var rotation = RotationOf(world);

        // Measured from where the bone rests, not from the identity. A jaw points down and forward
        // at rest, so its world rotation is already a long way from identity. Limiting that would
        // clamp the rest pose itself and the joint would appear stuck rather than limited.
        var rest = RotationOf(pose.BaseOf(bone));
        var local = (rest.Conjugated() * rotation).Normalized();

        // A quaternion's w is the cosine of half its angle, which is the cheapest way to ask how far
        // the joint has turned.
        var angle = 2 * Math.Acos(Doubles.Clamp(Math.Abs(local.W), 0.0, 1.0));
        if (angle <= MaximumAngle)
        {
            return world;
        }

        var limited = Ik.SlerpShortest(Quaternion.Identity(), local, MaximumAngle / angle);
        var wanted = (rest * limited).Normalized();
        var blended = Ik.SlerpShortest(rotation, wanted, amount);

        return Matrix4.Compose(world.GetTranslation(), blended, ScaleOf(world));
    }
}

/// <summary>
/// Takes another bone's position, orientation and scale together. The most-used constraint in a
/// generated rig by a wide margin: every deform bone is one of these pointed at the original it
/// shadows, which is what lets the machinery above be rebuilt without the mesh noticing.
/// </summary>
public sealed class CopyTransform(
    string source,
    double influence = 1,
    string? influenceProperty = null,
    bool invertInfluence = false) : BoneConstraint(influence, influenceProperty, invertInfluence)
{
    public string Source { get; } = source;

    public override IReadOnlyList<string> Dependencies => [Source];

    public override Matrix4 Apply(Matrix4 world, Pose pose, string bone)
    {
        var amount = InfluenceIn(pose);
        if (amount <= 0)
        {
            return world;
        }

        var target = pose.WorldOf(Source);
        if (amount >= 1)
        {
            return target.Clone();
        }

        // Blended part by part rather than by interpolating sixteen numbers, which would shear the
        // bone on the way across.
        Vector3 from = Vector3.Zero(), to = Vector3.Zero();
        Quaternion fromRotation = Quaternion.Identity(), toRotation = Quaternion.Identity();
        Vector3 fromScale = Vector3.Zero(), toScale = Vector3.Zero();
        world.Decompose(from, fromRotation, fromScale);
        target.Decompose(to, toRotation, toScale);

        return Matrix4.Compose(
            from + ((to - from) * amount),
            Ik.SlerpShortest(fromRotation.Normalized(), toRotation.Normalized(), amount),
            fromScale + ((toScale - fromScale) * amount));
    }
}

/// <summary>
/// Points a bone at something and stretches it to reach. The other half of stretch, and the one that
/// is not inverse kinematics: a single bone spanning a gap, such as a tongue, a rubber hose or the
/// bone between two controls that has to stay attached to both. Volume is preserved by narrowing the
/// cross-section as the bone lengthens. Scaling all three axes together would be a zoom.
/// </summary>
// F2: six parameters, called by name.
public sealed class StretchTo(
    string target,
    double restLength,
    double volume = 1,
    double influence = 1,
    string? influenceProperty = null,
    bool invertInfluence = false) : BoneConstraint(influence, influenceProperty, invertInfluence)
{
    /// <summary>The bone whose head this one reaches for.</summary>
    public string Target { get; } = target;

    /// <summary>
    /// The distance at which the bone is at its natural length. Required rather than taken from the
    /// bone, because unstretched is a decision. It is usually the rest length, and it is deliberately
    /// something else whenever a rig is built around a pose that is not the rest pose.
    /// </summary>
    public double RestLength { get; } = restLength;

    /// <summary>How much of the volume to keep, from zero (scale only along the length) to one.</summary>
    public double Volume { get; } = volume;

    public override IReadOnlyList<string> Dependencies => [Target];

    public override Matrix4 Apply(Matrix4 world, Pose pose, string bone)
    {
        var amount = InfluenceIn(pose);
        if (amount <= 0 || RestLength <= 0)
        {
            return world;
        }

        var origin = world.GetTranslation();
        var toTarget = pose.WorldOf(Target).GetTranslation() - origin;
        var distance = toTarget.Length;
        if (distance < 1e-9)
        {
            return world;
        }

        var rotation = RotationOf(world);
        var aimed = Ik.RotationBetween(TrackAxis.Y.Of(world), toTarget / distance) * rotation;

        var scale = ScaleOf(world);
        var factor = distance / RestLength;
        var cross = 1 / Math.Sqrt(factor);
        // Volume at zero leaves the cross-section alone. At one it narrows fully.
        var narrowed = 1 + ((cross - 1) * Doubles.Clamp(Volume, 0.0, 1.0));

        var stretched = new Vector3(scale.X * narrowed, scale.Y * factor, scale.Z * narrowed);

        if (amount >= 1)
        {
            return Matrix4.Compose(origin, aimed.Normalized(), stretched);
        }

        return Matrix4.Compose(
            origin,
            Ik.SlerpShortest(rotation, aimed.Normalized(), amount),
            scale + ((stretched - scale) * amount));
    }
}
