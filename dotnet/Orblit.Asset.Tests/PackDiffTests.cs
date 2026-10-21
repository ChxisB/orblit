using System.Text;
using Orblit.Json;

namespace Orblit.Asset.Tests;

using JsonMap = OrderedDictionary<string, object?>;

public sealed class PackDiffTests : IDisposable
{
    private readonly PackFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    private static Dictionary<string, string> Files(params (string Id, string Text)[] files) =>
        files.ToDictionary(file => file.Id, file => file.Text);

    private static AssetId Id(string text) => AssetId.Parse(text);

    // A scene that places each of paths as a model.
    private static string Scene(params string[] paths) => DartJson.Encode(new JsonMap
    {
        ["entities"] = paths.Select(path => (object?)new JsonMap { ["name"] = "thing", ["model"] = path }).ToList(),
    });

    private static Task<PackDiff> Diff(AssetPack before, AssetPack after, params AssetPack[] pool) =>
        PackDiff.BetweenAsync(MountedPacks.WithBases(before, pool), MountedPacks.WithBases(after, pool));

    // The older pack of a pair, holding every asset the newer one drops.
    private Task<AssetPack> Older(params (string Id, string Text)[] files)
    {
        var all = Files(("textures/gone.png", "gone"), ("models/tree.glb", "tree"), ("anim/gone.oclip", "{}"));
        foreach (var (id, text) in files)
        {
            all[id] = text;
        }

        return _fixture.PackAsync("before", all);
    }

    // PackDiff

    [Fact]
    public async Task ListsWhatWasAddedRemovedAndChangedByName()
    {
        var before = await _fixture.PackAsync(
            "before",
            Files(("textures/wall.png", "wall one"), ("models/tree.glb", "tree"), ("textures/gone.png", "gone")));
        var after = await _fixture.PackAsync(
            "after",
            Files(("textures/wall.png", "wall two is longer"), ("models/tree.glb", "tree"), ("models/fresh.glb", "fresh")));

        var result = await Diff(before, after);

        Assert.Equal([Id("models/fresh.glb")], result.Added);
        Assert.Equal([Id("textures/gone.png")], result.Removed);
        var changed = Assert.Single(result.Changed);
        Assert.Equal(Id("textures/wall.png"), changed.Id);
        Assert.Equal("wall one".Length, changed.Before);
        Assert.Equal("wall two is longer".Length, changed.After);
        Assert.False(result.IsBroken);
    }

    [Fact]
    public async Task SaysNothingAboutTwoPacksThatHoldTheSameThings()
    {
        var files = Files(("models/tree.glb", "tree"), ("a/scene.oscene", "{}"));

        var result = await Diff(await _fixture.PackAsync("one", files), await _fixture.PackAsync("two", files));

        Assert.Empty(result.Added);
        Assert.Empty(result.Removed);
        Assert.Empty(result.Changed);
        Assert.Empty(result.Dangling);
        Assert.False(result.IsBroken);
    }

    [Fact]
    public async Task ASceneThatNamesARemovedTextureIsDangling()
    {
        var before = await _fixture.PackAsync(
            "before",
            Files(
                ("textures/wall.png", "wall"),
                ("models/tree.glb", "tree"),
                ("scenes/town.oscene", Scene("models/tree.glb", "textures/wall.png"))));
        var after = await _fixture.PackAsync(
            "after",
            Files(("models/tree.glb", "tree"), ("scenes/town.oscene", Scene("models/tree.glb", "textures/wall.png"))));

        var result = await Diff(before, after);

        Assert.Equal([Id("textures/wall.png")], result.Removed);
        var dangling = Assert.Single(result.Dangling);
        Assert.Equal(Id("scenes/town.oscene"), dangling.Document);
        Assert.Equal(Id("textures/wall.png"), dangling.Missing);
        Assert.True(result.IsBroken);
    }

    [Fact]
    public async Task EveryKindOfDocumentIsReadByName()
    {
        var names = DartJson.Encode(new JsonMap { ["clips"] = new List<object?> { "anim/gone.oclip" } });
        var after = await _fixture.PackAsync(
            "after",
            Files(
                ("models/tree.glb", "tree"),
                ("a/rig.oprefab", names),
                ("a/walk.oclip", names),
                ("a/move.oblend", DartJson.Encode(new JsonMap
                {
                    ["states"] = new List<object?> { new JsonMap { ["clip"] = "anim/gone.oclip" } },
                })),
                ("a/look.omat", DartJson.Encode(new JsonMap { ["albedo"] = "textures/gone.png" }))));

        var result = await Diff(await Older(), after);

        Assert.Equal(
            ["a/look.omat", "a/move.oblend", "a/rig.oprefab", "a/walk.oclip"],
            result.Dangling.Select(d => d.Document.ToString()));
    }

    [Fact]
    public async Task ANameUsedAsAKeyCounts()
    {
        var after = await _fixture.PackAsync(
            "after",
            Files(
                ("models/tree.glb", "tree"),
                ("scenes/town.oscene", DartJson.Encode(new JsonMap
                {
                    ["overrides"] = new JsonMap { ["textures/gone.png"] = 1L },
                }))));

        var result = await Diff(await Older(), after);

        Assert.Equal(Id("textures/gone.png"), Assert.Single(result.Dangling).Missing);
    }

    // "./tree.glb" is still there and "./models/tree.glb" is another file.
    [Fact]
    public async Task AReferenceThatStartsDotSlashIsReadAgainstItsDocument()
    {
        var after = await _fixture.PackAsync(
            "after",
            Files(
                ("scenes/town.oscene", Scene("../models/tree.glb", "./tree.glb", "./models/tree.glb")),
                ("scenes/tree.glb", "tree"),
                ("textures/gone.png", "kept")));

        var result = await Diff(await Older(("scenes/tree.glb", "tree")), after);

        Assert.Equal(["models/tree.glb"], result.Dangling.Select(d => d.Missing.ToString()));
    }

    [Fact]
    public async Task FreeTextThatEndsInARemovedNameIsNotAReference()
    {
        var after = await _fixture.PackAsync(
            "after",
            Files(
                ("models/tree.glb", "tree"),
                ("scenes/town.oscene", DartJson.Encode(new JsonMap
                {
                    ["name"] = "Hero.1",
                    ["note"] = "see textures/gone.png",
                    ["looks"] = "matte white",
                    ["other"] = "readme.txt",
                }))));

        var result = await Diff(await Older(), after);

        Assert.Empty(result.Dangling);
    }

    [Fact]
    public async Task ANameThatNeverWasInTheOlderPackIsNotDangling()
    {
        var after = await _fixture.PackAsync(
            "after",
            Files(("models/tree.glb", "tree"), ("scenes/town.oscene", Scene("textures/never.png", "models/tree.glb"))));

        var result = await Diff(await Older(), after);

        Assert.Equal([Id("anim/gone.oclip"), Id("textures/gone.png")], result.Removed);
        Assert.Empty(result.Dangling);
    }

    [Fact]
    public async Task ANameThatIsRemovedAndStillPresentIsNotDangling()
    {
        var after = await _fixture.PackAsync(
            "after",
            Files(
                ("models/tree.glb", "tree"),
                ("textures/gone.png", "back again"),
                ("scenes/town.oscene", Scene("textures/gone.png"))));

        var result = await Diff(await Older(), after);

        Assert.Empty(result.Dangling);
    }

    [Fact]
    public async Task ADocumentThatIsNotJsonIsUnreadableOnceSomethingIsRemoved()
    {
        var after = await _fixture.PackAsync(
            "after", Files(("models/tree.glb", "tree"), ("scenes/town.oscene", "not json")));

        var result = await Diff(await Older(), after);

        Assert.Equal([Id("scenes/town.oscene")], result.Unreadable);
        Assert.True(result.IsBroken);
    }

    [Fact]
    public async Task DocumentsAreNotReadWhenNothingWasRemoved()
    {
        var files = Files(("scenes/town.oscene", "not json"));
        var more = Files(("scenes/town.oscene", "not json"), ("models/fresh.glb", "fresh"));

        var result = await Diff(await _fixture.PackAsync("before", files), await _fixture.PackAsync("after", more));

        Assert.Empty(result.Unreadable);
        Assert.False(result.IsBroken);
    }

    [Fact]
    public async Task APatchIsReadWithItsBaseSoWhatItDidNotTouchIsPresent()
    {
        var game = await _fixture.PackAsync(
            "game",
            Files(
                ("textures/wall.png", "wall"),
                ("textures/roof.png", "roof"),
                ("scenes/town.oscene", Scene("textures/wall.png", "textures/roof.png"))));
        var patch = await _fixture.PackAsync(
            "patch",
            Files(
                ("textures/wall.png", "wall repainted"),
                ("scenes/town.oscene", Scene("textures/wall.png", "textures/roof.png"))),
            [game]);

        var result = await Diff(game, patch, game);

        Assert.Equal(["textures/wall.png"], result.Changed.Select(c => c.Id.ToString()));
        Assert.Equal([Id("textures/roof.png")], result.Removed);

        // The scene is in the base, the removal is in the patch.
        var dangling = Assert.Single(result.Dangling);
        Assert.Equal(Id("scenes/town.oscene"), dangling.Document);
        Assert.Equal(Id("textures/roof.png"), dangling.Missing);
    }

    [Fact]
    public async Task APatchThatRemovesNothingLeavesItsBaseSceneWhole()
    {
        var game = await _fixture.PackAsync(
            "game", Files(("textures/wall.png", "wall"), ("scenes/town.oscene", Scene("textures/wall.png"))));
        var patch = await _fixture.PackAsync(
            "patch",
            Files(("textures/wall.png", "wall repainted"), ("scenes/town.oscene", Scene("textures/wall.png"))),
            [game]);

        var result = await Diff(game, patch, game);

        Assert.Empty(result.Dangling);
        Assert.Single(result.Changed);
    }

    [Fact]
    public async Task PacksForDifferentTargetsAreRefused()
    {
        var android = await _fixture.PackAsync("android", Files(("a/x.glb", "one")));
        var web = await _fixture.PackAsync("web", Files(("a/x.glb", "one")), target: "web");

        var error = await Assert.ThrowsAsync<PackMountRefusedException>(() => Diff(android, web));

        Assert.Contains("android", error.Message);
        Assert.Contains("web", error.Message);
    }

    [Fact]
    public async Task TheListsCannotBeChangedByTheCaller()
    {
        var result = await Diff(await Older(), await _fixture.PackAsync("after", Files(("models/tree.glb", "tree"))));

        Assert.Throws<NotSupportedException>(() => ((IList<AssetId>)result.Added).Add(Id("a/b.glb")));
        Assert.Throws<NotSupportedException>(() => ((IList<AssetId>)result.Removed).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<ChangedAsset>)result.Changed).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<DanglingReference>)result.Dangling).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<AssetId>)result.Unreadable).Clear());
    }

    // MountedPacks.WithBases

    [Fact]
    public async Task FindsThePacksUnderAPatchByHashInAnyOrder()
    {
        var game = await _fixture.PackAsync("game", Files(("a/x.glb", "one")));
        var first = await _fixture.PackAsync("first", Files(("a/x.glb", "two")), [game]);
        var second = await _fixture.PackAsync("second", Files(("a/x.glb", "three")), [game, first]);

        var packs = MountedPacks.WithBases(second, [first, game]);

        Assert.Equal(second.Hash, packs.ChainHash);
        Assert.Equal("android", packs.Target);
        Assert.Equal("three", Encoding.UTF8.GetString(await packs.ReadAsync(Id("a/x.glb"))));
        Assert.Equal(second.Hash, MountedPacks.WithBases(second, [game, first]).ChainHash);
    }

    [Fact]
    public async Task NamesThePackThatIsMissing()
    {
        var game = await _fixture.PackAsync("game", Files(("a/x.glb", "one")));
        var patch = await _fixture.PackAsync("patch", Files(("a/x.glb", "two")), [game]);

        var error = Assert.Throws<PackMountRefusedException>(() => MountedPacks.WithBases(patch, []));

        Assert.Contains(game.Hash.Hex[..12], error.Message);
    }

    [Fact]
    public async Task APackWithNoBaseStandsOnItsOwn()
    {
        var game = await _fixture.PackAsync("game", Files(("a/x.glb", "one")));

        Assert.Equal(game.Hash, MountedPacks.WithBases(game, []).ChainHash);
    }
}
