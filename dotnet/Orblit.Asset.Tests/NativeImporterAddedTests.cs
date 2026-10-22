using System.Text;

namespace Orblit.Asset.Tests;

[CollectionDefinition("NativeToolSearchPath", DisableParallelization = true)]
public sealed class NativeToolSearchPathLock;

/// <summary>Cases the fixtures do not hold, for the places where the port had to choose how to find or run a program.</summary>
[Collection("NativeToolSearchPath")]
public sealed class NativeImporterAddedTests : NativeToolWork
{
    [ShellFact]
    public void AProgramOnThePathIsFoundWhenNothingElseSaysWhere()
    {
        var found = new NativeTool("sh").Locate();
        Assert.NotNull(found);
        Assert.True(File.Exists(found), found);
    }

    [ShellFact]
    public void ASearchedDirectoryIsLookedInBeforeThePath()
    {
        var tool = Script("on_search_path", "exit 0", "on_search_path");
        var before = NativeTool.SearchPath;
        NativeTool.SearchPath = [Path.GetRelativePath(Environment.CurrentDirectory, Work.FullName)];
        try
        {
            Assert.Equal(tool.Locate(), new NativeTool("on_search_path").Locate());
        }
        finally
        {
            NativeTool.SearchPath = before;
        }
    }

    [Fact]
    public void AnEmptyOverrideIsTheSameAsNone()
    {
        const string Variable = "ORBLIT_ADDED_TEST_EMPTY_OVERRIDE";
        Environment.SetEnvironmentVariable(Variable, "");
        Assert.Null(new NativeTool("no_such_program_either", environmentVariable: Variable).Locate());
    }

    [Fact]
    public void AnOverrideWinsOverTheSearch()
    {
        const string Variable = "ORBLIT_ADDED_TEST_OVERRIDE";
        Environment.SetEnvironmentVariable(Variable, "/somewhere/else");
        Assert.Equal("/somewhere/else", new NativeTool("sh", environmentVariable: Variable).Locate());
    }

    [ShellFact]
    public async Task WhatAFailedProgramSaidComesBackErrorsFirstAndNothingElse()
    {
        var tool = Script("loud.sh", "echo out\necho err >&2\nexit 2", "loud");
        var failure = await Assert.ThrowsAsync<ImportFailureException>(() => tool.RunAsync([], AssetId.Parse("a.png")));
        Assert.Equal("err\nout", failure.Cause);

        var onlyOut = Script("out.sh", "echo out\nexit 2", "out");
        failure = await Assert.ThrowsAsync<ImportFailureException>(() => onlyOut.RunAsync([], AssetId.Parse("a.png")));
        Assert.Equal("out", failure.Cause);
    }

    [Fact]
    public async Task TheDirectoryOfAnImportIsGoneAfterwardsWhetherOrNotTheImportWorked()
    {
        DirectoryInfo? used = null;
        await NativeWork.WithTemporaryDirectoryAsync("added", work =>
        {
            used = work;
            File.WriteAllText(NativeWork.Inside(work, "left.txt"), "x");
            return Task.FromResult(0);
        });
        Assert.False(used!.Exists);

        await Assert.ThrowsAsync<InvalidOperationException>(() => NativeWork.WithTemporaryDirectoryAsync<int>("added", work =>
        {
            used = work;
            throw new InvalidOperationException();
        }));
        Assert.False(used.Exists);
    }

    [ShellFact]
    public async Task ABadExitThatPrintedNothingCarriesNoCause()
    {
        var tool = Script("quiet.sh", "exit 4", "quiet");
        var failure = await Assert.ThrowsAsync<ImportFailureException>(() => tool.RunAsync([], AssetId.Parse("a.png")));
        Assert.Contains("exited 4", failure.Reason);
        Assert.Null(failure.Cause);
    }

    [ShellFact]
    public async Task AWholeNumberedCutoutIsPrintedAsADecimal()
    {
        var importer = new TextureImporter(Fake("writeInto=1:a.ktx2"));
        var settings = importer.ResolveSettings(new ImportSettings(values: new Dictionary<string, object?> { ["cutout"] = 1L }));
        await importer.ImportAsync(RequestFor("a.png", settings));
        AssertInOrder(OnlyRun(), "--cutout", "1.0");
    }

    [ShellFact]
    public void AMissingMatcIsPartOfTheKeyAsMissing()
    {
        var importer = new MaterialImporter(new NativeTool("no_such_matc_anywhere"));
        Assert.Equal("missing", importer.ResolveSettings(new ImportSettings())["matc"]);
    }

    [ShellFact]
    public void AMatcThatCannotSayItsVersionIsPartOfTheKeyAsUnknown()
    {
        var importer = new MaterialImporter(Script("matc", "exit 1", "matc"));
        Assert.Equal("unknown", importer.ResolveSettings(new ImportSettings())["matc"]);
    }

    private async Task<List<string>> TextureRunAsync(Dictionary<string, object?> values)
    {
        var importer = new TextureImporter(Fake("writeInto=1:a.ktx2"));
        await importer.ImportAsync(RequestFor("a.png", importer.ResolveSettings(new ImportSettings(values: values))));
        return Runs()[^1];
    }

    [ShellFact]
    public async Task ASingleChannelSrgbTextureGetsBothFlagsAndTheEncoderIsToldToBeQuiet()
    {
        var run = await TextureRunAsync(new Dictionary<string, object?> { ["singleChannel"] = true, ["colourSpace"] = "srgb" });
        Assert.Contains("--single-channel", run);
        Assert.Contains("--srgb", run);
        Assert.DoesNotContain("--linear", run);
        Assert.Equal("--quiet", run[^1]);
    }

    [Fact]
    public void TheEffortSettingsAcceptTheirEndsAndRefuseTheNextWholeNumber()
    {
        var texture = new TextureImporter();
        foreach (var (key, low, high) in new[] { ("uastc", 0L, 4L), ("zstd", 1L, 22L) })
        {
            Assert.Equal(low, texture.ResolveSettings(new ImportSettings(values: new Dictionary<string, object?> { [key] = low }))[key]);
            Assert.Equal(high, texture.ResolveSettings(new ImportSettings(values: new Dictionary<string, object?> { [key] = high }))[key]);
            Assert.Throws<FormatException>(() => texture.ResolveSettings(new ImportSettings(values: new Dictionary<string, object?> { [key] = low - 1 })));
            Assert.Throws<FormatException>(() => texture.ResolveSettings(new ImportSettings(values: new Dictionary<string, object?> { [key] = high + 1 })));
        }
    }

    [Fact]
    public void ACutoutOfZeroOrOneIsAThresholdAndAnythingBeyondIsNot()
    {
        var texture = new TextureImporter();
        Assert.Equal(0.0, texture.ResolveSettings(new ImportSettings(values: new Dictionary<string, object?> { ["cutout"] = 0.0 }))["cutout"]);
        Assert.Equal(1.0, texture.ResolveSettings(new ImportSettings(values: new Dictionary<string, object?> { ["cutout"] = 1.0 }))["cutout"]);
        Assert.Throws<FormatException>(() => texture.ResolveSettings(new ImportSettings(values: new Dictionary<string, object?> { ["cutout"] = 1.5 })));
        Assert.Throws<FormatException>(() => texture.ResolveSettings(new ImportSettings(values: new Dictionary<string, object?> { ["cutout"] = -0.5 })));
    }

    [Fact]
    public void ASplatKeepsNoBandsWhenToldTo()
    {
        var splat = new SplatImporter();
        Assert.Equal(0L, splat.ResolveSettings(new ImportSettings(values: new Dictionary<string, object?> { ["harmonics"] = 0L }))["harmonics"]);
        Assert.Throws<FormatException>(() => splat.ResolveSettings(new ImportSettings(values: new Dictionary<string, object?> { ["harmonics"] = 4L })));
    }

    [Fact]
    public void TheBackdropOfALargeBakeIsStillHeldTo1024()
    {
        var resolved = new EnvironmentImporter().ResolveSettings(new ImportSettings(values: new Dictionary<string, object?> { ["size"] = 512L }));
        Assert.Equal(1024L, resolved["skyboxSize"]);
    }

    [ShellFact]
    public async Task BothCmgenRunsAskForKtxAndStayQuiet()
    {
        var importer = new EnvironmentImporter(Fake(
            "writeAfter=--deploy:environment_ibl.ktx;writeAfter=--deploy:sh.txt;writeAfter=--extract:environment_skybox.ktx"));
        await importer.ImportAsync(RequestFor("sky.hdr", importer.ResolveSettings(new ImportSettings())));
        var runs = Runs();
        Assert.Equal(2, runs.Count);
        foreach (var run in runs)
        {
            Assert.Contains("--format=ktx", run);
            Assert.Contains("--quiet", run);
        }
    }

    [ShellFact]
    public async Task EachTargetIsCompiledForTheBackendsItDrawsWith()
    {
        var importer = new MaterialImporter(MatcRecording("echo x > \"$out\""));
        var wanted = new (CookTarget Target, string Flags)[]
        {
            (CookTargets.Macos, "-a metal -p all"),
            (CookTargets.Ios, "-a metal -p all"),
            (CookTargets.Windows, "-a vulkan -a opengl -p desktop"),
            (CookTargets.Linux, "-a vulkan -a opengl -p desktop"),
            (CookTargets.Android, "-a opengl -a vulkan -p mobile"),
            (CookTargets.Web, "-a webgpu -p all"),
            (CookTarget.Any, "-a all -p all"),
        };
        foreach (var (target, _) in wanted)
        {
            await importer.ImportAsync(MaterialRequest("void fragment() {}", target));
        }

        var lines = File.ReadAllLines(NativeWork.Inside(Work, "record")).Where(line => line != "--version").ToList();
        Assert.Equal(wanted.Length, lines.Count);
        for (var i = 0; i < wanted.Length; i++)
        {
            Assert.StartsWith($"{wanted[i].Flags} -o ", lines[i]);
        }
    }

    [ShellFact]
    public async Task TheMaterialIsNamedAfterTheFileWithoutItsExtension()
    {
        var importer = new MaterialImporter(MatcRecording("cp \"$in\" \"" + Work.FullName + "/seen.mat\"; echo x > \"$out\""));
        await importer.ImportAsync(MaterialRequest("void fragment() {}", CookTargets.Macos));
        Assert.Contains("name : \"flat\",", File.ReadAllText(NativeWork.Inside(Work, "seen.mat")));
    }

    [Fact]
    public async Task IncludesThatIncludeEachOtherAreEachReadOnce()
    {
        var source = new MemoryAssetSource(new Dictionary<AssetId, byte[]>
        {
            [AssetId.Parse("materials/a.oshaderinc")] = Encoding.UTF8.GetBytes("#include \"b.oshaderinc\""),
            [AssetId.Parse("materials/b.oshaderinc")] = Encoding.UTF8.GetBytes("#include \"a.oshaderinc\""),
        });

        var found = await new MaterialImporter().DependenciesInAsync(
            AssetId.Parse("materials/flat.oshader"),
            Encoding.UTF8.GetBytes("#include \"a.oshaderinc\"\nvoid fragment() {}"),
            new Dictionary<string, object?>(),
            source);

        Assert.Equal(["materials/a.oshaderinc", "materials/b.oshaderinc"], found.Select(id => id.ToString()).Order());
    }

    private NativeTool MatcRecording(string body) => Script(
        "matc",
        $"echo \"$@\" >> \"{Work.FullName}/record\"\n"
        + "[ \"$1\" = \"--version\" ] && { echo 77; exit 0; }\n"
        + "out=\"\"\nin=\"\"\n"
        + "while [ $# -gt 0 ]; do [ \"$1\" = \"-o\" ] && out=\"$2\"; in=\"$1\"; shift; done\n"
        + body,
        "matc");

    private static ImportRequest MaterialRequest(string text, CookTarget target) => new()
    {
        Id = AssetId.Parse("materials/flat.oshader"),
        Bytes = Encoding.UTF8.GetBytes(text),
        Settings = new Dictionary<string, object?>(),
        Target = target,
        Source = new MemoryAssetSource(),
    };

    [ShellFact]
    public async Task AMatcFailureThatNamesNoLineOfTheSourceIsPassedOnAsItWas()
    {
        var importer = new MaterialImporter(Script("matc", "[ \"$1\" = \"--version\" ] && { echo 77; exit 0; }\necho 'out of memory' >&2\nexit 1", "matc"));
        var request = new ImportRequest
        {
            Id = AssetId.Parse("materials/flat.oshader"),
            Bytes = Encoding.UTF8.GetBytes("void fragment() {\n  ALBEDO = vec3(1.0);\n}\n"),
            Settings = new Dictionary<string, object?>(),
            Target = CookTargets.Macos,
            Source = new MemoryAssetSource(),
        };

        var failure = await Assert.ThrowsAsync<ImportFailureException>(() => importer.ImportAsync(request));

        Assert.Contains("matc exited 1", failure.Reason);
        Assert.Contains("out of memory", failure.Cause?.ToString());
    }
}
