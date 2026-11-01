using Orblit.Filament;
using Orblit.Particles;
using Orblit.VectorMath;

namespace Orblit.Stage.Tests;

public class TrailRibbonTests
{
    private static readonly Vector3 Eye = new(0, 0, 10);

    // A strip of xs along x, newest first, each width wide.
    private static TrailStrip Strip(double[] xs, double width = 0.4, int? capacity = null)
    {
        var output = new TrailStrip(capacity ?? xs.Length);
        var trail = new Trail(life: 1, rate: xs.Length - 1, width: [width]);
        var at = 0;
        trail.SampleAlong(10, _ => new Vector3(xs[at++], 0, 0), output);
        return output;
    }

    private static Vector3 Where(JointPose pose) => pose.Transform.GetTranslation();

    [Fact]
    public void ARibbonNeedsTwoTo128Points()
    {
        Assert.Throws<ArgumentException>(() => new TrailRibbon(1));
        Assert.Throws<ArgumentException>(() => new TrailRibbon(129));
        Assert.NotEmpty(new TrailRibbon(128).ToGlb());
    }

    [Fact]
    public void TheGlbHasAnEdgeOnEachSideOfEveryPoint()
    {
        var glb = new Glb(new TrailRibbon(5).ToGlb());

        Assert.Equal(10, glb.Accessors.Count(glb.Attribute("POSITION")));
        Assert.Equal(4 * 6, glb.Accessors.Indices(Glb.Whole(glb.Primitive["indices"])).Count);
    }

    [Fact]
    public void PosesPutAnEdgeEachSideOfThePointAWidthApart()
    {
        var poses = new TrailRibbon(4).Poses(Strip([3, 2, 1, 0]), Eye);

        Assert.Equal(8, poses.Count);
        for (var i = 0; i < 4; i++)
        {
            var left = Where(poses[i * 2]);
            var right = Where(poses[(i * 2) + 1]);
            Assert.Equal(0.4, (right - left).Length, 1e-6);
            Near.Vector((left + right) / 2, new Vector3(-i, 0, 0), tolerance: 1e-6);
        }
    }

    [Fact]
    public void TheGlbIsWhiteUnlessAColourIsGiven()
    {
        var white = new Glb(new TrailRibbon(3).ToGlb());
        var factor = Glb.List(Glb.Map(white.First("materials")["pbrMetallicRoughness"])["baseColorFactor"]);

        Assert.Equal<double>([1, 1, 1, 1], factor.Select(Glb.Number));
    }

    [Fact]
    public void PosesLayTheBandAcrossTheWayThePointGoesAndTheWayItIsSeen()
    {
        var poses = new TrailRibbon(4).Poses(Strip([3, 2, 1, 0]), Eye);

        var across = Where(poses[3]) - Where(poses[2]);

        Assert.Equal(0, across.Dot(new Vector3(1, 0, 0)), 1e-6);
        Assert.Equal(0, across.Dot(Eye - new Vector3(2, 0, 0)), 1e-6);
    }

    [Fact]
    public void PosesFollowTheCameraRoundToTheOtherSide()
    {
        var ribbon = new TrailRibbon(4);
        var front = ribbon.Poses(Strip([3, 2, 1, 0]), new Vector3(0, 0, 10));
        var above = ribbon.Poses(Strip([3, 2, 1, 0]), new Vector3(0, 10, 0));

        var seenFront = Where(front[3]) - Where(front[2]);
        var seenAbove = Where(above[3]) - Where(above[2]);

        Assert.Equal(0.4, Math.Abs(seenFront.Y), 1e-6);
        Assert.Equal(0.4, Math.Abs(seenAbove.Z), 1e-6);
    }

    [Fact]
    public void PosesFoldRowsPastTheEndOfTheStripOntoItsLastPoint()
    {
        var poses = new TrailRibbon(4).Poses(Strip([3, 2], capacity: 4), Eye);

        var last = new Vector3(-1, 0, 0);
        Near.Vector(Where(poses[4]), last, tolerance: 1e-6);
        Near.Vector(Where(poses[5]), last, tolerance: 1e-6);
        Near.Vector(Where(poses[7]), last, tolerance: 1e-6);
    }

    [Fact]
    public void PosesAreRelativeToTheHeadSoTheObjectCanSitOnIt()
    {
        var ribbon = new TrailRibbon(4);
        var here = Strip([3, 2, 1, 0]);
        var far = Strip([103, 102, 101, 100]);

        Assert.Equal(new Vector3(3, 0, 0), here.Head);
        var near = ribbon.Poses(here, Eye);
        var moved = ribbon.Poses(far, Eye + new Vector3(100, 0, 0));

        for (var i = 0; i < 8; i++)
        {
            Near.Vector(Where(moved[i]), Where(near[i]), tolerance: 1e-6);
        }
    }

    [Fact]
    public void AnEmptyStripDrawsNothing()
    {
        var poses = new TrailRibbon(4).Poses(new TrailStrip(4), Eye);

        Assert.All(poses, pose => Near.Vector(Where(pose), Vector3.Zero(), tolerance: 1e-6));
    }

    [Fact]
    public void PosesPinchWhereThereIsNoWidthNoDirectionOrTheEyeIsInLine()
    {
        var ribbon = new TrailRibbon(4);
        var thin = ribbon.Poses(Strip([3, 2, 1, 0], width: 0), Eye);
        var still = ribbon.Poses(Strip([1, 1, 1, 1]), Eye);
        var head = ribbon.Poses(Strip([3, 2, 1, 0]), new Vector3(-10, 0, 0));

        foreach (var poses in new[] { thin, still, head })
        {
            for (var i = 0; i < 4; i++)
            {
                Near.Vector(Where(poses[i * 2]), Where(poses[(i * 2) + 1]), tolerance: 1e-6);
            }
        }
    }
}
