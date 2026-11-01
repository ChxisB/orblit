namespace Orblit.Rig.Tests;

public class PresentationTests
{
    /// <summary>An arm, marked up as a limb.</summary>
    private static GeneratedRig Arm(double stretch = 0)
    {
        var armature = new Armature(
        [
            new Bone("upper_arm.L", new Vector3(0.2, 1.5, 0), new Vector3(0.6, 1.5, -0.1)),
            new Bone("forearm.L", new Vector3(0.6, 1.5, -0.1), new Vector3(1.0, 1.5, 0), parent: "upper_arm.L", connected: true),
            new Bone("hand.L", new Vector3(1.0, 1.5, 0), new Vector3(1.2, 1.5, 0), parent: "forearm.L", connected: true),
        ]);
        var meta = new MetaRig(armature);
        meta.Assign("upper_arm.L", new LimbRig(stretch: stretch));
        return new RigGenerator().Generate(meta);
    }

    // control widgets

    [Fact]
    public void EveryShapeDrawsSomething()
    {
        foreach (var shape in Enum.GetValues<WidgetShape>())
        {
            var lines = new BoneWidget(shape: shape).Outline(1);
            Assert.True(lines.Count > 0, $"{shape} drew no lines");
            foreach (var line in lines)
            {
                Assert.True(line.Count >= 2, $"{shape}");
            }
        }
    }

    [Fact]
    public void ASphereIsThreeRingsACircleIsOne()
    {
        Assert.Equal(3, new BoneWidget(shape: WidgetShape.Sphere).Outline(1).Count);
        Assert.Single(new BoneWidget(shape: WidgetShape.Circle).Outline(1));
    }

    [Fact]
    public void ScalesWithTheBoneSoABiggerCharacterKeepsItsProportions()
    {
        var small = new BoneWidget(shape: WidgetShape.Circle).Outline(1);
        var large = new BoneWidget(shape: WidgetShape.Circle).Outline(4);
        Assert.Equal(small[0][0].X * 4, large[0][0].X, 1e-9);
    }

    [Fact]
    public void SizeMultipliesTheBoneLengthRatherThanReplacingIt()
    {
        var plain = new BoneWidget(shape: WidgetShape.Circle).Outline(2);
        var doubled = new BoneWidget(shape: WidgetShape.Circle, size: 2).Outline(2);
        Assert.Equal(plain[0][0].X * 2, doubled[0][0].X, 1e-9);
    }

    [Fact]
    public void AnOffsetMovesTheShapeWithoutResizingIt()
    {
        var moved = new BoneWidget(shape: WidgetShape.Circle, offset: new Vector3(0, 1, 0)).Outline(2);
        foreach (var point in moved[0])
        {
            Assert.Equal(2, point.Y, 1e-9);
        }
    }

    [Fact]
    public void ARollTurnsTheShapeTheWayABoneRollTurnsTheBone()
    {
        var bone = new Bone("b", Vector3.Zero(), new Vector3(0, 1, 0), roll: Math.PI / 2);
        var plain = new BoneWidget(shape: WidgetShape.Square).Outline(1);
        var rolled = new BoneWidget(shape: WidgetShape.Square, roll: Math.PI / 2).Outline(1);
        for (var i = 0; i < plain[0].Count; i++)
        {
            var expected = bone.RestMatrix.Transformed3(plain[0][i]);
            Assert.Equal(0, (rolled[0][i] - expected).Length, 1e-9);
        }
    }

    [Fact]
    public void ALimbGivesItsControlsShapesAnAnimatorCanTellApart()
    {
        var rig = Arm();

        Assert.Equal(WidgetShape.Cube, rig.Widgets["hand_ik.L"].Shape);
        Assert.Equal(WidgetShape.Square, rig.Widgets["upper_arm_pole.L"].Shape);
        Assert.Equal(WidgetShape.Circle, rig.Widgets["upper_arm_fk.L"].Shape);
    }

    [Fact]
    public void EveryControlHasAShape()
    {
        var rig = Arm();
        foreach (var control in rig.Controls)
        {
            Assert.True(rig.Widgets.ContainsKey(control), control);
        }
    }

    // bone collections

    [Fact]
    public void MachineryIsHiddenAndControlsAreNot()
    {
        var rig = Arm();

        foreach (var name in new[] { "ORG", "DEF", "MCH" })
        {
            Assert.False(rig.Collections[name]?.Visible, name);
        }

        Assert.True(rig.Collections["upper_arm.L (IK)"]?.Visible);
        Assert.True(rig.Collections["upper_arm.L (FK)"]?.Visible);
    }

    [Fact]
    public void EveryOriginalDeformAndMechanismBoneIsFiledAway()
    {
        var rig = Arm();

        foreach (var bone in rig.Armature.Bones)
        {
            if (BoneNaming.RoleOf(bone.Name) == BoneRole.Control)
            {
                continue;
            }

            Assert.True(
                rig.Collections.CollectionsOf(bone.Name).Count > 0,
                $"{bone.Name} is in nothing and cannot be hidden");
        }
    }

    [Fact]
    public void TheTwoSidesAreDifferentColours()
    {
        Assert.NotEqual(BoneCollections.ColourFor(Side.Left), BoneCollections.ColourFor(Side.Right));
    }

    [Fact]
    public void ABoneCanBeInMoreThanOneCollection()
    {
        var collections = new BoneCollections();
        collections.Add("hand_ik.L", "Arm.L (IK)");
        collections.Add("hand_ik.L", "IK");

        Assert.Equal(["Arm.L (IK)", "IK"], collections.CollectionsOf("hand_ik.L"));
    }

    [Fact]
    public void DeclaringTheSameCollectionTwiceIsNotAMistake()
    {
        var collections = new BoneCollections();
        collections.Declare("IK", colour: 1);
        collections.Declare("IK", colour: 2);

        Assert.Single(collections.All);
        Assert.Equal(1, collections["IK"]?.Colour);
    }

    // stretch through the generator

    [Fact]
    public void ALimbDoesNotStretchUnlessItWasAskedTo()
    {
        var rig = Arm();
        Assert.Equal(0, Assert.Single(rig.Pose.IkChains).Stretch);
        Assert.DoesNotContain("upper_arm_stretch.L", rig.Properties.Keys);
    }

    [Fact]
    public void AStretchyLimbGetsAPropertyAnAnimatorCanTurnDown()
    {
        var rig = Arm(stretch: 0.5);
        Assert.Equal(0.5, rig.Properties["upper_arm_stretch.L"]);
        Assert.Equal("upper_arm_stretch.L", Assert.Single(rig.Pose.IkChains).StretchProperty);
    }
}
