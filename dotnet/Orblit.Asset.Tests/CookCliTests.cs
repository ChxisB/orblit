using System.Text.Json;

namespace Orblit.Asset.Tests;

// Ported from the 'the command' group in test/cook_project_test.dart. Dart starts a process for each one. These run
// the command in this process on writers of their own, which reads the same exit code and the same text.
public sealed class CookCliTests : IDisposable
{
    private readonly CookProjectWork _work = new();
    private readonly StringWriter _out = new();
    private readonly StringWriter _err = new();

    public void Dispose()
    {
        _out.Dispose();
        _err.Dispose();
        _work.Dispose();
    }

    private string At(string name) => _work.At(name);

    private Task<int> Cook(params string[] arguments) => CookCli.RunAsync(arguments, _out, _err);

    [Fact]
    public async Task RefusesToRunWithoutATargetAndSaysWhatTheTargetsAre()
    {
        var code = await Cook("--assets", At("assets"));

        Assert.Equal(2, code);
        Assert.Contains("macos", _err.ToString());
    }

    [Fact]
    public async Task RefusesATargetNobodyHas()
    {
        var code = await Cook("--target", "playstation");

        Assert.Equal(2, code);
        Assert.Contains("playstation", _err.ToString());
    }

    [Fact]
    public async Task RefusesTwoOptionsThatAskForOppositeThings() =>
        Assert.Equal(2, await Cook("--target", "macos", "--quiet", "--verbose"));

    [Fact]
    public async Task RefusesAnOptionItDoesNotHave()
    {
        var code = await Cook("--target", "macos", "--fast");

        Assert.Equal(2, code);
        Assert.Contains("--fast", _err.ToString());
    }

    [Fact]
    public async Task ExplainsItself()
    {
        var code = await Cook("--help");

        Assert.Equal(0, code);
        Assert.Contains("--target", _out.ToString());
    }

    [Fact]
    public async Task CooksAProjectAndLeaves0BehindIt()
    {
        _work.WriteAsset("a.gltf", JsonSerializer.Serialize(new { images = Array.Empty<object>() }));

        var code = await Cook(
            "--target", "macos", "--assets", At("assets"), "--out", At("out"), "--cache", At("cache"));

        Assert.True(code == 0, _err.ToString());
        Assert.Contains("macos:", _out.ToString());
        Assert.True(File.Exists(At("out/macos/a.gltf.gltf")));
    }

    [Fact]
    public async Task Leaves1BehindItWhenAnAssetFailed()
    {
        _work.WriteAsset("broken.gltf", "not json at all");

        var code = await Cook(
            "--target", "macos", "--assets", At("assets"), "--out", At("out"), "--cache", At("cache"));

        Assert.Equal(1, code);
        Assert.Contains("broken.gltf", _err.ToString());
        Assert.Contains("1 asset failed", _err.ToString());
    }
}
