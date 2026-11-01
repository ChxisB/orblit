using static Orblit.Sprite.Tests.Tree;

namespace Orblit.Sprite.Tests;

public class AtlasPackTests
{
    private readonly record struct Rect(long X, long Y, long W, long H);

    /// <summary>A solid rectangle of one colour.</summary>
    private static byte[] Solid(long w, long h, long r, long g, long b, long a)
    {
        var output = new byte[w * h * 4];
        for (var i = 0; i < w * h; i++)
        {
            output[i * 4] = (byte)r;
            output[(i * 4) + 1] = (byte)g;
            output[(i * 4) + 2] = (byte)b;
            output[(i * 4) + 3] = (byte)a;
        }

        return output;
    }

    /// <summary>
    /// A picture with no two pixels alike, so a rotation that lands on its side or comes out
    /// mirrored shows up as a pixel mismatch rather than nothing at all. A checkerboard or a flat
    /// colour can't tell a right rotation from a wrong one.
    /// </summary>
    private static byte[] AsymmetricPattern(long w, long h)
    {
        var output = new byte[w * h * 4];
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var i = ((y * w) + x) * 4;
                output[i] = (byte)(((x * 23) + 7) % 256);
                output[i + 1] = (byte)(((y * 61) + 13) % 256);
                output[i + 2] = (byte)((x + (y * 5) + 1) % 256);
                output[i + 3] = 255;
            }
        }

        return output;
    }

    /// <summary>
    /// A sprite <paramref name="w"/> by <paramref name="h"/> with a solid, asymmetric inner
    /// rectangle placed at (<paramref name="offsetX"/>, <paramref name="offsetY"/>) and every
    /// other texel fully transparent black. What trimming is supposed to see through to and hand
    /// back unchanged.
    /// </summary>
    private static byte[] WithTransparentMargin(long w, long h, long offsetX, long offsetY, long innerW, long innerH)
    {
        var output = new byte[w * h * 4];
        var inner = AsymmetricPattern(innerW, innerH);
        for (var y = 0; y < innerH; y++)
        {
            for (var x = 0; x < innerW; x++)
            {
                var dst = (((offsetY + y) * w) + (offsetX + x)) * 4;
                var src = ((y * innerW) + x) * 4;
                output[dst] = inner[src];
                output[dst + 1] = inner[src + 1];
                output[dst + 2] = inner[src + 2];
                output[dst + 3] = inner[src + 3];
            }
        }

        return output;
    }

    private static Rect PhysBox(Region r) => new(r.X, r.Y, r.Rotated ? r.Height : r.Width, r.Rotated ? r.Width : r.Height);

    private static bool RectsOverlap(Rect a, Rect b) => a.X < b.X + b.W && a.X + a.W > b.X && a.Y < b.Y + b.H && a.Y + a.H > b.Y;

    private static AtlasSprite Sprite(string name, long w, long h, byte[] pixels) => new(name, w, h, pixels);

    private static AtlasSprite Plain(string name, long w, long h, long red = 1, long green = 1, long blue = 1) =>
        Sprite(name, w, h, Solid(w, h, red, green, blue, 255));

    private static byte[] Texel(AtlasPackPage page, long x, long y) =>
        page.Pixels.AsSpan((int)(((y * page.Width) + x) * 4), 4).ToArray();

    private static List<AtlasSprite> TwentyFive() => [.. Enumerable.Range(0, 25).Select(i => Plain($"tile_{i}", 12, 12, i % 256, (i * 3) % 256, (i * 7) % 256))];

    private static List<AtlasSprite> Sixty() => [.. Enumerable.Range(0, 60).Select(i => Plain($"d_{i}", 5 + ((i * 3) % 20), 5 + ((i * 7) % 16), i % 256, 0, 0))];

    // packing rectangles

    [Fact]
    public void NothingOverlapsEverythingSitsInsideItsPage()
    {
        var sprites = Enumerable.Range(0, 40).Select(i => Plain(
            $"sprite_{i}", 6 + ((i * 7) % 37), 6 + ((i * 13) % 29), i % 256, (i * 3) % 256, (i * 5) % 256)).ToList();
        var result = AtlasPacker.PackAtlas(
            sprites,
            new AtlasPackOptions(maxPageSize: 256, padding: 3, border: 2, trim: false, mergeDuplicates: false));
        Assert.Empty(result.Problems);

        foreach (var page in result.Pages)
        {
            var boxes = page.Regions.Values.Select(PhysBox).ToList();
            foreach (var box in boxes)
            {
                Assert.True(box.X >= 2);
                Assert.True(box.Y >= 2);
                Assert.True(box.X + box.W <= page.Width - 2);
                Assert.True(box.Y + box.H <= page.Height - 2);
            }

            for (var i = 0; i < boxes.Count; i++)
            {
                for (var j = i + 1; j < boxes.Count; j++)
                {
                    Assert.False(RectsOverlap(boxes[i], boxes[j]), $"regions {i} and {j} overlap");
                }
            }
        }
    }

    [Fact]
    public void PaddingKeepsAGapBetweenNeighboursNotJustNoOverlap()
    {
        var sprites = Enumerable.Range(0, 12).Select(i => Sprite($"p{i}", 10, 10, Solid(10, 10, 1, 2, 3, 255))).ToList();
        const int padding = 4;
        var result = AtlasPacker.PackAtlas(
            sprites,
            new AtlasPackOptions(maxPageSize: 128, padding: padding, border: 0, trim: false, mergeDuplicates: false));
        foreach (var page in result.Pages)
        {
            var boxes = page.Regions.Values.Select(PhysBox).ToList();
            for (var i = 0; i < boxes.Count; i++)
            {
                // Every box padded out on its own right and bottom edge, checked
                // against every other box, since that is exactly the space the
                // packer reserved for it.
                var grown = new Rect(boxes[i].X, boxes[i].Y, boxes[i].W + padding, boxes[i].H + padding);
                for (var j = 0; j < boxes.Count; j++)
                {
                    if (i == j)
                    {
                        continue;
                    }

                    Assert.False(RectsOverlap(grown, boxes[j]), $"boxes {i} and {j} are closer than padding");
                }
            }
        }
    }

    [Fact]
    public void APageNeverExceedsTheMaximumItWasGiven()
    {
        var sprites = Enumerable.Range(0, 200).Select(i => Sprite($"s{i}", 8, 8, Solid(8, 8, 9, 9, 9, 255))).ToList();
        var result = AtlasPacker.PackAtlas(sprites, new AtlasPackOptions(maxPageSize: 64, trim: false));
        foreach (var page in result.Pages)
        {
            Assert.True(page.Width <= 64);
            Assert.True(page.Height <= 64);
        }
    }

    // rotation

    [Fact]
    public void ARotatedRegionReadsBackRotatedTheRightWayRound()
    {
        // A filler placed first leaves exactly one free strip behind it, too
        // narrow for the banner unless the banner turns on its side, so
        // whichever heuristic runs, only a correct rotation can have produced
        // a valid pack at all.
        var filler = Sprite("filler", 6, 10, Solid(6, 10, 5, 5, 5, 255));
        var bannerPixels = AsymmetricPattern(8, 3);
        var banner = Sprite("banner", 8, 3, bannerPixels);

        var result = AtlasPacker.PackAtlas(
            [filler, banner],
            new AtlasPackOptions(
                maxPageSize: 10,
                minPageSize: 1,
                padding: 0,
                border: 0,
                allowRotation: true,
                trim: false,
                powerOfTwo: false,
                mergeDuplicates: false,
                heuristic: MaxRectsHeuristic.BestShortSideFit));

        Assert.Empty(result.Problems);
        var page = Assert.Single(result.Pages);
        var region = Some(page.Regions.GetValueOrDefault("banner"));
        Assert.True(region.Rotated, "the only free space left was too narrow unless it turned");
        Assert.Equal(8, region.Width);
        Assert.Equal(3, region.Height);

        var recovered = AtlasPacker.ExtractRegionPixels(page.Width, page.Height, page.Pixels, region);
        Assert.Equal(bannerPixels, recovered);
    }

    [Fact]
    public void AnUnrotatedRegionReadsBackExactlyAsPacked()
    {
        var pixels = AsymmetricPattern(5, 9);
        var sprite = Sprite("plain", 5, 9, pixels);
        var result = AtlasPacker.PackAtlas(
            [sprite],
            new AtlasPackOptions(maxPageSize: 32, padding: 0, border: 0, trim: false, allowRotation: false));
        var page = Assert.Single(result.Pages);
        var region = Some(page.Regions.GetValueOrDefault("plain"));
        Assert.False(region.Rotated);
        var recovered = AtlasPacker.ExtractRegionPixels(page.Width, page.Height, page.Pixels, region);
        Assert.Equal(pixels, recovered);
    }

    // trimming

    [Fact]
    public void ATrimmedSpriteComesBackAtItsOriginalSizeAndPlace()
    {
        var original = WithTransparentMargin(20, 16, 5, 4, 9, 7);
        var sprite = Sprite("card", 20, 16, original);
        var result = AtlasPacker.PackAtlas(
            [sprite],
            new AtlasPackOptions(maxPageSize: 64, padding: 1, trim: true, allowRotation: false));
        var page = Assert.Single(result.Pages);
        var region = Some(page.Regions.GetValueOrDefault("card"));

        Assert.True(region.Trimmed);
        Assert.Equal(5, region.OffsetX);
        Assert.Equal(4, region.OffsetY);
        Assert.Equal(20, region.SourceWidth);
        Assert.Equal(16, region.SourceHeight);
        Assert.Equal(9, region.Width);
        Assert.Equal(7, region.Height);
        Assert.Equal((20L, 16L), region.PlacedSize);

        var recovered = AtlasPacker.ExtractRegionPixels(page.Width, page.Height, page.Pixels, region);
        Assert.Equal(original, recovered);
    }

    [Fact]
    public void ASpriteWithNoTransparentMarginIsNotMarkedTrimmed()
    {
        var sprite = Sprite("full", 10, 10, Solid(10, 10, 4, 5, 6, 255));
        var result = AtlasPacker.PackAtlas([sprite], new AtlasPackOptions(maxPageSize: 32, trim: true));
        var region = Some(Assert.Single(result.Pages).Regions.GetValueOrDefault("full"));
        Assert.False(region.Trimmed);
        Assert.Equal(0, region.SourceWidth);
        Assert.Equal(0, region.SourceHeight);
    }

    // extrusion

    [Fact]
    public void APlacedSpriteBleedsItsEdgeColourIntoItsPadding()
    {
        var sprite = Sprite("chip", 4, 4, Solid(4, 4, 200, 100, 50, 255));
        var result = AtlasPacker.PackAtlas(
            [sprite],
            new AtlasPackOptions(maxPageSize: 16, minPageSize: 16, padding: 3, border: 0, trim: false, extrude: 2, powerOfTwo: false));
        var page = Assert.Single(result.Pages);
        var region = Some(page.Regions.GetValueOrDefault("chip"));
        Assert.False(region.Rotated);

        for (var row = 0; row < 4; row++)
        {
            Assert.Equal(200, Texel(page, region.X + 3, region.Y + row)[0]);
        }

        // One and two texels to the right of the sprite's own right edge, both
        // inside the padding, both extruded.
        byte[] colour = [200, 100, 50, 255];
        Assert.Equal(colour, Texel(page, region.X + 4, region.Y));
        Assert.Equal(colour, Texel(page, region.X + 5, region.Y));
        Assert.Equal(colour, Texel(page, region.X, region.Y + 4));
    }

    [Fact]
    public void NothingIsExtrudedWhenExtrudeIsNought()
    {
        var sprite = Sprite("chip", 4, 4, Solid(4, 4, 200, 100, 50, 255));
        var result = AtlasPacker.PackAtlas(
            [sprite],
            new AtlasPackOptions(maxPageSize: 16, minPageSize: 16, padding: 3, trim: false, extrude: 0, powerOfTwo: false));
        var page = Assert.Single(result.Pages);
        var region = Some(page.Regions.GetValueOrDefault("chip"));
        Assert.Equal(new byte[] { 0, 0, 0, 0 }, Texel(page, region.X + 4, region.Y));
    }

    // multiple pages

    [Fact]
    public void AFullPageOverflowsIntoAnotherRatherThanGrowingWithoutLimit()
    {
        var result = AtlasPacker.PackAtlas(TwentyFive(), new AtlasPackOptions(maxPageSize: 32, padding: 1, trim: false));
        Assert.Empty(result.Problems);
        Assert.True(result.Pages.Count > 1);

        var placedNames = new HashSet<string>();
        foreach (var page in result.Pages)
        {
            placedNames.UnionWith(page.Regions.Keys);
        }

        Assert.Equal(25, placedNames.Count);
        foreach (var page in result.Pages)
        {
            Assert.True(page.Width <= 32);
            Assert.True(page.Height <= 32);
        }
    }

    [Fact]
    public void OnlyTheLastPageShrinksEarlierPagesStayAtTheMaximum()
    {
        var result = AtlasPacker.PackAtlas(
            TwentyFive(),
            new AtlasPackOptions(maxPageSize: 32, padding: 1, trim: false, powerOfTwo: false));
        foreach (var page in result.Pages.Take(result.Pages.Count - 1))
        {
            Assert.Equal(32, page.Width);
            Assert.Equal(32, page.Height);
        }

        var last = result.Pages[^1];
        Assert.True(last.Width <= 32 && last.Height <= 32);
    }

    [Fact]
    public void AnAtlasSetFindsARegionWhereverItsPageIs()
    {
        var result = AtlasPacker.PackAtlas(TwentyFive(), new AtlasPackOptions(maxPageSize: 32, padding: 1, trim: false));
        var set = result.ToAtlasSet();
        Assert.Equal(25, set.Length);
        var located = set.Locate("tile_24");
        Assert.NotNull(located);
        Assert.Contains(located.Value.Atlas, set.Pages);
        Assert.Null(set.Find("nonesuch"));
    }

    // determinism

    [Fact]
    public void TheSameInputPacksToTheSamePagesEveryTime()
    {
        var sprites = Sixty();
        var options = new AtlasPackOptions(maxPageSize: 128, padding: 2, trim: false);
        var a = AtlasPacker.PackAtlas(sprites, options);
        var b = AtlasPacker.PackAtlas(sprites, options);

        Assert.Equal(a.Pages.Count, b.Pages.Count);
        for (var i = 0; i < a.Pages.Count; i++)
        {
            Assert.Equal(a.Pages[i].Pixels, b.Pages[i].Pixels);
            Assert.Equal(a.Pages[i].Width, b.Pages[i].Width);
            Assert.Equal(a.Pages[i].Height, b.Pages[i].Height);
        }
    }

    [Fact]
    public void ShufflingTheInputListDoesNotChangeWhatIsProduced()
    {
        var sprites = Sixty();
        var shuffled = Enumerable.Reverse(sprites).ToList();
        var options = new AtlasPackOptions(maxPageSize: 128, padding: 2, trim: false);
        var a = AtlasPacker.PackAtlas(sprites, options);
        var b = AtlasPacker.PackAtlas(shuffled, options);

        Assert.Equal(a.Pages.Count, b.Pages.Count);
        for (var i = 0; i < a.Pages.Count; i++)
        {
            Assert.Equal(a.Pages[i].Pixels, b.Pages[i].Pixels);
        }
    }

    // duplicates

    [Fact]
    public void IdenticalSpritesShareOnePackedRectangle()
    {
        var pixels = AsymmetricPattern(6, 6);
        var sprites = new List<AtlasSprite>
        {
            Sprite("a", 6, 6, pixels),
            Sprite("b", 6, 6, [.. pixels]),
            Sprite("c", 6, 6, Solid(6, 6, 9, 9, 9, 255)),
        };
        var result = AtlasPacker.PackAtlas(sprites, new AtlasPackOptions(maxPageSize: 64, trim: false));
        var page = Assert.Single(result.Pages);
        var a = Some(page.Regions.GetValueOrDefault("a"));
        var b = Some(page.Regions.GetValueOrDefault("b"));
        var c = Some(page.Regions.GetValueOrDefault("c"));
        Assert.Equal((a.X, a.Y), (b.X, b.Y));
        Assert.False((a.X, a.Y) == (c.X, c.Y));
    }

    [Fact]
    public void TurningDuplicateMergingOffPacksEverySpriteOnItsOwn()
    {
        var pixels = AsymmetricPattern(6, 6);
        var sprites = new List<AtlasSprite> { Sprite("a", 6, 6, pixels), Sprite("b", 6, 6, [.. pixels]) };
        var result = AtlasPacker.PackAtlas(sprites, new AtlasPackOptions(maxPageSize: 64, trim: false, mergeDuplicates: false));
        var page = Assert.Single(result.Pages);
        var a = Some(page.Regions.GetValueOrDefault("a"));
        var b = Some(page.Regions.GetValueOrDefault("b"));
        Assert.False((a.X, a.Y) == (b.X, b.Y));
    }

    // the written descriptor

    [Fact]
    public void PackWriteAndReadGivesBackAnEqualAtlas()
    {
        var sprites = new List<AtlasSprite>
        {
            Sprite("rot", 8, 3, AsymmetricPattern(8, 3)),
            Sprite("trimmed", 12, 10, WithTransparentMargin(12, 10, 2, 1, 8, 7)),
            Sprite("plain", 5, 5, Solid(5, 5, 1, 2, 3, 255)),
        };
        var result = AtlasPacker.PackAtlas(sprites, new AtlasPackOptions(maxPageSize: 64));
        foreach (var page in result.Pages)
        {
            var atlas = page.ToAtlas("sheet.png");
            var text = AtlasPacker.WriteAtlas(atlas);
            var read = Some(Atlas.Read(text));
            Assert.Equal("sheet.png", read.Image);
            Assert.Equal(atlas.Width, read.Width);
            Assert.Equal(atlas.Height, read.Height);
            Assert.Equal(atlas.Length, read.Length);
            foreach (var name in atlas.Regions.Keys)
            {
                Assert.Equal(Some(atlas[name]), Some(read[name]));
            }
        }
    }

    // the background entry point

    [Fact]
    public async Task PackingOffTheCallingThreadGivesTheSameAnswer()
    {
        var sprites = Enumerable.Range(0, 10).Select(i => Sprite($"s{i}", 8, 8, Solid(8, 8, 1, 2, 3, 255))).ToList();
        var options = new AtlasPackOptions(maxPageSize: 64, trim: false);
        var direct = AtlasPacker.PackAtlas(sprites, options);
        var background = await AtlasPacker.PackAtlasInBackground(sprites, options);
        Assert.Equal(direct.Pages.Count, background.Pages.Count);
        Assert.Equal(direct.Pages.Single().Pixels, background.Pages.Single().Pixels);
    }

    // edge cases

    [Fact]
    public void ASpriteLargerThanAnyPageIsRefusedWithAReasonNotThrown()
    {
        var sprites = new List<AtlasSprite>
        {
            Sprite("huge", 200, 200, Solid(200, 200, 1, 1, 1, 255)),
            Sprite("fine", 10, 10, Solid(10, 10, 2, 2, 2, 255)),
        };
        var result = AtlasPacker.PackAtlas(sprites, new AtlasPackOptions(maxPageSize: 64, trim: false));
        var problem = Assert.Single(result.Problems);
        Assert.Equal("huge", problem.Name);
        Assert.NotEmpty(problem.Reason);
        Assert.Null(result.ToAtlasSet().Find("huge"));
        Assert.NotNull(result.ToAtlasSet().Find("fine"));
    }

    [Fact]
    public void AZeroSizeSpriteIsHandledRatherThanThrown()
    {
        var sprites = new List<AtlasSprite> { Sprite("zero", 0, 0, []), Plain("fine", 6, 6) };
        var result = AtlasPacker.PackAtlas(sprites, new AtlasPackOptions(maxPageSize: 32));
        Assert.Empty(result.Problems);
        var zero = Some(result.ToAtlasSet().Find("zero"));
        Assert.Equal(0, zero.Width);
        Assert.Equal(0, zero.Height);
    }

    [Fact]
    public void AFullyTransparentSpriteDrawsNothingButStillHasARegion()
    {
        var sprites = new List<AtlasSprite> { Sprite("ghost", 10, 8, new byte[10 * 8 * 4]), Plain("fine", 6, 6) };
        var result = AtlasPacker.PackAtlas(sprites, new AtlasPackOptions(maxPageSize: 32));
        Assert.Empty(result.Problems);
        var ghost = Some(result.ToAtlasSet().Find("ghost"));
        Assert.Equal(0, ghost.Width);
        Assert.Equal(0, ghost.Height);
        Assert.True(ghost.Trimmed);
        Assert.Equal(10, ghost.SourceWidth);
        Assert.Equal(8, ghost.SourceHeight);
    }

    [Fact]
    public void ThousandsOfTinySpritesPackWithoutErrorOrOverlap()
    {
        var sprites = Enumerable.Range(0, 2000).Select(i => Plain($"tiny_{i}", 2 + (i % 6), 2 + ((i * 3) % 6))).ToList();

        // Duplicate merging is its own group's concern; on, it would legally
        // give many of these identical-content sprites the very same
        // rectangle, which the overlap check below would misread as a bug.
        var result = AtlasPacker.PackAtlas(
            sprites,
            new AtlasPackOptions(maxPageSize: 512, padding: 1, trim: false, mergeDuplicates: false));
        Assert.Empty(result.Problems);
        var total = 0;
        foreach (var page in result.Pages)
        {
            total += page.Regions.Count;
            var boxes = page.Regions.Values.Select(PhysBox).ToList();
            for (var i = 0; i < boxes.Count; i++)
            {
                for (var j = i + 1; j < boxes.Count; j++)
                {
                    Assert.False(RectsOverlap(boxes[i], boxes[j]));
                }
            }
        }

        Assert.Equal(2000, total);
    }

    // heuristics

    [Fact]
    public void PinningAHeuristicIsHonoured()
    {
        var result = AtlasPacker.PackAtlas(
            [Plain("a", 10, 10)],
            new AtlasPackOptions(maxPageSize: 32, heuristic: MaxRectsHeuristic.ContactPoint));
        Assert.Equal(MaxRectsHeuristic.ContactPoint, result.Heuristic);
    }

    [Fact]
    public void LeftToChooseThePackerPicksOneOfTheFiveAndSaysWhich()
    {
        var sprites = Enumerable.Range(0, 30).Select(i => Plain($"s{i}", 4 + ((i * 5) % 20), 4 + ((i * 9) % 18))).ToList();
        var result = AtlasPacker.PackAtlas(sprites, new AtlasPackOptions(maxPageSize: 128, trim: false));
        Assert.Contains(result.Heuristic, Enum.GetValues<MaxRectsHeuristic>());
    }
}
