using System.Text;

namespace Orblit.Asset.Tests;

public sealed class PatchChainTests : IDisposable
{
    private readonly PackFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    // What a game holds. A patch is built from the whole of what the game should hold after it, so each one
    // here names every asset.
    private static Dictionary<string, string> State(string wall = "wall v1", string tree = "tree") => new()
    {
        ["textures/wall.ktx2"] = wall,
        ["models/tree.glb"] = tree,
        ["scenes/town.oscene"] = "town",
    };

    private static async Task<string> Text(MountedPacks source, string id) =>
        Encoding.UTF8.GetString(await source.ReadAsync(AssetId.Parse(id)));

    private static void Mount(MountedPacks packs, PatchChain chain)
    {
        foreach (var pack in chain.Ordered)
        {
            packs.Patch(pack);
        }

        foreach (var pack in chain.Standalone)
        {
            packs.AddMod(pack);
        }
    }

    private static IEnumerable<ContentHash> Hashes(IEnumerable<AssetPack> packs) => packs.Select(p => p.Hash);

    [Fact]
    public async Task PutsPatchesInTheOrderEachNamesTheOneBefore()
    {
        var shipped = await _fixture.PackAsync("game", State());
        var one = await _fixture.PackAsync("one", State(wall: "wall v2"), [shipped]);
        var two = await _fixture.PackAsync("two", State(wall: "wall v3"), [shipped, one]);
        var three = await _fixture.PackAsync("three", State(wall: "wall v4", tree: "tree v2"), [shipped, one, two]);

        foreach (var listing in new[] { new[] { one, two, three }, [three, two, one], [two, three, one], [three, one, two] })
        {
            var chain = PatchChain.Of(shipped, listing);

            Assert.Equal([one.Hash, two.Hash, three.Hash], Hashes(chain.Ordered));
            Assert.Empty(chain.Skipped);
            Assert.Empty(chain.Standalone);
        }
    }

    [Fact]
    public async Task MountsThreePatchesInChainOrderAndReadsTheLast()
    {
        var shipped = await _fixture.PackAsync("game", State());
        var one = await _fixture.PackAsync("one", State(wall: "wall v2"), [shipped]);
        var two = await _fixture.PackAsync("two", State(wall: "wall v2", tree: "tree v2"), [shipped, one]);
        var three = await _fixture.PackAsync("three", State(wall: "wall v4", tree: "tree v2"), [shipped, one, two]);
        var packs = new MountedPacks(CookTargets.Android);
        packs.Start(shipped);

        Mount(packs, PatchChain.Of(shipped, [three, one, two]));

        Assert.Equal("wall v4", await Text(packs, "textures/wall.ktx2"));
        Assert.Equal("tree v2", await Text(packs, "models/tree.glb"));
        Assert.Equal("town", await Text(packs, "scenes/town.oscene"));
    }

    [Fact]
    public async Task SkipsAPatchWhoseBaseIsNotHereAndSaysWhich()
    {
        var shipped = await _fixture.PackAsync("game", State());
        var one = await _fixture.PackAsync("one", State(wall: "wall v2"), [shipped]);
        var two = await _fixture.PackAsync("two", State(wall: "wall v2", tree: "tree v2"), [shipped, one]);
        var three = await _fixture.PackAsync("three", State(wall: "wall v4", tree: "tree v2"), [shipped, one, two]);

        var chain = PatchChain.Of(shipped, [three, one]);

        Assert.Equal([one.Hash], Hashes(chain.Ordered));
        Assert.Equal([three.Hash], Hashes(chain.Skipped.Select(s => s.Pack)));
        var reason = Assert.Single(chain.Skipped).Reason;
        Assert.Contains(two.Hash.Hex[..12], reason);
        Assert.Contains($"shipped pack is {shipped.Hash.Hex[..12]}", reason);
    }

    [Fact]
    public async Task SkipsAPatchBuiltOnTheShippedPackOfAnOlderVersion()
    {
        var older = await _fixture.PackAsync("older", State());
        var patch = await _fixture.PackAsync("patch", State(wall: "wall v2"), [older]);
        var extra = State();
        extra["models/rock.glb"] = "rock";
        var shipped = await _fixture.PackAsync("game", extra);

        var chain = PatchChain.Of(shipped, [patch]);

        Assert.Empty(chain.Ordered);
        Assert.Equal(patch.Hash, Assert.Single(chain.Skipped).Pack.Hash);
    }

    [Fact]
    public async Task StopsAtAForkAndSkipsBothSidesOfIt()
    {
        var shipped = await _fixture.PackAsync("game", State());
        var left = await _fixture.PackAsync("left", State(wall: "left"), [shipped]);
        var right = await _fixture.PackAsync("right", State(wall: "right"), [shipped]);
        var after = await _fixture.PackAsync("after", State(wall: "wall v2", tree: "tree v2"), [shipped, left]);

        var chain = PatchChain.Of(shipped, [left, right, after]);

        Assert.Empty(chain.Ordered);
        Assert.Equal(
            new HashSet<ContentHash> { left.Hash, right.Hash, after.Hash },
            chain.Skipped.Select(s => s.Pack.Hash).ToHashSet());
        var forked = chain.Skipped.First(s => s.Pack.Hash == left.Hash);
        Assert.Contains("2 patches were built on", forked.Reason);
        var stranded = chain.Skipped.First(s => s.Pack.Hash == after.Hash);
        Assert.Contains("not in the chain", stranded.Reason);
    }

    [Fact]
    public async Task KeepsAPatchThatComesBeforeAForkLowerDown()
    {
        var shipped = await _fixture.PackAsync("game", State());
        var one = await _fixture.PackAsync("one", State(wall: "wall v2"), [shipped]);
        var left = await _fixture.PackAsync("left", State(wall: "wall v2", tree: "left"), [shipped, one]);
        var right = await _fixture.PackAsync("right", State(wall: "wall v2", tree: "right"), [shipped, one]);

        var chain = PatchChain.Of(shipped, [left, one, right]);

        Assert.Equal([one.Hash], Hashes(chain.Ordered));
        Assert.Equal(2, chain.Skipped.Count);
    }

    [Fact]
    public async Task SkipsAPackForAnotherTarget()
    {
        var shipped = await _fixture.PackAsync("game", State());
        var other = await _fixture.PackAsync("web", State(), target: "web");

        var chain = PatchChain.Of(shipped, [other]);

        Assert.Empty(chain.Standalone);
        var reason = Assert.Single(chain.Skipped).Reason;
        Assert.Contains("for web", reason);
        Assert.Contains("for android", reason);
    }

    [Fact]
    public async Task OffersPacksWithNoBaseAsModsAndLeavesTheShippedOne()
    {
        var shipped = await _fixture.PackAsync("game", State());
        var mod = await _fixture.PackAsync("mod", State(wall: "mod wall"));

        var chain = PatchChain.Of(shipped, [shipped, mod, mod]);

        Assert.Equal([mod.Hash], Hashes(chain.Standalone));
        Assert.Empty(chain.Ordered);
        Assert.Empty(chain.Skipped);
    }
}
