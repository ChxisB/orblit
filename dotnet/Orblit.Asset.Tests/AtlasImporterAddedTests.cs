using System.Text;
using Orblit.Sprite;

namespace Orblit.Asset.Tests;

public sealed class AtlasImporterAddedTests
{
    private static readonly string[] OptionKeys =
    [
        "maxPageSize", "padding", "border", "extrude", "trimAlphaThreshold",
        "trim", "mergeDuplicates", "allowRotation", "square", "powerOfTwo", "heuristic",
    ];

    private static AssetId Id(string text) => AtlasImporterTests.Id(text);

    private static byte[] Block(int width, int height, int red) => AtlasImporterTests.Block(width, height, red);

    /// <summary>
    /// A red square with <paramref name="inner"/> alpha in a rectangle in the middle and
    /// <paramref name="outer"/> around it, so a test can say what trimming should cut.
    /// </summary>
    private static byte[] Framed(int size, int margin, byte outer, byte inner)
    {
        var pixels = new byte[size * size * 4];
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var inside = x >= margin && x < size - margin && y >= margin && y < size - margin;
                pixels[(y * size + x) * 4] = 200;
                pixels[(y * size + x) * 4 + 3] = inside ? inner : outer;
            }
        }

        return PngCodec.Encode(size, size, pixels);
    }

    private static byte[][] Three() => [Block(60, 10, 0), Block(10, 50, 40), Block(10, 40, 80)];

    /// <summary>The document, with its settings and a sprite list of the pictures, and the pictures themselves.</summary>
    private static Dictionary<AssetId, byte[]> Files(Dictionary<string, object?> document, params byte[][] pictures)
    {
        document["sprites"] = pictures.Select((_, index) => $"ui/s{index}.png").ToList();
        var files = new Dictionary<AssetId, byte[]> { [Id("ui.atlas.json")] = AtlasImporterTests.Document(document) };
        for (var index = 0; index < pictures.Length; index++)
        {
            files[Id($"ui/s{index}.png")] = pictures[index];
        }

        return files;
    }

    private static Task<ImportResult> Import(
        Dictionary<AssetId, byte[]> files, Dictionary<string, object?>? settings = null, CookTarget? target = null) =>
        new AtlasImporter().ImportAsync(new ImportRequest
        {
            Id = Id("ui.atlas.json"),
            Bytes = files[Id("ui.atlas.json")],
            Settings = settings ?? new Dictionary<string, object?>(),
            Target = target ?? CookTarget.Any,
            Source = new MemoryAssetSource(files),
        });

    private static Task<IReadOnlySet<AssetId>> Dependencies(string document, string id = "ui.atlas.json") =>
        new AtlasImporter().DependenciesOfAsync(
            Id(id), AtlasImporterTests.Document(document), new Dictionary<string, object?>());

    private static IReadOnlyDictionary<string, object?> Resolve(string key, object? value) =>
        new AtlasImporter().ResolveSettings(
            new ImportSettings(values: new Dictionary<string, object?> { [key] = value }));

    private static Atlas Descriptor(ImportResult result, int page = 0) =>
        Atlas.Read(Encoding.UTF8.GetString(result.Outputs[$"page{page}.json"]))!;

    private static Region Placed(ImportResult result, string sprite, int page = 0) =>
        Descriptor(result, page).Regions[$"ui/{sprite}"];

    private static (long X, long Y) Position(ImportResult result, string sprite, int page = 0)
    {
        var region = Placed(result, sprite, page);
        return (region.X, region.Y);
    }

    private static byte Alpha(ImportResult result, int x, int y)
    {
        var page = PngCodec.Decode(result.Outputs["page0.png"]);
        return page.Pixels[(y * (int)page.Width + x) * 4 + 3];
    }

    [Fact]
    public void IsNamedAtlasAtVersionOneAndClaimsJson()
    {
        var importer = new AtlasImporter();
        Assert.Equal("atlas", importer.Name);
        Assert.Equal(1, importer.Version);
        Assert.Equal(["json"], importer.Extensions);
    }

    [Fact]
    public void ClaimsOnlyANameThatEndsInDotAtlasDotJson()
    {
        Assert.False(new AtlasImporter().CanHandle(Id("ui.atlas.json.bak")));
        Assert.False(new AtlasImporter().CanHandle(Id("ui.atlas.jsonx")));
    }

    [Fact]
    public void ResolvesEveryOptionToNullWhenNothingWasSet()
    {
        var resolved = new AtlasImporter().ResolveSettings(new ImportSettings());
        Assert.Equal(OptionKeys.Order(), resolved.Keys.Order());
        Assert.All(resolved.Values, value => Assert.Null(value));
    }

    [Theory]
    [InlineData("maxPageSize", 1L)]
    [InlineData("maxPageSize", 4096L)]
    [InlineData("padding", 0L)]
    [InlineData("padding", 7L)]
    [InlineData("border", 0L)]
    [InlineData("border", 7L)]
    [InlineData("extrude", 0L)]
    [InlineData("extrude", 7L)]
    [InlineData("trimAlphaThreshold", 0L)]
    [InlineData("trimAlphaThreshold", 7L)]
    public void KeepsAWholeNumberThatIsInRangeUnderItsOwnKey(string key, long value) =>
        Assert.Equal((object)value, Resolve(key, value)[key]);

    [Theory]
    [InlineData(0L, "0")]
    [InlineData(-1L, "-1")]
    [InlineData(2.5, "2.5")]
    [InlineData("big", "big")]
    [InlineData(true, "true")]
    public void RefusesAMaxPageSizeThatIsNotAPositiveWholeNumber(object value, string printed)
    {
        var error = Assert.Throws<ArgumentException>(() => Resolve("maxPageSize", value));
        Assert.StartsWith($"Invalid argument (maxPageSize): has to be a size in texels: {printed}", error.Message);
        Assert.Equal("maxPageSize", error.ParamName);
    }

    [Theory]
    [InlineData("padding")]
    [InlineData("border")]
    [InlineData("extrude")]
    [InlineData("trimAlphaThreshold")]
    public void RefusesACountThatIsNegativeOrNotWhole(string key)
    {
        foreach (var (value, printed) in new (object, string)[] { (-1L, "-1"), (1.5, "1.5"), ("2", "2"), (false, "false") })
        {
            var error = Assert.Throws<ArgumentException>(() => Resolve(key, value));
            Assert.StartsWith($"Invalid argument ({key}): cannot be negative: {printed}", error.Message);
            Assert.Equal(key, error.ParamName);
        }
    }

    // A flag is read when the pack runs, so that a wrong one is reported against the document that held it.
    [Theory]
    [InlineData("trim")]
    [InlineData("mergeDuplicates")]
    [InlineData("allowRotation")]
    [InlineData("square")]
    [InlineData("powerOfTwo")]
    [InlineData("heuristic")]
    public void PassesAFlagOrAHeuristicThroughWithoutChecking(string key)
    {
        foreach (var value in new object[] { false, "yes", 5L })
        {
            Assert.Equal(value, Resolve(key, value)[key]);
        }
    }

    [Fact]
    public async Task PacksWithTheDefaultsWhenNothingSaysOtherwise()
    {
        var result = await Import(Files(new(), Block(16, 16, 0), Block(16, 16, 40)));
        Assert.Equal(["page0.json", "page0.png"], result.Outputs.Keys.Order());
        Assert.Equal(
            ["packed 2 sprites onto 1 page with bestShortSideFit", "page0 is 64x64, 13% full"], result.Notes);
        Assert.Equal((0L, 0L), Position(result, "s0.png"));
        Assert.Equal((18L, 0L), Position(result, "s1.png"));
    }

    [Fact]
    public async Task WritesEachSpritesPixelsWhereTheDescriptorSaysItIs()
    {
        var result = await Import(Files(new(), Block(16, 16, 0), Block(16, 16, 40)));
        var page = PngCodec.Decode(result.Outputs["page0.png"]);
        Assert.Equal((64L, 64L), (page.Width, page.Height));
        Assert.Equal(0, page.Pixels[0]);
        Assert.Equal(40, page.Pixels[18 * 4]);
    }

    [Fact]
    public async Task RoundsAHalfPercentUpAndNotToEven()
    {
        var result = await Import(Files(new(), Block(16, 32, 0)));
        Assert.Equal("page0 is 64x64, 13% full", result.Notes[1]);
    }

    [Fact]
    public async Task UsesTheSettingsTheDocumentCarries()
    {
        var result = await Import(Files(new() { ["padding"] = 8L }, Block(16, 16, 0), Block(16, 16, 40)));
        Assert.Equal((24L, 0L), Position(result, "s1.png"));
    }

    [Fact]
    public async Task LetsASettingGivenAtTheCallWinOverTheDocument()
    {
        var result = await Import(
            Files(new() { ["padding"] = 8L }, Block(16, 16, 0), Block(16, 16, 40)),
            new() { ["padding"] = 0L });
        Assert.Equal((16L, 0L), Position(result, "s1.png"));
    }

    [Fact]
    public async Task KeepsADocumentSettingWhenTheCallLeavesItNull()
    {
        var result = await Import(
            Files(new() { ["padding"] = 8L }, Block(16, 16, 0), Block(16, 16, 40)),
            new() { ["padding"] = null });
        Assert.Equal((24L, 0L), Position(result, "s1.png"));
    }

    [Fact]
    public async Task TreatsANullInTheDocumentAsLeftOut()
    {
        var result = await Import(Files(new() { ["padding"] = null }, Block(16, 16, 0), Block(16, 16, 40)));
        Assert.Equal((18L, 0L), Position(result, "s1.png"));
    }

    [Fact]
    public async Task TakesAPageSizeFromTheDocument()
    {
        var failure = await Assert.ThrowsAsync<ImportFailureException>(
            () => Import(Files(new() { ["maxPageSize"] = 32L }, Block(40, 40, 0))));
        Assert.StartsWith("these sprites do not fit on a 32x32 page: ui/s0.png: ", failure.Reason);
        Assert.Equal(Id("ui.atlas.json"), failure.Id);
    }

    [Fact]
    public async Task JoinsEverySpriteThatDoesNotFitWithASemicolon()
    {
        var failure = await Assert.ThrowsAsync<ImportFailureException>(
            () => Import(Files(new() { ["maxPageSize"] = 32L }, Block(40, 40, 0), Block(50, 50, 40))));
        Assert.Matches(@"^these sprites do not fit on a 32x32 page: ui/s0\.png: [^;]+; ui/s1\.png: [^;]+$", failure.Reason);
    }

    [Fact]
    public async Task LetsAPageSizeGivenAtTheCallBeatTheDocumentsOwn()
    {
        var failure = await Assert.ThrowsAsync<ImportFailureException>(
            () => Import(Files(new() { ["maxPageSize"] = 32L }, Block(20, 20, 0)), new() { ["maxPageSize"] = 16L }));
        Assert.Contains("do not fit on a 16x16 page", failure.Reason);
    }

    [Fact]
    public async Task FallsBackToTheTargetsLargestTextureWhenNoSettingGivesAPageSize()
    {
        var target = new CookTarget("small", new Dictionary<string, object?> { ["maxTextureSize"] = 16L });
        var failure = await Assert.ThrowsAsync<ImportFailureException>(
            () => Import(Files(new(), Block(20, 20, 0)), target: target));
        Assert.Contains("do not fit on a 16x16 page", failure.Reason);
    }

    [Fact]
    public async Task PrefersAPageSizeSettingToTheTargetsLargestTexture()
    {
        var target = new CookTarget("small", new Dictionary<string, object?> { ["maxTextureSize"] = 16L });
        var result = await Import(Files(new(), Block(20, 20, 0)), new() { ["maxPageSize"] = 32L }, target);
        Assert.Equal((32L, 32L), (Descriptor(result).Width, Descriptor(result).Height));
    }

    [Fact]
    public async Task FallsBackToTwoThousandAndFortyEightWithNeitherASettingNorALimit()
    {
        var failure = await Assert.ThrowsAsync<ImportFailureException>(
            () => Import(Files(new(), Block(2100, 4, 0))));
        Assert.Contains("do not fit on a 2048x2048 page", failure.Reason);
    }

    [Theory]
    [InlineData("maxPageSize", "big", "String", "int")]
    [InlineData("maxPageSize", 2.5, "double", "int")]
    [InlineData("padding", "2", "String", "int")]
    [InlineData("padding", 2.5, "double", "int")]
    [InlineData("padding", true, "bool", "int")]
    [InlineData("border", "x", "String", "int")]
    [InlineData("extrude", "x", "String", "int")]
    [InlineData("trimAlphaThreshold", "x", "String", "int")]
    [InlineData("trim", 1, "int", "bool")]
    [InlineData("mergeDuplicates", "yes", "String", "bool")]
    [InlineData("allowRotation", 2.5, "double", "bool")]
    [InlineData("square", "no", "String", "bool")]
    [InlineData("powerOfTwo", 0, "int", "bool")]
    public async Task RefusesASettingOfTheWrongTypeWithTheMessageDartGives(
        string key, object value, string held, string wanted)
    {
        var error = await Assert.ThrowsAsync<InvalidCastException>(
            () => Import(Files(new() { [key] = value }, Block(16, 16, 0))));
        Assert.Equal($"type '{held}' is not a subtype of type '{wanted}?' in type cast", error.Message);
    }

    [Theory]
    [InlineData("[1]", "_GrowableList<dynamic>")]
    [InlineData("{}", "_Map<String, dynamic>")]
    public async Task NamesAListOrAMapByTheTypeDartWouldSayItIs(string json, string held)
    {
        var files = Files(new(), Block(16, 16, 0));
        files[Id("ui.atlas.json")] = AtlasImporterTests.Document($$"""{"sprites": ["ui/s0.png"], "padding": {{json}}}""");
        var error = await Assert.ThrowsAsync<InvalidCastException>(() => Import(files));
        Assert.Equal($"type '{held}' is not a subtype of type 'int?' in type cast", error.Message);
    }

    [Theory]
    [InlineData("short", "bestShortSideFit")]
    [InlineData("long", "bestLongSideFit")]
    [InlineData("area", "bestAreaFit")]
    [InlineData("corner", "bottomLeft")]
    [InlineData("contact", "contactPoint")]
    public async Task NamesTheHeuristicItWasAskedForInTheNote(string name, string label)
    {
        var result = await Import(Files(new() { ["heuristic"] = name }, Three()));
        Assert.Equal($"packed 3 sprites onto 1 page with {label}", result.Notes[0]);
    }

    [Fact]
    public async Task LaysOutByTheHeuristicNamedAndNotJustLabelsIt()
    {
        var short1 = await Import(Files(new() { ["heuristic"] = "short" }, Three()));
        var corner = await Import(Files(new() { ["heuristic"] = "corner" }, Three()));
        var contact = await Import(Files(new() { ["heuristic"] = "contact" }, Three()));
        Assert.Equal((0L, 12L), Position(short1, "s2.png"));
        Assert.Equal((74L, 0L), Position(corner, "s2.png"));
        Assert.Equal((0L, 12L), Position(contact, "s1.png"));
        Assert.Equal((64L, 64L), (Descriptor(contact).Width, Descriptor(contact).Height));
    }

    [Fact]
    public async Task ChoosesTheBestHeuristicWhenTheDocumentLeavesItNull()
    {
        var result = await Import(Files(new() { ["heuristic"] = null }, Block(16, 16, 0), Block(16, 16, 40)));
        Assert.Equal("packed 2 sprites onto 1 page with bestShortSideFit", result.Notes[0]);
    }

    [Theory]
    [InlineData("sideways", "sideways")]
    [InlineData("Short", "Short")]
    [InlineData(5, "5")]
    [InlineData(true, "true")]
    public async Task ListsTheHeuristicsInOrderForOneItDoesNotKnow(object value, string printed)
    {
        var failure = await Assert.ThrowsAsync<ImportFailureException>(
            () => Import(Files(new() { ["heuristic"] = value }, Block(16, 16, 0))));
        Assert.Equal(
            $"\"{printed}\" is not a packing heuristic. Use one of short, long, area, corner, contact, "
            + "or leave it out to have the best one chosen",
            failure.Reason);
    }

    [Fact]
    public async Task KeepsABorderOffTheEdgeOfThePage()
    {
        var result = await Import(Files(new() { ["border"] = 5L }, Block(16, 16, 0), Block(16, 16, 40)));
        Assert.Equal((5L, 5L), Position(result, "s0.png"));
        Assert.Equal((23L, 5L), Position(result, "s1.png"));
    }

    [Fact]
    public async Task PacksFlushWithNoPadding()
    {
        var result = await Import(Files(new() { ["padding"] = 0L }, Block(16, 16, 0), Block(16, 16, 40)));
        Assert.Equal((16L, 0L), Position(result, "s1.png"));
    }

    [Fact]
    public async Task FillsTheGapWithEachSpritesEdgeByTheExtrudeGiven()
    {
        var files = Files(new() { ["padding"] = 8L }, Block(16, 16, 0), Block(16, 16, 40));
        var wide = await Import(files, new() { ["extrude"] = 3L });
        Assert.Equal(255, Alpha(wide, 18, 0));
        Assert.Equal(0, Alpha(wide, 19, 0));
        Assert.Equal(0, Alpha(wide, 20, 0));
        Assert.Equal(255, Alpha(wide, 21, 0));
    }

    [Fact]
    public async Task ExtrudesOnePixelByDefaultAndNoneWhenAskedForNone()
    {
        var files = Files(new(), Block(16, 16, 0), Block(16, 16, 40));
        Assert.Equal(255, Alpha(await Import(files), 16, 0));
        Assert.Equal(0, Alpha(await Import(files, new() { ["extrude"] = 0L }), 16, 0));
    }

    [Fact]
    public async Task ExtrudesNoMoreThanOnePixelByDefaultWhenThereIsRoomForMore()
    {
        var files = Files(new() { ["padding"] = 8L }, Block(16, 16, 0), Block(16, 16, 40));
        var result = await Import(files);
        Assert.Equal(255, Alpha(result, 16, 0));
        Assert.Equal(0, Alpha(result, 17, 0));
    }

    [Fact]
    public async Task ShareOnePlaceBetweenIdenticalSpritesUnlessToldNotTo()
    {
        var files = Files(new(), Block(16, 16, 40), Block(16, 16, 40));
        Assert.Equal((0L, 0L), Position(await Import(files), "s1.png"));
        Assert.Equal((18L, 0L), Position(await Import(files, new() { ["mergeDuplicates"] = false }), "s1.png"));
    }

    [Fact]
    public async Task CutsTheEmptySpaceOffASpriteByDefault()
    {
        var result = await Import(Files(new(), Framed(16, 4, 0, 255)));
        var region = Placed(result, "s0.png");
        Assert.Equal((8L, 8L, true), (region.Width, region.Height, region.Trimmed));
        Assert.Equal((4L, 4L, 16L, 16L), (region.OffsetX, region.OffsetY, region.SourceWidth, region.SourceHeight));
    }

    [Fact]
    public async Task KeepsTheEmptySpaceWhenTrimIsOff()
    {
        var result = await Import(Files(new() { ["trim"] = false }, Framed(16, 4, 0, 255)));
        var region = Placed(result, "s0.png");
        Assert.Equal((16L, 16L, false), (region.Width, region.Height, region.Trimmed));
    }

    [Fact]
    public async Task CountsFaintPixelsAsContentUnlessTheThresholdSaysTheyAreNot()
    {
        var files = Files(new(), Framed(16, 4, 1, 255));
        Assert.Equal(16L, Placed(await Import(files), "s0.png").Width);

        var cut = Placed(await Import(files, new() { ["trimAlphaThreshold"] = 10L }), "s0.png");
        Assert.Equal((8L, true), (cut.Width, cut.Trimmed));
    }

    [Fact]
    public async Task RoundsThePageUpToAPowerOfTwoByDefault()
    {
        var result = await Import(Files(new(), Block(100, 10, 0), Block(20, 20, 40)));
        Assert.Equal((128L, 64L), (Descriptor(result).Width, Descriptor(result).Height));
        var page = PngCodec.Decode(result.Outputs["page0.png"]);
        Assert.Equal((128L, 64L), (page.Width, page.Height));
        Assert.Equal("page0 is 128x64, 17% full", result.Notes[1]);
    }

    [Fact]
    public async Task KeepsThePageSnugWhenPowerOfTwoIsOff()
    {
        var result = await Import(Files(new() { ["powerOfTwo"] = false }, Block(100, 10, 0), Block(20, 20, 40)));
        Assert.Equal((124L, 64L), (Descriptor(result).Width, Descriptor(result).Height));
    }

    [Fact]
    public async Task MakesThePageSquareWhenAskedTo()
    {
        var files = Files(new() { ["square"] = true }, Block(100, 10, 0), Block(20, 20, 40));
        var rounded = await Import(files);
        var snug = await Import(files, new() { ["powerOfTwo"] = false });
        Assert.Equal((128L, 128L), (Descriptor(rounded).Width, Descriptor(rounded).Height));
        Assert.Equal((124L, 124L), (Descriptor(snug).Width, Descriptor(snug).Height));
    }

    [Fact]
    public async Task TurnsSpritesOnTheirSideOnlyWhenRotationIsAllowed()
    {
        var settings = new Dictionary<string, object?> { ["maxPageSize"] = 64L, ["padding"] = 0L, ["extrude"] = 0L };
        var upright = await Import(Files(settings, Three()));
        Assert.All(Descriptor(upright).Regions.Values, region => Assert.False(region.Rotated));

        var turned = await Import(Files(settings, Three()), new() { ["allowRotation"] = true });
        Assert.True(Placed(turned, "s1.png").Rotated);
        Assert.True(Placed(turned, "s2.png").Rotated);
        Assert.False(Placed(turned, "s0.png").Rotated);
        Assert.Equal("packed 3 sprites onto 1 page with bottomLeft", turned.Notes[0]);
    }

    [Fact]
    public async Task SpillsOntoASecondPageWhenTheFirstIsFull()
    {
        var result = await Import(Files(new() { ["maxPageSize"] = 32L }, Block(20, 20, 0), Block(20, 20, 40)));
        Assert.Equal(["page0.json", "page0.png", "page1.json", "page1.png"], result.Outputs.Keys.Order());
        Assert.Equal(
            [
                "packed 2 sprites onto 2 pages with bestShortSideFit",
                "page0 is 32x32, 39% full",
                "page1 is 32x32, 39% full",
            ],
            result.Notes);
        Assert.Equal(["page0.png", "page1.png"], [Descriptor(result, 0).Image, Descriptor(result, 1).Image]);
        Assert.Equal(["ui/s0.png"], Descriptor(result, 0).Regions.Keys);
        Assert.Equal(["ui/s1.png"], Descriptor(result, 1).Regions.Keys);
        Assert.Equal(32L, PngCodec.Decode(result.Outputs["page1.png"]).Width);
    }

    [Fact]
    public async Task KeepsTwoSpritesThatShareAFileNameApartByFolder()
    {
        var files = new Dictionary<AssetId, byte[]>
        {
            [Id("ui.atlas.json")] = AtlasImporterTests.Document(new { sprites = new[] { "a/play.png", "b/play.png" } }),
            [Id("a/play.png")] = Block(16, 16, 0),
            [Id("b/play.png")] = Block(16, 16, 40),
        };
        var result = await Import(files);
        Assert.Equal(["a/play.png", "b/play.png"], Descriptor(result).Regions.Keys.Order());
    }

    [Fact]
    public async Task SaysAnEmptyListNamesNoSprites()
    {
        var files = new Dictionary<AssetId, byte[]>
        {
            [Id("ui.atlas.json")] = AtlasImporterTests.Document(new { sprites = Array.Empty<string>() }),
        };
        var failure = await Assert.ThrowsAsync<ImportFailureException>(() => Import(files));
        Assert.Equal("an atlas has to name at least one sprite, and this one names none", failure.Reason);
    }

    [Fact]
    public async Task SaysADocumentThatIsNotJsonIsNotJsonWhenImportingToo()
    {
        var files = new Dictionary<AssetId, byte[]> { [Id("ui.atlas.json")] = AtlasImporterTests.Document("{ nope") };
        var failure = await Assert.ThrowsAsync<ImportFailureException>(() => Import(files));
        Assert.StartsWith("this is not JSON: ", failure.Reason);
        Assert.IsType<string>(failure.Cause);
    }

    [Fact]
    public async Task NamesTheSpriteAndWhyItCouldNotBeReadWhenItIsNotAPng()
    {
        var files = Files(new(), Block(16, 16, 0));
        files[Id("ui/s0.png")] = Encoding.UTF8.GetBytes("this is not a png");
        var failure = await Assert.ThrowsAsync<ImportFailureException>(() => Import(files));
        Assert.Equal("the sprite ui/s0.png could not be read: not a PNG: missing signature", failure.Reason);
        Assert.Equal("not a PNG: missing signature", failure.Cause);
        Assert.Equal(Id("ui.atlas.json"), failure.Id);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("\"x\"")]
    [InlineData("5")]
    public async Task RefusesADocumentThatIsNotAnObject(string json)
    {
        var failure = await Assert.ThrowsAsync<ImportFailureException>(() => Dependencies(json));
        Assert.Equal("an atlas document has to be a JSON object", failure.Reason);
        Assert.Equal(Id("ui.atlas.json"), failure.Id);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"sprites": null}""")]
    public async Task AsksForASpritesListWhenThereIsNone(string json)
    {
        var failure = await Assert.ThrowsAsync<ImportFailureException>(() => Dependencies(json));
        Assert.Equal("an atlas document needs a \"sprites\" list", failure.Reason);
    }

    [Theory]
    [InlineData("\"a.png\"", "String")]
    [InlineData("5", "int")]
    [InlineData("1.5", "double")]
    [InlineData("true", "bool")]
    [InlineData("{}", "_Map<String, dynamic>")]
    public async Task NamesTheTypeWhenSpritesIsNotAList(string json, string type)
    {
        var failure = await Assert.ThrowsAsync<ImportFailureException>(
            () => Dependencies($$"""{"sprites": {{json}}}"""));
        Assert.Equal($"\"sprites\" has to be a list of asset names, and this is a {type}", failure.Reason);
    }

    [Theory]
    [InlineData("5", "int")]
    [InlineData("1.5", "double")]
    [InlineData("true", "bool")]
    [InlineData("null", "Null")]
    [InlineData("{}", "_Map<String, dynamic>")]
    [InlineData("[]", "_GrowableList<dynamic>")]
    public async Task NamesTheTypeWhenAnEntryIsNotAString(string json, string type)
    {
        var failure = await Assert.ThrowsAsync<ImportFailureException>(
            () => Dependencies($$"""{"sprites": [{{json}}]}"""));
        Assert.Equal($"every entry in \"sprites\" has to be an asset name, and one is a {type}", failure.Reason);
    }

    [Theory]
    [InlineData("")]
    [InlineData("../x.png")]
    [InlineData("a:b.png")]
    public async Task SaysWhichEntryIsNotAnAssetName(string name)
    {
        var failure = await Assert.ThrowsAsync<ImportFailureException>(
            () => Dependencies($$"""{"sprites": ["{{name}}"]}"""));
        Assert.Equal($"\"{name}\" is not an asset name", failure.Reason);
    }

    [Fact]
    public async Task ReadsDotDotAndDotSlashAgainstTheDocumentsFolder()
    {
        var found = await Dependencies(
            """{"sprites": ["../shared/a.png", "./sub/b.png", "c/d.png"]}""", "menus/ui.atlas.json");
        Assert.Equal([Id("shared/a.png"), Id("menus/sub/b.png"), Id("c/d.png")], found.ToHashSet());
    }

    [Fact]
    public async Task CountsASpriteListedTwiceOnceAsADependency()
    {
        var found = await Dependencies("""{"sprites": ["a.png", "a.png"]}""");
        Assert.Equal([Id("a.png")], found.ToHashSet());
    }

    [Fact]
    public async Task FindsNoDependenciesInAnEmptyList()
    {
        Assert.Empty(await Dependencies("""{"sprites": []}"""));
    }

    [Fact]
    public async Task FaultsTheTaskRatherThanThrowingAtTheCallForADocumentItCannotRead()
    {
        var task = Dependencies("{ not json");
        await Assert.ThrowsAsync<ImportFailureException>(() => task);
    }
}
