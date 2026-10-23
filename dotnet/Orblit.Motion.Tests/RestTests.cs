namespace Orblit.Motion.Tests;

public class RestTests
{
    private readonly RestSkeleton _arm = new(
        ["root", "upper", "lower"],
        [-1, 0, 1],
        [At(0, 1, 0), At(1, 0, 0), At(1, 0, 0)]);

    [Fact]
    public void PosedOriginsAreTheRestOriginsWhenNothingIsPosed()
    {
        var posed = _arm.PosedOrigins(new Dictionary<string, BoneLocal>());
        for (var bone = 0; bone < _arm.Length; bone++)
        {
            ExpectNear(posed[bone], _arm.OriginOf(bone));
        }
    }

    [Fact]
    public void PosedOriginsCarryATurnOfAParentDownToItsChildren()
    {
        // A quarter turn about Z lifts the arm from along +X to along +Y.
        var posed = _arm.PosedOrigins(new Dictionary<string, BoneLocal>
        {
            ["upper"] = new BoneLocal(rotation: Quaternion.AxisAngle(new Vector3(0, 0, 1), Math.PI / 2)),
        });
        ExpectNear(posed[0], new Vector3(0, 1, 0));
        ExpectNear(posed[1], new Vector3(1, 1, 0));
        ExpectNear(posed[2], new Vector3(1, 2, 0));
    }

    [Fact]
    public void PosedOriginsMoveABoneByItsOwnPositionAndScaleItsChildrenAway()
    {
        var posed = _arm.PosedOrigins(new Dictionary<string, BoneLocal>
        {
            ["root"] = new BoneLocal(position: new Vector3(0, 0, 2), scale: Vector3.All(2)),
        });
        ExpectNear(posed[0], new Vector3(0, 0, 2));
        ExpectNear(posed[1], new Vector3(2, 0, 2));
        ExpectNear(posed[2], new Vector3(4, 0, 2));
    }

    [Fact]
    public void PosedOriginsKeepTheRestForAPartThePoseLeavesNull()
    {
        var posed = _arm.PosedOrigins(new Dictionary<string, BoneLocal> { ["lower"] = new BoneLocal() });
        ExpectNear(posed[2], _arm.OriginOf(2));
    }

    [Fact]
    public void PosedOriginsIgnoreANameTheSkeletonDoesNotHave()
    {
        var posed = _arm.PosedOrigins(new Dictionary<string, BoneLocal>
        {
            ["tail"] = new BoneLocal(position: new Vector3(9, 9, 9)),
        });
        ExpectNear(posed[2], _arm.OriginOf(2));
    }

    [Fact]
    public void PosedOriginsHangFromWhateverTheRootsHangFrom()
    {
        var lifted = new RestSkeleton(
            ["root", "upper"],
            [-1, 0],
            [At(0, 0, 0), At(1, 0, 0)],
            At(0, 5, 0));
        var posed = lifted.PosedOrigins(new Dictionary<string, BoneLocal>
        {
            ["root"] = new BoneLocal(position: new Vector3(0, 1, 0)),
        });
        ExpectNear(posed[0], new Vector3(0, 6, 0));
        ExpectNear(posed[1], new Vector3(1, 6, 0));
    }

    private static Matrix4 At(double x, double y, double z) =>
        Matrix4.Compose(new Vector3(x, y, z), Quaternion.Identity(), Vector3.All(1));

    private static void ExpectNear(Vector3 got, Vector3 want) =>
        Assert.True((got - want).Length < 1e-9, $"{got} is not {want}");
}
