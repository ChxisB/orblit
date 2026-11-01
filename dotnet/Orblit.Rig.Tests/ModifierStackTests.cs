namespace Orblit.Rig.Tests;

public class ModifierStackTests
{
    /// <summary>Two bones along +Y, a metre each.</summary>
    private static Armature TwoBones() => new(
    [
        new Bone("a", new Vector3(0, 0, 0), new Vector3(0, 1, 0)),
        new Bone("b", new Vector3(0, 1, 0), new Vector3(0, 2, 0), parent: "a", connected: true),
    ]);

    /// <summary>Sets a bone's pose location, and counts how often it was reset.</summary>
    private sealed class Shove(string bone, Vector3 by) : Modifier
    {
        public int Resets { get; private set; }

        public int Solves { get; private set; }

        public override void Solve(Pose pose, ModifierFrame frame)
        {
            Solves++;
            pose[bone].Location.SetFrom(by);
        }

        public override void Reset() => Resets++;
    }

    /// <summary>Adds to what the bone already holds, so a second run shows the order.</summary>
    private sealed class Add(string bone, double by) : Modifier
    {
        public override void Solve(Pose pose, ModifierFrame frame)
        {
            pose[bone].Location.X += by;
        }
    }

    private sealed class Turn(string bone, double angle) : Modifier
    {
        public override void Solve(Pose pose, ModifierFrame frame)
        {
            pose[bone].Rotation = Quaternion.AxisAngle(new Vector3(0, 0, 1), angle);
        }
    }

    private static Pose Evaluated(Armature armature)
    {
        var pose = new Pose(armature);
        pose.Evaluate();
        return pose;
    }

    // the frame

    [Fact]
    public void CarriesAPointBetweenTheModelAndTheWorld()
    {
        var frame = new ModifierFrame(modelToWorld: Matrix4.TranslationValues(5, 0, 0));
        Assert.Equal(new Vector3(6, 2, 3), frame.ToWorld(new Vector3(1, 2, 3)));
        Assert.Equal(new Vector3(1, 2, 3), frame.ToModel(new Vector3(6, 2, 3)));
    }

    [Fact]
    public void DoesNotMoveADirectionWhenTheModelMoves()
    {
        var frame = new ModifierFrame(modelToWorld: Matrix4.TranslationValues(5, 0, 0));
        Assert.Equal(new Vector3(0, 1, 0), frame.DirectionToWorld(new Vector3(0, 1, 0)));
    }

    [Fact]
    public void TurnsADirectionWithTheModel()
    {
        var frame = new ModifierFrame(modelToWorld: Matrix4.RotationZ(Math.PI / 2));
        var turned = frame.DirectionToWorld(new Vector3(1, 0, 0));
        Assert.Equal(0, turned.X, 1e-9);
        Assert.Equal(1, turned.Y, 1e-9);
    }

    [Fact]
    public void ReportsHowMuchAModelIsScaled()
    {
        var frame = new ModifierFrame(modelToWorld: Matrix4.Diagonal3Values(2, 2, 2));
        Assert.Equal(2, frame.Scale, 1e-9);
    }

    [Fact]
    public void RefusesANegativeTime()
    {
        Assert.ThrowsAny<ArgumentException>(() => new ModifierFrame(dt: -0.1));
    }

    [Fact]
    public void RefusesATimeThatNeverEnds()
    {
        Assert.ThrowsAny<ArgumentException>(() => new ModifierFrame(dt: double.PositiveInfinity));
    }

    [Fact]
    public void RefusesAMatrixThatSquashesSpaceFlat()
    {
        Assert.ThrowsAny<ArgumentException>(() => new ModifierFrame(modelToWorld: Matrix4.Diagonal3Values(0, 1, 1)));
    }

    // the stack

    [Fact]
    public void RunsModifiersInTheOrderTheyWereAdded()
    {
        var pose = Evaluated(TwoBones());
        var stack = new ModifierStack();
        stack.Add("set", new Shove("a", new Vector3(1, 0, 0)));
        stack.Add("add", new Add("a", 2));
        stack.Apply(pose);
        Assert.Equal(3, pose["a"].Location.X);
    }

    [Fact]
    public void RunsThemTheOtherWayWhenAddedTheOtherWay()
    {
        var pose = Evaluated(TwoBones());
        var stack = new ModifierStack();
        stack.Add("add", new Add("a", 2));
        stack.Add("set", new Shove("a", new Vector3(1, 0, 0)));
        stack.Apply(pose);
        Assert.Equal(1, pose["a"].Location.X);
    }

    [Fact]
    public void LeavesThePoseUpToDate()
    {
        var pose = Evaluated(TwoBones());
        var stack = new ModifierStack();
        stack.Add("set", new Shove("a", new Vector3(0, 0, 4)));
        stack.Apply(pose);
        Assert.Equal(4, pose.HeadOf("b").Z, 1e-9);
    }

    [Fact]
    public void KeepsAShareOfTheResultForAPartialInfluence()
    {
        var pose = Evaluated(TwoBones());
        var stack = new ModifierStack();
        stack.Add("set", new Shove("a", new Vector3(4, 0, 0)), influence: 0.25);
        stack.Apply(pose);
        Assert.Equal(1, pose["a"].Location.X, 1e-9);
    }

    [Fact]
    public void MixesARotationByTheShorterArc()
    {
        var pose = Evaluated(TwoBones());
        var stack = new ModifierStack();
        stack.Add("turn", new Turn("a", Math.PI / 2), influence: 0.5);
        stack.Apply(pose);
        var turn = pose["a"].Rotation;
        Assert.Equal(Math.PI / 4, 2 * Math.Acos(turn.W), 1e-9);
    }

    [Fact]
    public void SkipsAModifierAtZeroInfluenceAndResetsIt()
    {
        var shove = new Shove("a", new Vector3(1, 0, 0));
        var pose = Evaluated(TwoBones());
        var stack = new ModifierStack();
        stack.Add("set", shove, influence: 0);
        stack.Apply(pose);
        Assert.Equal(0, shove.Solves);
        Assert.Equal(1, shove.Resets);
        Assert.Equal(0, pose["a"].Location.X);
    }

    [Fact]
    public void SkipsAModifierThatIsSwitchedOff()
    {
        var shove = new Shove("a", new Vector3(1, 0, 0));
        var pose = Evaluated(TwoBones());
        var stack = new ModifierStack();
        stack.Add("set", shove).Enabled = false;
        stack.Apply(pose);
        Assert.Equal(0, shove.Solves);
        Assert.Equal(1, shove.Resets);
    }

    [Fact]
    public void TakesAnInfluenceFromAPoseProperty()
    {
        var pose = Evaluated(TwoBones());
        pose.SetProperty("look", 0.5);
        var stack = new ModifierStack();
        stack.Add("set", new Shove("a", new Vector3(2, 0, 0)), influenceProperty: "look");
        stack.Apply(pose);
        Assert.Equal(1, pose["a"].Location.X, 1e-9);
    }

    [Fact]
    public void FallsBackToItsOwnInfluenceWhenThePropertyIsNotSet()
    {
        var pose = Evaluated(TwoBones());
        var stack = new ModifierStack();
        stack.Add("set", new Shove("a", new Vector3(2, 0, 0)), influence: 0.5, influenceProperty: "missing");
        stack.Apply(pose);
        Assert.Equal(1, pose["a"].Location.X, 1e-9);
    }

    [Fact]
    public void ClampsAnInfluenceToZeroAndOne()
    {
        var stack = new ModifierStack();
        var slot = stack.Add("set", new Shove("a", Vector3.Zero()), influence: 3);
        Assert.Equal(1, slot.Influence);
        slot.Influence = -2;
        Assert.Equal(0, slot.Influence);
    }

    [Fact]
    public void RefusesTwoModifiersWithOneName()
    {
        var stack = new ModifierStack();
        stack.Add("x", new Shove("a", Vector3.Zero()));
        Assert.ThrowsAny<ArgumentException>(() => stack.Add("x", new Shove("b", Vector3.Zero())));
    }

    [Fact]
    public void FindsASlotByNameAndTakesItOut()
    {
        var shove = new Shove("a", Vector3.Zero());
        var stack = new ModifierStack();
        stack.Add("x", shove);
        Assert.Same(shove, stack["x"]?.Modifier);
        Assert.True(stack.Remove("x"));
        Assert.False(stack.Remove("x"));
        Assert.Equal(0, stack.Length);
        Assert.Equal(1, shove.Resets);
    }

    [Fact]
    public void NamesTheBoneAModifierIsMissingBeforeItSolves()
    {
        var pose = Evaluated(TwoBones());
        var stack = new ModifierStack();
        stack.Add("look", new LookAt("head", (_, _) => new Vector3(1, 1, 1)));
        var error = Assert.Throws<ArmatureException>(() => stack.Apply(pose));
        Assert.Contains("\"head\"", error.Message);
        Assert.Contains("nothing to turn", error.Message);
    }

    [Fact]
    public void ResetsEveryModifierOnRequest()
    {
        var shove = new Shove("a", Vector3.Zero());
        var stack = new ModifierStack();
        stack.Add("x", shove);
        stack.Reset();
        Assert.Equal(1, shove.Resets);
    }

    // refreshing part of a pose

    [Fact]
    public void MovesTheBoneAndWhatHangsBelowItAndNotTheRest()
    {
        var armature = new Armature(
        [
            new Bone("a", new Vector3(0, 0, 0), new Vector3(0, 1, 0)),
            new Bone("b", new Vector3(0, 1, 0), new Vector3(0, 2, 0), parent: "a"),
            new Bone("c", new Vector3(1, 0, 0), new Vector3(1, 1, 0)),
        ]);
        var pose = new Pose(armature);
        pose.Evaluate();
        pose["a"].Location.SetValues(0, 0, 3);
        pose["c"].Location.SetValues(0, 0, 3);

        pose.Refresh(from: "a");

        Assert.Equal(3, pose.HeadOf("a").Z);
        Assert.Equal(3, pose.HeadOf("b").Z);
        Assert.Equal(0, pose.HeadOf("c").Z);
    }

    [Fact]
    public void NamesABoneThatIsNotThere()
    {
        var pose = Evaluated(TwoBones());
        Assert.Throws<ArmatureException>(() => pose.Refresh(from: "nope"));
    }

    [Fact]
    public void GivesTheSameAnswerHoweverOftenItRuns()
    {
        var pose = new Pose(TwoBones());
        pose.AddIkChain(new IkChain("a", "b", "a", influence: 0.5));
        pose.Evaluate();
        var before = pose.HeadOf("b").Clone();
        pose.Refresh();
        pose.Refresh();
        Assert.True(pose.HeadOf("b").DistanceTo(before) < 1e-12);
    }
}
