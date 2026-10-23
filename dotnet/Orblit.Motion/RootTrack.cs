using Orblit.Scene;

namespace Orblit.Motion;

/// <summary>
/// A clip's root channels, read the way its <see cref="RootMotion"/> asks.
/// </summary>
/// <remarks>
/// Splits the root into a body and a pose. The body is the part that goes to the character: across the
/// ground always, up and down when the root <see cref="RootMotion.Rises"/>, and the turn about up when it
/// <see cref="RootMotion.Turns"/>. The pose is what is left, which the model keeps. The body at any moment
/// is the root's extracted part as the clip has it, so the model's root sits exactly over the character
/// rather than wherever the clip started it.
/// </remarks>
internal sealed class RootTrack
{
    private readonly RootMotion _motion;
    private readonly ClipChannel? _position;
    private readonly ClipChannel? _rotation;

    private RootTrack(RootMotion motion, ClipChannel? position, ClipChannel? rotation)
    {
        _motion = motion;
        _position = position;
        _rotation = rotation;
    }

    /// <summary>The root <paramref name="clip"/> names, or null when it names none.</summary>
    internal static RootTrack? Of(ClipDocument clip)
    {
        if (clip.RootMotion is not { } motion)
        {
            return null;
        }

        var onBone = motion.Bone is not null;
        return new RootTrack(
            motion,
            clip.ChannelFor(motion.Target, onBone ? "position" : "transform.position", motion.Bone),
            clip.ChannelFor(motion.Target, onBone ? "rotation" : "transform.rotation", motion.Bone));
    }

    /// <summary>
    /// How far the body goes from <paramref name="from"/> seconds in to <paramref name="to"/>. Backwards when
    /// <paramref name="to"/> is before <paramref name="from"/>, which is what playing a walk in reverse
    /// should do.
    /// </summary>
    internal RootStep Step(double from, double to)
    {
        var start = BodyAt(from);
        var end = BodyAt(to);
        var back = start.Heading.Conjugated();
        return new RootStep(RootStep.Turned(back, end.Ground - start.Ground), back * end.Heading);
    }

    /// <summary>Takes the body out of <paramref name="frame"/>, leaving the root where the body is.</summary>
    internal void HoldInPlace(ClipFrame frame)
    {
        var body = BodyAt(frame.At);
        var back = body.Heading.Conjugated();
        if (_motion.Bone is { } bone)
        {
            HoldBone(frame.BoneOf(_motion.Target, bone), body.Ground, back);
        }
        else
        {
            HoldEntity(frame, body.Ground, back);
        }
    }

    private static Quaternion FromDegrees(Vector3 degrees) => Quaternion.FromRotation(TransformComponent.RotationOf(degrees));

    private void HoldBone(BoneLocal? local, Vector3 ground, Quaternion back)
    {
        if (local is null)
        {
            return;
        }

        if (local.Position is { } position)
        {
            local.Position = position - ground;
        }

        if (local.Rotation is { } rotation && _motion.Turns)
        {
            local.Rotation = back * rotation;
        }
    }

    private void HoldEntity(ClipFrame frame, Vector3 ground, Quaternion back)
    {
        if (!frame.Values.TryGetValue(_motion.Target, out var values))
        {
            return;
        }

        if (values.GetValueOrDefault("transform.position") is Vector3 position)
        {
            values["transform.position"] = position - ground;
        }

        if (!_motion.Turns)
        {
            return;
        }

        switch (values.GetValueOrDefault("transform.rotation"))
        {
            case Quaternion rotation:
                values["transform.rotation"] = back * rotation;
                break;
            case Vector3 degrees:
                values["transform.rotation"] = TransformComponent.AnglesOf((back * FromDegrees(degrees)).AsRotationMatrix());
                break;
        }
    }

    private (Vector3 Ground, Quaternion Heading) BodyAt(double at)
    {
        var position = _position?.ValueAt(at);
        var rotation = _rotation?.ValueAt(at) switch
        {
            Quaternion turn => turn,
            Vector3 degrees => FromDegrees(degrees),
            _ => null,
        };
        return (
            position is Vector3 spot ? Ground(spot) : Vector3.Zero(),
            rotation is null ? Quaternion.Identity() : Heading(rotation));
    }

    /// <summary>The part of <paramref name="position"/> the body takes.</summary>
    private Vector3 Ground(Vector3 position)
    {
        if (_motion.Rises)
        {
            return position.Clone();
        }

        var up = _motion.Up;
        return position - (up * position.Dot(up));
    }

    /// <summary>
    /// The part of <paramref name="rotation"/> the body takes: its turn about up, or none.
    /// </summary>
    /// <remarks>
    /// The turn is the rotation with everything but its spin about up taken out, which is the same whether
    /// the lean came before the turn or after. A root on its back has no turn to speak of, and gives none
    /// rather than a sudden half turn.
    /// </remarks>
    private Quaternion Heading(Quaternion rotation)
    {
        if (!_motion.Turns)
        {
            return Quaternion.Identity();
        }

        var up = _motion.Up;
        var along = up.Dot(new Vector3(rotation.X, rotation.Y, rotation.Z));
        var turn = new Quaternion(up.X * along, up.Y * along, up.Z * along, rotation.W);
        if (turn.Length < 1e-6)
        {
            return Quaternion.Identity();
        }

        turn.Normalize();
        return turn;
    }
}
