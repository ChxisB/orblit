using System.Text;

namespace Orblit.Asset.Tests;

// An importer that writes one file per texture family the target asked for, so a test can see which families a
// bundle ends up carrying without building the real encoder.
internal sealed class FamilyImporter : Importer
{
    public override string Name => "family";

    public override int Version => 1;

    public override IReadOnlySet<string> Extensions { get; } = new HashSet<string> { "png" };

    public override IReadOnlyDictionary<string, object?> ResolveSettings(ImportSettings settings) =>
        new Dictionary<string, object?>();

    public override Task<ImportResult> ImportAsync(ImportRequest request)
    {
        var outputs = new Dictionary<string, byte[]>();
        foreach (var family in request.Target.TextureFamilies())
        {
            // Derived from the source, the way a real encoder's output is, so that a changed asset cooks to
            // changed bytes.
            outputs[$"{family}.ktx2"] = [.. Encoding.UTF8.GetBytes($"{family} of {request.Id}\n"), .. request.Bytes];
        }

        return Task.FromResult(new ImportResult { Outputs = outputs });
    }
}

// An importer for png that fails every asset it is given. It throws at the call, and the cook has to turn that
// into a failed result.
internal sealed class BrokenImporter : Importer
{
    public override string Name => "broken";

    public override int Version => 1;

    public override IReadOnlySet<string> Extensions { get; } = new HashSet<string> { "png" };

    public override IReadOnlyDictionary<string, object?> ResolveSettings(ImportSettings settings) =>
        new Dictionary<string, object?>();

    public override Task<ImportResult> ImportAsync(ImportRequest request) =>
        throw new ImportFailureException(request.Id, "this importer never works");
}

// A project in a temporary folder: assets under assets, a bundle under out and a cache under cache.
internal sealed class CookProjectWork : IDisposable
{
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("orblit_cook_project_test_");

    public string Root => _directory.FullName;

    public void Dispose() => _directory.Delete(recursive: true);

    public string At(string name) => Path.Combine([Root, .. name.Split('/')]);

    public void WriteAsset(string name, string contents)
    {
        var file = At($"assets/{name}");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, contents);
    }

    public CookProject ProjectFor(CookTarget target, ImporterRegistry? importers = null) => new(
        assetsPath: At("assets"),
        outPath: At("out"),
        target: target,
        cachePath: At("cache"),
        importers: importers ?? new ImporterRegistry([new FamilyImporter()]));

    public static ImporterRegistry Broken() => new([new BrokenImporter()]);
}
