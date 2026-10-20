using Orblit.VectorMath;

namespace Orblit.Rig;

/// <summary>
/// Holds bones to a top speed, so a snap in the target is a turn. A head that follows a target whips
/// round the moment the target jumps. This remembers where each bone was left last frame and lets it
/// travel no more than <see cref="TurnSpeed"/> radians a second, or <see cref="MoveSpeed"/> units a
/// second, towards where it is asked to be.
/// The first frame passes as it is. A frame of no time passes nothing on: a bone stays where it was
/// left, because no time went by for it to move in.
/// </summary>
public sealed class SpeedLimit : Modifier
{
    private readonly Dictionary<string, PoseTransform> _left = [];

    /// <param name="bones">The bones to hold.</param>
    /// <param name="turnSpeed">The most a bone may turn, in radians a second. Null leaves turns alone.</param>
    /// <param name="moveSpeed">The most a bone's pose may move, in units a second. Null leaves it.</param>
    public SpeedLimit(IReadOnlyList<string> bones, double? turnSpeed = null, double? moveSpeed = null)
    {
        if (bones.Count == 0)
        {
            throw new ArgumentException("Give at least one bone.", nameof(bones));
        }

        foreach (var (name, speed) in new[] { (nameof(turnSpeed), turnSpeed), (nameof(moveSpeed), moveSpeed) })
        {
            if (speed is { } value && (value < 0 || !double.IsFinite(value)))
            {
                throw new ArgumentOutOfRangeException(name, value, "A speed is 0 or more.");
            }
        }

        Bones = bones;
        TurnSpeed = turnSpeed;
        MoveSpeed = moveSpeed;
    }

    public IReadOnlyList<string> Bones { get; }

    public double? TurnSpeed { get; }

    public double? MoveSpeed { get; }

    public override void Check(Armature armature)
    {
        foreach (var bone in Bones)
        {
            BoneEdit.RequireBone(armature, bone, "so there is nothing to slow");
        }
    }

    public override void Solve(Pose pose, ModifierFrame frame)
    {
        foreach (var bone in Bones)
        {
            var now = pose[bone];
            if (_left.TryGetValue(bone, out var left))
            {
                Hold(now, left, frame.Dt);
            }

            _left[bone] = now.Copy();
        }
    }

    public override void Reset() => _left.Clear();

    private void Hold(PoseTransform now, PoseTransform left, double dt)
    {
        if (TurnSpeed is { } turn)
        {
            var gap = Ik.TurnAngle(left.Rotation.Conjugated() * now.Rotation);
            var allowed = turn * dt;
            if (gap > allowed)
            {
                now.Rotation = Ik.SlerpShortest(left.Rotation, now.Rotation, allowed / gap);
            }
        }

        if (MoveSpeed is { } move)
        {
            var step = now.Location - left.Location;
            var allowed = move * dt;
            if (step.Length > allowed)
            {
                now.Location = left.Location + (step.Normalized() * allowed);
            }
        }
    }
}
