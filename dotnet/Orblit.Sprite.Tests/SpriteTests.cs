using Orblit.VectorMath;
using static Orblit.Sprite.Tests.Tree;

namespace Orblit.Sprite.Tests;

public class SpriteTests
{
    private static Atlas Sheet(string image, long width, long height, string name = "frame", int count = 0) =>
        Atlas.Grid(image, width, height, 32, 32, name, count);

    private static Atlas Read(string text) => Some(Atlas.Read(text));

    private static Region Need(Region? region) => Some(region);

    // an atlas from a grid

    [Fact]
    public void ItCutsTheWholeSheetInReadingOrder()
    {
        var sheet = Sheet("hero.png", 96, 64, "run");
        Assert.Equal(6, sheet.Length);
        Assert.Equal(0, Need(sheet["run_0"]).X);
        Assert.Equal(64, Need(sheet["run_2"]).X);
        Assert.Equal(32, Need(sheet["run_3"]).Y);
    }

    [Fact]
    public void ACountStopsItEarly()
    {
        var four = Sheet("a.png", 96, 64, count: 4);
        Assert.Equal(4, four.Length);
    }

    [Fact]
    public void SpacingAndMarginAreCounted()
    {
        var spaced = Atlas.Grid("a.png", 70, 36, 32, 32, spacing: 2, margin: 2);
        Assert.Equal(2, spaced.Length);
        Assert.Equal(36, Need(spaced["frame_1"]).X);
    }

    [Fact]
    public void ACellOfNoSizeIsRefusedRatherThanLooping()
    {
        Assert.Equal(0, Atlas.Grid("a.png", 96, 64, 0, 0).Length);
    }

    [Fact]
    public void TextureCoordinatesAreFractionsOfTheImage()
    {
        var uv = Need(Sheet("hero.png", 96, 64, "run")["run_1"]).Uv(96, 64);
        Assert.Equal(32.0 / 96, uv.U0, 1e-9);
        Assert.Equal(64.0 / 96, uv.U1, 1e-9);
        Assert.Equal(0, uv.V0, 1e-9);
    }

    // an atlas from a packer

    private static string Packed(bool asList = false)
    {
        var frame = new (string, object?)[]
        {
            ("frame", Box(10, 20, 30, 40)),
            ("rotated", true),
            ("trimmed", true),
            ("spriteSourceSize", Map(("x", 3L), ("y", 4L))),
            ("sourceSize", Map(("w", 36L), ("h", 48L))),
        };
        object? frames = asList
            ? List(Map([("filename", "walk_1"), .. frame]))
            : Map(("walk_1", Map(frame)));
        return Text(Map(
            ("frames", frames),
            ("meta", Map(("image", "sheet.png"), ("size", Map(("w", 512L), ("h", 256L)))))));
    }

    [Fact]
    public void BothShapesOfTheSameFormatReadTheSame()
    {
        // Which one a file has depends on a checkbox in the tool, which is not
        // a thing a game should have to care about.
        var asMap = Read(Packed());
        var asList = Read(Packed(asList: true));

        Assert.Equal(asMap.Length, asList.Length);
        Assert.Equal(Need(asMap["walk_1"]).X, Need(asList["walk_1"]).X);
        Assert.Equal("sheet.png", asMap.Image);
        Assert.Equal(512, asMap.Width);
    }

    [Fact]
    public void RotationAndTrimmingAreCarriedNotDropped()
    {
        // Dropping rotation draws the handful of frames that packed sideways on
        // their side; dropping the trim offset makes every frame jump about.
        var region = Need(Read(Packed())["walk_1"]);
        Assert.True(region.Rotated);
        Assert.True(region.Trimmed);
        Assert.Equal(3, region.OffsetX);
        Assert.Equal((36L, 48L), region.PlacedSize);
    }

    [Fact]
    public void AnUntrimmedRegionIsItsOwnSize()
    {
        var plain = Atlas.Grid("a.png", 32, 32, 32, 32);
        Assert.Equal((32L, 32L), Need(plain["frame_0"]).PlacedSize);
    }

    [Fact]
    public void SomethingThatIsNotAnAtlasIsNullNotAnException()
    {
        // An atlas is a path somebody typed; refusing to load the level because
        // one is malformed is worse than drawing it without.
        Assert.Null(Atlas.Read("not json at all"));
        Assert.Null(Atlas.Read("{\"nothing\": 1}"));
    }

    [Fact]
    public void FramesAreOrderedByNumberNotByString()
    {
        // Plain string order puts run_10 before run_2, which reverses the
        // middle of every animation with more than nine frames.
        var frames = Enumerable.Range(0, 12).Select(i => (Key: $"run_{i}", Value: (object?)Map(("frame", Box(i, 0, 1, 1)))));
        var many = Read(Text(Map(("frames", Map([.. frames])))));
        Assert.Equal([0L, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11], many.Sequence("run_").Select(region => region.X));
    }

    [Fact]
    public void ASequenceTakesOnlyWhatMatches()
    {
        var mixed = Read(Text(Map(("frames", Map(
            ("run_0", Map(("frame", Box(1, 0, 1, 1)))),
            ("idle_0", Map(("frame", Box(2, 0, 1, 1)))))))));
        Assert.Single(mixed.Sequence("run_"));
    }

    // an animation

    private static Atlas RunSheet(long width = 128) => Sheet("hero.png", width, 32, "run");

    [Fact]
    public void ItShowsEachFrameInTurn()
    {
        var run = SpriteAnimation.From(RunSheet(), "run", fps: 10);
        Assert.Equal(0.4, run.Duration, 1e-9);
        Assert.Equal(0, run.IndexAt(0));
        Assert.Equal(1, run.IndexAt(0.15));
        Assert.Equal(2, run.IndexAt(0.25));
        Assert.Equal(3, run.IndexAt(0.35));
    }

    [Fact]
    public void ALoopStartsAgainAndIsCheapToAskAboutLate()
    {
        var run = SpriteAnimation.From(RunSheet(), "run", fps: 10);
        Assert.Equal(0, run.IndexAt(0.45));
        Assert.Equal(0, run.IndexAt(1000.45));
    }

    [Fact]
    public void ARunThatDoesNotLoopHoldsItsLastFrame()
    {
        var once = SpriteAnimation.From(RunSheet(), "run", fps: 10, loop: false);
        Assert.Equal(3, once.IndexAt(9));
        Assert.True(once.DoneBy(0.5));
        Assert.False(once.DoneBy(0.2));
    }

    [Fact]
    public void PingPongComesBackDownWithoutHoldingTheFarEndTwice()
    {
        // A plain loop shows a jump on every repeat; a ping-pong that repeated
        // the end frame would hold it for twice as long as every other.
        var breathe = SpriteAnimation.From(RunSheet(), "run", fps: 10, pingPong: true);
        Assert.Equal(0.8, breathe.Cycle, 1e-9);
        Assert.Equal(3, breathe.IndexAt(0.35));
        Assert.Equal(3, breathe.IndexAt(0.45));
        Assert.Equal(2, breathe.IndexAt(0.55));
        Assert.Equal(0, breathe.IndexAt(0.75));
    }

    [Fact]
    public void FramesCanBeHeldForDifferentLengths()
    {
        // An animator's timing is not even: a punch holds on the wind-up and
        // flies through the strike.
        var sheet = RunSheet();
        var punch = new SpriteAnimation(
            [
                new Frame(Need(sheet["run_0"]), 0.5),
                new Frame(Need(sheet["run_1"]), 0.05),
                new Frame(Need(sheet["run_2"]), 0.05),
            ],
            loop: false);

        Assert.Equal(0, punch.IndexAt(0.4));
        Assert.Equal(1, punch.IndexAt(0.52));
        Assert.Equal(2, punch.IndexAt(0.58));
    }

    [Fact]
    public void AnEmptyAnimationShowsNothingRatherThanThrowing()
    {
        var nothing = new SpriteAnimation([]);
        Assert.True(nothing.IsEmpty);
        Assert.Null(nothing.At(1));
        Assert.Equal(0, nothing.IndexAt(1));
    }

    [Fact]
    public void ANegativeTimeIsTheBeginning()
    {
        var run = SpriteAnimation.From(RunSheet(), "run", fps: 10);
        Assert.Equal(0, run.IndexAt(-5));
    }

    // a flipbook

    private static Flipbook Make()
    {
        var sheet = Sheet("hero.png", 64, 32, "f");
        return new Flipbook(
            new OrderedDictionary<string, SpriteAnimation>
            {
                ["idle"] = SpriteAnimation.From(sheet, "f", fps: 10),
                ["walk"] = SpriteAnimation.From(sheet, "f", fps: 20),
            },
            playing: "idle");
    }

    [Fact]
    public void PlayingTheSameClipAgainIsNotARestart()
    {
        // A game says "walk" on every frame the stick is held; restarting on
        // each of those is a character stuck on frame one.
        var book = Make();
        book.Advance(0.15);
        book.Play("idle");
        Assert.Equal(0.15, book.Since, 1e-9);
    }

    [Fact]
    public void ChangingClipStartsTheNewOneFromTheBeginning()
    {
        var book = Make();
        book.Advance(0.15);
        book.Play("walk");
        Assert.Equal("walk", book.Playing);
        Assert.Equal(0, book.Since);
    }

    [Fact]
    public void RestartingIsPossibleWhenItIsAskedFor()
    {
        var book = Make();
        book.Advance(0.15);
        book.Play("idle", restart: true);
        Assert.Equal(0, book.Since);
    }

    [Fact]
    public void AClipItDoesNotHaveIsIgnored()
    {
        var book = Make();
        book.Play("nonesuch");
        Assert.Equal("idle", book.Playing);
    }

    [Fact]
    public void ItSaysWhichFrameToDraw()
    {
        var book = Make();
        book.Advance(0.15);
        Assert.Equal("f_1", Need(book.Frame).Name);
    }

    // parallax

    [Fact]
    public void NearerLayersMoveMoreThanFurtherOnes()
    {
        var scene = new Parallax([new Layer("sky.png", Depth: 0), new Layer("hills.png", Depth: 0.2), new Layer("ground.png", Depth: 1)]);
        var places = scene.At(100, 0);

        Assert.Equal(0, places[0], 1e-9);
        Assert.Equal(-20, places[1], 1e-9);
        Assert.Equal(-100, places[2], 1e-9);
    }

    [Fact]
    public void AWrappingLayerStaysInsideOneWidthHoweverFarItScrolls()
    {
        // What keeps a long-running background from juddering: the numbers
        // never grow large enough to lose precision.
        var scene = new Parallax([new Layer("a.png", Depth: 1, Size: 64)]);
        foreach (var camera in new[] { 0.0, 100.0, 10000.0, 1e7 })
        {
            var place = scene.At(camera, 0).Single();
            Assert.True(place > -64.0000001, $"{place}");
            Assert.True(place <= 0.0000001, $"{place}");
        }
    }

    [Fact]
    public void DriftMovesALayerWithNoCameraAtAll()
    {
        // A still sky reads as a painted backdrop rather than as weather.
        var scene = new Parallax([new Layer("cloud.png", Depth: 0, Drift: 3)]);
        Assert.Equal(6, scene.At(0, 2).Single(), 1e-9);
    }

    [Fact]
    public void EnoughCopiesAreAskedForToCoverTheScreen()
    {
        // One short is a gap at the edge that appears once per wrap.
        var layer = new Layer("a.png", Size: 64);
        Assert.Equal(6, Parallax.CopiesFor(layer, 200));
        Assert.Equal(1, Parallax.CopiesFor(new Layer("a.png"), 200));
    }

    // a 2D camera

    [Fact]
    public void ItFollowsWithoutSnapping()
    {
        var view = new View2(width: 10, height: 10);
        view.Follow(new Vector2(100, 0), 0.1);

        Assert.True(view.At.X > 0);
        Assert.True(view.At.X < 100);
    }

    [Fact]
    public void FollowingIsTheSameAtAnyFrameRate()
    {
        // A camera that follows by a fixed fraction each frame follows twice as
        // fast at twice the frame rate.
        var slow = new View2(width: 10, height: 10);
        var fast = new View2(width: 10, height: 10);
        for (var i = 0; i < 30; i++)
        {
            slow.Follow(new Vector2(100, 0), 1.0 / 30);
        }

        for (var i = 0; i < 120; i++)
        {
            fast.Follow(new Vector2(100, 0), 1.0 / 120);
        }

        Assert.Equal(slow.At.X, fast.At.X, 1.5);
    }

    [Fact]
    public void ItStaysInsideItsBounds()
    {
        var view = new View2(width: 10, height: 10, bounds: new ViewBounds(new Vector2(0, 0), new Vector2(100, 100)));
        view.JumpTo(new Vector2(-50, 200));

        Assert.Equal(5, view.At.X, 1e-9);
        Assert.Equal(95, view.At.Y, 1e-9);
    }

    [Fact]
    public void AWorldNarrowerThanTheScreenCentresRatherThanInverting()
    {
        // Clamping to a negative range would put the camera outside the world
        // it was being kept inside.
        var view = new View2(width: 100, height: 100, bounds: new ViewBounds(new Vector2(0, 0), new Vector2(20, 20)));
        view.JumpTo(new Vector2(0, 0));
        Assert.Equal(10, view.At.X, 1e-9);
    }

    // a tile map

    private static string Tiled(string orientation = "orthogonal", string? encoding = null, string? compression = null, bool external = false)
    {
        var ground = new List<(string, object?)>
        {
            ("type", "tilelayer"),
            ("name", "ground"),
            ("width", 3L),
            ("height", 2L),
            ("data", List(1L, 0L, 2L, 0L, 3L, 0L)),
        };
        if (encoding is not null)
        {
            ground.Add(("encoding", encoding));
        }

        if (compression is not null)
        {
            ground.Add(("compression", compression));
        }

        return Text(Map(
            ("orientation", orientation),
            ("width", 3L),
            ("height", 2L),
            ("tilewidth", 16L),
            ("tileheight", 16L),
            ("layers", List(Map([.. ground]), Map(("type", "objectgroup"), ("name", "things")))),
            ("tilesets", external ? List(Map(("firstgid", 1L), ("source", "outside.tsx"))) : List(StockSet(1, "grass"), StockSet(9, "stone")))));
    }

    private static OrderedDictionary<string, object?> StockSet(long first, string name) => Map(
        ("firstgid", first),
        ("name", name),
        ("image", $"{name}.png"),
        ("tilewidth", 16L),
        ("tileheight", 16L),
        ("columns", 4L),
        ("tilecount", 8L));

    private static TileMap ReadMap(string text) => Some(TileMap.Read(text));

    private static TileLayer Ground(TileMap map) => Some(map.Layer("ground"));

    [Fact]
    public void ItReadsTheLayersAndSkipsTheOnesItDoesNotDraw()
    {
        var map = ReadMap(Tiled());
        Assert.Equal(3, map.Width);
        Assert.Single(map.Layers);
        Assert.Equal(1, Ground(map).At(0, 0));
        Assert.Equal(2, Ground(map).At(2, 0));
        Assert.Equal(3, Ground(map).At(1, 1));
    }

    [Fact]
    public void OutsideTheMapIsEmptyRatherThanAnError()
    {
        // A map is walked by things that wander off the edge of it.
        var ground = Ground(ReadMap(Tiled()));
        Assert.Equal(0, ground.At(-1, 0));
        Assert.Equal(0, ground.At(99, 99));
        Assert.True(ground.IsEmptyAt(1, 0));
    }

    [Fact]
    public void ATileIsTracedBackToTheSetItCameFrom()
    {
        // Getting this backwards draws the right shape from the wrong sheet.
        var map = ReadMap(Tiled());
        Assert.Equal("grass", Some(map.SetFor(1)).Name);
        Assert.Equal("grass", Some(map.SetFor(8)).Name);
        Assert.Equal("stone", Some(map.SetFor(9)).Name);
        Assert.Equal("stone", Some(map.SetFor(12)).Name);
        Assert.Null(map.SetFor(0));
    }

    [Fact]
    public void ATileKnowsWhereItSitsInItsOwnImage()
    {
        var set = ReadMap(Tiled()).Tilesets[0];
        Assert.Equal((0L, 0L, 16L, 16L), set.RectOf(1));
        Assert.Equal((0L, 16L, 16L, 16L), set.RectOf(5));
    }

    [Fact]
    public void APointOnTheMapPicksOutACell()
    {
        var map = ReadMap(Tiled());
        Assert.Equal((0L, 0L), map.CellAt(0, 0));
        Assert.Equal((1L, 1L), map.CellAt(31.5, 17));
        Assert.Equal((-1L, -1L), map.CellAt(-1, -1));
    }

    [Fact]
    public void SolidityCanBeAskedOfTheWholeMapOrOfNamedLayers()
    {
        var map = ReadMap(Tiled());
        Assert.True(map.IsSolidAt(0, 0));
        Assert.False(map.IsSolidAt(1, 0));
        Assert.False(map.IsSolidAt(0, 0, only: new HashSet<string> { "nothing" }));
    }

    [Fact]
    public void WhatItCannotReadComesBackNullRatherThanHalfAMap()
    {
        // A map that silently lost a floor is worse than one that did not load.
        Assert.Null(TileMap.Read(Tiled(orientation: "isometric")));
        Assert.Null(TileMap.Read(Tiled(encoding: "base64")));
        Assert.Null(TileMap.Read(Tiled(compression: "zlib")));
        Assert.Null(TileMap.Read(Tiled(external: true)));
        Assert.Null(TileMap.Read("not json"));
    }

    [Fact]
    public void CsvEncodingIsReadBecauseThatIsWhatItAlreadyIs()
    {
        Assert.True(TileMap.Read(Tiled(encoding: "csv")) is not null);
    }
}
