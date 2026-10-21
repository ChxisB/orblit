using System.Text;

namespace Orblit.Asset.Tests;

public sealed class MountedPacksTests : IDisposable
{
    private static readonly Dictionary<string, string> Game = new()
    {
        ["textures/wall.ktx2"] = "wall v1",
        ["models/tree.glb"] = "tree",
        ["scenes/town.oscene"] = "town",
    };

    private readonly PackFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    // Game with some files added or changed.
    private static Dictionary<string, string> GamePlus(params (string Id, string Text)[] files)
    {
        var all = new Dictionary<string, string>(Game);
        foreach (var (id, text) in files)
        {
            all[id] = text;
        }

        return all;
    }

    private static Dictionary<string, string> Files(params (string Id, string Text)[] files) =>
        files.ToDictionary(file => file.Id, file => file.Text);

    private static MountedPacks Mounted(string target = "android") => new(CookTargets.Find(target)!);

    private static async Task<string> Text(IAssetSource source, string id) =>
        Encoding.UTF8.GetString(await source.ReadAsync(AssetId.Parse(id)));

    private static void Refused(Action mounting, string text)
    {
        var error = Assert.Throws<PackMountRefusedException>(mounting);
        Assert.Contains(text, error.Message);
    }

    private static List<string> Ids(IEnumerable<AssetId> ids) => [.. ids.Select(id => $"{id}")];

    private Task<PackSummary> PackNext(string folder, IReadOnlyList<AssetPack> @base) =>
        PackBundle.PackAsync(folder, _fixture.At("next.opack"), CookTargets.Android, @base);

    // a patch

    [Fact]
    public async Task HoldsWhatChangedWhatIsNewAndNamesWhatWent()
    {
        var shipped = await _fixture.PackAsync("game", Game);
        var patch = await _fixture.PackAsync(
            "patch",
            Files(("textures/wall.ktx2", "wall v2"), ("models/tree.glb", "tree"), ("models/rock.glb", "rock")),
            [shipped]);

        Assert.Equal(2, patch.Manifest.Entries.Count);
        Assert.Equal(["models/rock.glb", "textures/wall.ktx2"], Ids(patch.Manifest.Entries.Keys).Order(StringComparer.Ordinal));
        Assert.Equal(["scenes/town.oscene"], Ids(patch.Removed));
        Assert.Equal(shipped.Hash, patch.Base);

        var packs = Mounted();
        packs.Start(shipped);
        packs.Patch(patch);
        Assert.Equal("wall v2", await Text(packs, "textures/wall.ktx2"));
        Assert.Equal("rock", await Text(packs, "models/rock.glb"));
        Assert.Equal("tree", await Text(packs, "models/tree.glb"));
        await Assert.ThrowsAsync<AssetNotFoundException>(() => packs.ReadAsync(AssetId.Parse("scenes/town.oscene")));
        Assert.False(await packs.ExistsAsync(AssetId.Parse("scenes/town.oscene")));
    }

    [Fact]
    public async Task ReportsTwoBlobsInItsSummary()
    {
        var shipped = await _fixture.PackAsync("game", Game);
        var folder = await _fixture.BundleAsync(
            "next", Files(("textures/wall.ktx2", "wall v2"), ("models/tree.glb", "tree"), ("models/rock.glb", "rock")));

        var summary = await PackNext(folder, [shipped]);

        Assert.Equal(2, summary.Blobs);
        Assert.Equal(2, summary.Assets);
        Assert.Equal(1, summary.Removed);
    }

    [Fact]
    public async Task CountsWhatIsNewApartFromWhatItReplaces()
    {
        var shipped = await _fixture.PackAsync("game", Game);
        var folder = await _fixture.BundleAsync(
            "next",
            Files(
                ("textures/wall.ktx2", "wall v2"),
                ("models/tree.glb", "tree"),
                ("models/rock.glb", "rock"),
                ("models/twin.glb", "tree")));

        var summary = await PackNext(folder, [shipped]);

        Assert.Equal(3, summary.Assets);
        Assert.Equal(1, summary.Replaced);
        Assert.Equal(2, summary.Added);
        Assert.Equal(1, summary.Removed);
    }

    [Fact]
    public async Task OfNothingIsAllAddedWhenItHasNoBase()
    {
        var folder = await _fixture.BundleAsync("whole", Game);

        var summary = await PackBundle.PackAsync(folder, _fixture.At("whole.opack"), CookTargets.Android);

        Assert.Equal(3, summary.Added);
        Assert.Equal(0, summary.Replaced);
    }

    [Fact]
    public async Task IsTheSameFileEveryTime()
    {
        var token = TestContext.Current.CancellationToken;
        var shipped = await _fixture.PackAsync("game", Game);
        var folder = await _fixture.BundleAsync("next", Files(("textures/wall.ktx2", "wall v2")));
        foreach (var name in new[] { "a", "b" })
        {
            await PackBundle.PackAsync(folder, _fixture.At($"{name}.opack"), CookTargets.Android, [shipped]);
        }

        Assert.Equal(
            await File.ReadAllBytesAsync(_fixture.At("a.opack"), token),
            await File.ReadAllBytesAsync(_fixture.At("b.opack"), token));
    }

    [Fact]
    public async Task OfAnUnchangedBundleHoldsNothingAndStillMounts()
    {
        var shipped = await _fixture.PackAsync("game", Game);
        var patch = await _fixture.PackAsync("patch", Game, [shipped]);
        Assert.Empty(patch.Manifest.Entries);
        Assert.Empty(patch.Removed);

        var packs = Mounted();
        packs.Start(shipped);
        packs.Patch(patch);
        Assert.Equal(3, packs.Manifest.Entries.Count);
        Assert.Equal(patch.Hash, packs.ChainHash);
    }

    [Fact]
    public async Task HoldsANewIdEvenWhenItsBytesAreInTheBase()
    {
        var shipped = await _fixture.PackAsync("game", Game);
        var patch = await _fixture.PackAsync("patch", GamePlus(("models/oak.glb", "tree")), [shipped]);
        Assert.Equal(["models/oak.glb"], Ids(patch.Manifest.Entries.Keys));
        var packs = Mounted();
        packs.Start(shipped);
        packs.Patch(patch);
        Assert.Equal("tree", await Text(packs, "models/oak.glb"));
    }

    [Fact]
    public async Task BuildsAgainstAChainAndNamesTheTopOfIt()
    {
        var shipped = await _fixture.PackAsync("game", Game);
        var one = await _fixture.PackAsync("one", Files(("textures/wall.ktx2", "wall v2"), ("models/tree.glb", "tree")), [shipped]);
        var two = await _fixture.PackAsync(
            "two",
            Files(("textures/wall.ktx2", "wall v2"), ("models/tree.glb", "tree"), ("scenes/town.oscene", "town again")),
            [shipped, one]);

        Assert.Equal(["scenes/town.oscene"], Ids(one.Removed));
        Assert.Equal(one.Hash, two.Base);
        Assert.Empty(two.Removed);
        Assert.Equal(["scenes/town.oscene"], Ids(two.Manifest.Entries.Keys));

        var packs = Mounted();
        packs.Start(shipped);
        packs.Patch(one);
        packs.Patch(two);
        Assert.Equal("town again", await Text(packs, "scenes/town.oscene"));
        Assert.Equal("wall v2", await Text(packs, "textures/wall.ktx2"));
    }

    [Fact]
    public async Task BuiltAgainstTheWrongTargetIsRefusedBeforeItIsWritten()
    {
        var macos = await _fixture.PackAsync("game", Game, target: "macos");
        var folder = await _fixture.BundleAsync("next", Game);

        await Assert.ThrowsAsync<PackMountRefusedException>(() => PackNext(folder, [macos]));

        Assert.False(File.Exists(_fixture.At("next.opack")));
    }

    [Fact]
    public async Task BuiltAgainstAChainOutOfOrderIsRefused()
    {
        var shipped = await _fixture.PackAsync("game", Game);
        var one = await _fixture.PackAsync("one", Files(("models/tree.glb", "oak")), [shipped]);
        var folder = await _fixture.BundleAsync("next", Game);

        await Assert.ThrowsAsync<PackMountRefusedException>(() => PackNext(folder, [one, shipped]));
    }

    // mounting

    [Fact]
    public async Task RefusesAPatchBuiltAgainstADifferentPack()
    {
        var shipped = await _fixture.PackAsync("game", Game);
        var patch = await _fixture.PackAsync("patch", GamePlus(("models/rock.glb", "rock")), [shipped]);
        var other = await _fixture.PackAsync("other", GamePlus(("models/other.glb", "x")));

        var packs = Mounted();
        packs.Start(other);
        Refused(() => packs.Patch(patch), "was built against");
    }

    [Fact]
    public async Task RefusesAPackMadeForAnotherTarget()
    {
        var macos = await _fixture.PackAsync("mac", Game, target: "macos");
        var macosMod = await _fixture.PackAsync("modmac", Files(("a.txt", "x")), target: "macos");
        var android = await _fixture.PackAsync("droid", Game);
        var androidPatch = await _fixture.PackAsync("patch", GamePlus(("a.txt", "x")), [android]);
        var macosPatch = await _fixture.PackAsync("macpatch", GamePlus(("a.txt", "x")), [macos], target: "macos");

        var packs = Mounted();
        Refused(() => packs.Start(macos), "A pack for macos");
        packs.Start(android);
        Refused(() => packs.Patch(macosPatch), "A pack for macos");
        Refused(() => packs.AddMod(macosMod), "A pack for macos");
        packs.Patch(androidPatch);
    }

    [Fact]
    public async Task KeepsStartPatchAndAddModToTheirOwnKindOfPack()
    {
        var shipped = await _fixture.PackAsync("game", Game);
        var patch = await _fixture.PackAsync("patch", GamePlus(("a.txt", "x")), [shipped]);
        var mod = await _fixture.PackAsync("mod", Files(("a.txt", "mod")));

        var fresh = Mounted();
        Refused(() => fresh.Start(patch), "is a patch");
        Refused(() => fresh.Patch(patch), "Start the chain");
        fresh.Start(shipped);
        Refused(() => fresh.Start(mod), "already started");
        Refused(() => fresh.Patch(mod), "no base");
        Refused(() => fresh.AddMod(patch), "is a patch");
    }

    [Fact]
    public async Task RefusesTheSameModTwice()
    {
        var shipped = await _fixture.PackAsync("game", Game);
        var mod = await _fixture.PackAsync("mod", Files(("a.txt", "mod")));
        var packs = Mounted();
        packs.Start(shipped);
        packs.AddMod(mod);
        Refused(() => packs.AddMod(mod), "already mounted");
    }

    [Fact]
    public async Task LeavesTheChainAloneWhenAMountIsRefused()
    {
        var shipped = await _fixture.PackAsync("game", Game);
        var macos = await _fixture.PackAsync("mac", Game, target: "macos");
        var packs = Mounted();
        packs.Start(shipped);
        Assert.Throws<PackMountRefusedException>(() => packs.AddMod(macos));
        Assert.Equal(shipped.Hash, packs.ChainHash);
        Assert.Equal(3, packs.Manifest.Entries.Count);
    }

    // a mod

    [Fact]
    public async Task WinsOverAPatchWhicheverWasMountedFirst()
    {
        var shipped = await _fixture.PackAsync("game", Game);
        var patch = await _fixture.PackAsync("patch", GamePlus(("textures/wall.ktx2", "wall patched")), [shipped]);
        var mod = await _fixture.PackAsync("mod", Files(("textures/wall.ktx2", "wall modded")));

        var patchFirst = Mounted();
        patchFirst.Start(shipped);
        patchFirst.Patch(patch);
        patchFirst.AddMod(mod);
        var modFirst = Mounted();
        modFirst.Start(shipped);
        modFirst.AddMod(mod);
        modFirst.Patch(patch);
        var modBeforeAnything = Mounted();
        modBeforeAnything.AddMod(mod);
        modBeforeAnything.Start(shipped);
        modBeforeAnything.Patch(patch);

        foreach (var packs in new[] { patchFirst, modFirst, modBeforeAnything })
        {
            Assert.Equal("wall modded", await Text(packs, "textures/wall.ktx2"));
            Assert.Equal("tree", await Text(packs, "models/tree.glb"));
        }
    }

    [Fact]
    public async Task MountedLaterSitsAboveOneMountedEarlier()
    {
        var shipped = await _fixture.PackAsync("game", Game);
        var first = await _fixture.PackAsync("first", Files(("textures/wall.ktx2", "first")));
        var second = await _fixture.PackAsync("second", Files(("textures/wall.ktx2", "second")));
        var packs = Mounted();
        packs.Start(shipped);
        packs.AddMod(first);
        packs.AddMod(second);
        Assert.Equal("second", await Text(packs, "textures/wall.ktx2"));
    }

    [Fact]
    public async Task CanAddBackAnIdAPatchRemoved()
    {
        var shipped = await _fixture.PackAsync("game", Game);
        var patch = await _fixture.PackAsync("patch", Files(("textures/wall.ktx2", "wall v1"), ("models/tree.glb", "tree")), [shipped]);
        var mod = await _fixture.PackAsync("mod", Files(("scenes/town.oscene", "my town")));
        var packs = Mounted();
        packs.Start(shipped);
        packs.Patch(patch);
        Assert.False(await packs.ExistsAsync(AssetId.Parse("scenes/town.oscene")));
        packs.AddMod(mod);
        Assert.Equal("my town", await Text(packs, "scenes/town.oscene"));
    }

    // ownership

    [Fact]
    public async Task NamesThePackThatAnswersForEachId()
    {
        var shipped = await _fixture.PackAsync("game", Game);
        var patch = await _fixture.PackAsync("one", Files(("textures/wall.ktx2", "wall v2"), ("models/tree.glb", "tree")), [shipped]);
        var mod = await _fixture.PackAsync("mod", Files(("models/tree.glb", "oak")));
        var packs = Mounted();
        packs.Start(shipped);
        packs.Patch(patch);
        packs.AddMod(mod);

        Assert.Same(patch, packs.OwnerOf(AssetId.Parse("textures/wall.ktx2")));
        Assert.Same(mod, packs.OwnerOf(AssetId.Parse("models/tree.glb")));
    }

    [Fact]
    public async Task NamesTheBaseForAnIdNoPatchTouched()
    {
        var shipped = await _fixture.PackAsync("game", Game);
        var patch = await _fixture.PackAsync("one", Files(("textures/wall.ktx2", "wall v2"), ("models/tree.glb", "tree")), [shipped]);
        var packs = Mounted();
        packs.Start(shipped);
        packs.Patch(patch);

        Assert.Same(shipped, packs.OwnerOf(AssetId.Parse("models/tree.glb")));
    }

    [Fact]
    public async Task IsNullForAnIdThatIsRemovedOrNeverWas()
    {
        var shipped = await _fixture.PackAsync("game", Game);
        var patch = await _fixture.PackAsync("one", Files(("textures/wall.ktx2", "wall v1"), ("models/tree.glb", "tree")), [shipped]);
        var packs = Mounted();
        packs.Start(shipped);
        packs.Patch(patch);

        Assert.Null(packs.OwnerOf(AssetId.Parse("scenes/town.oscene")));
        Assert.Null(packs.OwnerOf(AssetId.Parse("models/ghost.glb")));
    }

    // reading through the chain

    [Fact]
    public async Task SaysWhyAnAssetIsMissing()
    {
        var empty = Mounted();
        var none = await Assert.ThrowsAsync<AssetNotFoundException>(() => empty.ReadAsync(AssetId.Parse("a.txt")));
        Assert.Contains("no pack is mounted", none.Message);
        var shipped = await _fixture.PackAsync("game", Game);
        var packs = Mounted();
        packs.Start(shipped);
        var missing = await Assert.ThrowsAsync<AssetNotFoundException>(() => packs.ReadAsync(AssetId.Parse("a.txt")));
        Assert.Contains("none of the 1 mounted packs", missing.Message);
    }

    [Fact]
    public async Task LayersUnderADirectoryLikeAnyOtherSource()
    {
        var shipped = await _fixture.PackAsync("game", Game);
        var packs = Mounted();
        packs.Start(shipped);
        var layered = new LayeredAssetSource(
            [new MemoryAssetSource(new Dictionary<AssetId, byte[]> { [AssetId.Parse("models/tree.glb")] = Encoding.UTF8.GetBytes("dev") }), packs]);
        Assert.Equal("dev", await Text(layered, "models/tree.glb"));
        Assert.Equal("town", await Text(layered, "scenes/town.oscene"));
    }
}
