using System.Text.Json;

namespace Orblit.Asset.Tests;

// What the Dart tests leave open: every option's refusal, what each verbosity prints, several targets and several
// failures. Run in this process, on writers of their own.
public sealed class CookCliAddedTests : IDisposable
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

    // The options every cook of the project in the work folder needs.
    private string[] Here(params string[] more) =>
        ["--assets", At("assets"), "--out", At("out"), "--cache", At("cache"), .. more];

    private void WriteModel(string name) =>
        _work.WriteAsset(name, JsonSerializer.Serialize(new { images = Array.Empty<object>() }));

    private static CookResult Result(CookStatus status, IReadOnlyList<string>? notes = null, Exception? error = null) =>
        new() { Id = AssetId.Parse("robot.glb"), Status = status, Notes = notes ?? [], Error = error };

    private string Reported(CookStatus status, CookCli.Options options, IReadOnlyList<string>? notes = null)
    {
        CookCli.Report(Result(status, notes, new InvalidOperationException("nope")), options, _out, _err);
        return $"{_out}|{_err}";
    }

    [Fact]
    public async Task AnOutFolderThatIsAFileEndsTheCookWithOneAndNotACrash()
    {
        WriteModel("a.gltf");
        File.WriteAllText(At("out"), "in the way");

        var code = await Cook(Here("--target", "macos"));

        Assert.Equal(1, code);
        Assert.StartsWith("cook: ", _err.ToString());
    }

    // Asking for help

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    public async Task HelpNeedsNoTargetAndPrintsTheUsageAloneOnStdout(string flag)
    {
        var code = await Cook(flag);

        Assert.Equal(0, code);
        Assert.Equal("", _err.ToString());
        Assert.StartsWith("Usage: orblit-cook --target <name> [options]\n", _out.ToString());
        Assert.Contains("One of: macos, windows, linux, ios, android, web\n", _out.ToString());
        Assert.EndsWith("how an iOS build ends up carrying ASTC and no BC.\n", _out.ToString());
    }

    [Fact]
    public async Task HelpDoesNotCookAnything()
    {
        WriteModel("a.gltf");

        await Cook(Here("--target", "macos", "--help"));

        Assert.False(Directory.Exists(At("out")));
    }

    [Fact]
    public async Task HelpStillRefusesOptionsThatAskForOppositeThings()
    {
        var code = await Cook("--help", "--quiet", "--verbose");

        Assert.Equal(2, code);
        Assert.Equal("", _out.ToString());
        Assert.Contains("--quiet and --verbose ask for opposite things.", _err.ToString());
    }

    // Refusing arguments

    [Theory]
    [InlineData("--target")]
    [InlineData("--assets")]
    [InlineData("--out")]
    [InlineData("--cache")]
    [InlineData("--cache-limit")]
    [InlineData("--jobs")]
    public async Task AnOptionThatTakesAValueSaysSoWhenItIsLast(string option)
    {
        var code = await Cook(option);

        Assert.Equal(2, code);
        Assert.Equal($"cook: {option} needs a value\n", _err.ToString());
        Assert.Equal("", _out.ToString());
    }

    [Fact]
    public async Task AnOptionThatTakesAValueTakesTheNextArgumentWhateverItLooksLike()
    {
        var code = await Cook("--target", "--quiet");

        Assert.Equal(2, code);
        Assert.Contains("\"--quiet\" is not a target. The targets are macos, windows, linux, ios, android, web.", _err.ToString());
    }

    [Fact]
    public async Task NoTargetNamesTheTargetsToChooseFrom()
    {
        var code = await Cook("--quiet");

        Assert.Equal(2, code);
        Assert.Equal("cook: nothing to cook for. Pass --target, one of macos, windows, linux, ios, android, web.\n", _err.ToString());
    }

    [Fact]
    public async Task AnUnknownOptionIsNamedAndFollowedByTheUsageOnStderr()
    {
        var code = await Cook("--target", "macos", "--fast");

        Assert.Equal(2, code);
        Assert.StartsWith("cook: unknown option \"--fast\"\nUsage: orblit-cook", _err.ToString());
        Assert.EndsWith("no BC.\n", _err.ToString());
        Assert.Equal("", _out.ToString());
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("-1")]
    [InlineData("")]
    [InlineData("1.5")]
    public async Task ACacheLimitThatIsNotAWholeNumberOfMegabytesIsRefused(string value)
    {
        var code = await Cook("--target", "macos", "--cache-limit", value);

        Assert.Equal(2, code);
        Assert.Equal($"cook: --cache-limit is megabytes, so \"{value}\" is not one. Pass 0 for no limit.\n", _err.ToString());
    }

    [Theory]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("2048")]
    public async Task ACacheLimitOfZeroOrMoreIsAccepted(string value)
    {
        WriteModel("a.gltf");

        var code = await Cook(Here("--target", "macos", "--cache-limit", value));

        Assert.True(code == 0, _err.ToString());
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("-3")]
    [InlineData("")]
    public async Task AJobCountBelowOneIsRefused(string value)
    {
        var code = await Cook("--target", "macos", "--jobs", value);

        Assert.Equal(2, code);
        Assert.Equal($"cook: --jobs is how many at once, so it is at least 1, not \"{value}\".\n", _err.ToString());
    }

    [Theory]
    [InlineData("1")]
    [InlineData("99999999999")]
    [InlineData("4294967296")]
    public async Task AnyJobCountOfOneOrMoreIsAccepted(string value)
    {
        WriteModel("a.gltf");

        var code = await Cook(Here("--target", "macos", "--jobs", value));

        Assert.True(code == 0, _err.ToString());
    }

    [Fact]
    public async Task ATargetIsKnownByAnyCase()
    {
        WriteModel("a.gltf");

        var code = await Cook(Here("--target", "MacOS"));

        Assert.True(code == 0, _err.ToString());
        Assert.True(File.Exists(At("out/macos/a.gltf.gltf")));
    }

    [Fact]
    public async Task AFolderThatIsNotThereIsRefusedWithTheFolderNamed()
    {
        var code = await Cook("--target", "macos", "--assets", At("nowhere"), "--out", At("out"), "--cache", At("cache"));

        Assert.Equal(2, code);
        Assert.StartsWith("cook: ", _err.ToString());
        Assert.Contains(At("nowhere"), _err.ToString());
        Assert.Equal("", _out.ToString());
    }

    // The defaults the usage text promises

    [Fact]
    public void TheOptionsStartAtTheDefaultsTheUsageTextPromises()
    {
        var options = new CookCli.Options();

        Assert.Equal("assets", options.AssetsPath);
        Assert.Equal(Path.Combine("build", "assets"), options.OutPath);
        Assert.Null(options.CachePath);
        Assert.Equal(4, options.Jobs);
        Assert.Equal(2048L * 1024 * 1024, options.CacheLimitBytes);
        Assert.Empty(options.Targets);
        Assert.False(options.Quiet);
        Assert.False(options.Verbose);
        Assert.False(options.Help);
    }

    [Fact]
    public void ParseReadsEachValueIntoItsOwnOption()
    {
        var options = CookCli.Parse(
            ["--target", "macos", "--assets", "a", "--out", "o", "--cache", "c", "--cache-limit", "7", "--jobs", "3"],
            _err)!;

        Assert.Equal("a", options.AssetsPath);
        Assert.Equal("o", options.OutPath);
        Assert.Equal("c", options.CachePath);
        Assert.Equal(7, options.CacheLimitMegabytes);
        Assert.Equal(3, options.Jobs);
        Assert.Equal("", _err.ToString());
    }

    [Fact]
    public void AJobCountPastWhatAnIntHoldsStopsAtTheIntLimit() =>
        Assert.Equal(int.MaxValue, CookCli.Parse(["--target", "macos", "--jobs", "99999999999"], _err)!.Jobs);

    [Fact]
    public void TheProjectIsMadeFromTheOptionsAndTheTarget()
    {
        var options = new CookCli.Options
        {
            AssetsPath = "a",
            OutPath = "o",
            CachePath = "c",
            CacheLimitMegabytes = 3,
            Jobs = 2,
        };

        var project = CookCli.ProjectFor(options, CookTargets.Ios);

        Assert.Equal("a", project.AssetsPath);
        Assert.Equal("o", project.OutPath);
        Assert.Equal("c", project.CachePath);
        Assert.Same(CookTargets.Ios, project.Target);
        Assert.Equal(3L * 1024 * 1024, project.LimitBytes);
        Assert.Equal(2, project.Concurrency);
    }

    [Fact]
    public void ZeroMegabytesLeavesTheCacheWithoutALimit() =>
        Assert.Null(CookCli.ProjectFor(new CookCli.Options { CacheLimitMegabytes = 0 }, CookTargets.Web).LimitBytes);

    [Theory]
    [InlineData(0, null)]
    [InlineData(1, 1048576L)]
    [InlineData(5, 5242880L)]
    public void AMegabyteCountBecomesBytesAndZeroBecomesNoLimit(long megabytes, long? bytes) =>
        Assert.Equal(bytes, new CookCli.Options { CacheLimitMegabytes = megabytes }.CacheLimitBytes);

    // What is printed

    [Fact]
    public async Task TheSummaryNamesTheTargetAndWhereItsBundleWent()
    {
        WriteModel("a.gltf");

        await Cook(Here("--target", "macos"));

        var lines = _out.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("  cooked  a.gltf", lines[0]);
        Assert.StartsWith("macos: ", lines[1]);
        Assert.EndsWith($" -> {At("out/macos")}", lines[1]);
        Assert.Equal(2, lines.Length);
    }

    [Fact]
    public async Task QuietPrintsNothingWhenEverythingCooked()
    {
        WriteModel("a.gltf");

        var code = await Cook(Here("--target", "macos", "--quiet"));

        Assert.Equal(0, code);
        Assert.Equal("", _out.ToString());
        Assert.Equal("", _err.ToString());
    }

    [Fact]
    public async Task QuietStillSaysWhatWentWrong()
    {
        _work.WriteAsset("broken.gltf", "not json at all");

        var code = await Cook(Here("--target", "macos", "--quiet"));

        Assert.Equal(1, code);
        Assert.Equal("", _out.ToString());
        Assert.Contains("  failed  broken.gltf: ", _err.ToString());
    }

    [Fact]
    public async Task OnlyVerboseSaysWhatWasCachedOrSkipped()
    {
        WriteModel("a.gltf");
        _work.WriteAsset("notes.txt", "hello");
        await Cook(Here("--target", "macos"));
        _out.GetStringBuilder().Clear();

        await Cook(Here("--target", "macos"));
        var plain = _out.ToString();
        _out.GetStringBuilder().Clear();
        await Cook(Here("--target", "macos", "--verbose"));

        Assert.DoesNotContain("  cached  a.gltf", plain);
        Assert.DoesNotContain("  skipped notes.txt", plain);
        Assert.Contains("  cached  a.gltf\n", _out.ToString());
        Assert.Contains("  skipped notes.txt\n", _out.ToString());
    }

    [Fact]
    public async Task EachTargetGetsItsOwnFolderAndItsOwnSummaryInTheOrderGiven()
    {
        WriteModel("a.gltf");

        var code = await Cook(Here("--target", "web", "--target", "ios"));

        Assert.Equal(0, code);
        Assert.True(File.Exists(At("out/web/a.gltf.gltf")));
        Assert.True(File.Exists(At("out/ios/a.gltf.gltf")));
        Assert.True(_out.ToString().IndexOf("web: ", StringComparison.Ordinal) < _out.ToString().IndexOf("ios: ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task FailuresAreCountedAcrossTargetsAndTheClosingLineIsPlural()
    {
        _work.WriteAsset("broken.gltf", "not json at all");

        var code = await Cook(Here("--target", "web", "--target", "ios"));

        Assert.Equal(1, code);
        Assert.EndsWith("\n\ncook: 2 assets failed. The bundle was written without them.\n", _err.ToString());
    }

    [Fact]
    public async Task AFailureNamesTheAssetAndTheClosingLineIsSingular()
    {
        _work.WriteAsset("broken.gltf", "not json at all");

        await Cook(Here("--target", "macos"));

        Assert.EndsWith("\ncook: 1 asset failed. The bundle was written without it.\n", _err.ToString());
    }

    // One result at a time

    [Fact]
    public void ACookedResultPrintsItsNotesIndentedBeneathIt()
    {
        var text = Reported(CookStatus.Cooked, new CookCli.Options(), ["one", "two"]);

        Assert.Equal("  cooked  robot.glb\n          one\n          two\n|", text);
    }

    [Fact]
    public void ACookedResultPrintsNothingWhenQuiet()
    {
        Assert.Equal("|", Reported(CookStatus.Cooked, new CookCli.Options { Quiet = true }, ["one"]));
    }

    [Fact]
    public void AFailedResultPrintsItsMessageOnStderrWhateverTheVerbosity()
    {
        Assert.Equal("|  failed  robot.glb: nope\n", Reported(CookStatus.Failed, new CookCli.Options { Quiet = true }));
        _err.GetStringBuilder().Clear();
        Assert.Equal("|  failed  robot.glb: nope\n", Reported(CookStatus.Failed, new CookCli.Options { Verbose = true }));
    }

    [Fact]
    public void CachedAndSkippedResultsPrintOnlyWhenVerbose()
    {
        var plain = new CookCli.Options();

        Assert.Equal("|", Reported(CookStatus.Cached, plain));
        Assert.Equal("|", Reported(CookStatus.Skipped, plain));
        Assert.Equal("  cached  robot.glb\n|", Reported(CookStatus.Cached, new CookCli.Options { Verbose = true }));
    }

    [Fact]
    public void ASkippedResultLinesUpWithTheRest() =>
        Assert.Equal("  skipped robot.glb\n|", Reported(CookStatus.Skipped, new CookCli.Options { Verbose = true }));
}
