using Orblit.VectorMath;

namespace Orblit.Rig;

/// <summary>
/// Turns a bone to face a point, no further than a limit, and easing there. The turn is measured from
/// where the clip had the bone pointing. A head that nods in the animation keeps nodding, and the
/// look-at is added to it. The limit is on that added turn, so a head can never be asked to face
/// backwards, and a target behind the character is looked at as far round as the limit goes.
/// It takes the shortest turn to the target and leaves the bone's roll alone, so a bone does not spin
/// as the target passes overhead.
/// While <see cref="Ease"/> is above zero the turn approaches its goal and does not jump to it, and a
/// target that goes away is let go of the same way. The easing is exponential: the gap closes by about
/// two thirds in <see cref="Ease"/> seconds.
/// </summary>
public sealed class LookAt : Modifier
{
    private Quaternion? _turn;

    /// <param name="bone">The bone that turns.</param>
    /// <param name="target">What it faces, in the model's space. Null lets the turn go.</param>
    /// <param name="axis">Which of the bone's own axes points at the target.</param>
    /// <param name="limit">The most the bone may turn from where the clip had it, in radians.</param>
    /// <param name="ease">Seconds for the turn to close by about two thirds. Zero snaps.</param>
    public LookAt(string bone, ModelPoint target, TrackAxis axis = TrackAxis.Y, double limit = Math.PI / 2, double ease = 0)
    {
        if (limit < 0 || !double.IsFinite(limit))
        {
            throw new ArgumentOutOfRangeException(nameof(limit), limit, "A limit is an angle of 0+.");
        }

        if (ease < 0 || !double.IsFinite(ease))
        {
            throw new ArgumentOutOfRangeException(nameof(ease), ease, "Easing takes 0 or more seconds");
        }

        Bone = bone;
        Target = target;
        Axis = axis;
        Limit = limit;
        Ease = ease;
    }

    public string Bone { get; }

    public ModelPoint Target { get; }

    public TrackAxis Axis { get; }

    public double Limit { get; }

    public double Ease { get; }

    public override void Check(Armature armature) =>
        BoneEdit.RequireBone(armature, Bone, "so there is nothing to turn");

    public override void Solve(Pose pose, ModifierFrame frame)
    {
        var goal = GoalTurn(pose, frame);
        var held = _turn ?? Quaternion.Identity();

        // Time that has not passed moves nothing, so a pose solved once with easing on stays where it
        // was. Snapping is what zero asks for.
        var share = Ease <= 0 ? 1.0 : 1 - Math.Exp(-frame.Dt / Ease);
        var turn = Ik.SlerpShortest(held, goal, share);
        _turn = turn;

        if (Ik.TurnAngle(turn) < 1e-9)
        {
            return;
        }

        BoneEdit.TurnBone(pose, Bone, turn);
    }

    public override void Reset() => _turn = null;

    private Quaternion GoalTurn(Pose pose, ModifierFrame frame)
    {
        var point = Target(pose, frame);
        if (point is null)
        {
            return Quaternion.Identity();
        }

        var toward = point - pose.HeadOf(Bone);
        if (toward.Length2 < 1e-12)
        {
            return Quaternion.Identity();
        }

        var goal = Ik.RotationBetween(Axis.Of(pose.WorldOf(Bone)), toward);
        var angle = Ik.TurnAngle(goal);
        return angle <= Limit ? goal : Ik.SlerpShortest(Quaternion.Identity(), goal, Limit / angle);
    }
}
