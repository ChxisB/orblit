using Orblit.VectorMath;

namespace Orblit.Rig;

/// <summary>Where a probe found the ground, in the world.</summary>
public sealed class GroundHit
{
    public GroundHit(Vector3 point, Vector3 normal)
    {
        Point = point.Clone();
        Normal = normal.Clone();
    }

    public Vector3 Point { get; }

    /// <summary>Which way the ground faces. Does not have to be a unit vector.</summary>
    public Vector3 Normal { get; }
}

/// <summary>
/// Looks for ground along a line in the world, from <paramref name="from"/> to <paramref name="to"/>.
/// The rig does not know what the ground is made of. A game answers this with whatever it has: a
/// physics ray cast, a heightfield, a flat plane. Return null when nothing is in the way.
/// </summary>
public delegate GroundHit? GroundProbe(Vector3 from, Vector3 to);

/// <summary>One leg that has to stand on the ground.</summary>
/// <param name="thigh">The upper bone of the leg.</param>
/// <param name="shin">The lower bone. Its tail is the ankle, which is what gets placed.</param>
/// <param name="foot">The foot bone, tilted to the slope. Null leaves the foot as it is.</param>
/// <param name="ankleHeight">How far the ankle is above the sole, in the model's units.</param>
/// <param name="soleAxis">
/// Which of the foot bone's own axes points up out of the sole. A foot bone that lies along the ground
/// with no roll has its Z axis pointing down, which is what the default of negative Z is for. A rig
/// that was built differently needs to say so.
/// </param>
public sealed class FootLeg(
    string thigh,
    string shin,
    string? foot = null,
    double ankleHeight = 0,
    TrackAxis soleAxis = TrackAxis.NegativeZ)
{
    public string Thigh { get; } = thigh;

    public string Shin { get; } = shin;

    public string? Foot { get; } = foot;

    public double AnkleHeight { get; } = ankleHeight;

    public TrackAxis SoleAxis { get; } = soleAxis;
}

/// <summary>
/// Puts feet on the ground, whatever shape it is. A walk cycle is made for flat ground. On a ramp one
/// foot hangs in the air and the other is inside the slope. For each leg this asks the probe where the
/// ground is under the animated ankle, moves the ankle to stand on it with two-bone IK, and tilts the
/// foot to the slope.
/// The height the animation lifts a foot above its own ground is kept, so a stride still steps. Only
/// the planted part of a stride is pinned to the ground. The animation's own ground is the plane
/// through the model's origin, across <see cref="Up"/>.
/// When a pelvis is given it is lowered by the most a foot had to go down, so the leg on the low side
/// can still reach.
/// The ankle moves towards its goal at <see cref="Ease"/> seconds, so a step in the ground is a short
/// glide and not a pop. A frame of no time moves it nowhere. Zero ease snaps. The first frame after
/// the modifier is made or reset snaps too, because there is no earlier foot to glide from, so a
/// character that appears on a slope is already standing on it.
/// </summary>
public sealed class FootPlacement : Modifier
{
    private readonly double[] _offsets;
    private double _hips;
    private bool _placed;

    /// <param name="legs">The legs to place.</param>
    /// <param name="probe">Where the ground is.</param>
    /// <param name="pelvis">The hip bone. Null keeps it where the animation has it.</param>
    /// <param name="up">Which way is up, in the model's space. Along Y when left out.</param>
    /// <param name="rise">
    /// How far above the ankle a probe starts, so a foot inside a slope still finds the surface it is
    /// inside.
    /// </param>
    /// <param name="depth">
    /// How far below the ankle a probe looks. A drop further than this is not stood on: the foot follows
    /// the animation.
    /// </param>
    /// <param name="ease">Seconds for the foot to close about two thirds of a gap.</param>
    /// <param name="maxTilt">The most the foot tilts to follow the ground, in radians.</param>
    /// <param name="airborne">
    /// How far above its own ground the animation lifts a foot before it is taken to be in the air, and
    /// left level.
    /// </param>
    // F2: nine tuning values, two of them required, all named at every call, as the Dart constructor is.
    public FootPlacement(
        IReadOnlyList<FootLeg> legs,
        GroundProbe probe,
        string? pelvis = null,
        Vector3? up = null,
        double rise = 0.5,
        double depth = 0.5,
        double ease = 0.1,
        double maxTilt = Math.PI / 4,
        double airborne = 0.1)
    {
        var unit = (up ?? new Vector3(0, 1, 0)).Normalized();
        if (legs.Count == 0)
        {
            throw new ArgumentException("Give at least one leg.", nameof(legs));
        }

        if (unit.Length2 < 0.5)
        {
            throw new ArgumentException("Up needs a direction.", nameof(up));
        }

        foreach (var (name, value) in new[] { (nameof(rise), rise), (nameof(depth), depth), (nameof(ease), ease), (nameof(maxTilt), maxTilt), (nameof(airborne), airborne) })
        {
            if (!double.IsFinite(value) || value < 0)
            {
                throw new ArgumentOutOfRangeException(name, value, "This is 0 or more.");
            }
        }

        Legs = legs;
        Probe = probe;
        Pelvis = pelvis;
        Up = unit;
        Rise = rise;
        Depth = depth;
        Ease = ease;
        MaxTilt = maxTilt;
        Airborne = airborne;
        _offsets = new double[legs.Count];
    }

    public IReadOnlyList<FootLeg> Legs { get; }

    public GroundProbe Probe { get; }

    public string? Pelvis { get; }

    /// <summary>Which way is up, in the model's space. Always a unit vector.</summary>
    public Vector3 Up { get; }

    public double Rise { get; }

    public double Depth { get; }

    public double Ease { get; }

    public double MaxTilt { get; }

    public double Airborne { get; }

    public override void Check(Armature armature)
    {
        foreach (var leg in Legs)
        {
            BoneEdit.RequireBone(armature, leg.Thigh, "so there is no leg to place");
            BoneEdit.RequireBone(armature, leg.Shin, "so there is no leg to place");

            // RequireBone has just shown the bone is there.
            if (armature[leg.Shin]!.Parent != leg.Thigh)
            {
                throw new ArmatureException($"Bone \"{leg.Shin}\" is not a child of \"{leg.Thigh}\", so the two are not one leg.");
            }

            if (leg.Foot is { } foot)
            {
                BoneEdit.RequireBone(armature, foot, "so there is no foot");
            }
        }

        if (Pelvis is { } hips)
        {
            BoneEdit.RequireBone(armature, hips, "so there is no pelvis");
        }
    }

    public override void Solve(Pose pose, ModifierFrame frame)
    {
        var plans = Legs.Select(leg => Look(leg, pose, frame)).ToList();
        var share = Ease <= 0 || !_placed ? 1.0 : 1 - Math.Exp(-frame.Dt / Ease);
        _placed = true;

        for (var i = 0; i < plans.Count; i++)
        {
            _offsets[i] += (plans[i].Offset - _offsets[i]) * share;
        }

        LowerPelvis(pose, plans, share);

        for (var i = 0; i < Legs.Count; i++)
        {
            var leg = Legs[i];

            // A leg with nothing to correct is left to the animation. Re-solving it would only nudge a
            // pose that was already right.
            if (Math.Abs(_offsets[i]) < 1e-9)
            {
                continue;
            }

            TwoBoneIk.Reach(pose, leg.Thigh, leg.Shin, plans[i].Ankle + (Up * _offsets[i]));
            TiltFoot(pose, leg, plans[i]);
        }
    }

    public override void Reset()
    {
        Array.Fill(_offsets, 0.0);
        _hips = 0;
        _placed = false;
    }

    private Plan Look(FootLeg leg, Pose pose, ModifierFrame frame)
    {
        var ankle = pose.TailOf(leg.Shin);
        var found = Probe(frame.ToWorld(ankle + (Up * Rise)), frame.ToWorld(ankle - (Up * Depth)));
        if (found is null)
        {
            return new Plan(ankle);
        }

        var normal = frame.DirectionToModel(found.Normal).Normalized();
        var ground = frame.ToModel(found.Point);

        // Where, straight above or below the ankle, the sole would touch the ground. The sloped ground
        // is a plane, and the ankle stands off it by its own height along the normal.
        var tilt = Math.Max(normal.Dot(Up), 0.2);
        var toGround = (ankle - ground).Dot(normal);
        var lift = Math.Max(0.0, ankle.Dot(Up) - leg.AnkleHeight);
        var offset = ((leg.AnkleHeight - toGround) / tilt) + lift;

        return new Plan(ankle, offset, normal, lift);
    }

    private void LowerPelvis(Pose pose, List<Plan> plans, double share)
    {
        if (Pelvis is not { } hips)
        {
            return;
        }

        var lowest = plans.Aggregate(0.0, (low, plan) => Math.Min(low, plan.Offset));
        _hips += (lowest - _hips) * share;
        if (_hips == 0)
        {
            return;
        }

        var world = pose.WorldOf(hips).Clone();
        world.SetTranslation(world.GetTranslation() + (Up * _hips));
        BoneEdit.PlaceBone(pose, hips, world);
    }

    private void TiltFoot(Pose pose, FootLeg leg, Plan plan)
    {
        var normal = plan.Normal;
        if (leg.Foot is not { } foot || normal is null || MaxTilt <= 0)
        {
            return;
        }

        var planted = 1 - Doubles.Clamp(plan.Lift / Math.Max(Airborne, 1e-9), 0.0, 1.0);
        if (planted <= 0)
        {
            return;
        }

        var sole = leg.SoleAxis.Of(pose.WorldOf(foot));
        var full = Ik.RotationBetween(sole, normal);
        var angle = Ik.TurnAngle(full);
        var allowed = Math.Min(angle, MaxTilt) * planted;
        if (angle < 1e-9)
        {
            return;
        }

        BoneEdit.TurnBone(pose, foot, Ik.SlerpShortest(Quaternion.Identity(), full, allowed / angle));
    }

    /// <summary>What a probe found under one ankle.</summary>
    /// <param name="ankle">Where the ankle is.</param>
    /// <param name="offset">How far the ankle has to move along up to stand on the ground.</param>
    /// <param name="normal">The ground's normal in the model's space, or null when none was found.</param>
    /// <param name="lift">How far the animation has lifted the foot above its own ground.</param>
    private sealed class Plan(Vector3 ankle, double offset = 0, Vector3? normal = null, double lift = 0)
    {
        public Vector3 Ankle { get; } = ankle;

        public double Offset { get; } = offset;

        public Vector3? Normal { get; } = normal;

        public double Lift { get; } = lift;
    }
}
