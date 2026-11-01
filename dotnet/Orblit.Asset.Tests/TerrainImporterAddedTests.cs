using System.Text;
using Orblit.Terrain;
using TerrainData = Orblit.Terrain.Terrain;

namespace Orblit.Asset.Tests;

public sealed class TerrainImporterAddedTests : IDisposable
{
    private static readonly TerrainImporter Importer = new();
    private static readonly Dictionary<string, object?> NoSettings = [];

    private readonly PackFixture _packs = new();

    public void Dispose() => _packs.Dispose();

    private static AssetId Id(string text) => TerrainImporterTests.Id(text);

    private static byte[] Text(string text) => Encoding.UTF8.GetBytes(text);

    /// <summary>A terrain file written by hand, so that it can say what <see cref="TerrainData"/> would never write.</summary>
    private static byte[] Written(string tail) =>
        Text($"{{\"kind\":\"orblit.terrain\",\"formatVersion\":1,\"regionSize\":4{tail}}}");

    private static byte[] Spaced(double spacing, params (int X, int Z)[] regions)
    {
        var terrain = new TerrainData(regionSize: 4, spacing: spacing);
        foreach (var (x, z) in regions)
        {
            terrain.AddRegion(new RegionKey(x, z));
        }

        return Text(terrain.Encode());
    }

    // A prefix is "terrain/" or nothing, because an empty directory has no slash.
    private static Dictionary<AssetId, byte[]> Regions(string prefix, params (int X, int Z)[] keys) =>
        keys.ToDictionary(
            key => Id($"{prefix}x{key.X}_z{key.Z}.oregion"),
            key => TerrainImporterTests.RegionFile(key.X, key.Z));

    private static Task<ImportResult> ImportTerrain(byte[] bytes, params (int X, int Z)[] present) =>
        Importer.ImportAsync(TerrainImporterTests.RequestFor("terrain/world.oterrain", bytes, Regions("terrain/", present)));

    private static Task<IReadOnlySet<AssetId>> DependenciesOf(string asset, byte[] bytes) =>
        Importer.DependenciesOfAsync(Id(asset), bytes, NoSettings);

    private static async Task<ImportFailureException> Fails(Task<ImportResult> import) =>
        await Assert.ThrowsAsync<ImportFailureException>(() => import);

    private static async Task<ImportFailureException> Fails(Task<IReadOnlySet<AssetId>> dependencies) =>
        await Assert.ThrowsAsync<ImportFailureException>(() => dependencies);

    /// <summary>A terrain whose set or layer names <paramref name="path"/> as the image or model called <paramref name="what"/>.</summary>
    private static byte[] NamingAPath(string what, string path)
    {
        var sets = what switch
        {
            "albedo" => new[] { new TerrainSet("grass", albedo: path) },
            "normal" => [new TerrainSet("grass", normal: path)],
            _ => Array.Empty<TerrainSet>(),
        };
        var scatter = what switch
        {
            "mesh" => new[] { new ScatterLayer("pines", mesh: path) },
            "material" => [new ScatterLayer("pines", material: path)],
            _ => Array.Empty<ScatterLayer>(),
        };
        return TerrainImporterTests.TerrainFile(sets: sets, scatter: scatter);
    }

    private async Task<MountedPacks> MountedAsync(Dictionary<string, byte[]> files)
    {
        var folder = await _packs.BundleAsync("hand", files);
        var pack = await _packs.PackFolderAsync(folder, "hand", null, "android");
        var packs = new MountedPacks(CookTargets.Find("android")!);
        packs.Start(pack.Pack);
        return packs;
    }

    private static MemoryAssetSource Cooked(string prefix, byte[] terrain, params (int X, int Z)[] present)
    {
        var files = new Dictionary<AssetId, byte[]> { [Id($"{prefix}world.oterrain.oterrain")] = terrain };
        foreach (var key in present)
        {
            files[Id($"{prefix}x{key.X}_z{key.Z}.oregion.oregion")] = TerrainImporterTests.RegionFile(key.X, key.Z);
        }

        return new MemoryAssetSource(files);
    }

    // the importer

    [Fact]
    public void TheImporterIsNamedTerrainAtVersionOne()
    {
        Assert.Equal("terrain", Importer.Name);
        Assert.Equal(1, Importer.Version);
    }

    [Fact]
    public void TheOutputsAreNamedForTheirFiles()
    {
        Assert.Equal("oterrain", TerrainImporter.TerrainOutput);
        Assert.Equal("oregion", TerrainImporter.RegionOutput);
    }

    [Fact]
    public void ItClaimsTheTerrainAndRegionExtensionsAndNoOthers()
    {
        Assert.Equal(["oregion", "oterrain"], Importer.Extensions.Order());
        Assert.True(Importer.CanHandle(Id("a/b.oterrain")));
        Assert.True(Importer.CanHandle(Id("a/x0_z0.oregion")));
        Assert.True(Importer.CanHandle(Id("a/WORLD.OTERRAIN")));
        Assert.False(Importer.CanHandle(Id("a/world.png")));
        Assert.False(Importer.CanHandle(Id("a/oterrain")));
    }

    [Fact]
    public void ItHasNoSettingsWhateverTheProjectSays()
    {
        Assert.Empty(Importer.ResolveSettings(new ImportSettings()));
        Assert.Empty(Importer.ResolveSettings(new ImportSettings(values: new Dictionary<string, object?> { ["padding"] = 3L })));
    }

    [Theory]
    [InlineData("terrain/world.oterrain", 0, 0, "terrain/x0_z0.oregion")]
    [InlineData("world.oterrain", 3, -4, "x3_z-4.oregion")]
    [InlineData("a/b/world.oterrain", -1, 2, "a/b/x-1_z2.oregion")]
    public void ARegionFileSitsBesideItsTerrain(string terrain, int x, int z, string expected) =>
        Assert.Equal(Id(expected), TerrainImporter.RegionFile(Id(terrain), new RegionKey(x, z)));

    // dependencies

    [Fact]
    public async Task AnImageTwoSetsShareIsOneDependency()
    {
        var bytes = TerrainImporterTests.TerrainFile(
            sets: [new TerrainSet("grass", albedo: "textures/shared.png"), new TerrainSet("moss", albedo: "textures/shared.png")]);
        Assert.Equal([Id("textures/shared.png")], await DependenciesOf("terrain/world.oterrain", bytes));
    }

    [Fact]
    public async Task ASetWithOnlyANormalMapDependsOnIt()
    {
        var found = await DependenciesOf("terrain/world.oterrain", NamingAPath("normal", "textures/n.png"));
        Assert.Equal([Id("textures/n.png")], found);
    }

    [Fact]
    public async Task ALayerWithOnlyAModelDependsOnIt()
    {
        var found = await DependenciesOf("terrain/world.oterrain", NamingAPath("mesh", "models/pine.glb"));
        Assert.Equal([Id("models/pine.glb")], found);
    }

    [Fact]
    public async Task ALayerWithOnlyAMaterialDependsOnIt()
    {
        var found = await DependenciesOf("terrain/world.oterrain", NamingAPath("material", "materials/pine.omat"));
        Assert.Equal([Id("materials/pine.omat")], found);
    }

    [Fact]
    public async Task ATerrainThatListsAndNamesNothingHasNoDependencies() =>
        Assert.Empty(await DependenciesOf("terrain/world.oterrain", TerrainImporterTests.TerrainFile()));

    [Fact]
    public async Task AnUpperCaseExtensionIsStillATerrain()
    {
        var found = await DependenciesOf("terrain/WORLD.OTERRAIN", TerrainImporterTests.TerrainFile(regions: [(0, 0)]));
        Assert.Equal([Id("terrain/x0_z0.oregion")], found);
    }

    [Fact]
    public async Task ARegionIsNotReadWhenItsDependenciesAreAsked() =>
        Assert.Empty(await DependenciesOf("terrain/x0_z0.oregion", [0xff, 0x00, 0x01]));

    [Theory]
    [InlineData("albedo")]
    [InlineData("normal")]
    [InlineData("mesh")]
    [InlineData("material")]
    public async Task APathThatIsNotWithinTheProjectFailsTheTerrainWhicheverFieldHoldsIt(string what)
    {
        var failure = await Fails(DependenciesOf("terrain/world.oterrain", NamingAPath(what, "/abs/thing")));
        Assert.Equal(Id("terrain/world.oterrain"), failure.Id);
        Assert.Equal("names \"/abs/thing\", which is not a path within the project", failure.Reason);
        Assert.Null(failure.Cause);
    }

    // a file that cannot be read

    [Fact]
    public async Task ATerrainFileThatIsNotATerrainFailsWithTheDecoderSaying()
    {
        var failure = await Fails(DependenciesOf("terrain/world.oterrain", Text("[]")));
        Assert.Equal(Id("terrain/world.oterrain"), failure.Id);
        Assert.Contains("not a terrain file", failure.Reason);
        Assert.Equal(failure.Reason, failure.Cause);
    }

    [Fact]
    public async Task ATerrainFileThatIsNotTextSaysSoAndKeepsTheDecoderMessage()
    {
        var failure = await Fails(DependenciesOf("terrain/world.oterrain", [0xff]));
        Assert.Equal(Id("terrain/world.oterrain"), failure.Id);
        var cause = Assert.IsType<string>(failure.Cause);
        Assert.Equal($"this is not text: {cause}", failure.Reason);
    }

    [Fact]
    public async Task ARegionFileThatIsNotARegionFailsWithTheRegionAsTheId()
    {
        var failure = await Fails(Importer.ImportAsync(TerrainImporterTests.RequestFor("terrain/x0_z0.oregion", Text("hello"))));
        Assert.Equal(Id("terrain/x0_z0.oregion"), failure.Id);
        Assert.Contains("not a region file", failure.Reason);
        Assert.Equal(failure.Reason, failure.Cause);
    }

    // a region

    [Fact]
    public async Task ARegionNamedForAnotherPlaceSaysBothPlacesWhateverTheirSigns()
    {
        var failure = await Fails(
            Importer.ImportAsync(TerrainImporterTests.RequestFor("terrain/x-1_z2.oregion", TerrainImporterTests.RegionFile(-3, -4))));
        Assert.Equal(Id("terrain/x-1_z2.oregion"), failure.Id);
        Assert.Equal("this file is named for region -1, 2 and holds region -3, -4", failure.Reason);
        Assert.Null(failure.Cause);
    }

    [Fact]
    public async Task ARegionNotNamedForItsPlaceNotesItsOwnNameAndThenWhatTheFileLeftOut()
    {
        var result = await Importer.ImportAsync(
            TerrainImporterTests.RequestFor("terrain/hills.oregion", TerrainImporterTests.RegionWithAnUnknownMap(3, 4)));
        Assert.Equal(2, result.Notes.Count);
        Assert.Equal(
            "hills.oregion is not named for its place, so no terrain will find it. This one is x3_z4.oregion.",
            result.Notes[0]);
        Assert.Contains("wetness", result.Notes[1]);
    }

    [Fact]
    public async Task ARegionNamedForItsPlaceHasOnlyWhatTheFileLeftOutAsNotes()
    {
        var result = await Importer.ImportAsync(
            TerrainImporterTests.RequestFor("terrain/x3_z4.oregion", TerrainImporterTests.RegionWithAnUnknownMap(3, 4)));
        Assert.Contains("wetness", Assert.Single(result.Notes));
    }

    // a terrain

    [Fact]
    public async Task ATerrainWithNoRegionsSaysSoInThePlural()
    {
        var result = await ImportTerrain(TerrainImporterTests.TerrainFile());
        Assert.Equal(["0 regions of 4 texels, 1.0 m apart"], result.Notes);
    }

    [Theory]
    [InlineData(0.5, "0.5 m apart")]
    [InlineData(10.0, "10.0 m apart")]
    [InlineData(2.25, "2.25 m apart")]
    public async Task TheNoteGivesTheSpacingInFileFormat(double spacing, string expected)
    {
        var result = await ImportTerrain(Spaced(spacing, (0, 0)), (0, 0));
        Assert.Equal($"1 region of 4 texels, {expected}", result.Notes[0]);
    }

    [Fact]
    public async Task ATerrainOfLargerRegionsSaysTheirSize()
    {
        var terrain = TerrainImporterTests.TerrainFile(regions: [(0, 0)], regionSize: 8);
        var result = await Importer.ImportAsync(
            TerrainImporterTests.RequestFor(
                "terrain/world.oterrain",
                terrain,
                new Dictionary<AssetId, byte[]> { [Id("terrain/x0_z0.oregion")] = TerrainImporterTests.RegionFile(0, 0, size: 8) }));
        Assert.Equal("1 region of 8 texels, 1.0 m apart", result.Notes[0]);
    }

    [Fact]
    public async Task ATerrainAtTheProjectRootReadsItsRegionsFromTheRoot()
    {
        var result = await Importer.ImportAsync(
            TerrainImporterTests.RequestFor(
                "world.oterrain",
                TerrainImporterTests.TerrainFile(regions: [(0, 0)]),
                Regions(string.Empty, (0, 0))));
        Assert.Equal(["oterrain"], result.Outputs.Keys);
    }

    [Fact]
    public async Task WhatTheTerrainFileLeftOutIsNotedAfterTheCount()
    {
        var result = await ImportTerrain(Written(",\"spacing\":-2,\"regions\":[[0,0]]"), (0, 0));
        Assert.Equal(
            ["1 region of 4 texels, 1.0 m apart", "The texels were -2.0 m apart; they are 1 m now."],
            result.Notes);
    }

    [Fact]
    public async Task ARegionListedTwiceIsCountedOnceAndNoted()
    {
        var result = await ImportTerrain(Written(",\"regions\":[[0,0],[0,0],[1]]"), (0, 0));
        Assert.Equal(
            [
                "1 region of 4 texels, 1.0 m apart",
                "Region 0, 0 was listed twice.",
                "A region that does not say where it is was left out.",
            ],
            result.Notes);
    }

    [Fact]
    public async Task AListedRegionThatHoldsAnotherPlaceSaysBothWhateverTheirOrder()
    {
        var failure = await Fails(
            Importer.ImportAsync(
                TerrainImporterTests.RequestFor(
                    "terrain/world.oterrain",
                    TerrainImporterTests.TerrainFile(regions: [(1, 2)]),
                    new Dictionary<AssetId, byte[]> { [Id("terrain/x1_z2.oregion")] = TerrainImporterTests.RegionFile(3, 4) })));
        Assert.Equal(Id("terrain/world.oterrain"), failure.Id);
        Assert.Equal("lists region 1, 2, and x1_z2.oregion holds region 3, 4", failure.Reason);
    }

    [Fact]
    public async Task AListedRegionOfAnotherSizeSaysBothSizes()
    {
        var failure = await Fails(
            Importer.ImportAsync(
                TerrainImporterTests.RequestFor(
                    "terrain/world.oterrain",
                    TerrainImporterTests.TerrainFile(regions: [(0, 0)]),
                    new Dictionary<AssetId, byte[]> { [Id("terrain/x0_z0.oregion")] = TerrainImporterTests.RegionFile(0, 0, size: 8) })));
        Assert.Equal(Id("terrain/world.oterrain"), failure.Id);
        Assert.Equal(
            "x0_z0.oregion is 8 texels across and this terrain's regions are 4, so it cannot be put in",
            failure.Reason);
    }

    [Fact]
    public async Task AListedRegionSmallerThanTheTerrainsIsAFailureToo()
    {
        var failure = await Fails(
            Importer.ImportAsync(
                TerrainImporterTests.RequestFor(
                    "terrain/world.oterrain",
                    TerrainImporterTests.TerrainFile(regions: [(0, 0)]),
                    new Dictionary<AssetId, byte[]> { [Id("terrain/x0_z0.oregion")] = TerrainImporterTests.RegionFile(0, 0, size: 2) })));
        Assert.Contains("x0_z0.oregion is 2 texels across and this terrain's regions are 4", failure.Reason);
    }

    [Fact]
    public async Task AKeyThatDoesNotMatchIsReportedBeforeASizeThatDoesNotMatch()
    {
        var failure = await Fails(
            Importer.ImportAsync(
                TerrainImporterTests.RequestFor(
                    "terrain/world.oterrain",
                    TerrainImporterTests.TerrainFile(regions: [(1, 0)]),
                    new Dictionary<AssetId, byte[]> { [Id("terrain/x1_z0.oregion")] = TerrainImporterTests.RegionFile(0, 0, size: 8) })));
        Assert.Contains("lists region 1, 0", failure.Reason);
    }

    [Fact]
    public async Task TheFirstListedRegionThatIsWrongIsTheOneReported()
    {
        var failure = await Fails(
            Importer.ImportAsync(
                TerrainImporterTests.RequestFor(
                    "terrain/world.oterrain",
                    TerrainImporterTests.TerrainFile(regions: [(0, 0), (1, 0)]),
                    new Dictionary<AssetId, byte[]>
                    {
                        [Id("terrain/x0_z0.oregion")] = TerrainImporterTests.RegionFile(0, 0, size: 8),
                        [Id("terrain/x1_z0.oregion")] = Text("nope"),
                    })));
        Assert.Contains("x0_z0.oregion is 8 texels", failure.Reason);
    }

    [Fact]
    public async Task ARegionThatCannotBeReadFailsWithTheRegionAsTheId()
    {
        var failure = await Fails(
            Importer.ImportAsync(
                TerrainImporterTests.RequestFor(
                    "terrain/world.oterrain",
                    TerrainImporterTests.TerrainFile(regions: [(0, 0)]),
                    new Dictionary<AssetId, byte[]> { [Id("terrain/x0_z0.oregion")] = Text("nope") })));
        Assert.Equal(Id("terrain/x0_z0.oregion"), failure.Id);
    }

    [Fact]
    public async Task AListedRegionThatIsNotInTheSourceIsNotFound()
    {
        var missing = await Assert.ThrowsAsync<AssetNotFoundException>(
            () => ImportTerrain(TerrainImporterTests.TerrainFile(regions: [(0, 0), (1, 0)]), (0, 0)));
        Assert.Equal(Id("terrain/x1_z0.oregion"), missing.Id);
    }

    [Fact]
    public async Task ATerrainThatCannotBeReadFailsBeforeAnyRegionIsAsked()
    {
        var failure = await Fails(ImportTerrain(Text("[]")));
        Assert.Equal(Id("terrain/world.oterrain"), failure.Id);
        Assert.Contains("not a terrain file", failure.Reason);
    }

    // a cooked terrain

    [Fact]
    public async Task ACookedTerrainKeepsTheRegionsInTheOrderTheFileListsThem()
    {
        var terrain = TerrainImporterTests.TerrainFile(regions: [(0, 0), (0, 1), (1, 0)]);
        var cooked = await CookedTerrain.OpenAsync(Cooked("terrain/", terrain, (0, 0), (1, 0), (0, 1)), Id("terrain/world.oterrain"));
        Assert.Equal([new RegionKey(0, 0), new RegionKey(1, 0), new RegionKey(0, 1)], cooked.Regions.Keys);
        Assert.Empty(cooked.Problems);
        Assert.Equal(4, cooked.Terrain.RegionSize);
        Assert.Empty(cooked.Terrain.Regions);
    }

    [Fact]
    public async Task ACookedTerrainLeavesOutOnlyTheRegionThePackLacks()
    {
        var terrain = TerrainImporterTests.TerrainFile(regions: [(0, 0), (1, 0), (2, 0)]);
        var cooked = await CookedTerrain.OpenAsync(Cooked("terrain/", terrain, (0, 0), (2, 0)), Id("terrain/world.oterrain"));
        Assert.Equal([new RegionKey(0, 0), new RegionKey(2, 0)], cooked.Regions.Keys);
        Assert.Equal(["x1_z0.oregion is listed and is not in the bundle."], cooked.Problems);
    }

    [Fact]
    public async Task WhatTheTerrainFileLeftOutComesBeforeTheRegionsThePackLacks()
    {
        var cooked = await CookedTerrain.OpenAsync(Cooked("terrain/", Written(",\"spacing\":-2,\"regions\":[[0,0]]")), Id("terrain/world.oterrain"));
        Assert.Equal(
            ["The texels were -2.0 m apart; they are 1 m now.", "x0_z0.oregion is listed and is not in the bundle."],
            cooked.Problems);
        Assert.Empty(cooked.Regions.Keys);
    }

    [Fact]
    public async Task ACookedRegionIsReadFromBesideItsTerrainAsItWasCooked()
    {
        var terrain = TerrainImporterTests.TerrainFile(regions: [(1, 0)]);
        var cooked = await CookedTerrain.OpenAsync(Cooked("terrain/", terrain, (1, 0)), Id("terrain/world.oterrain"));
        Assert.Equal(TerrainImporterTests.RegionFile(1, 0), await cooked.Regions.ReadAsync(new RegionKey(1, 0)));
    }

    [Fact]
    public async Task ACookedTerrainAtTheProjectRootFindsItsRegionsAtTheRoot()
    {
        var terrain = TerrainImporterTests.TerrainFile(regions: [(0, 0)]);
        var cooked = await CookedTerrain.OpenAsync(Cooked(string.Empty, terrain, (0, 0)), Id("world.oterrain"));
        Assert.Equal([new RegionKey(0, 0)], cooked.Regions.Keys);
    }

    [Fact]
    public async Task ACookedTerrainThatIsNotThereIsNotFound()
    {
        var missing = await Assert.ThrowsAsync<AssetNotFoundException>(
            () => CookedTerrain.OpenAsync(new MemoryAssetSource(new Dictionary<AssetId, byte[]>()), Id("terrain/world.oterrain")));
        Assert.Equal(Id("terrain/world.oterrain.oterrain"), missing.Id);
    }

    [Fact]
    public async Task ACookedTerrainThatCannotBeReadIsAFormatError()
    {
        await Assert.ThrowsAsync<TerrainFormatException>(
            () => CookedTerrain.OpenAsync(Cooked("terrain/", Text("[]")), Id("terrain/world.oterrain")));
        await Assert.ThrowsAsync<FormatException>(
            () => CookedTerrain.OpenAsync(Cooked("terrain/", [0xff]), Id("terrain/world.oterrain")));
    }

    [Fact]
    public async Task WhatACookedTerrainReportsCannotBeChangedByTheCaller()
    {
        var terrain = TerrainImporterTests.TerrainFile(regions: [(0, 0), (1, 0)]);
        var cooked = await CookedTerrain.OpenAsync(Cooked("terrain/", terrain, (0, 0)), Id("terrain/world.oterrain"));
        Assert.True(((IList<string>)cooked.Problems).IsReadOnly);
        Assert.True(((IList<RegionKey>)cooked.Regions.Keys).IsReadOnly);
    }

    [Fact]
    public async Task ATerrainOpensFromAPackWithItsSettingsAndItsRegions()
    {
        var packs = await MountedAsync(
            new Dictionary<string, byte[]>
            {
                ["terrain/world.oterrain.oterrain"] = TerrainImporterTests.TerrainFile(regions: [(0, 0), (1, 0)]),
                ["terrain/x0_z0.oregion.oregion"] = TerrainImporterTests.RegionFile(0, 0, height: 10),
                ["terrain/x1_z0.oregion.oregion"] = TerrainImporterTests.RegionFile(1, 0, height: 100),
            });
        var cooked = await CookedTerrain.OpenAsync(packs, Id("terrain/world.oterrain"));
        Assert.Equal(4, cooked.Terrain.RegionSize);
        Assert.Empty(cooked.Terrain.Regions);
        Assert.Equal([new RegionKey(0, 0), new RegionKey(1, 0)], cooked.Regions.Keys);
        Assert.Empty(cooked.Problems);
    }

    [Fact]
    public async Task ATerrainStreamedFromAPackHasTheHeightsThatWereAuthored()
    {
        var packs = await MountedAsync(
            new Dictionary<string, byte[]>
            {
                ["terrain/world.oterrain.oterrain"] = TerrainImporterTests.TerrainFile(regions: [(0, 0), (1, 0)]),
                ["terrain/x0_z0.oregion.oregion"] = TerrainImporterTests.RegionFile(0, 0, height: 10),
                ["terrain/x1_z0.oregion.oregion"] = TerrainImporterTests.RegionFile(1, 0, height: 100),
            });
        var cooked = await CookedTerrain.OpenAsync(packs, Id("terrain/world.oterrain"));
        var streamer = new TerrainStreamer(terrain: cooked.Terrain, source: cooked.Regions, loadRadius: 1, keepRadius: 1);

        await streamer.FollowAsync(0.5, 0.5);
        Assert.Equal(new RegionKey(0, 0), Assert.Single(streamer.Resident));
        Assert.Equal(10, cooked.Terrain.HeightAt(0, 0));

        await streamer.FollowAsync(6.5, 0.5);
        Assert.Equal(new RegionKey(1, 0), Assert.Single(streamer.Resident));
        Assert.Equal(100, cooked.Terrain.HeightAt(4, 0));
        Assert.Empty(streamer.Failures);
    }

    [Fact]
    public async Task ATerrainThatIsNotInThePackIsNotFound()
    {
        var packs = await MountedAsync(
            new Dictionary<string, byte[]> { ["terrain/x0_z0.oregion.oregion"] = TerrainImporterTests.RegionFile(0, 0) });
        await Assert.ThrowsAsync<AssetNotFoundException>(
            () => CookedTerrain.OpenAsync(packs, Id("terrain/world.oterrain")));
    }
}
