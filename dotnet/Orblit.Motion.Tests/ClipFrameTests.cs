namespace Orblit.Motion.Tests;

public class ClipFrameTests
{
    [Fact]
    public void AveragesNumbersAndVectorsByWeight()
    {
        var mixed = ClipFrame.Mix(
            [
                Frame(static f =>
                {
                    f.Values["lamp"] = new OrderedDictionary<string, object> { ["light.power"] = 0.0 };
                    f.Values[string.Empty] = new OrderedDictionary<string, object> { ["transform.position"] = new Vector3(0, 0, 0) };
                }),
                Frame(static f =>
                {
                    f.Values["lamp"] = new OrderedDictionary<string, object> { ["light.power"] = 4.0 };
                    f.Values[string.Empty] = new OrderedDictionary<string, object> { ["transform.position"] = new Vector3(2, 0, 4) };
                }),
            ],
            [3, 1]);

        Assert.Equal(1, Assert.IsType<double>(mixed.ValueOf("lamp", "light.power")), 1e-12);
        var position = Assert.IsType<Vector3>(mixed.ValueOf(string.Empty, "transform.position"));
        Assert.True((position - new Vector3(0.5, 0, 1)).Length < 1e-12);
    }

    [Fact]
    public void TurnsRotationsTheShortWayByWeight()
    {
        var quarter = Quaternion.AxisAngle(new Vector3(0, 1, 0), Math.PI / 2);
        var mixed = ClipFrame.Mix(
            [
                Frame(static f => f.Bones[string.Empty] = Bones(("hips", new BoneLocal(rotation: Quaternion.Identity())))),
                Frame(f => f.Bones[string.Empty] = Bones(("hips", new BoneLocal(rotation: quarter)))),
            ],
            [1, 1]);

        var eighth = Quaternion.AxisAngle(new Vector3(0, 1, 0), Math.PI / 4);
        var turned = mixed.BoneOf(string.Empty, "hips")!.Rotation!;
        var dot = (turned.X * eighth.X) + (turned.Y * eighth.Y) + (turned.Z * eighth.Z) + (turned.W * eighth.W);
        Assert.Equal(1, Math.Abs(dot), 1e-9);
    }

    [Fact]
    public void LetsTheFrameWithMostSayDecideAFlag()
    {
        var mixed = ClipFrame.Mix(
            [
                Frame(static f => f.Values["door"] = new OrderedDictionary<string, object> { ["door.open"] = true }),
                Frame(static f => f.Values["door"] = new OrderedDictionary<string, object> { ["door.open"] = false }),
            ],
            [0.4, 0.6]);

        Assert.Equal(false, mixed.ValueOf("door", "door.open"));
    }

    [Fact]
    public void LeavesAValueToTheFramesThatSaySomethingAboutIt()
    {
        var mixed = ClipFrame.Mix(
            [
                Frame(static f => f.Bones[string.Empty] = Bones(("arm", new BoneLocal(rotation: Quaternion.Identity())))),
                Frame(static f => f.Bones[string.Empty] = Bones(
                    ("arm", new BoneLocal(position: new Vector3(0, 2, 0))),
                    ("hand", new BoneLocal(scale: Vector3.All(2))))),
            ],
            [0.9, 0.1]);

        var arm = mixed.BoneOf(string.Empty, "arm")!;
        Assert.NotNull(arm.Rotation);
        Assert.True((arm.Position! - new Vector3(0, 2, 0)).Length < 1e-12);
        Assert.Null(arm.Scale);
        Assert.Equal(Vector3.All(2), mixed.BoneOf(string.Empty, "hand")!.Scale);
    }

    [Fact]
    public void GivesAFrameWithNoWeightNoSay()
    {
        var mixed = ClipFrame.Mix(
            [
                Frame(static f => f.Values["lamp"] = new OrderedDictionary<string, object> { ["light.power"] = 9.0 }),
                Frame(static f => f.Values["lamp"] = new OrderedDictionary<string, object> { ["light.power"] = 1.0 }),
            ],
            [0, 1]);

        Assert.Equal(1.0, mixed.ValueOf("lamp", "light.power"));
    }

    [Fact]
    public void CopiesRatherThanSharesAValueOnlyOneFrameHas()
    {
        var kept = new Vector3(1, 2, 3);
        var mixed = ClipFrame.Mix(
            [Frame(f => f.Values[string.Empty] = new OrderedDictionary<string, object> { ["transform.scale"] = kept })],
            [1]);

        var made = Assert.IsType<Vector3>(mixed.ValueOf(string.Empty, "transform.scale"));
        Assert.Equal(kept, made);
        Assert.NotSame(kept, made);
    }

    [Fact]
    public void KeepsTheTimeItIsGiven() => Assert.Equal(0.25, ClipFrame.Mix([], [], 0.25).At);

    [Fact]
    public void WantsAWeightForEveryFrame() =>
        Assert.Throws<ArgumentException>(static () => ClipFrame.Mix([new ClipFrame(0)], []));

    private static ClipFrame Frame(Action<ClipFrame> fill)
    {
        var frame = new ClipFrame(0);
        fill(frame);
        return frame;
    }

    private static OrderedDictionary<string, BoneLocal> Bones(params (string Name, BoneLocal Local)[] bones)
    {
        var made = new OrderedDictionary<string, BoneLocal>();
        foreach (var (name, local) in bones)
        {
            made[name] = local;
        }

        return made;
    }
}
