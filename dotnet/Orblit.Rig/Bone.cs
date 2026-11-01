using Orblit.VectorMath;

namespace Orblit.Rig;

/// <summary>
/// A bone in its rest pose, described by where it starts, where it ends, and how it is twisted
/// about its own length: head, tail and roll. Not by a matrix, because a matrix is the answer,
/// and an artist adjusting a skeleton is moving endpoints.
/// A bone's own Y axis runs from head to tail. That is Blender's convention, and matching it
/// exactly matters, because a rig authored in one and evaluated in the other is wrong in a way
/// that only shows when a limb twists.
/// </summary>
public class Bone
{
    // F2: seven parameters, called by name, stand in for the options object.
    public Bone(
        string name,
        Vector3 head,
        Vector3 tail,
        double roll = 0,
        string? parent = null,
        bool connected = false,
        bool deform = true)
    {
        Name = name;
        Head = head.Clone();
        Tail = tail.Clone();
        Roll = roll;
        Parent = parent;
        Connected = connected;
        Deform = deform;
    }

    public string Name { get; }

    /// <summary>Where the bone starts, in armature space.</summary>
    public Vector3 Head { get; set; }

    /// <summary>Where it ends. The direction between them is the bone's Y axis, and the distance is its length.</summary>
    public Vector3 Tail { get; set; }

    /// <summary>
    /// Twist about the bone's own length, in radians. Two bones can point the same way and still be
    /// rotated differently around that direction, and which one you have decides where a knee bends.
    /// </summary>
    public double Roll { get; set; }

    /// <summary>The bone this one hangs from, by name.</summary>
    public string? Parent { get; set; }

    /// <summary>
    /// Whether the head is glued to the parent's tail. A connected bone cannot be moved
    /// independently in the rest pose, which is right for a forearm and wrong for a finger that
    /// should sit off the palm.
    /// </summary>
    public bool Connected { get; set; }

    /// <summary>
    /// Whether a mesh is bound to this bone. A generated rig has far more bones than deforming
    /// ones: controls an animator grabs and hidden mechanisms that wire them together do not touch
    /// the mesh at all.
    /// </summary>
    public bool Deform { get; set; }

    public double Length => (Tail - Head).Length;

    /// <summary>The bone's Y axis: the direction it points.</summary>
    public Vector3 Direction
    {
        get
        {
            var delta = Tail - Head;
            var length = delta.Length;

            // A zero-length bone has no direction to speak of. +Y is the identity orientation and
            // keeps the matrix well formed rather than full of NaNs.
            if (length < 1e-9)
            {
                return new Vector3(0, 1, 0);
            }

            return delta / length;
        }
    }

    /// <summary>
    /// The bone's orientation in armature space. Built from the direction and then twisted by the
    /// roll, rather than from three Euler angles, so there is no order to get wrong and no gimbal
    /// to fall into.
    /// </summary>
    public Quaternion Orientation
    {
        get
        {
            var aligned = ShortestArc(new Vector3(0, 1, 0), Direction);
            if (Roll == 0)
            {
                return aligned;
            }

            return Quaternion.AxisAngle(Direction, Roll) * aligned;
        }
    }

    /// <summary>Where the bone sits, and how it is turned, in armature space.</summary>
    public Matrix4 RestMatrix => Matrix4.Compose(Head, Orientation, new Vector3(1, 1, 1));

    public Bone Copy() => new(Name, Head.Clone(), Tail.Clone(), Roll, Parent, Connected, Deform);

    public override string ToString() => $"Bone({Name}, {Doubles.ToStringAsFixed(Length, 3)})";

    /// <summary>
    /// The rotation taking one direction to another by the shortest path. Written out rather than
    /// taken from the library, whose matrix conversions have been unreliable in this codebase
    /// before.
    /// </summary>
    private static Quaternion ShortestArc(Vector3 from, Vector3 to)
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
            // Exactly opposed. Any perpendicular axis gives a valid half turn. Pick one that is
            // definitely not parallel to the input.
            var opposed = a.Cross(new Vector3(1, 0, 0));
            if (opposed.Length2 < 1e-6)
            {
                opposed = a.Cross(new Vector3(0, 0, 1));
            }

            return Quaternion.AxisAngle(opposed.Normalized(), Math.PI);
        }

        var axis = a.Cross(b);
        var arc = new Quaternion(axis.X, axis.Y, axis.Z, 1 + dot);
        arc.Normalize();
        return arc;
    }
}
