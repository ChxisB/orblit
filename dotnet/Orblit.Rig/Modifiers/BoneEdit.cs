using Orblit.VectorMath;

namespace Orblit.Rig;

/// <summary>The few ways a modifier writes to a pose, and the questions it asks of an armature.</summary>
public static class BoneEdit
{
    /// <summary>The axis a bone runs along. A new vector each time, so nothing can move it.</summary>
    internal static Vector3 AlongBone => new(0, 1, 0);

    /// <summary>
    /// Writes <paramref name="bone"/>'s pose so the bone stands at <paramref name="world"/>, then brings
    /// it and the bones below it up to date. A pose is a departure from rest, measured from where the
    /// parent puts the bone, so this works the departure out from the parent's current world. A parent
    /// scaled to nothing has no frame to measure in and leaves the pose as it was.
    /// </summary>
    public static void PlaceBone(Pose pose, string bone, Matrix4 world)
    {
        var inverse = Matrix4.Zero();
        var determinant = inverse.CopyInverse(pose.BaseOf(bone));
        if (determinant == 0 || !double.IsFinite(determinant))
        {
            return;
        }

        var transform = pose[bone];
        inverse.Multiply(world);
        inverse.Decompose(transform.Location, transform.Rotation, transform.Scale);
        var turn = transform.Rotation;
        var finite = double.IsFinite(turn.X) && double.IsFinite(turn.Y) && double.IsFinite(turn.Z) && double.IsFinite(turn.W);

        // A scale of nothing on an axis leaves no way to tell which way that axis was turned.
        // Anything scaled flat looks the same either way.
        if (!finite)
        {
            turn.SetValues(0, 0, 0, 1);
        }

        pose.Refresh(from: bone);
    }

    /// <summary>Turns <paramref name="bone"/> about its own head by <paramref name="turn"/>, a rotation in model space.</summary>
    public static void TurnBone(Pose pose, string bone, Quaternion turn)
    {
        var world = pose.WorldOf(bone);
        var head = Vector3.Zero();
        var rotation = Quaternion.Identity();
        var scale = Vector3.Zero();
        world.Decompose(head, rotation, scale);
        PlaceBone(pose, bone, Matrix4.Compose(head, (turn * rotation.Normalized()).Normalized(), scale));
    }

    /// <summary>
    /// Points <paramref name="bone"/>'s Y axis along <paramref name="direction"/>, keeping its roll, then
    /// brings it and the bones below it up to date.
    /// </summary>
    public static void AimBone(Pose pose, string bone, Vector3 direction)
    {
        var rotation = Ik.AimedRotation(pose.WorldOf(bone), pose.BaseOf(bone), direction);
        if (rotation is null)
        {
            return;
        }

        pose[bone].Rotation = rotation;
        pose.Refresh(from: bone);
    }

    /// <summary>
    /// The bones from <paramref name="root"/> down to <paramref name="tip"/>, parents first, or null
    /// when <paramref name="tip"/> is not below <paramref name="root"/>.
    /// </summary>
    public static List<string>? BonePath(Armature armature, string root, string tip)
    {
        var path = new List<string>();
        var name = tip;
        while (name is not null)
        {
            path.Add(name);
            if (name == root)
            {
                path.Reverse();
                return path;
            }

            name = armature[name]?.Parent;
        }

        return null;
    }

    /// <summary>Throws if <paramref name="armature"/> has no bone called <paramref name="name"/>.</summary>
    public static void RequireBone(Armature armature, string name, string why)
    {
        if (!armature.Contains(name))
        {
            throw new ArmatureException($"No bone called \"{name}\" in this armature, {why}.");
        }
    }
}
