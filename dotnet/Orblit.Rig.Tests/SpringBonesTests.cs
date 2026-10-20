namespace Orblit.Rig.Tests;

public class SpringBonesTests
{
    /// <summary>An anchor bone and two hair bones hung from it, a metre each, along +Y.</summary>
    private static Armature Hair() => new(
    [
        new Bone("anchor", new Vector3(0, 0, 0), new Vector3(0, 1, 0)),
        new Bone("h0", new Vector3(0, 1, 0), new Vector3(0, 2, 0), parent: "anchor", connected: true),
        new Bone("h1", new Vector3(0, 2, 0), new Vector3(0, 3, 0), parent: "h0", connected: true),
    ]);

    /// <summary>The same chain laid out along +X from the origin, so gravity can swing it.</summary>
    private static Armature Fringe() => new(
    [
        new Bone("anchor", new Vector3(0, 0, 0), new Vector3(1, 0, 0)),
        new Bone("h0", new Vector3(1, 0, 0), new Vector3(2, 0, 0), parent: "anchor", connected: true),
        new Bone("h1", new Vector3(2, 0, 0), new Vector3(3, 0, 0), parent: "h0", connected: true),
    ]);

    /// <summary>A chain that gravity alone pulls, and that has to settle in 10 seconds.</summary>
    private static SpringBones Loose(IReadOnlyList<SpringCollider>? colliders = null) =>
        new(bones: ["h0", "h1"], stiffness: 0, drag: 4, colliders: colliders);

    private static Pose Settle(IReadOnlyList<SpringCollider> colliders, Vector3? anchor = null)
    {
        var pose = new Pose(Fringe());
        var stack = new ModifierStack();
        stack.Add("hair", Loose(colliders));
        Frame(pose, stack, anchor: anchor);
        Run(pose, stack, frames: 600, anchor: anchor);
        return pose;
    }

    /// <summary>One frame the way a game runs it: the clip writes the anchor, the pose is evaluated, then the stack runs.</summary>
    private static void Frame(Pose pose, ModifierStack stack, Vector3? anchor = null, double dt = 1.0 / 60)
    {
        pose.Reset();
        if (anchor is not null)
        {
            pose["anchor"].Location.SetFrom(anchor);
        }

        pose.Evaluate();
        stack.Apply(pose, new ModifierFrame(dt: dt));
    }

    private static void Run(Pose pose, ModifierStack stack, int frames, Vector3? anchor = null)
    {
        for (var i = 0; i < frames; i++)
        {
            Frame(pose, stack, anchor: anchor);
        }
    }

    private static SpringBones Calm(IReadOnlyList<SpringCollider>? colliders = null) =>
        new(bones: ["h0", "h1"], gravity: Vector3.Zero(), colliders: colliders);

    // spring bones

    [Fact]
    public void PassTheFirstFrameAsAnimated()
    {
        var pose = new Pose(Hair());
        var stack = new ModifierStack();
        stack.Add("hair", Calm());
        Frame(pose, stack);
        Assert.True(pose["h0"].IsRest);
        Assert.True(pose["h1"].IsRest);
    }

    [Fact]
    public void StayAtRestWhenNothingMovesAndNothingPulls()
    {
        var pose = new Pose(Hair());
        var stack = new ModifierStack();
        stack.Add("hair", Calm());
        Run(pose, stack, frames: 120);
        Assert.True(pose.TailOf("h1").DistanceTo(new Vector3(0, 3, 0)) < 1e-6);
    }

    [Fact]
    public void LagBehindAHeadThatMoves()
    {
        var pose = new Pose(Hair());
        var stack = new ModifierStack();
        stack.Add("hair", Calm());
        Frame(pose, stack, anchor: Vector3.Zero());
        Frame(pose, stack, anchor: new Vector3(0.5, 0, 0));

        Assert.True(pose.TailOf("h1").X < 0.25);
        Assert.True(pose.TailOf("h1").X > -0.05);
    }

    [Fact]
    public void CatchUpOnceTheHeadStops()
    {
        var pose = new Pose(Hair());
        var stack = new ModifierStack();
        stack.Add("hair", Calm());
        Frame(pose, stack, anchor: Vector3.Zero());
        Run(pose, stack, frames: 300, anchor: new Vector3(0.5, 0, 0));
        Assert.Equal(0.5, pose.TailOf("h1").X, 0.02);
    }

    [Fact]
    public void SwingOnAfterAModelThatWasCarriedAwayStops()
    {
        var pose = new Pose(Hair());
        pose.Evaluate();
        var stack = new ModifierStack();
        stack.Add("hair", Calm());
        stack.Apply(pose, new ModifierFrame(dt: 1.0 / 60));

        pose.Evaluate();
        stack.Apply(pose, new ModifierFrame(dt: 1.0 / 60, modelToWorld: Matrix4.TranslationValues(0.5, 0, 0)));
        Assert.True(pose.TailOf("h1").X < -0.01);
    }

    [Fact]
    public void HangDownUnderGravityWhenNothingHoldsThem()
    {
        var pose = Settle([]);
        Assert.Equal(1, pose.TailOf("h0").X, 0.05);
        Assert.Equal(-1, pose.TailOf("h0").Y, 0.05);
        Assert.Equal(-2, pose.TailOf("h1").Y, 0.1);
    }

    [Fact]
    public void KeepBoneLengthsWhileTheySwing()
    {
        var pose = Settle([]);
        Assert.Equal(1, pose.TailOf("h0").DistanceTo(pose.HeadOf("h0")), 1e-6);
        Assert.Equal(1, pose.TailOf("h1").DistanceTo(pose.HeadOf("h1")), 1e-6);
    }

    [Fact]
    public void LieOnAFloorThatCutsAcrossTheSwing()
    {
        var floor = new PlaneCollider(new Vector3(0, -0.5, 0), new Vector3(0, 1, 0));
        var pose = Settle([floor]);
        Assert.Equal(-0.5, pose.TailOf("h0").Y, 1e-3);
        Assert.True(pose.TailOf("h1").Y > -0.5 - 1e-3);
        Assert.Equal(1, pose.TailOf("h0").DistanceTo(pose.HeadOf("h0")), 1e-6);
    }

    [Fact]
    public void RestOnABallTheSwingRunsInto()
    {
        var ball = new SphereCollider(new Vector3(1.3, -0.9, 0), 0.6);
        var pose = Settle([ball]);
        var away = pose.TailOf("h0").DistanceTo(new Vector3(1.3, -0.9, 0));
        Assert.Equal(0.6, away, 1e-3);
    }

    [Fact]
    public void RestOnARodTheSwingRunsInto()
    {
        var rod = new CapsuleCollider(new Vector3(1.3, -0.9, -1), new Vector3(1.3, -0.9, 1), 0.6);
        var pose = Settle([rod]);
        var tail = pose.TailOf("h0");
        var away = Math.Sqrt(Math.Pow(tail.X - 1.3, 2) + Math.Pow(tail.Y + 0.9, 2));
        Assert.Equal(0.6, away, 1e-3);
    }

    [Fact]
    public void KeepClearByTheMargin()
    {
        var floor = new PlaneCollider(new Vector3(0, -0.5, 0), new Vector3(0, 1, 0));
        var pose = new Pose(Fringe());
        var stack = new ModifierStack();
        stack.Add("hair", new SpringBones(bones: ["h0", "h1"], stiffness: 0, drag: 4, margin: 0.1, colliders: [floor]));
        Frame(pose, stack);
        Run(pose, stack, frames: 600);
        Assert.Equal(-0.4, pose.TailOf("h0").Y, 1e-3);
    }

    [Fact]
    public void CarryAColliderAlongWithItsBone()
    {
        var rest = new Pose(Fringe());
        rest.Evaluate();
        var inverse = Matrix4.Inverted(rest.WorldOf("anchor"));
        var ball = new SphereCollider(inverse.Transformed3(new Vector3(1.3, -0.9, 0)), 0.6, bone: "anchor");

        var pose = Settle([ball], anchor: new Vector3(0, 0, 4));
        var away = pose.TailOf("h0").DistanceTo(new Vector3(1.3, -0.9, 4));
        Assert.Equal(0.6, away, 1e-3);
    }

    [Fact]
    public void LeaveAColliderWhereItWasGiven()
    {
        var ball = new SphereCollider(new Vector3(1.3, -0.9, 0), 0.6);
        var pose = new Pose(Fringe());
        var stack = new ModifierStack();
        stack.Add("hair", Loose([ball]));
        Frame(pose, stack);
        stack.Apply(pose, new ModifierFrame(dt: 1.0 / 60, modelToWorld: Matrix4.TranslationValues(0, 0, 3)));
        Assert.Equal(0, ball.Centre.Z);
    }

    [Fact]
    public void HoldInAFrameOfNoTime()
    {
        var pose = new Pose(Hair());
        var stack = new ModifierStack();
        stack.Add("hair", Calm());
        Frame(pose, stack, anchor: Vector3.Zero());
        Frame(pose, stack, anchor: new Vector3(0.5, 0, 0));
        var held = pose.TailOf("h1").Clone();

        Frame(pose, stack, anchor: new Vector3(0.5, 0, 0), dt: 0);
        Assert.True(pose.TailOf("h1").DistanceTo(held) < 1e-9);
    }

    [Fact]
    public void StartAgainFromTheAnimationAfterATeleport()
    {
        var pose = new Pose(Hair());
        var stack = new ModifierStack();
        stack.Add("hair", Calm());
        Frame(pose, stack, anchor: Vector3.Zero());
        Frame(pose, stack, anchor: new Vector3(100, 0, 0));
        Assert.True(pose["h0"].IsRest);
        Assert.True(pose["h1"].IsRest);
    }

    [Fact]
    public void StartAgainFromTheAnimationAfterAReset()
    {
        var pose = new Pose(Hair());
        var stack = new ModifierStack();
        stack.Add("hair", Calm());
        Frame(pose, stack, anchor: Vector3.Zero());
        Frame(pose, stack, anchor: new Vector3(0.5, 0, 0));
        stack.Reset();
        Frame(pose, stack, anchor: new Vector3(0.5, 0, 0));
        Assert.True(pose["h0"].IsRest);
    }

    [Fact]
    public void RefuseABoneListedBeforeItsParent()
    {
        var pose = new Pose(Hair());
        pose.Evaluate();
        var stack = new ModifierStack();
        stack.Add("hair", new SpringBones(bones: ["h1", "h0"]));
        Assert.Throws<ArmatureException>(() => stack.Apply(pose));
    }

    [Fact]
    public void RefuseAColliderOnABoneThatIsNotThere()
    {
        var pose = new Pose(Hair());
        pose.Evaluate();
        var stack = new ModifierStack();
        stack.Add("hair", new SpringBones(bones: ["h0"], colliders: [new SphereCollider(Vector3.Zero(), 1, bone: "skull")]));
        Assert.Throws<ArmatureException>(() => stack.Apply(pose));
    }

    [Fact]
    public void RefuseAColliderWithoutASize()
    {
        Assert.ThrowsAny<ArgumentException>(() => new SphereCollider(Vector3.Zero(), -1));
    }
}
