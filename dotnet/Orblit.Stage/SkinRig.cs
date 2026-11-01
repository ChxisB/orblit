using Orblit.Filament;
using Orblit.Rig;
using Orblit.VectorMath;

namespace Orblit.Stage;

/// <summary>An armature for a model's skin, with one deforming bone per joint.</summary>
/// <remarks>
/// This is how a file's skeleton looks to <c>Orblit.Rig</c>: the same names, hung the same way, with each
/// bone's head where its joint stands at rest. A file stores a joint as a frame, though, and a bone is two
/// ends and a twist. So the rest of each bone is worked out from the skeleton rather than read from the
/// file. The tail is the middle of the joint's children, which is where a limb is going. A joint at the
/// end of a chain carries on the way it came, from its parent, by as far again, so a fingertip ends up as
/// long as the last knuckle. A joint with neither points along its own Y axis, a tenth of the skeleton's
/// size long. No bone is left with no length: a bone of no length has no direction, and anything that
/// measures a limb, such as a reach or a stretch, divides by it. The roll lays the bone's X axis as close
/// to its joint's own X axis as the bone's direction allows. A file exported from Blender keeps each
/// joint's frame the same as its bone's, so wherever a bone pointed at its child it comes back exactly as
/// it was authored. Where a joint's X runs along the bone, there is nothing to lay, and the Z axes are
/// matched instead. None of this has to agree with the joints for <see cref="SkinBinding"/> to be right,
/// because the binding takes up any difference between a bone's frame and its joint's. It is a sensible
/// default to author against, not a requirement.
/// </remarks>
public static class SkinRig
{
    /// <summary>
    /// The name each of a skin's joints goes by as a bone, in the skin's order. Usually its own, made
    /// unique: a name already taken gets <c>.001</c>. The renderer calls a joint the file left unnamed
    /// after the mesh, light or camera it carries, or else <c>&lt;unknown&gt;</c>, so two of those are
    /// <c>&lt;unknown&gt;</c> and <c>&lt;unknown&gt;.001</c>. A joint with no name at all, in a skin
    /// described by hand, is called <c>joint</c> and its position in the skin, which is the number a
    /// <see cref="JointPose"/> reaches it by.
    /// </summary>
    /// <remarks>
    /// Public because an armature made by hand has to use these names to reach a joint the file named
    /// twice. A clip imported from the same file names its bones the same way.
    /// </remarks>
    public static IReadOnlyList<string> BoneNamesOf(SkinInfo skin) => BoneNaming.Unique(skin.Joints);

    public static Armature ArmatureOf(SkinInfo skin)
    {
        var shape = new SkinShape(skin);
        var heads = shape.Rest.Select(rest => rest.GetTranslation()).ToList();
        var ends = new Tails(shape, heads);
        var tails = Enumerable.Range(0, heads.Count).Select(ends.Of).ToList();

        return new Armature(shape.Order.Select(joint => new Bone(
            shape.Names[joint],
            heads[joint],
            tails[joint],
            roll: RollFor(heads[joint], tails[joint], shape.Rest[joint]),
            parent: shape.Parents[joint] >= 0 ? shape.Names[shape.Parents[joint]] : null)));
    }

    /// <summary>
    /// The roll that lays a bone's X axis nearest its joint's. Measured against the bone as the armature
    /// will build it with no roll, rather than against a frame worked out here. That way the answer means
    /// the same thing to <see cref="Bone.RestMatrix"/> however it goes about building that frame.
    /// </summary>
    private static double RollFor(Vector3 head, Vector3 tail, Matrix4 rest)
    {
        var along = (tail - head).Normalized();
        var unrolled = new Bone(string.Empty, head, tail).RestMatrix;
        var x = AxisOf(unrolled, 0);
        var z = AxisOf(unrolled, 2);

        // Turning by an angle about the bone's own length takes X to X cos(angle) - Z sin(angle), and Z to
        // Z cos(angle) + X sin(angle).
        if (Flattened(AxisOf(rest, 0), along) is { } jointX)
        {
            return Math.Atan2(-jointX.Dot(z), jointX.Dot(x));
        }

        return Flattened(AxisOf(rest, 2), along) is { } jointZ ? Math.Atan2(jointZ.Dot(x), jointZ.Dot(z)) : 0;
    }

    private static Vector3 AxisOf(Matrix4 matrix, int column)
    {
        var axis = matrix.GetColumn(column);
        return new Vector3(axis.X, axis.Y, axis.Z);
    }

    // The axis with the part of it that runs along `along` taken out, or null when that leaves nothing to
    // point with.
    private static Vector3? Flattened(Vector3 axis, Vector3 along)
    {
        var length = axis.Length;
        if (length < 1e-12)
        {
            return null;
        }

        var unit = axis / length;
        var flat = unit - (along * unit.Dot(along));
        return flat.Length < 1e-6 ? null : flat;
    }

    // The diagonal of the box around every point.
    private static double ExtentOf(IReadOnlyList<Vector3> points)
    {
        if (points.Count == 0)
        {
            return 0;
        }

        var low = points[0].Clone();
        var high = points[0].Clone();
        foreach (var point in points)
        {
            low.SetValues(Math.Min(low.X, point.X), Math.Min(low.Y, point.Y), Math.Min(low.Z, point.Z));
            high.SetValues(Math.Max(high.X, point.X), Math.Max(high.Y, point.Y), Math.Max(high.Z, point.Z));
        }

        return (high - low).Length;
    }

    /// <summary>Where each bone ends, worked out from the skeleton and not read from the file.</summary>
    private sealed class Tails
    {
        private readonly SkinShape _shape;
        private readonly IReadOnlyList<Vector3> _heads;
        private readonly List<int>[] _children;
        private readonly double _negligible;
        private readonly double _fallback;

        public Tails(SkinShape shape, IReadOnlyList<Vector3> heads)
        {
            _shape = shape;
            _heads = heads;
            _children = [.. heads.Select(static _ => new List<int>())];
            for (var joint = 0; joint < heads.Count; joint++)
            {
                if (shape.Parents[joint] >= 0)
                {
                    _children[shape.Parents[joint]].Add(joint);
                }
            }

            // Measured against the skeleton's own size rather than in metres, so a model in centimetres
            // is judged the same as one in metres.
            var extent = ExtentOf(heads);
            _negligible = Math.Max(extent * 1e-4, 1e-9);
            _fallback = extent > 1e-6 ? extent / 10 : 0.1;
        }

        public Vector3 Of(int joint) => MiddleOfChildren(joint) ?? CarriedOn(joint) ?? AlongOwnY(joint);

        private Vector3? MiddleOfChildren(int joint)
        {
            var head = _heads[joint];
            var ahead = _children[joint]
                .Where(child => (_heads[child] - head).Length > _negligible)
                .Select(child => _heads[child])
                .ToList();
            if (ahead.Count == 0)
            {
                return null;
            }

            var middle = Vector3.Zero();
            foreach (var point in ahead)
            {
                middle.Add(point);
            }

            middle.Scale(1.0 / ahead.Count);

            // Children spread evenly around a joint, like a pelvis between two legs, can average out to
            // the joint itself.
            return (middle - head).Length > _negligible ? middle : null;
        }

        private Vector3? CarriedOn(int joint)
        {
            var parent = _shape.Parents[joint];
            if (parent < 0)
            {
                return null;
            }

            var onward = _heads[joint] - _heads[parent];
            return onward.Length > _negligible ? _heads[joint] + onward : null;
        }

        private Vector3 AlongOwnY(int joint)
        {
            var own = AxisOf(_shape.Rest[joint], 1);
            return own.Length > 1e-12
                ? _heads[joint] + (own.Normalized() * _fallback)
                : _heads[joint] + new Vector3(0, _fallback, 0);
        }
    }
}
