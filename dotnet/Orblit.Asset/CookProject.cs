namespace Orblit.Asset;

/// <summary>
/// Cooking a whole project for one target, and writing the result somewhere a build can pick it up.
/// This is the part a build hook calls. The cook command is argument parsing over it and nothing else, so that a
/// project driving the cook from its own script gets exactly what the command gets, including the exit code's
/// meaning, which is the thing a build actually reads.
/// </summary>
public sealed class CookProject
{
    private const long DefaultLimitBytes = 2L * 1024 * 1024 * 1024;

    // Links are skipped. .NET also skips files with the hidden or system attribute unless told not to, and Dart
    // lists them, so this lists them too. The cook decides what a leading dot hides.
    private static readonly EnumerationOptions Walk = new()
    {
        RecurseSubdirectories = true,
        AttributesToSkip = FileAttributes.ReparsePoint,
        IgnoreInaccessible = false,
    };

    // F2: seven parameters, all named at each call, as in the Dart constructor.
    public CookProject(
        string assetsPath,
        string outPath,
        CookTarget target,
        string? cachePath = null,
        ImporterRegistry? importers = null,
        long? limitBytes = DefaultLimitBytes,
        int concurrency = 4)
    {
        AssetsPath = assetsPath;
        OutPath = outPath;
        Target = target;
        CachePath = cachePath ?? DefaultCachePath(Environment.GetEnvironmentVariable);
        Importers = importers ?? DefaultImporters();
        LimitBytes = limitBytes;
        Concurrency = concurrency;
    }

    /// <summary>The folder assets are read from. Ids are relative to it, so an asset's name does not change when the project moves.</summary>
    public string AssetsPath { get; }

    /// <summary>
    /// Where the bundle is written. Each target gets its own folder under it, because a build copies one folder
    /// and must not have to filter it.
    /// </summary>
    public string OutPath { get; }

    public CookTarget Target { get; }

    /// <summary>
    /// Where cooked bytes are kept between runs. Outside the project by default: a cache in a repository is a
    /// cache somebody commits.
    /// </summary>
    public string CachePath { get; }

    public ImporterRegistry Importers { get; }

    /// <summary>How large the cache may grow before it starts evicting.</summary>
    public long? LimitBytes { get; }

    public int Concurrency { get; }

    /// <summary>Where the bundle for this target goes.</summary>
    public string BundlePath => NativeWork.Beside(OutPath, Target.Name);

    /// <summary>
    /// What the machine this is running on wants assets cooked as.
    /// A runtime import is cooking for the device doing the importing, so unlike an offline cook there is no choice
    /// to make. Asking the caller to pick would only give them a way to get it wrong.
    /// </summary>
    /// <remarks>
    /// A platform Orblit has no target for throws rather than falling back to something plausible. A wrong guess
    /// here does not fail. It produces textures the device cannot decode, and that turns up as a blank model
    /// rather than as this line.
    /// </remarks>
    public static CookTarget CurrentCookTarget => CookTargetFor(OperatingSystemName());

    /// <summary>
    /// Every importer that works on this machine.
    /// The pure managed ones first, because glTF and scenes are claimed by extension and nothing else wants those
    /// extensions, and because a project adding its own importer usually wants it ahead of the native ones.
    /// </summary>
    public static ImporterRegistry DefaultImporters() => new(
    [
        new GltfImporter(),
        new SceneImporter(),
        new AtlasImporter(),
        new TerrainImporter(),
        .. NativeImporters.Create(),
    ]);

    /// <summary>
    /// Cooks everything under <see cref="AssetsPath"/> and writes the bundle.
    /// Returns the report, having already written it: a caller that only wants the exit code reads
    /// <see cref="CookReport.Ok"/>, and a caller that wants to print what happened has it all.
    /// </summary>
    public async Task<CookReport> RunAsync(Action<CookResult>? onResult = null)
    {
        var cache = new DirectoryCookCache(CachePath, LimitBytes);
        var report = await CookOver(cache).CookAllAsync(await AssetsAsync().ConfigureAwait(false), onResult)
            .ConfigureAwait(false);
        await WriteAsync(report, cache).ConfigureAwait(false);
        return report;
    }

    /// <summary>
    /// Where each of <paramref name="ids"/> stands, without cooking any of them. Left out, it answers for every
    /// asset in the project.
    /// </summary>
    /// <remarks>
    /// This is what the editor puts beside the files in its browser. It asks through the project rather than
    /// building its own <see cref="Cook"/> so that the mark beside an asset is decided by the same assets folder,
    /// the same cache and the same importers the build will use. An editor that assembled its own would say
    /// "cooked" about a build that has everything still to do the first time the two were pointed at different
    /// caches. This reads and hashes every asset and everything under it, so an editor with a large project should
    /// run it off the interface thread.
    /// </remarks>
    public async Task<IReadOnlyDictionary<AssetId, CookState>> StatesOfAsync(IEnumerable<AssetId>? ids = null) =>
        await CookOver(new DirectoryCookCache(CachePath, LimitBytes))
            .StateOfAllAsync(ids ?? await AssetsAsync().ConfigureAwait(false)).ConfigureAwait(false);

    /// <summary>
    /// Every asset under <see cref="AssetsPath"/>, as ids, in a fixed order.
    /// Sorted, because a cook whose order depends on how the file system chose to list a directory is a cook whose
    /// log differs between machines even when its output does not, and the first thing anyone does with two CI
    /// runs is diff them. Settings files and hidden files are left out. A settings file is an input to a cook,
    /// never an asset, and <c>.DS_Store</c> has never been anybody's texture.
    /// </summary>
    public Task<IReadOnlyList<AssetId>> AssetsAsync() => Task.Run<IReadOnlyList<AssetId>>(ListAssets);

    /// <summary>
    /// Writes the bundle: every output beside the asset that produced it, and a manifest naming what each one now
    /// is.
    /// </summary>
    /// <remarks>
    /// An output is written as <c>&lt;asset id&gt;.&lt;output name&gt;</c>, such as <c>wall.png.bc.ktx2</c> or
    /// <c>chair.fbx.glb</c>. Keeping the source's own name and extension in the middle looks redundant and is not.
    /// It is what lets somebody looking at a bundle answer "where did this come from", and what stops two assets
    /// whose names differ only by extension from writing over each other. The bundle is emptied first. A cook that
    /// left yesterday's files behind would ship an asset that no longer exists, and the second time anyone noticed
    /// would be in a crash report.
    /// </remarks>
    public async Task<AssetManifest> WriteAsync(CookReport report, ICookCache cache)
    {
        if (Directory.Exists(BundlePath))
        {
            Directory.Delete(BundlePath, recursive: true);
        }

        Directory.CreateDirectory(BundlePath);

        var entries = new Dictionary<AssetId, AssetEntry>();
        foreach (var result in report.Results)
        {
            foreach (var output in result.Asset?.Outputs ?? [])
            {
                var bytes = await cache.ReadAsync(output.Hash).ConfigureAwait(false)
                    ?? throw new InvalidOperationException(
                        $"The cache reported {result.Id} as cooked but no longer has the bytes for {output.Name}. "
                        + "Something removed them underneath this cook. Running it again will cook them afresh.");
                var name = $"{result.Id}.{output.Name}";
                var file = NativeWork.Beside(BundlePath, name.Replace('/', Path.DirectorySeparatorChar));
                await AtomicFile.WriteBesideAsync(file, bytes).ConfigureAwait(false);
                entries[AssetId.Parse(name)] = new AssetEntry(output.Hash, bytes.Length);
            }
        }

        var manifest = new AssetManifest(entries);
        await AtomicFile.WriteBesideAsync(
            NativeWork.Beside(BundlePath, "manifest.json"), System.Text.Encoding.UTF8.GetBytes(manifest.Encode()))
            .ConfigureAwait(false);
        return manifest;
    }

    /// <summary>A path relative to <paramref name="root"/>, with <c>/</c> between its parts whatever the platform is.</summary>
    internal static string RelativeTo(string root, string path) =>
        Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/');

    /// <summary>Whether any part of <paramref name="relative"/>, a path written by <see cref="RelativeTo"/>, starts with a dot.</summary>
    internal static bool IsHidden(string relative) => relative.Split('/').Any(part => part.StartsWith('.'));

    internal static IEnumerable<string> DirectoriesUnder(string root) => Directory.EnumerateDirectories(root, "*", Walk);

    /// <summary>
    /// A cache outside the project, shared by every project on the machine.
    /// Shared because the point of content addressing is that two projects using the same texture cook it once,
    /// and because a developer switching branches should not pay for it twice.
    /// </summary>
    internal static string DefaultCachePath(Func<string, string?> environment)
    {
        if (environment("ORBLIT_CACHE") is { Length: > 0 } explicitPath)
        {
            return explicitPath;
        }

        var home = environment("HOME") ?? environment("USERPROFILE");
        return string.IsNullOrEmpty(home)
            ? NativeWork.Beside(Path.TrimEndingDirectorySeparator(Path.GetTempPath()), "orblit-cook-cache")
            : NativeWork.Beside(NativeWork.Beside(home, ".cache"), "orblit-cook");
    }

    internal static CookTarget CookTargetFor(string operatingSystem) =>
        CookTargets.Find(operatingSystem)
        ?? throw new NotSupportedException(
            $"Orblit does not cook assets for {operatingSystem}. It cooks for {string.Join(", ", CookTargets.Names)}.");

    // Dart's Platform.operatingSystem. Anything else, such as FreeBSD, is named by what the runtime calls it.
    private static string OperatingSystemName() =>
        OperatingSystem.IsAndroid() ? "android"
        : OperatingSystem.IsIOS() ? "ios"
        : OperatingSystem.IsMacOS() ? "macos"
        : OperatingSystem.IsWindows() ? "windows"
        : OperatingSystem.IsLinux() ? "linux"
        : System.Runtime.InteropServices.RuntimeInformation.OSDescription;

    private Cook CookOver(ICookCache cache) => new(
        source: new DirectoryAssetSource(AssetsPath),
        cache: cache,
        importers: Importers,
        target: Target,
        concurrency: Concurrency);

    private List<AssetId> ListAssets()
    {
        if (!Directory.Exists(AssetsPath))
        {
            throw DartArgument.Value(AssetsPath, "assetsPath", "is not a directory");
        }

        var root = Path.GetFullPath(AssetsPath);
        var found = new List<AssetId>();
        foreach (var file in Directory.EnumerateFiles(root, "*", Walk))
        {
            var relative = RelativeTo(root, file);
            if (!IsHidden(relative) && AssetId.TryParse(relative) is { } id && !ImportSettings.IsSettingsFile(id))
            {
                found.Add(id);
            }
        }

        return Ordered.ById(found);
    }
}
