using System.Text.Json;

namespace Orblit.Asset.Tests;

// The pack command: what it prints and the exit code it gives.
public sealed class PackCliTests : IDisposable
{
    private static readonly string Scene = JsonSerializer.Serialize(new Dictionary<string, string> { ["model"] = "textures/wall.png" });

    private readonly PackFixture _fixture = new();
    private readonly StringWriter _out = new();
    private readonly StringWriter _err = new();

    public void Dispose()
    {
        _out.Dispose();
        _err.Dispose();
        _fixture.Dispose();
    }

    private string File_(string name) => _fixture.At($"{name}.opack");

    private async Task<int> Run(params string[] args)
    {
        _out.GetStringBuilder().Clear();
        _err.GetStringBuilder().Clear();
        return await PackCli.RunAsync(args, _out, _err);
    }

    [Fact]
    public async Task DiffPrintsTheSectionsAndExits0ForACleanPair()
    {
        await _fixture.BuildAsync("before", new Dictionary<string, string> { ["models/tree.glb"] = "tree" });
        await _fixture.BuildAsync("after", new Dictionary<string, string> { ["models/tree.glb"] = "tree two", ["models/fresh.glb"] = "fresh" });

        var code = await Run("diff", File_("before"), File_("after"));

        Assert.Equal("", _err.ToString());
        Assert.Equal(0, code);
        Assert.Contains("added      1\n  models/fresh.glb", _out.ToString());
        Assert.Contains("removed    0", _out.ToString());
        Assert.Contains("models/tree.glb  4 to 8 bytes", _out.ToString());
        Assert.Contains("dangling   0", _out.ToString());
        Assert.DoesNotContain("unreadable", _out.ToString());
    }

    [Fact]
    public async Task DiffExits1AndNamesTheDocumentWhenARemovedNameIsStillUsed()
    {
        await _fixture.BuildAsync("before", new Dictionary<string, string> { ["textures/wall.png"] = "wall", ["scenes/town.oscene"] = Scene });
        await _fixture.BuildAsync("after", new Dictionary<string, string> { ["scenes/town.oscene"] = Scene });

        var code = await Run("diff", File_("before"), File_("after"));

        Assert.Equal(1, code);
        Assert.Contains("scenes/town.oscene names textures/wall.png", _out.ToString());
        Assert.Contains("missing names 1, unreadable documents 0", _err.ToString());
    }

    [Fact]
    public async Task DiffReadsAPatchWithThePacksItStandsOn()
    {
        var game = await _fixture.BuildAsync("game", new Dictionary<string, string> { ["textures/wall.png"] = "wall", ["scenes/town.oscene"] = Scene });
        var first = await _fixture.BuildAsync(
            "first", new Dictionary<string, string> { ["textures/wall.png"] = "wall two", ["scenes/town.oscene"] = Scene }, [game.Pack]);
        await _fixture.BuildAsync(
            "second", new Dictionary<string, string> { ["textures/wall.png"] = "wall three", ["scenes/town.oscene"] = Scene }, [game.Pack, first.Pack]);

        var lost = await Run("diff", File_("first"), File_("second"));
        var lostError = _err.ToString();
        var found = await Run("diff", File_("first"), File_("second"), "--base", File_("game"));

        Assert.Equal(1, lost);
        Assert.Contains(game.Hash.Hex[..12], lostError);
        Assert.Equal("", _err.ToString());
        Assert.Equal(0, found);
        Assert.Contains("textures/wall.png  8 to 10 bytes", _out.ToString());
    }

    [Fact]
    public async Task DiffExits1ForPacksOfTwoTargets()
    {
        await _fixture.BuildAsync("before", new Dictionary<string, string> { ["a/x.glb"] = "one" });
        await _fixture.BuildAsync("after", new Dictionary<string, string> { ["a/x.glb"] = "one" }, target: "web");

        var code = await Run("diff", File_("before"), File_("after"));

        Assert.Equal(1, code);
        Assert.Contains("android", _err.ToString());
        Assert.Contains("web", _err.ToString());
    }

    [Fact]
    public async Task DiffExits1ForAPackThatIsNotThere()
    {
        await _fixture.BuildAsync("before", new Dictionary<string, string> { ["a/x.glb"] = "one" });

        var code = await Run("diff", File_("before"), File_("nothing"));

        Assert.Equal(1, code);
        Assert.Contains("There is no such file", _err.ToString());
        Assert.Contains("nothing.opack", _err.ToString());
    }

    [Fact]
    public async Task DiffExits2WhenItIsNotGivenTwoPacks()
    {
        await _fixture.BuildAsync("before", new Dictionary<string, string> { ["a/x.glb"] = "one" });

        var code = await Run("diff", File_("before"));

        Assert.Equal(2, code);
        Assert.Contains("diff takes an old pack and a new one", _err.ToString());
        Assert.Equal(2, await Run("diff", File_("before"), File_("before"), File_("before")));
    }

    [Fact]
    public async Task BuildsListsAndChecksAPack()
    {
        var bundle = await _fixture.BundleAsync("bundle_game", new Dictionary<string, string> { ["models/tree.glb"] = "tree" });

        Assert.Equal(0, await Run("build", bundle, "--target", "android", "-o", File_("game")));
        Assert.Contains($"1 assets in 1 blobs, {new FileInfo(File_("game")).Length} bytes, 0 saved by sharing\n", _out.ToString());
        Assert.Equal(0, await Run("list", File_("game")));
        Assert.Contains("target   android", _out.ToString());
        Assert.Contains("models/tree.glb", _out.ToString());
        Assert.Equal(0, await Run("check", File_("game")));
        Assert.Equal("1 assets, all intact\n", _out.ToString());
    }

    [Fact]
    public async Task APatchBuiltWithABaseCountsAndListsWhatItChanged()
    {
        var game = await _fixture.BuildAsync(
            "game", new Dictionary<string, string> { ["models/tree.glb"] = "tree", ["models/rock.glb"] = "rock" });
        var bundle = await _fixture.BundleAsync(
            "bundle_patch", new Dictionary<string, string> { ["models/tree.glb"] = "tree two", ["models/new.glb"] = "new" });

        Assert.Equal(0, await Run("build", bundle, "--target", "android", "-o", File_("patch"), "--base", File_("game")));
        Assert.Contains(", 1 new, 1 replaced, 1 removed\n", _out.ToString());
        Assert.Equal(0, await Run("list", File_("patch")));

        Assert.Contains($"base     {game.Hash}", _out.ToString());
        Assert.Contains("removed  1\n", _out.ToString());
        Assert.Contains("  removed  models/rock.glb\n", _out.ToString());
    }

    [Fact]
    public async Task ACheckOfAChangedBlobListsItAndExits1()
    {
        await _fixture.BuildAsync("game", new Dictionary<string, string> { ["models/tree.glb"] = "tree blob" });
        var bytes = await File.ReadAllBytesAsync(File_("game"), TestContext.Current.CancellationToken);
        bytes[bytes.AsSpan().IndexOf("tree blob"u8)] ^= 1;
        await File.WriteAllBytesAsync(File_("game"), bytes, TestContext.Current.CancellationToken);

        Assert.Equal(1, await Run("check", File_("game")));

        Assert.Contains("  changed  models/tree.glb\n", _err.ToString());
        Assert.Contains("pack: 1 assets do not match their hash.", _err.ToString());
    }

    [Fact]
    public async Task BuildRefusesATargetThatTheFolderNameContradicts()
    {
        var bundle = await _fixture.BundleAsync("web", new Dictionary<string, string> { ["models/tree.glb"] = "tree" });

        var code = await Run("build", bundle, "--target", "android", "-o", File_("game"));

        Assert.Equal(2, code);
        Assert.Contains("holds the web bundle, and --target says android", _err.ToString());
        Assert.False(File.Exists(File_("game")));
    }

    [Fact]
    public async Task ArgumentsThatMakeNoSenseExit2()
    {
        Assert.Equal(2, await Run());
        Assert.Equal(0, await Run("--help"));
        Assert.Contains("Usage: orblit-pack", _out.ToString());
        Assert.Equal(2, await Run("spin"));
        Assert.Equal(2, await Run("build", _fixture.At("x"), "-o", File_("x")));
        Assert.Equal(2, await Run("build", _fixture.At("x"), "--target", "nowhere", "-o", File_("x")));
        Assert.Equal(2, await Run("list"));
        Assert.Equal(2, await Run("check", "a", "b"));
    }
}
