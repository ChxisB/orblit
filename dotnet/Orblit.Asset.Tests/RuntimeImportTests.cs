using System.Text;
using System.Text.Json;

namespace Orblit.Asset.Tests;

public sealed class RuntimeImportTests
{
    private readonly MemoryCookCache _cache = new();
    private readonly CountingImporter _importer = new();
    private readonly RuntimeImport _runtime;

    public RuntimeImportTests() => _runtime = RuntimeFor(_importer);

    private static AssetId Id(string text) => AssetId.Parse(text);

    private static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(text);

    private RuntimeImport RuntimeFor(Importer importer) =>
        new(_cache, CookTargets.Macos, new ImporterRegistry([importer]));

    private static async Task<string> TextOf(RuntimeImport runtime, CookedAsset asset) =>
        Encoding.UTF8.GetString((await runtime.ReadOnlyAsync(asset))!);

    // Naming a file a user chose

    [Fact]
    public void KeepsThePartTheUserWouldRecognise()
    {
        Assert.Equal(Id("My Model.glb"), RuntimeImport.NameFor("/var/mobile/Containers/My Model.glb"));
        Assert.Equal(Id("robot.glb"), RuntimeImport.NameFor(@"C:\Users\chris\Downloads\robot.glb"));
        Assert.Equal(Id("robot.glb"), RuntimeImport.NameFor("robot.glb"));
    }

    [Fact]
    public void DropsWhatAUrlCarriesAfterTheName() =>
        Assert.Equal(Id("robot.glb"), RuntimeImport.NameFor("https://example.com/a/robot.glb?v=2#frag"));

    [Fact]
    public void AColonCannotSurviveBecauseAnIdIsNotAUrl() =>
        Assert.Equal(Id("file_robot.glb"), RuntimeImport.NameFor("file:robot.glb"));

    [Fact]
    public void WalksBackPastATrailingSlashRatherThanGivingUp() =>
        Assert.Equal(Id("robot.glb"), RuntimeImport.NameFor("models/robot.glb/"));

    [Fact]
    public void GivesNothingWhenThereIsNoNameLeftToTake()
    {
        Assert.Null(RuntimeImport.NameFor("/"));
        Assert.Null(RuntimeImport.NameFor("   "));
        Assert.Null(RuntimeImport.NameFor("../.."));
    }

    // Importing a file

    [Fact]
    public async Task ProducesWhatTheImporterMade()
    {
        var result = await _runtime.ImportAsync(Id("robot.glb"), Bytes("body"));

        Assert.Equal(CookStatus.Cooked, result.Status);
        Assert.Equal("body tint=null", await TextOf(_runtime, result.Asset!));
    }

    [Fact]
    public async Task TheSameFileTwiceCostsOneImport()
    {
        await _runtime.ImportAsync(Id("robot.glb"), Bytes("body"));
        var again = await _runtime.ImportAsync(Id("robot.glb"), Bytes("body"));

        Assert.Equal(1, _importer.Runs);
        Assert.Equal(CookStatus.Cached, again.Status);
    }

    [Fact]
    public async Task ADifferentFileIsADifferentImportUnderTheSameName()
    {
        await _runtime.ImportAsync(Id("robot.glb"), Bytes("body"));
        var other = await _runtime.ImportAsync(Id("robot.glb"), Bytes("other"));

        Assert.Equal(2, _importer.Runs);
        Assert.Equal(CookStatus.Cooked, other.Status);
    }

    [Fact]
    public async Task SettingsGivenAtTheCallReachTheImporterAndChangeTheKey()
    {
        var plain = await _runtime.ImportAsync(Id("robot.glb"), Bytes("body"));
        var tinted = await _runtime.ImportAsync(
            Id("robot.glb"),
            Bytes("body"),
            new ImportSettings(values: new Dictionary<string, object?> { ["tint"] = "red" }));

        Assert.Equal(2, _importer.Runs);
        Assert.NotEqual(plain.Key, tinted.Key);
        Assert.Equal("body tint=red", await TextOf(_runtime, tinted.Asset!));
    }

    [Fact]
    public async Task AFileNothingClaimsIsAResultNotAnException()
    {
        var result = await _runtime.ImportAsync(Id("notes.txt"), Bytes("hello"));

        Assert.Equal(CookStatus.Skipped, result.Status);
        Assert.Null(result.Asset);
    }

    [Fact]
    public async Task AnImporterThatFailsIsReportedRatherThanThrown()
    {
        var failing = RuntimeFor(new AlwaysFailsImporter());

        var result = await failing.ImportAsync(Id("robot.glb"), Bytes("x"));

        Assert.Equal(CookStatus.Failed, result.Status);
        Assert.Contains("never works", $"{result.Error}");
    }

    [Fact]
    public void SaysUpFrontWhetherItCanOpenAFile()
    {
        Assert.True(_runtime.Handles(Id("robot.glb")));
        Assert.False(_runtime.Handles(Id("notes.txt")));
    }

    // A file that is not self-contained

    private static byte[] Document() =>
        Bytes(JsonSerializer.Serialize(new { buffers = new[] { new { uri = "robot.bin" } } }));

    private RuntimeImport Gltf() => RuntimeFor(new GltfImporter());

    private static MemoryAssetSource Beside(string vertices) =>
        new(new Dictionary<AssetId, byte[]> { [Id("robot.bin")] = Bytes(vertices) });

    [Fact]
    public async Task FailsWhenTheFilesItNamesWereNotHandedOverToo()
    {
        var result = await Gltf().ImportAsync(Id("robot.gltf"), Document());

        Assert.Equal(CookStatus.Failed, result.Status);
        Assert.Contains("robot.bin", $"{result.Error}");
    }

    [Fact]
    public async Task ImportsWhenTheyWere()
    {
        var result = await Gltf().ImportAsync(Id("robot.gltf"), Document(), alongside: Beside("vertices"));

        Assert.Equal(CookStatus.Cooked, result.Status);
        Assert.Contains(Id("robot.bin"), result.Dependencies);
    }

    [Fact]
    public async Task ChangingOnlyAFileItPointsAtReImportsIt()
    {
        var gltf = Gltf();
        await gltf.ImportAsync(Id("robot.gltf"), Document(), alongside: Beside("vertices"));

        var again = await gltf.ImportAsync(Id("robot.gltf"), Document(), alongside: Beside("different vertices"));

        // The document did not change, but what it points at did.
        Assert.Equal(CookStatus.Cooked, again.Status);
    }

    // Reading an import back

    [Fact]
    public async Task ReadsByTheNameTheImporterGaveIt()
    {
        var result = await _runtime.ImportAsync(Id("robot.glb"), Bytes("body"));

        Assert.NotNull(await _runtime.ReadAsync(result.Asset!, "out"));
        Assert.Null(await _runtime.ReadAsync(result.Asset!, "nope"));
    }

    [Fact]
    public async Task ReadOnlyRefusesToGuessWhenThereIsMoreThanOne()
    {
        var many = RuntimeFor(new TwoOutputsImporter());

        var result = await many.ImportAsync(Id("robot.glb"), Bytes("x"));

        Assert.Null(await many.ReadOnlyAsync(result.Asset!));
        Assert.NotNull(await many.ReadAsync(result.Asset!, "a"));
    }

    [Fact]
    public void BringsTheSameImportersABuildMachineHas()
    {
        var registry = RuntimeImport.DefaultImporters();

        Assert.NotNull(registry.ByName("gltf"));
        Assert.NotNull(registry.ByName("scene"));
    }
}

// Counts its runs, so a test can tell a real import from a cache hit.
internal sealed class CountingImporter : Importer
{
    private int _runs;

    public int Runs => _runs;

    public override string Name => "counting";

    public override int Version => 1;

    public override IReadOnlySet<string> Extensions { get; } = new HashSet<string> { "glb", "png" };

    public override IReadOnlyDictionary<string, object?> ResolveSettings(ImportSettings settings) =>
        new Dictionary<string, object?> { ["tint"] = settings.Values.GetValueOrDefault("tint") };

    public override Task<ImportResult> ImportAsync(ImportRequest request)
    {
        Interlocked.Increment(ref _runs);
        var tint = request.Settings["tint"];
        return Task.FromResult(new ImportResult
        {
            Outputs = new Dictionary<string, byte[]> { ["out"] = [.. request.Bytes, .. Encoding.UTF8.GetBytes($" tint={TextRules.Print(tint)}")] },
        });
    }
}

internal sealed class AlwaysFailsImporter : Importer
{
    public override string Name => "fails";

    public override int Version => 1;

    public override IReadOnlySet<string> Extensions { get; } = new HashSet<string> { "glb" };

    public override IReadOnlyDictionary<string, object?> ResolveSettings(ImportSettings settings) =>
        new Dictionary<string, object?>();

    public override Task<ImportResult> ImportAsync(ImportRequest request) =>
        throw new ImportFailureException(request.Id, "this importer never works");
}

internal sealed class TwoOutputsImporter : Importer
{
    public override string Name => "two";

    public override int Version => 1;

    public override IReadOnlySet<string> Extensions { get; } = new HashSet<string> { "glb" };

    public override IReadOnlyDictionary<string, object?> ResolveSettings(ImportSettings settings) =>
        new Dictionary<string, object?>();

    public override Task<ImportResult> ImportAsync(ImportRequest request) =>
        Task.FromResult(new ImportResult
        {
            Outputs = new Dictionary<string, byte[]> { ["a"] = Encoding.UTF8.GetBytes("a"), ["b"] = Encoding.UTF8.GetBytes("b") },
        });
}
