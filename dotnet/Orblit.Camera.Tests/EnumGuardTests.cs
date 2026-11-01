using Orblit.VectorMath;

namespace Orblit.Camera.Tests;

// A C# enum takes any number, so the two switches over one say so.
public class EnumGuardTests
{
    [Fact]
    public void RefusesABlendStyleThatIsNotOneOfTheFive() =>
        Assert.Throws<InvalidOperationException>(() => new Blend((BlendStyle)99, 1).Ease(0.5));

    [Fact]
    public void RefusesAFollowBindingThatIsNotOneOfTheThree()
    {
        var body = new FollowBody(Vector3.Zero(), (FollowBinding)99);
        var target = new FixedTarget(Vector3.Zero());
        Assert.Throws<InvalidOperationException>(
            () => body.Solve(Vector3.Zero(), target, Quaternion.Identity(), new Lens(), 1, 0.1));
    }
}
