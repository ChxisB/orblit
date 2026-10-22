using Orblit.VectorMath;

namespace Orblit.Rig;

/// <summary>Where a two-bone chain ends up reaching for something.</summary>
public sealed class IkSolution(Vector3 joint, Vector3 end, bool reachable, double stretch = 1)
{
    /// <summary>The elbow or knee.</summary>
    public Vector3 Joint { get; } = joint;

    /// <summary>The hand or foot. Equal to the target when it was reachable.</summary>
    public Vector3 End { get; } = end;

    /// <summary>
    /// False when the target was further than the limb is long, or so close the limb would have to
    /// fold through itself. The chain still points at it, straightened or folded as far as it goes.
    /// Refusing to solve would leave the limb wherever it was, which looks broken rather than strained.
    /// </summary>
    public bool Reachable { get; } = reachable;

    /// <summary>
    /// How much longer than its rest length the limb had to become, as a multiplier. One when it did
    /// not stretch. Reported rather than baked into the positions, because stretching is something
    /// the bones do: the caller has to scale them, or the mesh stays the length it was and tears
    /// away from the joint.
    /// </summary>
    public double Stretch { get; } = stretch;
}

/// <summary>The inverse kinematics and rotation helpers a rig is built from.</summary>
public static class Ik
{
    /// <summary>
    /// Places a two-bone chain so its end reaches a target. Solved with the law of cosines rather
    /// than iterated. A limb is a triangle with two known sides, so there is a closed-form answer,
    /// and an iterative solver would converge differently on different frames, which is how IK gets
    /// its reputation for jitter.
    /// <paramref name="pole"/> decides which way the joint bends. Two solutions satisfy any
    /// reachable target, mirrored about the line from root to target, and the pole picks one.
    /// <paramref name="stretch"/> lets the limb grow rather than stopping short: at zero it stops at
    /// full extension, at one it reaches anything.
    /// </summary>
    // F2: six parameters, called by name.
    public static IkSolution SolveTwoBoneIk(
        Vector3 root,
        Vector3 pole,
        Vector3 target,
        double upperLength,
        double lowerLength,
        double stretch = 0)
    {
        var toTarget = target - root;
        var reach = toTarget.Length;

        if (reach < 1e-9 || upperLength <= 0 || lowerLength <= 0)
        {
            return new IkSolution(
                root + new Vector3(0, upperLength, 0),
                root + new Vector3(0, upperLength + lowerLength, 0),
                reachable: false);
        }

        var direction = toTarget / reach;
        var maximum = upperLength + lowerLength;
        var minimum = Math.Abs(upperLength - lowerLength);

        // Past full extension with stretch allowed, the limb is a straight line and there is no
        // triangle left to solve.
        if (stretch > 0 && reach > maximum)
        {
            var factor = 1 + (((reach / maximum) - 1) * Doubles.Clamp(stretch, 0.0, 1.0));
            return new IkSolution(
                root + (direction * (upperLength * factor)),
                root + (direction * (maximum * factor)),
                reachable: stretch >= 1,
                stretch: factor);
        }

        // Kept just inside the limits. Exactly straight or exactly folded makes the bend plane
        // undefined, and the joint flips between frames as rounding pushes it either side.
        const double margin = 1e-4;
        var clamped = Doubles.Clamp(reach, minimum + margin, maximum - margin);
        var reachable = reach > minimum + margin && reach < maximum - margin;

        var cosRoot = Doubles.Clamp(
            ((upperLength * upperLength) + (clamped * clamped) - (lowerLength * lowerLength)) / (2 * upperLength * clamped),
            -1.0,
            1.0);
        var rootAngle = Math.Acos(cosRoot);

        var bendAxis = direction.Cross(pole - root);
        if (bendAxis.Length2 < 1e-12)
        {
            // The pole is on the line to the target, so it names no plane. Any perpendicular will do
            // and at least keeps the limb from collapsing.
            bendAxis = direction.Cross(new Vector3(0, 0, 1));
            if (bendAxis.Length2 < 1e-12)
            {
                bendAxis = direction.Cross(new Vector3(1, 0, 0));
            }
        }

        bendAxis.Normalize();

        // Through the matrix. A quaternion's own rotate turns by the inverse, which would bend the
        // joint away from the pole.
        var upperDirection = Quaternion.AxisAngle(bendAxis, rootAngle).AsRotationMatrix().Transformed(direction).Normalized();
        var joint = root + (upperDirection * upperLength);

        var end = reachable
            ? target.Clone()
            : joint + ((target - joint).Normalized() * lowerLength);

        return new IkSolution(joint, end, reachable);
    }

    /// <summary>
    /// The local rotation that points a bone's Y axis along <paramref name="direction"/>.
    /// <paramref name="world"/> is where the bone stands now and <paramref name="baseMatrix"/> is its
    /// parent's world times its rest offset, which is what a pose rotation is measured from. The
    /// shortest turn is used, so the bone keeps whatever roll it had. Null when the direction has no
    /// length to point along.
    /// </summary>
    public static Quaternion? AimedRotation(Matrix4 world, Matrix4 baseMatrix, Vector3 direction)
    {
        if (direction.Length2 < 1e-12)
        {
            return null;
        }

        var column = world.GetColumn(1);
        var currentAxis = new Vector3(column.X, column.Y, column.Z);
        currentAxis.Normalize();

        var currentRotation = Quaternion.Identity();
        world.Decompose(Vector3.Zero(), currentRotation, Vector3.Zero());
        currentRotation.Normalize();

        var correction = RotationBetween(currentAxis, direction.Normalized());
        var wanted = correction * currentRotation;

        var baseRotation = Quaternion.Identity();
        baseMatrix.Decompose(Vector3.Zero(), baseRotation, Vector3.Zero());
        baseRotation.Normalize();

        // Converted back to the bone's own space, because that is where a pose lives. A world
        // rotation would be undone the moment the parent moved.
        return (baseRotation.Conjugated() * wanted).Normalized();
    }

    /// <summary>
    /// The scale that lengthens a bone by <paramref name="factor"/> and keeps its volume. The
    /// cross-section narrows as the inverse square root of the stretch, which is what stops a
    /// stretched forearm from also getting fatter.
    /// </summary>
    public static Vector3 StretchScale(double factor)
    {
        var cross = factor <= 0 ? 1.0 : 1 / Math.Sqrt(factor);
        return new Vector3(cross, factor, cross);
    }

    /// <summary>
    /// The vector turned by the rotation. Through the matrix, because a quaternion's own rotate
    /// turns by the inverse of the rotation it is given.
    /// </summary>
    public static Vector3 Turned(Quaternion rotation, Vector3 vector) =>
        rotation.AsRotationMatrix().Transformed(vector);

    /// <summary>How far the rotation turns, from zero to pi.</summary>
    public static double TurnAngle(Quaternion rotation) =>
        2 * Math.Acos(Doubles.Clamp(Math.Abs(rotation.W), 0.0, 1.0));

    /// <summary>Spherical interpolation that always takes the shorter arc.</summary>
    public static Quaternion SlerpShortest(Quaternion a, Quaternion b, double t)
    {
        if (t <= 0)
        {
            return a.Clone();
        }

        if (t >= 1)
        {
            return b.Clone();
        }

        var dot = (a.X * b.X) + (a.Y * b.Y) + (a.Z * b.Z) + (a.W * b.W);
        var target = b;
        if (dot < 0)
        {
            target = new Quaternion(-b.X, -b.Y, -b.Z, -b.W);
            dot = -dot;
        }

        if (dot > 0.9995)
        {
            var near = new Quaternion(
                a.X + ((target.X - a.X) * t),
                a.Y + ((target.Y - a.Y) * t),
                a.Z + ((target.Z - a.Z) * t),
                a.W + ((target.W - a.W) * t));
            near.Normalize();
            return near;
        }

        var theta = Math.Acos(Doubles.Clamp(dot, -1.0, 1.0));
        var sinTheta = Math.Sin(theta);
        var from = Math.Sin((1 - t) * theta);
        var to = Math.Sin(t * theta);
        var result = new Quaternion(
            ((a.X * from) + (target.X * to)) / sinTheta,
            ((a.Y * from) + (target.Y * to)) / sinTheta,
            ((a.Z * from) + (target.Z * to)) / sinTheta,
            ((a.W * from) + (target.W * to)) / sinTheta);
        result.Normalize();
        return result;
    }

    /// <summary>The rotation taking one direction to another by the shortest path.</summary>
    public static Quaternion RotationBetween(Vector3 from, Vector3 to)
    {
        var a = from.Normalized();
        var b = to.Normalized();
        var dot = a.Dot(b);

        if (dot > 0.999999)
        {
            return Quaternion.Identity();
        }

        if (dot < -0.999999)
        {
            var opposite = a.Cross(new Vector3(1, 0, 0));
            if (opposite.Length2 < 1e-6)
            {
                opposite = a.Cross(new Vector3(0, 0, 1));
            }

            return Quaternion.AxisAngle(opposite.Normalized(), Math.PI);
        }

        var axis = a.Cross(b);
        var result = new Quaternion(axis.X, axis.Y, axis.Z, 1 + dot);
        result.Normalize();
        return result;
    }

    /// <summary>
    /// Splits the rotation into the turn about <paramref name="axis"/> and the swing that is left.
    /// The two multiply back to the rotation: <c>swing * twist</c>. The axis has to be a unit
    /// vector. A half turn that swings straight across the axis has no twist to speak of, and comes
    /// back with an identity twist.
    /// </summary>
    public static (Quaternion Swing, Quaternion Twist) SwingTwist(Quaternion rotation, Vector3 axis)
    {
        // Both signs of a quaternion are the same turn. Taking the one with a positive w keeps the
        // twist angle below a half turn either way round.
        var q = rotation.Normalized();
        if (q.W < 0)
        {
            q.SetValues(-q.X, -q.Y, -q.Z, -q.W);
        }

        var along = (q.X * axis.X) + (q.Y * axis.Y) + (q.Z * axis.Z);
        var twist = new Quaternion(axis.X * along, axis.Y * along, axis.Z * along, q.W);
        if (twist.Length < 1e-9)
        {
            return (q, Quaternion.Identity());
        }

        twist.Normalize();
        return ((q * twist.Conjugated()).Normalized(), twist);
    }

    /// <summary>How far a twist from <see cref="SwingTwist"/> turns about the axis, signed, in radians.</summary>
    public static double TwistAngle(Quaternion twist, Vector3 axis)
    {
        var along = (twist.X * axis.X) + (twist.Y * axis.Y) + (twist.Z * axis.Z);
        return 2 * Math.Atan2(along, twist.W);
    }

    /// <summary>
    /// Moves a chain of points so its last point lands on the target. Each point is pulled to where
    /// the one after it wants it and then pushed back to where the one before it wants it, which is
    /// FABRIK. The gap between two neighbours is kept at the length it began with, and the first
    /// point does not move. A target out of reach straightens the chain towards it.
    /// It reaches an answer by repetition, so <paramref name="iterations"/> is a budget and not a
    /// promise. Twelve is plenty for a spine or a tail. The result has the same number of points as
    /// the input, which is left alone.
    /// </summary>
    public static List<Vector3> SolveFabrik(IReadOnlyList<Vector3> points, Vector3 target, int iterations = 12, double tolerance = 1e-4)
    {
        var result = points.Select(point => point.Clone()).ToList();
        if (result.Count < 2)
        {
            return result;
        }

        var gaps = new List<double>();
        for (var i = 0; i + 1 < result.Count; i++)
        {
            gaps.Add(result[i].DistanceTo(result[i + 1]));
        }

        var root = result[0].Clone();
        var reach = 0.0;
        foreach (var gap in gaps)
        {
            reach += gap;
        }

        if (root.DistanceTo(target) >= reach)
        {
            Straighten(result, gaps, target);
            return result;
        }

        for (var pass = 0; pass < iterations; pass++)
        {
            if (result[^1].DistanceTo(target) <= tolerance)
            {
                break;
            }

            result[^1].SetFrom(target);
            for (var i = gaps.Count - 1; i >= 0; i--)
            {
                result[i] = KeepGap(result[i + 1], result[i], gaps[i]);
            }

            result[0].SetFrom(root);
            for (var i = 0; i < gaps.Count; i++)
            {
                result[i + 1] = KeepGap(result[i], result[i + 1], gaps[i]);
            }
        }

        return result;
    }

    private static void Straighten(List<Vector3> points, List<double> gaps, Vector3 target)
    {
        var direction = target - points[0];
        if (direction.Length2 < 1e-12)
        {
            return;
        }

        direction.Normalize();
        for (var i = 0; i < gaps.Count; i++)
        {
            points[i + 1] = points[i] + (direction * gaps[i]);
        }
    }

    // Two points on top of each other name no line. Straight up is as good as any, and it keeps the
    // chain from collapsing into a point.
    private static Vector3 KeepGap(Vector3 anchor, Vector3 moving, double gap)
    {
        var line = moving - anchor;
        return line.Length2 < 1e-18 ? anchor + new Vector3(0, gap, 0) : anchor + (line.Normalized() * gap);
    }
}
