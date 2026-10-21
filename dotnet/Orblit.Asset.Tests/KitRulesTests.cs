namespace Orblit.Asset.Tests;

public class KitRulesTests
{
    private static KitManifest Manifest(bool containsScripts = false, IReadOnlyList<AssetId>? previews = null) =>
        new(
            "crate-pack",
            "brass-lamp",
            "Crate pack",
            "Twelve crates.",
            "1.0.0",
            new KitLicence("MIT", "https://example.com/l"),
            new KitEngine("0.30.0", "0.40.0"),
            containsScripts,
            previews: previews);

    private static Dictionary<AssetId, long> Files(params string[] names) =>
        names.ToDictionary(AssetId.Parse, _ => 10L);

    private static List<string> Problems(string[] names, KitManifest? kit = null, long limit = KitRules.FileLimit) =>
        KitRules.Problems(kit ?? Manifest(), Files(names), limit);

    [Fact]
    public void AGoodKitHasNoProblems() => Assert.Empty(Problems(["models/crate.glb", "textures/wall.png"]));

    [Fact]
    public void AKitWithNoFilesIsAProblem() => Assert.Equal(["The kit holds no files."], Problems([]));

    [Fact]
    public void AHiddenFileOrFolderIsAProblem()
    {
        foreach (var name in new[] { ".env", ".git/hooks/pre-commit", "a/.b/c.png" })
        {
            Assert.Single(Problems([name]));
        }
    }

    [Fact]
    public void ANameWindowsReadsAsSomethingElseIsAProblem()
    {
        foreach (var name in new[] { "con", "models/NUL.txt", "a/aux.png", "lpt1.png", "a.png.", "models /a.png", "a.png " })
        {
            Assert.Single(Problems([name]));
        }

        Assert.Empty(Problems(["console.png", "auxiliary/a.png"]));
    }

    [Fact]
    public void ACharacterOutsideLettersDigitsAndAFewMarksIsAProblem()
    {
        foreach (var name in new[]
        {
            "a\u0001b.png",
            "caf\u00e9.png",
            "a<b.png",
            "a|b.png",
            "a?.png",
            "a\u202eb.png",
            "a\u200bb.png",
            "-a.png",
            "a~b.png",
        })
        {
            Assert.Single(Problems([name]));
        }

        Assert.Empty(Problems(["Wall (2).png", "a_b-c+d.png", "1.png", "a/B.v2.glb"]));
    }

    [Fact]
    public void AMessageShowsNoRawEscapeCharacter() =>
        Assert.DoesNotContain('\u001b', string.Join('\n', Problems(["a\u001b[31m.png"])));

    [Fact]
    public void MoreFilesThanTheLimitIsTheOneProblemNamed()
    {
        var many = Enumerable.Range(0, KitRules.CountLimit + 1).ToDictionary(i => AssetId.Parse($"f/{i}.bin"), _ => 1L);

        Assert.Equal(
            [$"The kit holds {KitRules.CountLimit + 1} files, over the limit of {KitRules.CountLimit}."],
            KitRules.Problems(Manifest(), many));
    }

    [Fact]
    public void MoreBytesThanTheLimitInAllIsAProblem()
    {
        var half = (KitRules.TotalLimit / 2) + 1;
        var big = new Dictionary<AssetId, long> { [AssetId.Parse("a.bin")] = half, [AssetId.Parse("b.bin")] = half };

        Assert.Equal(
            [$"The kit holds {half * 2} bytes in all, over the limit of {KitRules.TotalLimit}."],
            KitRules.Problems(Manifest(), big, KitRules.TotalLimit));
    }

    [Fact]
    public void ANameOrAPathThatIsTooLongIsAProblem()
    {
        Assert.Single(Problems([$"{new string('a', 97)}.png"]));
        Assert.Empty(Problems([$"{new string('a', 96)}.png"]));

        var problem = Assert.Single(Problems([string.Join('/', new string('b', 90), new string('c', 90), new string('d', 90), "e.png")]));
        Assert.Contains("longer than 240", problem, StringComparison.Ordinal);
    }

    [Fact]
    public void ANameThatIsAFileAndAFolderIsAProblem()
    {
        Assert.Single(Problems(["a", "a/b.png"]));
        Assert.Single(Problems(["Models", "models/b.png"]));
        Assert.Empty(Problems(["a/b.png", "a/c.png"]));
    }

    [Fact]
    public void AFileOverTheLimitIsAProblem()
    {
        Assert.Single(KitRules.Problems(Manifest(), new Dictionary<AssetId, long> { [AssetId.Parse("a.bin")] = 101 }, 100));
        Assert.Empty(KitRules.Problems(Manifest(), new Dictionary<AssetId, long> { [AssetId.Parse("a.bin")] = 100 }, 100));
    }

    [Fact]
    public void TwoNamesThatDifferOnlyInCaseAreAProblem() => Assert.Single(Problems(["a/Wall.png", "a/wall.png"]));

    [Fact]
    public void APreviewThatIsNotInTheKitIsAProblem()
    {
        var kit = Manifest(previews: [AssetId.Parse("previews/a.png")]);

        Assert.Single(Problems(["models/a.glb"], kit));
        Assert.Empty(Problems(["previews/a.png"], kit));
    }

    [Fact]
    public void TheScriptsFlagMustMatchTheFiles()
    {
        Assert.Equal(["The manifest says the kit holds no scripts and it holds some."], Problems(["scripts/run.js"]));
        Assert.Equal(
            ["The manifest says the kit holds scripts and it holds none."],
            Problems(["a.png"], Manifest(containsScripts: true)));
        Assert.Empty(Problems(["scripts/run.js"], Manifest(containsScripts: true)));
    }

    [Fact]
    public void CarriesScriptsCountsWhatAPersonCouldRunOrCompile()
    {
        foreach (var name in new[]
        {
            "a.js", "a.DART", "x/y.sh", "b.ps1", "c.py", "d.ts", "e.EXE", "Makefile", "tools/pubspec.yaml", "gradlew",
        })
        {
            Assert.True(KitRules.CarriesScripts([AssetId.Parse(name)]), name);
        }

        Assert.False(KitRules.CarriesScripts(Files("a.glb", "b.png", "c.json").Keys));
    }

    [Fact]
    public void PlaceProblemRefusesAFolderThatWritesWhereNobodyLooks()
    {
        var ids = Files("models/crate.glb").Keys;

        Assert.Null(KitRules.PlaceProblem("", ids));
        Assert.Null(KitRules.PlaceProblem("assets/kits/crate-pack", ids));
        foreach (var folder in new[] { ".git", "a/.b", "a//b", "a/", "/a", "con", "a b." })
        {
            Assert.NotNull(KitRules.PlaceProblem(folder, ids));
        }

        var tooLong = string.Join('/', new string('a', 80), new string('b', 80), new string('c', 70));
        Assert.Contains("limit", KitRules.PlaceProblem(tooLong, ids), StringComparison.Ordinal);
    }

    [Fact]
    public void PlaceProblemKeepsAKitOffTheKitsLockOfTheProject()
    {
        foreach (var name in new[] { "kits.lock", "Kits.Lock" })
        {
            var ids = Files(name).Keys;

            Assert.Contains("kits.lock", KitRules.PlaceProblem("", ids), StringComparison.Ordinal);
            Assert.Null(KitRules.PlaceProblem("assets", ids));
        }
    }

    [Fact]
    public void FitsCountsBothEndsAndGuessesWhenItCannotRead()
    {
        var range = new KitEngine("0.30.0", "0.40.0");

        Assert.True(range.Fits("0.30.0"));
        Assert.True(range.Fits("0.40.0"));
        Assert.True(range.Fits("0.35.2"));
        Assert.False(range.Fits("0.29.9"));
        Assert.False(range.Fits("0.40.1"));
        Assert.False(range.Fits("1.0.0"));
        Assert.True(range.Fits("unknown"));
    }
}
