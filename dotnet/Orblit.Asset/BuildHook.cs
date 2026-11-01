namespace Orblit.Asset;

/// <summary>
/// Cooking a project's assets as part of a build, for the platform being built.
/// A game's build script calls it with what the build already knows, and gets a bundle cooked for that platform.
/// Everything crossing the boundary is a string, a <see cref="Uri"/> or a function, so depending on this package
/// does not drag a build system into a project that only wants to read assets. The same call works from a plain
/// script, which is how most projects will run it.
/// </summary>
/// <remarks>
/// A bundle cooked here is written into the package and shipped as an ordinary asset of the app, so the project
/// has to list the output folder where the app's build looks for assets.
/// </remarks>
public static class BuildHook
{
    /// <summary>
    /// Cooks the project under <paramref name="packageRoot"/> for <paramref name="targetOs"/>.
    /// </summary>
    /// <param name="targetOs">
    /// The name of the platform being built for, as the build spells it: <c>ios</c>, <c>macos</c>, <c>android</c>,
    /// <c>windows</c>, <c>linux</c>. A null one means the web, because a web build is not told a target at all.
    /// That is the one platform where the absence is the answer.
    /// </param>
    /// <param name="dependencies">
    /// Given every file this cook read: the assets, their settings files, and whatever those assets pointed at. It
    /// is also given the folders the assets are in, which is how an asset or a settings file being added is
    /// noticed. A build that reports them runs the hook again when one changes and skips it when none did, which
    /// is the difference between a cook that costs nothing on an untouched build and one that costs a texture
    /// encode every time.
    /// </param>
    /// <exception cref="CookFailedException">Some asset could not be cooked. The report is on the exception.</exception>
    // F2: nine parameters, all named at each call.
    public static async Task<CookReport> CookDuringBuildAsync(
        string packageRoot,
        string? targetOs,
        string assets = "assets",
        string @out = "assets/cooked",
        string? cachePath = null,
        ImporterRegistry? importers = null,
        int concurrency = 4,
        Action<IEnumerable<Uri>>? dependencies = null,
        Action<string>? log = null)
    {
        var target = TargetFor(targetOs);
        var assetsPath = Under(packageRoot, assets);
        var project = new CookProject(
            assetsPath: assetsPath,
            outPath: Under(packageRoot, @out),
            target: target,
            cachePath: cachePath,
            importers: importers,
            concurrency: concurrency);

        var report = await project.RunAsync().ConfigureAwait(false);
        log?.Invoke($"orblit: cooked {target.Name}: {report.Summary}");

        dependencies?.Invoke(WatchedBy(report, assetsPath));

        return report.Ok ? report : throw new CookFailedException(report);
    }

    /// <summary>
    /// Cooks a project's assets as part of a build and packs them as one file,
    /// <c>&lt;out&gt;/&lt;name&gt;.&lt;target&gt;.opack</c>, for the platform being built.
    /// This is <see cref="CookDuringBuildAsync"/> followed by <see cref="PackBundle.PackAsync"/>, and takes
    /// everything it does. The cooked folder is still written, beside the pack, as the thing the pack was made
    /// from.
    /// </summary>
    /// <remarks>
    /// The app lists the one file for its platform, so a build ships its own target's pack and nothing else.
    /// <see cref="PackName.FileName"/> gives the name the running app reads it back by. The pack is not reported to
    /// <paramref name="dependencies"/>. A build treats a dependency that changed while the hook ran as stale and runs
    /// the hook again on every build, and the pack always changes while the hook runs.
    /// </remarks>
    // F2: ten parameters, all named at each call.
    public static async Task<PackSummary> PackDuringBuildAsync(
        string packageRoot,
        string? targetOs,
        string assets = "assets",
        string @out = "assets/cooked",
        string name = "game",
        string? cachePath = null,
        ImporterRegistry? importers = null,
        int concurrency = 4,
        Action<IEnumerable<Uri>>? dependencies = null,
        Action<string>? log = null)
    {
        var target = TargetFor(targetOs);
        await CookDuringBuildAsync(
            packageRoot: packageRoot,
            targetOs: targetOs,
            assets: assets,
            @out: @out,
            cachePath: cachePath,
            importers: importers,
            concurrency: concurrency,
            dependencies: dependencies,
            log: log).ConfigureAwait(false);
        var summary = await PackBundle.PackAsync(
            Under(packageRoot, $"{@out}/{target.Name}"),
            Under(packageRoot, $"{@out}/{PackName.FileName(target, name)}"),
            target).ConfigureAwait(false);
        log?.Invoke(
            $"orblit: packed {target.Name}: {summary.Assets} assets in {summary.Blobs} blobs, {summary.Bytes} bytes");
        return summary;
    }

    /// <summary>The target a hook's <c>targetOs</c> names, where a null one is the web.</summary>
    private static CookTarget TargetFor(string? targetOs) =>
        CookTargets.Find(targetOs ?? "web")
        ?? throw InvalidArgument.Value(
            targetOs,
            "targetOs",
            $"is not a platform Orblit cooks for. Orblit cooks for {string.Join(", ", CookTargets.Names)}");

    /// <summary>
    /// What a build is told a cook read, and what would change its answer.
    /// Both the assets and their settings files, because changing how a texture is cooked has to rebuild it just
    /// as surely as changing the texture. A settings file is reported only when it exists. A build takes a path
    /// that does not exist for changed on every run, so reporting the settings file an asset might have would run
    /// the hook again on each build of nearly every project. A settings file being added, or an asset, is noticed
    /// through its folder instead, which a build compares by the names it holds. Folders are reported one by one
    /// because a build does not look inside them.
    /// </summary>
    private static HashSet<Uri> WatchedBy(CookReport report, string assetsPath)
    {
        var watched = new HashSet<Uri>();
        foreach (var result in report.Results)
        {
            watched.Add(FileUnder(assetsPath, $"{result.Id}"));
            var settings = FileUnder(assetsPath, $"{result.Id}.import.json");
            if (File.Exists(settings.LocalPath))
            {
                watched.Add(settings);
            }
        }

        foreach (var id in report.Dependencies)
        {
            watched.Add(FileUnder(assetsPath, $"{id}"));
        }

        watched.Add(DirectoryUri(assetsPath));
        var root = Path.GetFullPath(assetsPath);
        foreach (var directory in CookProject.DirectoriesUnder(root))
        {
            if (!CookProject.IsHidden(CookProject.RelativeTo(root, directory)))
            {
                watched.Add(DirectoryUri(directory));
            }
        }

        return watched;
    }

    /// <summary>A path inside a package, where the inner part is written with <c>/</c> whatever the platform is.</summary>
    private static string Under(string root, string relative) =>
        relative.Split('/').Where(part => part.Length > 0).Aggregate(root, NativeWork.Beside);

    private static Uri FileUnder(string root, string relative) => new(Path.GetFullPath(Under(root, relative)));

    private static Uri DirectoryUri(string path) =>
        new(Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)) + Path.DirectorySeparatorChar);
}
