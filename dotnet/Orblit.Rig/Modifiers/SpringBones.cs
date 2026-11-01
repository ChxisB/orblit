using Orblit.VectorMath;

namespace Orblit.Rig;

/// <summary>
/// A shape a spring chain may not pass through. A collider is in the space of <see cref="Bone"/>, or
/// in the model's space when <see cref="Bone"/> is null, so a sphere on the head moves with the head.
/// </summary>
public abstract class SpringCollider
{
    // Internal, so the three shapes below are the only ones.
    private protected SpringCollider(string? bone)
    {
        Bone = bone;
    }

    public string? Bone { get; }

    private protected static void RequireSize(double radius)
    {
        if (!double.IsFinite(radius) || radius < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(radius), radius, "A radius is 0 or more.");
        }
    }
}

/// <summary>A solid ball.</summary>
public sealed class SphereCollider : SpringCollider
{
    public SphereCollider(Vector3 centre, double radius, string? bone = null)
        : base(bone)
    {
        RequireSize(radius);
        Centre = centre;
        Radius = radius;
    }

    public Vector3 Centre { get; }

    public double Radius { get; }
}

/// <summary>A solid rod with rounded ends, such as a limb or a torso.</summary>
public sealed class CapsuleCollider : SpringCollider
{
    public CapsuleCollider(Vector3 start, Vector3 end, double radius, string? bone = null)
        : base(bone)
    {
        RequireSize(radius);
        Start = start;
        End = end;
        Radius = radius;
    }

    public Vector3 Start { get; }

    public Vector3 End { get; }

    public double Radius { get; }
}

/// <summary>A wall that stops everything on the far side of it, such as a floor.</summary>
public sealed class PlaneCollider : SpringCollider
{
    public PlaneCollider(Vector3 point, Vector3 normal, string? bone = null)
        : base(bone)
    {
        var unit = normal.Normalized();
        if (unit.Length2 < 0.5)
        {
            throw new ArgumentException("A plane needs a normal.", nameof(normal));
        }

        Point = point;
        Normal = unit;
    }

    public Vector3 Point { get; }

    /// <summary>The side things are kept on. Always a unit vector.</summary>
    public Vector3 Normal { get; }
}

/// <summary>
/// Makes a chain of bones swing, such as hair, a tail, a cloak or a tassel. Each bone's tail is a
/// particle. It is pulled back to where the animation has it by <see cref="Stiffness"/>, slowed by
/// <see cref="Drag"/>, pulled down by <see cref="Gravity"/>, kept the length of its bone from the
/// bone's head, and kept out of the <see cref="Colliders"/>. The bone is then aimed at the particle.
/// A tail that a collider stops slides along the collider's surface at the bone's length, so a chain
/// that hangs onto a floor fans out across it and does not push through.
/// It runs in the world, so a character that runs and stops leaves its hair swinging on, which a
/// simulation in the model's space cannot do. Colliders are given in the model's space and carried
/// into the world by the frame.
/// List the bones from the root of the chain outwards. The first frame passes as animated, and a frame
/// of no time holds the bones where they were. After moving the model in one jump, reset the stack: a
/// head that moves further than <see cref="SnapDistance"/> in a frame is taken to have done so, and the
/// springs start again from the animation.
/// </summary>
public sealed class SpringBones : Modifier
{
    private const double Step = 1.0 / 60;
    private const int Most = 8;

    // Rounds of keeping out of the colliders and back to length per step. One collider needs one.
    // Several that squeeze a tail need a few.
    private const int Relax = 3;

    private readonly Dictionary<string, Tail> _tails = [];

    /// <param name="bones">From the root of the chain outwards.</param>
    /// <param name="stiffness">
    /// How hard a tail is pulled back to the animated pose, in 1/s². Zero lets it hang free of the
    /// animation.
    /// </param>
    /// <param name="drag">How quickly a tail loses speed, in 1/s.</param>
    /// <param name="gravity">Acceleration in the world, in units a second squared. Down at 9.81 when left out.</param>
    /// <param name="margin">
    /// How far a tail keeps from a collider, so hair rests on a shoulder and does not sink into it.
    /// </param>
    /// <param name="colliders">What the chain may not pass through.</param>
    /// <param name="snapDistance">A head that moves further than this in one frame has been teleported.</param>
    // F2: seven tuning values, one of them required, all named at every call.
    public SpringBones(
        IReadOnlyList<string> bones,
        double stiffness = 80,
        double drag = 6,
        Vector3? gravity = null,
        double margin = 0,
        IReadOnlyList<SpringCollider>? colliders = null,
        double snapDistance = 2)
    {
        if (bones.Count == 0)
        {
            throw new ArgumentException("Give at least one bone.", nameof(bones));
        }

        foreach (var (name, value) in new[] { (nameof(stiffness), stiffness), (nameof(drag), drag), (nameof(margin), margin), (nameof(snapDistance), snapDistance) })
        {
            if (!double.IsFinite(value) || value < 0)
            {
                throw new ArgumentOutOfRangeException(name, value, "This is 0 or more.");
            }
        }

        Bones = bones;
        Stiffness = stiffness;
        Drag = drag;
        Gravity = gravity?.Clone() ?? new Vector3(0, -9.81, 0);
        Margin = margin;
        Colliders = colliders ?? [];
        SnapDistance = snapDistance;
    }

    public IReadOnlyList<string> Bones { get; }

    public double Stiffness { get; }

    public double Drag { get; }

    public Vector3 Gravity { get; }

    public double Margin { get; }

    public IReadOnlyList<SpringCollider> Colliders { get; }

    public double SnapDistance { get; }

    public override void Check(Armature armature)
    {
        var order = Bones.ToList();
        for (var i = 0; i < order.Count; i++)
        {
            BoneEdit.RequireBone(armature, order[i], "so there is nothing to swing");

            // RequireBone has just shown the bone is there.
            var parent = armature[order[i]]!.Parent;
            if (order.IndexOf(parent ?? string.Empty) > i)
            {
                throw new ArmatureException($"Bone \"{order[i]}\" is listed before its parent \"{parent}\", so it would be simulated from last frame.");
            }
        }

        foreach (var collider in Colliders)
        {
            if (collider.Bone is { } bone)
            {
                BoneEdit.RequireBone(armature, bone, "so a collider has nothing to follow");
            }
        }
    }

    public override void Solve(Pose pose, ModifierFrame frame)
    {
        var count = (int)Math.Clamp(Doubles.Ceil(frame.Dt / Step), 1, Most);
        var solids = Colliders.Select(collider => SolidOf(collider, pose, frame)).ToList();
        foreach (var name in Bones)
        {
            var head = frame.ToWorld(pose.HeadOf(name));
            var animated = frame.ToWorld(pose.TailOf(name));

            if (!_tails.TryGetValue(name, out var tail) || tail.Head.DistanceTo(head) > SnapDistance)
            {
                _tails[name] = new Tail(animated, head);
                continue;
            }

            var pull = new Pull(head, animated, solids);
            var step = frame.Dt / count;
            for (var i = 0; i < count && frame.Dt > 0; i++)
            {
                Advance(tail, pull, step);
            }

            tail.Head.SetFrom(head);
            BoneEdit.AimBone(pose, name, frame.ToModel(tail.Position) - pose.HeadOf(name));
        }
    }

    public override void Reset() => _tails.Clear();

    private void Advance(Tail tail, Pull pull, double step)
    {
        var start = tail.Position.Clone();

        tail.Velocity.AddScaled(pull.Animated - tail.Position, Stiffness * step);
        tail.Velocity.AddScaled(Gravity, step);
        tail.Velocity.Scale(1 / (1 + (Drag * step)));

        var next = OnSphere(pull.Head, start + (tail.Velocity * step), pull.Length);
        for (var i = 0; i < Relax && pull.Solids.Count > 0; i++)
        {
            foreach (var solid in pull.Solids)
            {
                next = OutAtLength(solid, pull.Head, next, pull.Length);
            }
        }

        tail.Position.SetFrom(next);
        tail.Velocity.SetFrom((next - start) / step);
    }

    private Solid SolidOf(SpringCollider collider, Pose pose, ModifierFrame frame)
    {
        var space = collider.Bone is null
            ? frame.ModelToWorld
            : frame.ModelToWorld.Multiplied(pose.WorldOf(collider.Bone));
        var column = space.GetColumn(0);
        var scale = new Vector3(column.X, column.Y, column.Z).Length;

        switch (collider)
        {
            case SphereCollider sphere:
                return new Ball(space.Transformed3(sphere.Centre), (sphere.Radius * scale) + Margin);
            case CapsuleCollider capsule:
                return new Rod(space.Transformed3(capsule.Start), space.Transformed3(capsule.End), (capsule.Radius * scale) + Margin);
            case PlaneCollider plane:
                var facing = space.Rotated3(plane.Normal.Clone()).Normalized();
                return new Wall(space.Transformed3(plane.Point) + (facing * Margin), facing);
            default:
                throw new InvalidOperationException($"{collider.GetType().Name} is not a collider.");
        }
    }

    /// <summary>A tail's particle in the world.</summary>
    private sealed class Tail(Vector3 position, Vector3 head)
    {
        public Vector3 Position { get; } = position.Clone();

        public Vector3 Velocity { get; } = Vector3.Zero();

        /// <summary>Where the bone's head was last frame, to tell a teleport from a walk.</summary>
        public Vector3 Head { get; } = head.Clone();
    }

    /// <summary>What holds a tail for one frame: its head, the pose's own tail, and the things it may not enter.</summary>
    private sealed class Pull(Vector3 head, Vector3 animated, List<Solid> solids)
    {
        public Vector3 Head { get; } = head;

        public Vector3 Animated { get; } = animated;

        public double Length { get; } = animated.DistanceTo(head);

        public List<Solid> Solids { get; } = solids;
    }

    /// <summary>A collider in the world, already grown by the margin.</summary>
    private abstract class Solid;

    private sealed class Ball(Vector3 centre, double radius) : Solid
    {
        public Vector3 Centre { get; } = centre;

        public double Radius { get; } = radius;
    }

    private sealed class Rod(Vector3 start, Vector3 end, double radius) : Solid
    {
        public Vector3 Start { get; } = start;

        public Vector3 End { get; } = end;

        public double Radius { get; } = radius;
    }

    private sealed class Wall(Vector3 point, Vector3 normal) : Solid
    {
        public Vector3 Point { get; } = point;

        public Vector3 Normal { get; } = normal;
    }

    /// <summary><paramref name="point"/> brought to <paramref name="length"/> from <paramref name="anchor"/> along its own line.</summary>
    private static Vector3 OnSphere(Vector3 anchor, Vector3 point, double length)
    {
        var line = point - anchor;
        if (line.Length2 < 1e-18)
        {
            return anchor + new Vector3(0, -length, 0);
        }

        return anchor + (line.Normalized() * length);
    }

    /// <summary>
    /// <paramref name="point"/>, which is <paramref name="length"/> from <paramref name="head"/>, moved
    /// out of <paramref name="solid"/> along the sphere of that radius around <paramref name="head"/>, to
    /// the nearest place that is clear. Pushing a point out and pulling it back to length undoes itself
    /// when the push is along the bone. Walking the sphere's own surface does not.
    /// </summary>
    private static Vector3 OutAtLength(Solid solid, Vector3 head, Vector3 point, double length)
    {
        switch (solid)
        {
            case Ball ball:
                return AroundBall(head, point, length, ball.Centre, ball.Radius);
            case Rod rod:
                var centre = NearestOnSegment(rod.Start, rod.End, point);
                return AroundBall(head, point, length, centre, rod.Radius);
            case Wall wall:
                return AbovePlane(head, point, length, wall.Point, wall.Normal);
            default:
                throw new InvalidOperationException($"{solid.GetType().Name} is not a solid.");
        }
    }

    // F2: five geometric inputs, none of which belongs with another.
    private static Vector3 AroundBall(Vector3 head, Vector3 point, double length, Vector3 centre, double radius)
    {
        if (point.DistanceTo(centre) >= radius)
        {
            return point;
        }

        // The sphere of the bone's reach and the ball meet in a circle. With no circle the head is
        // inside the ball or the ball is out of reach, and there is no way round to find.
        var toCentre = centre - head;
        var apart = toCentre.Length;
        var nothing = apart < 1e-9 || apart > length + radius || apart < Math.Abs(length - radius);
        if (nothing)
        {
            return point;
        }

        var axis = toCentre / apart;
        var along = ((length * length) - (radius * radius) + (apart * apart)) / (2 * apart);
        var middle = head + (axis * along);
        var ring = Math.Sqrt(Math.Max(0.0, (length * length) - (along * along)));
        return middle + (Sideways(point - middle, axis) * ring);
    }

    // F2: five geometric inputs, none of which belongs with another.
    private static Vector3 AbovePlane(Vector3 head, Vector3 point, double length, Vector3 origin, Vector3 normal)
    {
        if ((point - origin).Dot(normal) >= 0)
        {
            return point;
        }

        var height = (head - origin).Dot(normal);
        if (Math.Abs(height) >= length)
        {
            return point;
        }

        var middle = head - (normal * height);
        var ring = Math.Sqrt((length * length) - (height * height));
        return middle + (Sideways(point - middle, normal) * ring);
    }

    /// <summary>
    /// The unit vector across <paramref name="axis"/> that <paramref name="offset"/> leans towards. Any
    /// one when it leans along the axis, which is a tail straight below its head on a floor.
    /// </summary>
    private static Vector3 Sideways(Vector3 offset, Vector3 axis)
    {
        var across = offset - (axis * offset.Dot(axis));
        if (across.Length2 > 1e-18)
        {
            return across.Normalized();
        }

        var other = Math.Abs(axis.X) < 0.9 ? new Vector3(1, 0, 0) : new Vector3(0, 1, 0);
        return axis.Cross(other).Normalized();
    }

    private static Vector3 NearestOnSegment(Vector3 a, Vector3 b, Vector3 point)
    {
        var along = b - a;
        var length2 = along.Length2;
        if (length2 < 1e-18)
        {
            return a.Clone();
        }

        var t = Doubles.Clamp((point - a).Dot(along) / length2, 0.0, 1.0);
        return a + (along * t);
    }
}
