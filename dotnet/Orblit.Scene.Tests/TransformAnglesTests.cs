namespace Orblit.Scene.Tests;

public class TransformAnglesTests
{
    [Fact]
    public void AnglesComeBackAsTheyWentIn()
    {
        foreach (var angles in new[]
        {
            new Vector3(0, 0, 0),
            new Vector3(30, 0, 0),
            new Vector3(0, -45, 0),
            new Vector3(10, 20, 30),
            new Vector3(-170, 80, 120),
        })
        {
            var back = TransformComponent.AnglesOf(TransformComponent.RotationOf(angles));
            Assert.Equal(angles.X, back.X, 1e-9);
            Assert.Equal(angles.Y, back.Y, 1e-9);
            Assert.Equal(angles.Z, back.Z, 1e-9);
        }
    }

    [Fact]
    public void AnglesComposeZThenYThenX()
    {
        var composed = TransformComponent.RotationOf(new Vector3(90, 0, 90));

        // X first: Y goes to Z. Then Z: nothing moves Z.
        Assert.Equal(1, composed.Transformed(new Vector3(0, 1, 0)).Z, 1e-12);

        // X leaves X alone, then Z turns it to Y.
        Assert.Equal(1, composed.Transformed(new Vector3(1, 0, 0)).Y, 1e-12);
    }

    [Fact]
    public void AtThePoleAnglesStillMakeTheSameRotation()
    {
        var pole = TransformComponent.RotationOf(new Vector3(25, 90, 40));
        var again = TransformComponent.RotationOf(TransformComponent.AnglesOf(pole));

        for (var i = 0; i < 9; i++)
        {
            Assert.Equal(pole[i], again[i], 1e-9);
        }
    }
}
