using System.Text;
using Orblit.Terrain;
using TerrainData = Orblit.Terrain.Terrain;

namespace Orblit.Asset.Tests;

public sealed class TerrainImporterTests : IDisposable
{
    private static readonly AssetId World = Id("terrain/world.oterrain");

    private readonly TerrainImporter _importer = new();
    private readonly PackFixture _packs = new();

    private MemoryAssetSource _source = new(new Dictionary<AssetId, byte[]>());
    private MemoryCookCache _cache = new();

    public TerrainImporterTests() => Project();

    public void Dispose() => _packs.Dispose();

    internal static AssetId Id(string text) => AssetId.Parse(text);

    /// <summary>
    /// A region of <paramref name="size"/> texels whose heights are <c>height + texel index</c>, so two regions
    /// differ and a texel read back says which file it came from.
    /// </summary>
    internal static byte[] RegionFile(int x, int z, int size = 4, float height = 1)
    {
        var region = new TerrainRegion(new RegionKey(x, z), size);
        for (var i = 0; i < region.Heights.Length; i++)
        {
            region.Heights[i] = height + i;
        }

        return region.Encode();
    }

    /// <summary>A region at (<paramref name="x"/>, <paramref name="z"/>) that carries a map this Orblit does not know.</summary>
    internal static byte[] RegionWithAnUnknownMap(int x, int z)
    {
        var header = Encoding.UTF8.GetBytes(
            $"{{\"kind\":\"orblit.region\",\"formatVersion\":1,\"x\":{x},\"z\":{z},\"size\":4,"
            + "\"maps\":[{\"name\":\"wetness\",\"bytes\":4}]}\n");
        // The maps start on a four byte boundary after the header line.
        var start = (header.Length + 3) & ~3;
        var bytes = new byte[start + 4];
        header.CopyTo(bytes, 0);
        return bytes;
    }

    /// <summary>A terrain's settings file listing <paramref name="regions"/>, 4 texels to a region.</summary>
    internal static byte[] TerrainFile(
        IEnumerable<(int X, int Z)>? regions = null,
        IEnumerable<TerrainSet>? sets = null,
        IEnumerable<ScatterLayer>? scatter = null,
        int regionSize = 4)
    {
        var terrain = new TerrainData(regionSize: regionSize, sets: sets, scatter: scatter);
        foreach (var (x, z) in regions ?? [])
        {
            terrain.AddRegion(new RegionKey(x, z));
        }

        return Encoding.UTF8.GetBytes(terrain.Encode());
    }

    internal static ImportRequest RequestFor(string asset, byte[] bytes, Dictionary<AssetId, byte[]>? others = null) =>
        new()
        {
            Id = Id(asset),
            Bytes = bytes,
            Settings = new Dictionary<string, object?>(),
            Target = CookTarget.Any,
            Source = new MemoryAssetSource(others ?? []),
        };

    private static async Task FailsWith(Task<ImportResult> import, string text)
    {
        var failure = await Assert.ThrowsAsync<ImportFailureException>(() => import);
        Assert.Contains(text, failure.Reason);
    }

    private static async Task FailsWith(Task<IReadOnlySet<AssetId>> dependencies, string text)
    {
        var failure = await Assert.ThrowsAsync<ImportFailureException>(() => dependencies);
        Assert.Contains(text, failure.Reason);
    }

    private Cook NewCook() =>
        new(_source, _cache, new ImporterRegistry([_importer]), CookTarget.Any);

    private void Project(byte[]? region = null, bool texture = true)
    {
        var files = new Dictionary<AssetId, byte[]>
        {
            [World] = TerrainFile(
                regions: [(0, 0)], sets: [new TerrainSet("grass", albedo: "textures/grass.png")]),
            [Id("terrain/x0_z0.oregion")] = region ?? RegionFile(0, 0),
        };
        if (texture)
        {
            files[Id("textures/grass.png")] = [1, 2, 3];
        }

        _source = new MemoryAssetSource(files);
    }

    // a region

    [Fact]
    public async Task ARegionIsCarriedAsWritten()
    {
        var bytes = RegionFile(2, -1);
        var result = await _importer.ImportAsync(RequestFor("terrain/x2_z-1.oregion", bytes));
        Assert.Equal(["oregion"], result.Outputs.Keys);
        Assert.Equal(bytes, result.Outputs["oregion"]);
        Assert.Empty(result.Notes);
    }

    [Fact]
    public async Task ARegionHasNoDependencies()
    {
        var found = await _importer.DependenciesOfAsync(
            Id("terrain/x0_z0.oregion"), RegionFile(0, 0), new Dictionary<string, object?>());
        Assert.Empty(found);
    }

    [Fact]
    public Task ARegionNamedForAnotherPlaceIsAFailure() =>
        FailsWith(
            _importer.ImportAsync(RequestFor("terrain/x1_z0.oregion", RegionFile(0, 0))),
            "named for region 1, 0 and holds region 0, 0");

    [Fact]
    public async Task ARegionNotNamedForItsPlaceIsCookedWithANote()
    {
        var result = await _importer.ImportAsync(RequestFor("terrain/hills.oregion", RegionFile(3, 4)));
        Assert.Equal(["oregion"], result.Outputs.Keys);
        Assert.Contains("x3_z4.oregion", Assert.Single(result.Notes));
    }

    [Fact]
    public Task AFileThatIsNotARegionIsAFailureThatSaysSo() =>
        FailsWith(
            _importer.ImportAsync(RequestFor("terrain/x0_z0.oregion", Encoding.UTF8.GetBytes("hello"))),
            "not a region file");

    [Fact]
    public Task ARegionFromANewerOrblitIsAFailure()
    {
        var text = Encoding.UTF8.GetBytes(
            "{\"kind\":\"orblit.region\",\"formatVersion\":99,\"x\":0,\"z\":0,\"size\":4}\n");
        return FailsWith(_importer.ImportAsync(RequestFor("terrain/x0_z0.oregion", text)), "newer Orblit");
    }

    [Fact]
    public async Task ARegionWithAMapItDoesNotKnowIsCookedWithANote()
    {
        var result = await _importer.ImportAsync(RequestFor("terrain/x0_z0.oregion", RegionWithAnUnknownMap(0, 0)));
        Assert.Contains("wetness", Assert.Single(result.Notes));
    }

    // a terrain

    [Fact]
    public async Task ATerrainDependsOnTheRegionsItLists()
    {
        var found = await _importer.DependenciesOfAsync(
            World, TerrainFile(regions: [(0, 0), (-1, 2)]), new Dictionary<string, object?>());
        Assert.Equal([Id("terrain/x0_z0.oregion"), Id("terrain/x-1_z2.oregion")], found.ToHashSet());
    }

    [Fact]
    public async Task ATerrainDependsOnTheImagesModelsAndMaterialsItNames()
    {
        var found = await _importer.DependenciesOfAsync(
            World,
            TerrainFile(
                sets:
                [
                    new TerrainSet("grass", albedo: "textures/grass.png", normal: "textures/grass_n.png"),
                    new TerrainSet("bare"),
                ],
                scatter: [new ScatterLayer("pines", mesh: "models/pine.glb", material: "materials/pine.omat")]),
            new Dictionary<string, object?>());
        Assert.Equal(
            [
                Id("textures/grass.png"), Id("textures/grass_n.png"), Id("models/pine.glb"),
                Id("materials/pine.omat"),
            ],
            found.ToHashSet());
    }

    [Fact]
    public async Task ATerrainAtTheProjectRootKeepsItsRegionsAtTheRoot()
    {
        var found = await _importer.DependenciesOfAsync(
            Id("world.oterrain"), TerrainFile(regions: [(0, 0)]), new Dictionary<string, object?>());
        Assert.Equal([Id("x0_z0.oregion")], found.ToHashSet());
    }

    [Fact]
    public Task ATerrainNamingAPathThatIsNotOneIsAFailure() =>
        FailsWith(
            _importer.DependenciesOfAsync(
                World,
                TerrainFile(sets: [new TerrainSet("grass", albedo: "/abs/grass.png")]),
                new Dictionary<string, object?>()),
            "\"/abs/grass.png\", which is not a path within the project");

    [Fact]
    public async Task AFileThatIsNotATerrainIsAFailureThatSaysSo()
    {
        await FailsWith(
            _importer.DependenciesOfAsync(World, Encoding.UTF8.GetBytes("[]"), new Dictionary<string, object?>()),
            "not a terrain file");
        await FailsWith(
            _importer.DependenciesOfAsync(World, [0xff], new Dictionary<string, object?>()),
            "not text");
    }

    [Fact]
    public async Task ATerrainIsCarriedAsWrittenWithACountInItsNotes()
    {
        var bytes = TerrainFile(regions: [(0, 0), (1, 0)]);
        var result = await _importer.ImportAsync(
            RequestFor(
                "terrain/world.oterrain",
                bytes,
                new Dictionary<AssetId, byte[]>
                {
                    [Id("terrain/x0_z0.oregion")] = RegionFile(0, 0),
                    [Id("terrain/x1_z0.oregion")] = RegionFile(1, 0),
                }));
        Assert.Equal(["oterrain"], result.Outputs.Keys);
        Assert.Equal(bytes, result.Outputs["oterrain"]);
        Assert.Equal("2 regions of 4 texels, 1.0 m apart", result.Notes[0]);
    }

    [Fact]
    public async Task ATerrainWithOneRegionSaysRegionAndNotRegions()
    {
        var result = await _importer.ImportAsync(
            RequestFor(
                "terrain/world.oterrain",
                TerrainFile(regions: [(0, 0)]),
                new Dictionary<AssetId, byte[]> { [Id("terrain/x0_z0.oregion")] = RegionFile(0, 0) }));
        Assert.StartsWith("1 region of", result.Notes[0], StringComparison.Ordinal);
    }

    [Fact]
    public Task AListedRegionOfAnotherSizeIsAFailure() =>
        FailsWith(
            _importer.ImportAsync(
                RequestFor(
                    "terrain/world.oterrain",
                    TerrainFile(regions: [(0, 0)]),
                    new Dictionary<AssetId, byte[]> { [Id("terrain/x0_z0.oregion")] = RegionFile(0, 0, size: 8) })),
            "x0_z0.oregion is 8 texels across and this terrain's");

    [Fact]
    public Task AListedRegionWhoseFileHoldsAnotherIsAFailure() =>
        FailsWith(
            _importer.ImportAsync(
                RequestFor(
                    "terrain/world.oterrain",
                    TerrainFile(regions: [(1, 0)]),
                    new Dictionary<AssetId, byte[]> { [Id("terrain/x1_z0.oregion")] = RegionFile(0, 0) })),
            "lists region 1, 0, and x1_z0.oregion holds region 0, 0");

    [Fact]
    public Task AListedRegionThatCannotBeReadNamesTheFile() =>
        FailsWith(
            _importer.ImportAsync(
                RequestFor(
                    "terrain/world.oterrain",
                    TerrainFile(regions: [(0, 0)]),
                    new Dictionary<AssetId, byte[]> { [Id("terrain/x0_z0.oregion")] = Encoding.UTF8.GetBytes("nope") })),
            "not a region file");

    // cooked

    [Fact]
    public async Task ATerrainIsCookedThenServedFromTheCache()
    {
        var first = await NewCook().CookOneAsync(World);
        Assert.Equal(CookStatus.Cooked, first.Status);
        var second = await NewCook().CookOneAsync(World);
        Assert.Equal(CookStatus.Cached, second.Status);
    }

    [Fact]
    public async Task EditingARegionCooksTheTerrainAgain()
    {
        await NewCook().CookOneAsync(World);
        Project(region: RegionFile(0, 0, height: 50));
        var again = await NewCook().CookOneAsync(World);
        Assert.Equal(CookStatus.Cooked, again.Status);
    }

    [Fact]
    public async Task AMissingRegionFailsTheTerrain()
    {
        _source = new MemoryAssetSource(
            new Dictionary<AssetId, byte[]> { [World] = TerrainFile(regions: [(0, 0)]) });
        var result = await NewCook().CookOneAsync(World);
        Assert.Equal(CookStatus.Failed, result.Status);
        Assert.Contains("x0_z0.oregion", result.Error!.Message);
    }

    [Fact]
    public async Task AMissingImageFailsTheTerrain()
    {
        Project(texture: false);
        var result = await NewCook().CookOneAsync(World);
        Assert.Equal(CookStatus.Failed, result.Status);
        Assert.Contains("textures/grass.png", result.Error!.Message);
    }

    // a cooked terrain

    [Fact]
    public async Task ACookedTerrainNamesAListedRegionThePackDoesNotHold()
    {
        var terrain = TerrainFile(regions: [(0, 0), (1, 0)]);
        var folder = await _packs.BundleAsync(
            "hand",
            new Dictionary<string, byte[]>
            {
                ["world.oterrain.oterrain"] = terrain,
                ["x0_z0.oregion.oregion"] = RegionFile(0, 0),
            });
        var pack = await _packs.PackFolderAsync(folder, "hand", null, "android");
        var packs = new MountedPacks(CookTargets.Find("android")!);
        packs.Start(pack.Pack);

        var cooked = await CookedTerrain.OpenAsync(packs, Id("world.oterrain"));
        Assert.Equal([new RegionKey(0, 0)], cooked.Regions.Keys);
        Assert.Contains("x1_z0.oregion", Assert.Single(cooked.Problems));
    }

    [Fact]
    public async Task ACookedTerrainOpensFromAPackWithItsSettingsAndItsRegions()
    {
        var packs = await CookAndMountAsync();
        var cooked = await CookedTerrain.OpenAsync(packs, World);
        Assert.Equal(4, cooked.Terrain.RegionSize);
        Assert.Empty(cooked.Terrain.Regions);
        Assert.Equal([new RegionKey(0, 0), new RegionKey(1, 0)], cooked.Regions.Keys);
        Assert.Empty(cooked.Problems);
    }

    [Fact]
    public async Task ACookedTerrainStreamsTheHeightsThatWereAuthored()
    {
        var packs = await CookAndMountAsync();
        var cooked = await CookedTerrain.OpenAsync(packs, World);
        var streamer = new TerrainStreamer(
            terrain: cooked.Terrain, source: cooked.Regions, loadRadius: 1, keepRadius: 1);

        await streamer.FollowAsync(0.5, 0.5);
        Assert.Equal([new RegionKey(0, 0)], streamer.Resident);
        Assert.Equal(10d, cooked.Terrain.HeightAt(0, 0));

        await streamer.FollowAsync(6.5, 0.5);
        Assert.Equal([new RegionKey(1, 0)], streamer.Resident);
        Assert.Equal(100d, cooked.Terrain.HeightAt(4, 0));
        Assert.Empty(streamer.Failures);
    }

    [Fact]
    public async Task ACookedTerrainIsNotFoundWhenThePackHasNoSuchTerrain()
    {
        var packs = await CookAndMountAsync();
        await Assert.ThrowsAsync<AssetNotFoundException>(
            () => CookedTerrain.OpenAsync(packs, Id("terrain/other.oterrain")));
    }

    // the default importers

    [Fact]
    public void AProjectCookClaimsBothFiles()
    {
        var registry = CookProject.DefaultImporters();
        Assert.IsType<TerrainImporter>(registry.ByName("terrain"));
        foreach (var file in new[] { "a.oterrain", "x0_z0.oregion" })
        {
            Assert.NotNull(registry.ForAsset(Id(file), new ImportSettings()));
        }
    }

    // Cooks the three files a terrain is authored as and mounts the pack made from them, the way a running game
    // reads them back.
    private async Task<MountedPacks> CookAndMountAsync()
    {
        await WriteAssetAsync("terrain/world.oterrain", TerrainFile(regions: [(0, 0), (1, 0)]));
        await WriteAssetAsync("terrain/x0_z0.oregion", RegionFile(0, 0, height: 10));
        await WriteAssetAsync("terrain/x1_z0.oregion", RegionFile(1, 0, height: 100));
        var target = CookTargets.Find("android")!;
        var project = new CookProject(
            assetsPath: _packs.At("assets"),
            outPath: _packs.At("out"),
            target: target,
            cachePath: _packs.At("cache"),
            importers: new ImporterRegistry([_importer]));
        var report = await project.RunAsync();
        Assert.True(report.Ok, string.Join("\n", report.Results));
        await PackBundle.PackAsync(project.BundlePath, _packs.At("game.opack"), target);
        var bytes = await File.ReadAllBytesAsync(_packs.At("game.opack"), TestContext.Current.CancellationToken);
        var packs = new MountedPacks(target);
        packs.Start(await AssetPack.OpenAsync(new MemoryPackBytes(bytes)));
        return packs;
    }

    private async Task WriteAssetAsync(string relative, byte[] bytes)
    {
        var file = _packs.At($"assets/{relative}");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        await File.WriteAllBytesAsync(file, bytes, TestContext.Current.CancellationToken);
    }
}
