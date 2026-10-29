using Orblit.Filament;
using Orblit.VectorMath;

namespace Orblit.Examples.Tests;

/// <summary>
/// The world the blocks example builds and the body that walks in it. The
/// fixture only looks, so these cover walking, digging and what is drawn. The
/// player starts on sand at column 57, row 57, with a hill a block higher
/// behind and to each side and the sea ahead.
/// </summary>
public sealed class VoxelTests
{
    private const byte Air = 0;
    private const byte Grass = 1;
    private const byte Stone = 3;
    private const byte Deepslate = 4;
    private const byte Sand = 5;
    private const byte Water = 8;
    private const byte Log = 9;
    private const byte Leaves = 10;

    // Long enough to land from where the world puts you.
    private const double Landed = 1.5;

    private static readonly RenderCamera Camera = new ViewPoint().ToRenderCamera();

    [Fact]
    public void TheSeedGivesTheSameWorldEveryRun()
    {
        var first = Blocks(new VoxelExample());
        var again = Blocks(new VoxelExample());

        Assert.Equal(first.Transforms, again.Transforms);
        Assert.Equal(first.Colours, again.Colours);
        // Pinned from this port's own run. The Dart trees came from another generator.
        Assert.Equal("e1163348d0f14cc1", SceneDigest.Raw(first.Transforms));
        Assert.Equal("a0823583d2126334", SceneDigest.Raw(first.Colours));
    }

    /// <summary>
    /// Each world against Dart's, with its trees off. Nothing else in a world
    /// is random, so the whole frame matches. With trees, worlds 2 and 3 grow
    /// one in the middle and stand the body on it.
    /// </summary>
    [Theory]
    [InlineData("1", "c8ee7e0b27fbec4a")]
    [InlineData("2", "44ffbfb8e62373ea")]
    [InlineData("3", "2f8f13433930de94")]
    [InlineData("4", "71002fde19133b41")]
    public void EachWorldWithoutTreesMatchesDart(string world, string frame)
    {
        var example = new VoxelExample();
        example.Control<Toggle>("Trees").Change(false);
        example.Control<Choice>("World").Select!(world);

        Assert.Equal(frame, SceneDigest.Of(SceneDigest.Message(example, 1)));
    }

    [Fact]
    public void OnlyABlockBesideAirIsDrawn()
    {
        var example = new VoxelExample();
        var (side, tall) = example.Extent;
        var seen = 0;
        var solid = 0;
        foreach (var (x, y, z) in Cells(side, tall))
        {
            if (example.BlockAt(x, y, z) == 0)
            {
                continue;
            }

            solid++;
            int[][] around = [[1, 0, 0], [-1, 0, 0], [0, 1, 0], [0, -1, 0], [0, 0, 1], [0, 0, -1]];
            seen += around.Any(d => example.BlockAt(x + d[0], y + d[1], z + d[2]) == 0) ? 1 : 0;
        }

        var drawn = Blocks(example);

        Assert.Equal(seen, example.DrawnCount);
        Assert.True(seen * 2 < solid);
        Assert.Equal(seen, drawn.Count);
        Assert.Equal(seen * 3, drawn.Colours.Length);
        Assert.Equal($"{seen} blocks drawn, one buffer.", example.Settings().OfType<Caption>().First().Label);
    }

    [Fact]
    public void TreesStandOnGrassAndGoWhenTurnedOff()
    {
        var example = new VoxelExample();
        var (side, tall) = example.Extent;
        var trunks = Cells(side, tall)
            .Where(c => example.BlockAt(c.X, c.Y, c.Z) == Log && example.BlockAt(c.X, c.Y - 1, c.Z) != Log)
            .ToList();

        example.Control<Toggle>("Trees").Change(false);

        Assert.NotEmpty(trunks);
        Assert.All(trunks, c => Assert.Equal(Grass, example.BlockAt(c.X, c.Y - 1, c.Z)));
        Assert.DoesNotContain(Cells(side, tall), c => example.BlockAt(c.X, c.Y, c.Z) is Log or Leaves);
    }

    /// <summary>A tree with no other trunk near it, so nothing else grew into its crown.</summary>
    [Fact]
    public void ATreeIsATrunkUnderARoundCrown()
    {
        var example = new VoxelExample();
        var (side, tall) = example.Extent;
        var logs = Cells(side, tall).Where(c => example.BlockAt(c.X, c.Y, c.Z) == Log).ToList();
        var top = logs.First(c =>
            example.BlockAt(c.X, c.Y + 1, c.Z) != Log
            && c.X >= 2 && c.Z >= 2 && c.X < side - 2 && c.Z < side - 2
            && logs.All(o => (o.X == c.X && o.Z == c.Z) || Math.Max(Math.Abs(o.X - c.X), Math.Abs(o.Z - c.Z)) > 5));

        int LeavesAt(int dy) => Square(2).Count(d => example.BlockAt(top.X + d.X, top.Y + dy, top.Z + d.Z) == Leaves);

        // Five across less the corners and the trunk, then three across less
        // the corners, then a cap.
        Assert.Equal([20, 20, 5, 1, 0], new[] { -1, 0, 1, 2, 3 }.Select(LeavesAt));
        Assert.Equal(Log, example.BlockAt(top.X, top.Y - 1, top.Z));
        Assert.Equal(Leaves, example.BlockAt(top.X, top.Y + 1, top.Z));
        Assert.NotEqual(Leaves, example.BlockAt(top.X + 2, top.Y, top.Z + 2));
        Assert.NotEqual(Leaves, example.BlockAt(top.X + 1, top.Y + 1, top.Z + 1));
    }

    [Fact]
    public void TheSeaFillsToItsLevelAndGoesWithTheNextBuild()
    {
        var example = new VoxelExample();
        var (side, tall) = example.Extent;
        var water = Cells(side, tall).Where(c => example.BlockAt(c.X, c.Y, c.Z) == Water).ToList();
        var note = example.Control<Toggle>("Sea").Note;

        example.Control<Toggle>("Sea").Change(false);
        var kept = Cells(side, tall).Count(c => example.BlockAt(c.X, c.Y, c.Z) == Water);
        example.Control<Toggle>("Trees").Change(false);

        Assert.NotEmpty(water);
        Assert.Equal(4, water.Max(c => c.Y));
        Assert.Equal("Everything below 4 is under it", note);
        Assert.Equal(water.Count, kept);
        Assert.DoesNotContain(Cells(side, tall), c => example.BlockAt(c.X, c.Y, c.Z) == Water);
    }

    /// <summary>
    /// Deep stone darkens below 0.28 of the height. At the highest relief
    /// that is under row 9.52. At the default the threshold has slack, as
    /// rows 6.16 and 6.6 round the same way.
    /// </summary>
    [Fact]
    public void DeepStoneDarkensInTheLowestPartOfTheWorld()
    {
        var example = new VoxelExample();
        var relief = example.Control<Setting>("Relief");
        relief.Change(26);
        relief.Settle?.Invoke(26);
        var (side, tall) = example.Extent;

        Assert.Equal(34, tall);
        Assert.Equal(9, Cells(side, tall).Where(c => example.BlockAt(c.X, c.Y, c.Z) == Deepslate).Max(c => c.Y));
    }

    [Fact]
    public void TheBodyFallsOntoTheBlockUnderIt()
    {
        var example = new VoxelExample();
        var start = Eye(example, 0);

        var landed = Eye(example, Settle(example));
        var still = Eye(example, Landed + 0.05);

        Assert.Equal(new Vector3(0.5, 8.7, 0.5), start);
        // On the sand at height five, so the feet are at six and the eyes a
        // metre and sixty-two over them.
        Assert.Equal(Sand, example.BlockAt(57, 5, 57));
        Assert.Equal(7.62, landed.Y, 9);
        Assert.Equal(landed, still);
    }

    [Fact]
    public void SpaceJumpsAndGravityBringsItDown()
    {
        var example = new VoxelExample();
        Settle(example);

        example.Held.Add("Space");
        var up = Eye(example, Landed + 0.05).Y;
        example.Held.Clear();
        var heights = Enumerable.Range(2, 40).Select(i => Eye(example, Landed + (i * 0.05)).Y).ToList();

        Assert.Equal(8.04, up, 9);
        Assert.Equal(9.195, heights.Max(), 9);
        Assert.Equal(7.62, heights[^1], 9);
    }

    /// <summary>
    /// Jumping at the hill behind, a block high, the body is held back until
    /// its knee clears the top at seven. Standing, the knee and the feet are in
    /// the same block, so only a jump shows where the knee is.
    /// </summary>
    [Fact]
    public void AJumpClimbsTheHillOnceTheKneeClearsIt()
    {
        var example = new VoxelExample();
        var from = Eye(example, Settle(example));
        example.Held.UnionWith(["KeyS", "Space"]);

        var (before, at) = (from, from);
        for (var frame = 1; frame <= 60 && at.Z == from.Z; frame++)
        {
            before = at;
            at = Eye(example, Landed + (frame / 60.0));
        }

        Assert.NotEqual(from.Z, at.Z);
        // The knee is a fifth of a metre over the feet.
        Assert.InRange(before.Y - 1.62, 6.8, 6.9);
    }

    /// <summary>
    /// Straight ahead along the line the body lands on, the sea starts six
    /// blocks out, one deep over sand. Water is not solid, so the body walks
    /// in and stands on the sand under it.
    /// </summary>
    [Fact]
    public void TheBodyWalksIntoTheSeaAndStandsOnItsBed()
    {
        var example = new VoxelExample();
        var at = Settle(example);
        example.Look(-0.7, 0);

        example.Held.Add("KeyW");
        while (Eye(example, at += 1 / 60.0).Z < 6)
        {
            Assert.True(at < Landed + 2, "The walk never reached the sea.");
        }

        example.Held.Clear();
        var eye = Eye(example, Settle(example, at));

        Assert.Equal((Water, Sand), (example.BlockAt(57, 4, 62), example.BlockAt(57, 3, 62)));
        Assert.Equal(62, (int)Math.Floor(eye.Z + 56.5));
        Assert.InRange(eye.Y - 1.62, 4, 4.08);
    }

    /// <summary>Half a block rounds away from zero, as Dart's round does.</summary>
    [Fact]
    public void HalfABlockOfReachRoundsUp()
    {
        var example = new VoxelExample();
        var reach = example.Control<Setting>("Reach");

        reach.Change(40.5);
        reach.Settle?.Invoke(40.5);

        Assert.Equal(83, example.Extent.Side);
    }

    /// <summary>
    /// The hill behind is dug out at the knee and capped with stone at the
    /// chest. Walking into it then slides along it, held back by the chest.
    /// </summary>
    [Fact]
    public void StoneAtTheChestStopsAWalkThatTheKneeWouldNot()
    {
        var example = new VoxelExample();
        var from = Eye(example, Settle(example));

        // Down and to the hill, then level, to the face of the block past it.
        example.Look(Math.PI - 0.8, -0.45);
        example.Dig();
        example.Look(0, 0.6);
        example.Place();
        example.Held.Add("KeyW");
        var to = Eye(example, Landed + 0.05);

        Assert.Equal((Air, Stone), (example.BlockAt(57, 6, 56), example.BlockAt(57, 7, 56)));
        Assert.Equal(from.Z, to.Z);
        Assert.Equal(0.35 * Math.Sin(Math.PI - 0.1), to.X - from.X, 9);
    }

    /// <summary>
    /// In a pit with stone over it, a jump stops when the head, 1.8 over the
    /// feet, would reach the stone at seven.
    /// </summary>
    [Fact]
    public void AJumpUnderStoneStopsAtTheHead()
    {
        var example = new VoxelExample();
        var at = Settle(example);

        // Stone on the hill's face, a step back, the pit, then the stone
        // over it against the first.
        example.Look(Math.PI - 0.8, 0.15);
        example.Place();
        example.Held.Add("KeyS");
        Eye(example, at += 0.05);
        example.Held.Clear();
        example.Look(0, -3);
        example.Dig();
        var rest = Eye(example, at = Settle(example, at));
        example.Look(0, 2.5);
        example.Place();
        example.Held.Add("Space");
        var peak = Enumerable.Range(1, 60).Max(frame => Eye(example, at + (frame / 60.0)).Y);

        Assert.Equal(Stone, example.BlockAt(57, 7, 57));
        Assert.True(peak > rest.Y, $"No jump from {rest.Y}.");
        Assert.True(peak - 1.62 + 1.8 < 7, $"The head reached {peak - 1.62 + 1.8}.");
    }

    /// <summary>Down the slope ahead, the sand is a little over five blocks along the view.</summary>
    [Fact]
    public void TheReachIsSixBlocks()
    {
        var example = new VoxelExample();
        Settle(example);

        example.Look(0, -0.37);
        example.Dig();

        Assert.Equal(Air, example.BlockAt(59, 4, 60));
    }

    /// <summary>
    /// Stone placed into the eye's own block leaves the view inside it, where
    /// there is no empty face to place on.
    /// </summary>
    [Fact]
    public void FromInsideABlockNothingIsPlaced()
    {
        var example = new VoxelExample();
        Settle(example);
        example.Look(0, -2);
        example.Dig();
        Settle(example, Landed);

        example.Look(Math.PI - 0.8, 1.5);
        example.Place();
        var before = Blocks(example).Revision;
        example.Place();

        Assert.Equal(Stone, example.BlockAt(57, 6, 57));
        Assert.Equal(before, Blocks(example).Revision);
    }

    [Theory]
    [InlineData("KeyW", 0.225476, 0.267695)]
    [InlineData("KeyS", 0, 0)]
    [InlineData("KeyD", 0.267695, 0)]
    [InlineData("KeyA", 0, 0.225476)]
    public void EachKeyWalksItsWayUntilAWallStopsIt(string key, double x, double z)
    {
        var example = new VoxelExample();
        var from = Eye(example, Settle(example));

        example.Held.Add(key);
        var to = Eye(example, Landed + 0.05);

        // Backwards is into the hill on both axes. Sideways is into it on
        // one, and the other still moves, which is sliding along a wall.
        Assert.Equal(x, to.X - from.X, 6);
        Assert.Equal(z, to.Z - from.Z, 6);
    }

    [Fact]
    public void TwoKeysTogetherWalkNoFasterThanOne()
    {
        var example = new VoxelExample();
        var from = Eye(example, Settle(example));

        example.Held.UnionWith(["KeyW", "KeyD"]);
        var to = Eye(example, Landed + 0.05);

        Assert.Equal(0.35, Math.Sqrt(Math.Pow(to.X - from.X, 2) + Math.Pow(to.Z - from.Z, 2)), 9);
    }

    [Fact]
    public void AGapInTheFramesMovesOneFrameAtMost()
    {
        var example = new VoxelExample();
        var from = Eye(example, Settle(example));

        example.Held.Add("KeyW");
        var back = Eye(example, 1);
        var gap = Eye(example, 10);

        Assert.Equal(from, back);
        Assert.Equal(0.225476, gap.X - from.X, 6);
    }

    [Fact]
    public void TheClockRunsOnWhileYouAreNotWalking()
    {
        var example = new VoxelExample();
        example.Control<Toggle>("Walk").Change(false);
        example.Scene(Camera, 100);

        example.Control<Toggle>("Walk").Change(true);
        example.Held.Add("KeyW");
        var to = Eye(example, 100.01);

        Assert.Equal(0.5 + (Math.Sin(0.7) * 0.07), to.X, 9);
    }

    [Fact]
    public void NotWalkingLeavesTheGalleryCamera()
    {
        var example = new VoxelExample();
        var walking = example.Scene(Camera, 0).Camera;
        var walkNote = example.Control<Toggle>("Walk").Note;
        var wasWalking = example.Walking;

        example.Control<Toggle>("Walk").Change(false);

        Assert.True(wasWalking);
        Assert.False(example.Walking);
        Assert.Equal(68, walking.FieldOfView);
        Assert.Equal(Camera, example.Scene(Camera, 1).Camera);
        Assert.Equal("WASD, space, drag to look, click to dig", walkNote);
        Assert.Equal("Drag to orbit the world instead", example.Control<Toggle>("Walk").Note);
    }

    [Fact]
    public void WalkingAgainStandsYouInTheMiddle()
    {
        var example = new VoxelExample();
        Settle(example);

        example.Control<Toggle>("Walk").Change(false);
        example.Control<Toggle>("Walk").Change(true);

        Assert.Equal(new Vector3(0.5, 8.7, 0.5), Eye(example, Landed));
    }

    [Theory]
    [InlineData(10, 1.5)]
    [InlineData(-10, -1.5)]
    public void TheViewStopsShortOfStraightUpAndDown(double pitch, double held)
    {
        var example = new VoxelExample();

        example.Look(0.3, pitch);
        var camera = example.Scene(Camera, 0).Camera;
        var facing = camera.Target - camera.Position;

        Assert.Equal(Math.Sin(held), facing.Y, 9);
        Assert.Equal(Math.Sin(1.0) * Math.Cos(held), facing.X, 9);
    }

    [Fact]
    public void DigTakesOutTheBlockUnderfootAndTheBodyDropsIntoIt()
    {
        var example = new VoxelExample();
        Settle(example);
        var before = Blocks(example);

        example.Look(0, -2);
        example.Dig();
        var after = Blocks(example);
        var eye = Eye(example, Settle(example, Landed));

        Assert.Equal(0, example.BlockAt(57, 5, 57));
        Assert.Equal(before.Revision + 1, after.Revision);
        Assert.NotEqual(before.Transforms, after.Transforms);
        // A body counts as standing once its feet are within 0.08 of a block,
        // so a fall can stop just short of the top.
        Assert.InRange(eye.Y - 1.62, 5, 5.08);
    }

    [Fact]
    public void PlacePutsStoneOnTheFaceLookedAt()
    {
        var example = new VoxelExample();
        Settle(example);
        var before = Blocks(example).Revision;

        example.Look(0, -2);
        example.Place();

        Assert.Equal(Stone, example.BlockAt(57, 6, 57));
        Assert.Equal(before + 1, Blocks(example).Revision);
    }

    [Fact]
    public void NothingInReachIsNothingToDigOrPlace()
    {
        var example = new VoxelExample();
        Settle(example);
        var before = Blocks(example).Revision;

        example.Look(0, 3);
        example.Dig();
        example.Place();

        Assert.Equal(before, Blocks(example).Revision);
    }

    private static Population Blocks(VoxelExample example) => Assert.Single(example.Scene(Camera, 0).Populations);

    private static Vector3 Eye(VoxelExample example, double seconds) => example.Scene(Camera, seconds).Camera.Position;

    // Thirty frames from a start, as the gallery would draw them.
    private static double Settle(VoxelExample example, double from = 0)
    {
        for (var i = 0; i <= 30; i++)
        {
            example.Scene(Camera, from + (i * 0.05));
        }

        return from + Landed;
    }

    private static IEnumerable<(int X, int Y, int Z)> Cells(int side, int tall) =>
        from y in Enumerable.Range(0, tall)
        from z in Enumerable.Range(0, side)
        from x in Enumerable.Range(0, side)
        select (x, y, z);

    private static IEnumerable<(int X, int Z)> Square(int spread) =>
        from x in Enumerable.Range(-spread, (spread * 2) + 1)
        from z in Enumerable.Range(-spread, (spread * 2) + 1)
        select (x, z);
}
