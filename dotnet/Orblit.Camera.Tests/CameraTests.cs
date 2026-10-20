using Orblit.VectorMath;

namespace Orblit.Camera.Tests;

public class CameraTests
{
    private static readonly Lens Sixty = new(FieldOfView: 60);

    private const double Widescreen = 16.0 / 9;

    private const double Frame = 1.0 / 60;

    private static void Near(double expected, double actual, double tolerance = 1e-6) =>
        Assert.True(Math.Abs(expected - actual) <= tolerance, $"expected {expected:R} within {tolerance}, got {actual:R}");

    private static Vector3 Forward(Quaternion rotation) => Rotations.RotateVector(rotation, new Vector3(0, 0, -1));

    private static VirtualCamera Camera(string name, long priority, Vector3 at) =>
        new(name, priority: priority, position: at);

    private static Vector3 PathAt(double s) =>
        new(Math.Sin(s * 0.45) * 9, 0, Math.Sin(s * 0.9) * 5.5);

    private static Vector3 Follow(FollowBody body, Vector3 position, FixedTarget target) =>
        body.Solve(position, target, Quaternion.Identity(), new Lens(), Widescreen, Frame);

    // rotation maths

    [Fact]
    public void ACameraLookingDownZHasThatAsItsForward()
    {
        var rotation = Rotations.LookRotation(new Vector3(0, 0, -1));
        var forward = Forward(rotation);
        Near(0, forward.X, 1e-9);
        Near(-1, forward.Z, 1e-9);
    }

    [Fact]
    public void LookingAtAPointTurnsTheCameraTowardsIt()
    {
        // Camera at the origin, target to its right.
        var rotation = Rotations.LookRotation(new Vector3(1, 0, 0));
        var forward = Forward(rotation);
        Near(1, forward.X, 1e-9);
    }

    [Fact]
    public void LookingStraightDownDoesNotProduceARolledCamera()
    {
        // Parallel to the up vector, the case that spikes if it is not handled.
        var rotation = Rotations.LookRotation(new Vector3(0, -1, 0));
        var forward = Forward(rotation);
        Near(-1, forward.Y, 1e-6);
        Near(1, forward.Length, 1e-9);
    }

    [Fact]
    public void ABlendTakesTheShortWayRound()
    {
        // Almost a full turn one way is a small turn the other. Halfway along
        // the short arc the camera still faces roughly where it started; halfway
        // along the long one it would be facing behind itself.
        var a = Quaternion.AxisAngle(new Vector3(0, 1, 0), 0);
        var b = Quaternion.AxisAngle(new Vector3(0, 1, 0), (Math.PI * 2) - 0.175);

        var half = Rotations.SlerpShortest(a, b, 0.5);
        var forward = Forward(half);

        Assert.True(forward.Z < 0, "the long way round would have turned it to face +Z");
        Assert.True(Math.Abs(forward.X) < 0.2, "and only a few degrees off, not most of a turn");
    }

    // projection

    [Fact]
    public void ATargetDeadAheadLandsAtTheCentre()
    {
        var seen = Framing.Project(
            new Vector3(0, 0, 5),
            Rotations.LookRotation(new Vector3(0, 0, -1)),
            Vector3.Zero(),
            Sixty,
            Widescreen);
        Assert.True(seen.InFront);
        Near(0, seen.X, 1e-9);
        Near(0, seen.Y, 1e-9);
    }

    [Fact]
    public void ATargetBehindTheCameraIsReportedAsBehind()
    {
        var seen = Framing.Project(
            new Vector3(0, 0, 5),
            Rotations.LookRotation(new Vector3(0, 0, -1)),
            new Vector3(0, 0, 20),
            Sixty,
            Widescreen);
        Assert.False(
            seen.InFront,
            "projection folds over behind the camera, so a point there would otherwise be reported at a plausible screen position");
    }

    [Fact]
    public void PlacingAPointPutsItExactlyWhereItWasAskedFor()
    {
        // The round trip that says the whole composer is trustworthy: if solving
        // for a screen position and then projecting does not agree, every dead
        // zone in the engine is subtly wrong.
        var camera = Vector3.Zero();
        foreach (var (ndcX, ndcY) in new[] { (0.0, 0.0), (0.3, -0.2), (-0.6, 0.45), (0.9, 0.9) })
        {
            var target = new Vector3(2, 1, -8);
            var rotation = Framing.RotationPlacing(camera, target, ndcX, ndcY, Sixty, Widescreen);
            var seen = Framing.Project(camera, rotation, target, Sixty, Widescreen);

            Assert.True(seen.InFront);
            Assert.True(Math.Abs(ndcX - seen.X) <= 1e-6, $"x for ({ndcX}, {ndcY})");
            Assert.True(Math.Abs(ndcY - seen.Y) <= 1e-6, $"y for ({ndcX}, {ndcY})");
        }
    }

    // damping

    [Fact]
    public void ClosesMostOfTheGapOverItsStatedTime()
    {
        // A damping of one second should be most of the way there after one.
        Near(0.632, Smoothing.Factor(1, 1), 0.001);
    }

    [Fact]
    public void LandsInTheSamePlaceAtAnyFramerate()
    {
        // The property that matters: a camera must not drift when the machine
        // is busy, or the bug is unreproducible by definition.
        var coarse = 0.0;
        for (var i = 0; i < 30; i++)
        {
            coarse = Smoothing.Damp(coarse, 10, 0.5, 1.0 / 30);
        }

        var fine = 0.0;
        for (var i = 0; i < 240; i++)
        {
            fine = Smoothing.Damp(fine, 10, 0.5, 1.0 / 240);
        }

        Near(fine, coarse, 0.02);
    }

    [Fact]
    public void ZeroDampingArrivesImmediately() => Assert.Equal(10, Smoothing.Damp(0, 10, 0, Frame));

    // lenses

    [Fact]
    public void FieldOfViewBlendsLogarithmically()
    {
        // Halfway between 20 and 80 degrees is 40, not 50: the perceived change
        // is multiplicative, and a linear blend reads as an accelerating zoom.
        var middle = Lens.Lerp(new Lens(FieldOfView: 20), new Lens(FieldOfView: 80), 0.5);
        Near(40, middle.FieldOfView, 0.001);
    }

    // bodies

    [Fact]
    public void AFollowCameraReachesItsOffset()
    {
        var body = new FollowBody(new Vector3(0, 2, 6), damping: Vector3.Zero());
        var target = new FixedTarget(new Vector3(10, 0, 0));
        var position = Follow(body, Vector3.Zero(), target);
        Near(10, position.X, 1e-6);
        Near(2, position.Y, 1e-6);
        Near(6, position.Z, 1e-6);
    }

    [Fact]
    public void DampingMakesItArriveLateRatherThanNotAtAll()
    {
        var body = new FollowBody(new Vector3(0, 0, 5), damping: Vector3.All(0.5));
        var target = new FixedTarget(new Vector3(100, 0, 0));

        var position = Vector3.Zero();
        position = Follow(body, position, target);
        Assert.True(position.X > 0);
        Assert.True(position.X < 100, "it should still be catching up");

        for (var i = 0; i < 600; i++)
        {
            position = Follow(body, position, target);
        }

        Near(100, position.X, 0.01);
    }

    [Fact]
    public void HeadingBindingIgnoresATargetThatIsPitching()
    {
        var body = new FollowBody(new Vector3(0, 0, 5), FollowBinding.TargetHeading, Vector3.Zero());

        // Target tipped forwards; the camera should not tip with it.
        var target = new FixedTarget(Vector3.Zero(), Quaternion.AxisAngle(new Vector3(1, 0, 0), 0.6));
        var position = Follow(body, Vector3.Zero(), target);
        Assert.True(
            Math.Abs(position.Y) <= 1e-6,
            "a camera that tips when a character walks uphill is the classic third-person complaint");
    }

    [Fact]
    public void AnOrbitHoldsItsRadius()
    {
        var body = new OrbitBody(radius: 7, elevation: 30, damping: 0);
        var target = new FixedTarget(new Vector3(1, 2, 3));
        var position = body.Solve(Vector3.Zero(), target, Quaternion.Identity(), new Lens(), Widescreen, Frame);
        Near(7, (position - target.Position).Length, 1e-6);
    }

    // aiming

    [Fact]
    public void AHardLookAtCentresTheTarget()
    {
        var aim = new HardLookAt();
        var camera = new Vector3(0, 0, 5);
        var target = new FixedTarget(new Vector3(3, 1, 0));

        var rotation = aim.Solve(Quaternion.Identity(), camera, target, Sixty, Widescreen, Frame);
        var seen = Framing.Project(camera, rotation, target.Position, Sixty, Widescreen);

        Near(0, seen.X, 1e-6);
        Near(0, seen.Y, 1e-6);
    }

    [Fact]
    public void AComposerIgnoresMovementInsideTheDeadZone()
    {
        var aim = new ComposerAim(deadZoneWidth: 0.3, deadZoneHeight: 0.3);
        var camera = new Vector3(0, 0, 5);

        // Start centred, then nudge the target a little.
        var centred = Rotations.LookRotation(new Vector3(0, 0, -1));
        var target = new FixedTarget(new Vector3(0.4, 0, 0));

        var rotation = aim.Solve(centred, camera, target, Sixty, Widescreen, Frame);

        // The camera should not have moved: the whole point of a dead zone is
        // that small motion does not make the frame twitch.
        Near(centred.X, rotation.X, 1e-9);
        Near(centred.Y, rotation.Y, 1e-9);
        Near(centred.Z, rotation.Z, 1e-9);
    }

    [Fact]
    public void AComposerFollowsOnceTheTargetLeavesTheDeadZone()
    {
        var aim = new ComposerAim(deadZoneWidth: 0.1, deadZoneHeight: 0.1, damping: 0);
        var camera = new Vector3(0, 0, 5);
        var target = new FixedTarget(new Vector3(4, 0, 0));

        var rotation = aim.Solve(Rotations.LookRotation(new Vector3(0, 0, -1)), camera, target, Sixty, Widescreen, Frame);
        var seen = Framing.Project(camera, rotation, target.Position, Sixty, Widescreen);

        // Brought back to the dead zone edge, not to the centre: the rectangle
        // describes where the subject may sit, not where it must.
        Near(0.1, seen.X, 1e-4);
    }

    [Fact]
    public void AComposerNeverLetsTheTargetLeaveTheSoftZone()
    {
        var aim = new ComposerAim(
            deadZoneWidth: 0.05,
            deadZoneHeight: 0.05,
            softZoneWidth: 0.3,
            softZoneHeight: 0.3,
            // Heavy lag, so without the clamp the target would escape.
            damping: 10);
        var camera = new Vector3(0, 0, 5);
        var target = new FixedTarget(new Vector3(6, 0, 0));

        var rotation = aim.Solve(Rotations.LookRotation(new Vector3(0, 0, -1)), camera, target, Sixty, Widescreen, Frame);
        var seen = Framing.Project(camera, rotation, target.Position, Sixty, Widescreen);

        Assert.True(Math.Abs(seen.X) <= 0.3 + 1e-4, "the soft zone is a promise, not a preference");
    }

    [Fact]
    public void AComposerRecoversATargetThatGotBehindTheCamera()
    {
        var aim = new ComposerAim(damping: 0.5);
        var camera = Vector3.Zero();
        var target = new FixedTarget(new Vector3(0, 0, 10));

        var rotation = aim.Solve(Rotations.LookRotation(new Vector3(0, 0, -1)), camera, target, Sixty, Widescreen, Frame);
        var seen = Framing.Project(camera, rotation, target.Position, Sixty, Widescreen);

        Assert.True(seen.InFront);
        Near(0, seen.X, 1e-6);
    }

    [Fact]
    public void APointOfViewAimClampsItsPitch()
    {
        var aim = new PovAim(pitch: 300, maximumPitch: 85);
        var rotation = aim.Solve(Quaternion.Identity(), Vector3.Zero(), null, Sixty, Widescreen, Frame);
        var forward = Forward(rotation);

        // Clamped short of straight up, where the horizon would flip.
        Assert.True(forward.Y < 1.0);
    }

    // the brain

    [Fact]
    public void PicksTheHighestPriority()
    {
        var brain = new CameraBrain();
        brain.Add(Camera("wide", 10, new Vector3(0, 0, 20)));
        brain.Add(Camera("close", 20, new Vector3(0, 0, 2)));
        brain.Update(Frame);

        Assert.Equal("close", brain.Live?.Name);
        Near(2, brain.State.Position.Z, 1e-6);
    }

    [Fact]
    public void ADisabledCameraIsNotChosenHoweverHighItsPriority()
    {
        var brain = new CameraBrain();
        var close = Camera("close", 100, new Vector3(0, 0, 2));
        close.Enabled = false;
        brain.Add(Camera("wide", 10, new Vector3(0, 0, 20)));
        brain.Add(close);
        brain.Update(Frame);

        Assert.Equal("wide", brain.Live?.Name);
    }

    [Fact]
    public void RaisingAPriorityIsHowYouCut()
    {
        var brain = new CameraBrain(blends: new BlendTable(Blend.Cut()));
        var wide = Camera("wide", 10, new Vector3(0, 0, 20));
        var close = Camera("close", 5, new Vector3(0, 0, 2));
        brain.Add(wide);
        brain.Add(close);
        brain.Update(Frame);
        Assert.Equal("wide", brain.Live?.Name);

        // Nothing was moved; a number changed.
        close.Priority = 50;
        brain.Update(Frame);
        Assert.Equal("close", brain.Live?.Name);
        Near(2, brain.State.Position.Z, 1e-6);
    }

    [Fact]
    public void BlendsOverItsDurationRatherThanJumping()
    {
        var brain = new CameraBrain(blends: new BlendTable(new Blend(BlendStyle.Linear, 1.0)));
        var wide = Camera("wide", 10, new Vector3(0, 0, 20));
        var close = Camera("close", 5, new Vector3(0, 0, 0));
        brain.Add(wide);
        brain.Add(close);
        brain.Update(Frame);

        close.Priority = 50;
        brain.Update(0.5);

        Assert.True(brain.IsBlending);

        // Halfway through a linear blend from z=20 to z=0.
        Near(10, brain.State.Position.Z, 0.5);

        brain.Update(0.6);
        Assert.False(brain.IsBlending);
        Near(0, brain.State.Position.Z, 1e-6);
    }

    [Fact]
    public void InterruptingABlendContinuesFromWhatIsOnScreen()
    {
        var brain = new CameraBrain(blends: new BlendTable(new Blend(BlendStyle.Linear, 1.0)));
        var a = Camera("a", 30, new Vector3(0, 0, 0));
        var b = Camera("b", 20, new Vector3(0, 0, 100));
        var c = Camera("c", 10, new Vector3(0, 0, 50));
        brain.Add(a);
        brain.Add(b);
        brain.Add(c);
        brain.Update(Frame);

        b.Priority = 40;
        brain.Update(0.5);
        var midBlend = brain.State.Position.Z;
        Near(50, midBlend, 5);

        // Cut away mid-transition; the new blend must start from here, not from
        // where the interrupted one began.
        c.Priority = 90;
        brain.Update(0.0001);
        Assert.True(
            Math.Abs(midBlend - brain.State.Position.Z) <= 1,
            "a jump here is the visible artefact of blending from a camera rather than from the picture");
    }

    [Fact]
    public void ABlendTableCanSingleOutOneTransition()
    {
        var table = new BlendTable(new Blend(BlendStyle.Linear, 2));
        table.Set("wide", "close", Blend.Cut());

        Assert.Equal(0, table.Between("wide", "close").Duration);
        Assert.Equal(2, table.Between("close", "wide").Duration);
    }

    [Fact]
    public void ANewCameraStartsWhereItWantsToBeNotWhereItWas()
    {
        var brain = new CameraBrain();
        var follower = new VirtualCamera(
            "follow",
            priority: 10,
            follow: new FixedTarget(new Vector3(100, 0, 0)),
            body: new FollowBody(new Vector3(0, 0, 5), damping: Vector3.All(2)));
        brain.Add(follower);
        brain.Update(Frame);

        // Snapped on add: a camera that eased in from the origin on its first
        // frame would swoop across the level for no reason.
        Near(100, brain.State.Position.X, 0.01);
    }

    // noise

    [Fact]
    public void IsTheSameEveryTimeForTheSameMoment()
    {
        var noise = new CameraNoise(seed: 7);
        Assert.Equal(noise.PositionAt(3.5).X, noise.PositionAt(3.5).X);
    }

    [Fact]
    public void StaysInsideItsAmplitude()
    {
        var noise = new CameraNoise(positionAmplitude: Vector3.All(0.1));
        for (var t = 0.0; t < 20; t += 0.05)
        {
            Assert.True(Math.Abs(noise.PositionAt(t).X) <= 0.1 + 1e-9, $"at {t}");
        }
    }

    [Fact]
    public void TwoCamerasWithDifferentSeedsDoNotSwayTogether()
    {
        var a = new CameraNoise(seed: 1).PositionAt(2.0);
        var b = new CameraNoise(seed: 2).PositionAt(2.0);
        Assert.True((a - b).Length > 1e-6);
    }

    // a camera that has to survive a frame

    [Fact]
    public void ASurfaceWithNoSizeYetDoesNotPoisonTheCamera()
    {
        // A host reads the aspect off its own surface, and a surface before
        // layout is zero by zero, which is a NaN, not a small number. One of
        // those through the projection comes back as a NaN rotation, and the
        // next frame damps towards it from a value that is already NaN. Nothing
        // recovers: one frame during startup points the camera nowhere for the
        // rest of the run.
        var subject = new FixedTarget(new Vector3(0, 0, -10));
        var brain = new CameraBrain();
        brain.Add(new VirtualCamera(
            "Chase",
            lookAt: subject,
            body: new StaticBody(Vector3.Zero()),
            aim: new ComposerAim()));
        brain.Snap();

        brain.Aspect = double.NaN;
        brain.Aspect = double.PositiveInfinity;
        brain.Aspect = 0;
        brain.Aspect = -2;

        for (var i = 0; i < 5; i++)
        {
            subject.Position = new Vector3(i * 0.4, 0, -10);
            brain.Update(Frame);
        }

        var forward = brain.State.Forward;
        Assert.False(double.IsNaN(forward.X));
        Assert.False(double.IsNaN(forward.Y));
        Assert.False(double.IsNaN(forward.Z));
        Assert.Equal(16.0 / 9, brain.Aspect);
    }

    [Fact]
    public void ARealRatioIsStillTaken()
    {
        var brain = new CameraBrain { Aspect = 4.0 / 3 };
        Near(4.0 / 3, brain.Aspect, 1e-9);
    }

    [Fact]
    public void AComposedCameraStaysLevel()
    {
        // The shortest rotation from the centre of the frame to where the
        // subject should sit is the obvious construction, and it rolls: an
        // offset that is both sideways and vertical turns about an oblique
        // axis, and the part of that along the view direction is roll. It then
        // accumulates, because the next frame damps towards a rotation that is
        // already leaning. This subject wanders diagonally on purpose.
        var subject = new FixedTarget(Vector3.Zero());

        var brain = new CameraBrain();
        brain.Add(new VirtualCamera(
            "Chase",
            follow: subject,
            lookAt: subject,
            body: new FollowBody(new Vector3(0, 2.4, 7), damping: new Vector3(0.35, 0.18, 0.5)),
            // Off centre both ways, which is what makes the offset oblique.
            aim: new ComposerAim(screenX: 0.42, screenY: 0.45, damping: 0.4)));
        brain.Snap();

        var worst = 0.0;
        var seconds = 0.0;
        for (var i = 0; i < 600; i++)
        {
            seconds += Frame;
            var at = PathAt(seconds);
            subject.Position = at;
            subject.Rotation = Rotations.LookRotation(PathAt(seconds + 0.12) - at);
            brain.Update(Frame);
            worst = Math.Max(worst, Math.Abs(brain.State.Right.Y));
        }

        // A tenth of this is a degree. It used to reach nineteen.
        Assert.True(worst < 0.02, $"worst roll {worst}");
    }

    [Fact]
    public void FollowingASubjectOnAPathNeverProducesANaN()
    {
        var subject = new FixedTarget(Vector3.Zero());

        var brain = new CameraBrain();
        brain.Add(new VirtualCamera(
            "Chase",
            follow: subject,
            lookAt: subject,
            body: new FollowBody(new Vector3(0, 2.4, 7), damping: new Vector3(0.35, 0.18, 0.5)),
            aim: new ComposerAim(screenY: 0.45, damping: 0.4)));
        brain.Snap();

        var seconds = 0.0;
        for (var i = 0; i < 400; i++)
        {
            seconds += Frame;
            var at = PathAt(seconds);
            subject.Position = at;
            subject.Rotation = Rotations.LookRotation(PathAt(seconds + 0.12) - at);
            brain.Update(Frame);
            Assert.True(Math.Abs(1 - brain.State.Forward.Length) <= 1e-6, $"frame {i}");
        }
    }

    // first person

    [Fact]
    public void LooksExactlyWhereTheTargetLooksWithNoLagByDefault()
    {
        // The whole contract. A camera on a character's eyes that composes, or
        // damps, or frames, puts the view somewhere other than where they are
        // looking, and then the player is aiming at one thing and shooting at
        // another.
        var facing = Rotations.LookRotation(new Vector3(1, 0, -1));
        var target = new FixedTarget(Vector3.Zero(), facing);
        var aim = new HeadAim();

        var got = aim.Solve(Quaternion.Identity(), Vector3.Zero(), target, new Lens(), Widescreen, Frame);

        var wanted = Forward(facing);
        var looking = Forward(got);
        Assert.True((looking - wanted).Length < 1e-6);
    }

    [Fact]
    public void AVehicleCanLagBehindItsOwnChassis()
    {
        var target = new FixedTarget(Vector3.Zero(), Rotations.LookRotation(new Vector3(1, 0, 0)));
        var aim = new HeadAim(damping: 0.5);

        var got = aim.Solve(Quaternion.Identity(), Vector3.Zero(), target, new Lens(), Widescreen, Frame);

        // Started to turn, but nowhere near arrived.
        var looking = Forward(got);
        Assert.True(looking.X > 0.0);
        Assert.True(looking.X < 0.5);
    }

    // a game seen flat on

    private static Vector3 SolveOnce(
        ScreenFollowBody body,
        Vector3 at,
        Vector3 subject,
        Lens? lens = null,
        double delta = 1e6) =>
        // Looking down -Z, level, which is what a flat game is seen from.
        body.Solve(at, new FixedTarget(subject), Quaternion.Identity(), lens ?? Lens.Flat(10), Widescreen, delta);

    [Fact]
    public void AFlatLensDoesNotCareHowFarAwayAThingIs()
    {
        // The whole of what orthographic means, and the reason a camera over a
        // flat game has to move rather than turn: turning sweeps a perspective
        // view across the world and does nothing at all to a flat one.
        var lens = Lens.Flat(10);
        var near = Framing.Project(Vector3.Zero(), Quaternion.Identity(), new Vector3(2, 0, -5), lens, 1);
        var far = Framing.Project(Vector3.Zero(), Quaternion.Identity(), new Vector3(2, 0, -50), lens, 1);

        Near(far.X, near.X, 1e-9);
        Near(2.0 / 5, near.X, 1e-9);
    }

    [Fact]
    public void ASubjectInsideTheDeadZoneIsLeftAlone()
    {
        // The point of a dead zone. A camera that corrects for every step of a
        // walking character reads as a nervous operator rather than a steady
        // one, and in two dimensions it makes the whole background jitter.
        var body = new ScreenFollowBody(deadZoneWidth: 0.2, deadZoneHeight: 0.2);
        var at = new Vector3(0, 0, 10);

        // A tenth of the frame across is well inside a fifth.
        var after = SolveOnce(body, at, new Vector3(0.4, 0, 0));
        Assert.True((after - at).Length < 1e-6);
    }

    [Fact]
    public void ASubjectPastTheDeadZoneIsFollowedByMoving()
    {
        var body = new ScreenFollowBody(deadZoneWidth: 0.1, deadZoneHeight: 0.1);
        var at = new Vector3(0, 0, 10);
        var after = SolveOnce(body, at, new Vector3(6, 0, 0));

        // Moved towards it along the frame's own right, and not turned: the
        // rotation is not this body's to change.
        Assert.True(after.X > 0.5);
        Near(0, after.Y, 1e-6);
    }

    [Fact]
    public void TheShotDistanceIsHeldHoweverFarTheFramingMovesIt()
    {
        var body = new ScreenFollowBody(distance: 10, deadZoneWidth: 0);
        var after = SolveOnce(body, new Vector3(0, 0, 10), new Vector3(7, 3, 0));

        // Still ten in front of the subject along the view direction, so
        // framing sideways never drifts the camera in or out.
        Near(10, after.Z - 0.0, 1e-5);
    }

    [Fact]
    public void BoundsAreAWallNotAPreference()
    {
        // What stops a camera following a character to the edge of a level and
        // showing whatever is past it.
        var body = new ScreenFollowBody(
            deadZoneWidth: 0,
            bounds: (Minimum: new Vector3(-2, -2, -100), Maximum: new Vector3(2, 2, 100)));
        var after = SolveOnce(body, new Vector3(0, 0, 10), new Vector3(40, 0, 0));
        Assert.True(after.X <= 2.0000001);
    }

    [Fact]
    public void ItHandsOutTheZonesItFramesBy()
    {
        var body = new ScreenFollowBody(screenX: 0.5, deadZoneWidth: 0.15);
        Near(0.3, body.Guides.Dead.Width, 1e-9);

        // And a camera using it reports them, since the framing is the body's
        // here rather than the aim's.
        var camera = new VirtualCamera(
            "Flat",
            lookAt: new FixedTarget(Vector3.Zero()),
            body: body,
            aim: new StaticAim(Quaternion.Identity()),
            lens: Lens.Flat(10));
        Assert.NotNull(camera.Guides);
        Near(0.3, camera.Guides.Dead.Width, 1e-9);
    }

    [Fact]
    public void AFlatLensBlendsItsHeightNotItsAngle()
    {
        var near = Lens.Flat(5);
        var far = Lens.Flat(20);
        var middle = Lens.Lerp(near, far, 0.5);

        Assert.True(middle.Orthographic);

        // Through the logarithm, so a zoom looks even: halfway between five and
        // twenty is ten, not twelve and a half.
        Near(10, middle.Height, 1e-6);
    }

    // the guides

    [Fact]
    public void AComposerHandsOutTheZonesItIsActuallyUsing()
    {
        var aim = new ComposerAim(
            screenX: 0.5,
            screenY: 0.4,
            deadZoneWidth: 0.1,
            deadZoneHeight: 0.15,
            softZoneWidth: 0.4,
            softZoneHeight: 0.3);

        var guides = aim.Guides;

        // The widths are half-extents in normalised coordinates, where the
        // frame runs from minus one to one. A half-extent of a tenth is
        // therefore a fifth of the frame across. Get that factor wrong and
        // the box drawn is half the one the camera is using, which is worse
        // than drawing none.
        Near(0.2, guides.Dead.Width, 1e-9);
        Near(0.3, guides.Dead.Height, 1e-9);
        Near(0.8, guides.Soft.Width, 1e-9);

        // Centred on where the subject is meant to sit, not on the frame.
        Near(0.5, guides.Dead.Left + (guides.Dead.Width / 2), 1e-9);
        Near(0.4, guides.Dead.Top + (guides.Dead.Height / 2), 1e-9);
        Assert.Equal(0.4, guides.ScreenY);
    }

    [Fact]
    public void TheDeadZoneSitsInsideTheSoftZone()
    {
        var guides = new ComposerAim().Guides;
        Assert.True(guides.Soft.Contains(guides.Dead.Left, guides.Dead.Top));
        Assert.True(guides.Soft.Contains(guides.Dead.Right, guides.Dead.Bottom));
    }

    [Fact]
    public void AnAimWithNothingToComposeSaysSoRatherThanDrawingADot()
    {
        Assert.Null(new HardLookAt().Guides);
        Assert.Null(new StaticAim(Quaternion.Identity()).Guides);
        Assert.Null(new PovAim().Guides);
    }

    [Fact]
    public void TheZonesMoveWithTheScreenPosition()
    {
        var left = new ComposerAim(screenX: 0.25).Guides;
        var right = new ComposerAim(screenX: 0.75).Guides;
        Near(0.5, right.Dead.Left - left.Dead.Left, 1e-9);
        Near(left.Dead.Width, right.Dead.Width, 1e-9);
    }
}
