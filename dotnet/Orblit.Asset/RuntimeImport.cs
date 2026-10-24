using Orblit.Json;

namespace Orblit.Asset;

/// <summary>
/// Importing a file somebody hands the app while it is running.
/// A user drops a model onto a level, opens one from their photo library, or downloads a skin somebody else made.
/// The engine has to end up with the same thing it would have had if that file had been in the project all along,
/// and the way to get that is to run the same importers rather than a second, looser path that drifts from the
/// first.
/// </summary>
/// <remarks>
/// <para>
/// So this is <see cref="Cook"/> again, with the parts a project supplies replaced by the parts a running app has:
/// the device's cache instead of a build machine's, this device's target instead of a chosen one, and one file
/// somebody handed over instead of a folder full of them.
/// </para>
/// <para>
/// The cache is the point. Importing a model costs real time, and a user who adds one to their world expects it to
/// be there next launch without paying again. Because the key covers the file's own bytes, re-importing the same
/// file is free, and importing a file the project already shipped is free too, since it is the same key the build
/// machine used.
/// </para>
/// <para>
/// Not every importer runs here. The ones that drive a command-line tool need that tool, which a build machine has
/// and a phone does not. They are still registered, because a desktop app is a running app too and has them. What
/// a phone gets is an <see cref="ImportFailureException"/> naming the tool it could not find, rather than a file that
/// silently imported to nothing.
/// </para>
/// </remarks>
public sealed class RuntimeImport(ICookCache cache, CookTarget target, ImporterRegistry? importers = null)
{
    /// <summary>
    /// Where results are kept. On a device this is the app's cache directory, which is to say somewhere the system
    /// may empty when it needs the space. That is correct, because everything in it can be made again from the file.
    /// </summary>
    public ICookCache Cache { get; } = cache;

    /// <summary>
    /// What this device wants. A runtime import is cooking for the machine it is running on, so there is exactly
    /// one right answer and the caller should take it from <see cref="CookProject.CurrentCookTarget"/> rather than
    /// picking.
    /// </summary>
    public CookTarget Target { get; } = target;

    public ImporterRegistry Importers { get; } = importers ?? DefaultImporters();

    /// <summary>
    /// Every importer that could run in an app.
    /// The same list the cook command uses. A runtime import that quietly had fewer importers than the build
    /// machine would mean a file that works when shipped and fails when dropped in, which is the confusing way
    /// round. It is one list, so the two cannot drift.
    /// </summary>
    public static ImporterRegistry DefaultImporters() => CookProject.DefaultImporters();

    /// <summary>
    /// What to call a file a user chose, given wherever it came from.
    /// A file picker hands back a path, such as <c>/var/mobile/.../My Model.glb</c>,
    /// <c>C:\Users\chris\Downloads\robot.glb</c> or a URL. None of those is an asset id: an id is a name inside a
    /// project, and the whole point of it is that it does not carry a machine's directory layout around. The last
    /// segment is what survives, because it is the part the user would recognise and the only part that says what
    /// kind of file this is.
    /// </summary>
    /// <returns>
    /// Null when nothing usable is left: a path ending in only separators, or a name that is all dots. The caller
    /// then has a file it cannot name, and asking the user is better than inventing something.
    /// </returns>
    public static AssetId? NameFor(string path)
    {
        var cleaned = path.Split('?')[0].Split('#')[0];
        foreach (var segment in cleaned.Split('/', '\\').Reverse())
        {
            var name = PlainText.Trim(segment);
            if (name.Length > 0 && name is not ("." or ".."))
            {
                return AssetId.TryParse(name.Replace(':', '_'));
            }
        }

        return null;
    }

    /// <summary>
    /// Whether anything here claims a file by this name.
    /// Worth asking before offering a user a file picker full of things that will not open.
    /// <see cref="ImportSettings"/> can name an importer explicitly, so the settings are part of the question.
    /// </summary>
    public bool Handles(AssetId id, ImportSettings? settings = null) => Importers.ForAsset(id, settings) is not null;

    /// <summary>
    /// Imports one file, or gives back what a previous import of it produced.
    /// Never throws for a file that cannot be imported. The result says what happened, because "the user picked
    /// something we cannot read" is an ordinary thing for a user to do and not an error in the program.
    /// </summary>
    /// <param name="settings">
    /// What the user said at the call, such as "this is a normal map". It is laid over whatever settings files the
    /// source has. Left out, the source's own settings apply.
    /// </param>
    /// <param name="alongside">
    /// For a file that is not self-contained. A <c>.gltf</c> names its buffers and images in separate files, so a
    /// user who picked only the <c>.gltf</c> has handed over a fragment. Pass the folder they picked, or the
    /// contents of the archive they gave, and those references resolve. A <c>.glb</c> needs none of this, which is
    /// why it is the format to ask users for.
    /// </param>
    public Task<CookResult> ImportAsync(
        AssetId id, byte[] bytes, ImportSettings? settings = null, IAssetSource? alongside = null)
    {
        var supplied = new MemoryAssetSource(new Dictionary<AssetId, byte[]> { [id] = bytes });
        IAssetSource source = alongside is null ? supplied : new LayeredAssetSource([supplied, alongside]);
        var cook = new Cook(
            source: source,
            cache: Cache,
            importers: Importers,
            target: Target,
            settings: settings is null ? new ImportSettingsReader(source) : new FixedSettingsReader(source, settings),
            // One at a time: a runtime import is one file, and a phone that spent four cores on it would drop the
            // frames the user is looking at.
            concurrency: 1);
        return cook.CookOneAsync(id);
    }

    /// <summary>
    /// The bytes of one of an import's outputs, by the name the importer gave it: <c>glb</c>, <c>bc.ktx2</c>,
    /// <c>osplat</c>.
    /// Null when the import produced no such output, or when the cache has since dropped it. A caller that gets
    /// null from a result it just imported has been evicted mid-use and should import again.
    /// </summary>
    public async Task<byte[]?> ReadAsync(CookedAsset asset, string output) =>
        asset[output] is { } entry ? await Cache.ReadAsync(entry.Hash).ConfigureAwait(false) : null;

    /// <summary>
    /// The one output of an import that produced exactly one, which is the usual case for a model or a splat cloud.
    /// Null when there were none or several: a caller that does not know how many outputs to expect should not be
    /// guessing which of them it wanted.
    /// </summary>
    public async Task<byte[]?> ReadOnlyAsync(CookedAsset asset) =>
        asset.Outputs.Count == 1 ? await Cache.ReadAsync(asset.Outputs[0].Hash).ConfigureAwait(false) : null;

    /// <summary>
    /// Settings given at the call, falling back to whatever the source has.
    /// A user importing a file usually says what they want right there, rather than by writing a settings file
    /// next to it. This lets them, without losing the settings that a file shipped with.
    /// </summary>
    private sealed class FixedSettingsReader(IAssetSource source, ImportSettings fixedSettings)
        : ImportSettingsReader(source)
    {
        public override async Task<ImportSettings> ForAssetAsync(AssetId id) =>
            (await base.ForAssetAsync(id).ConfigureAwait(false)).MergedWith(fixedSettings);
    }
}
