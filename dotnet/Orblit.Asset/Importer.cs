namespace Orblit.Asset;

/// <summary>
/// Turns one kind of source file into the bytes a device loads. As far as the cache is concerned
/// an importer is a pure function: the same source bytes, dependencies, settings and target must
/// give the same outputs, or a cache hit is a lie. <see cref="Version"/> changes with the function,
/// so every old entry stops matching.
/// </summary>
public abstract class Importer
{
    /// <summary>A stable name, used in the key and in <c>.import.json</c>'s <c>importer</c> field.</summary>
    public abstract string Name { get; }

    /// <summary>Bumped whenever the output bytes could change for an unchanged input.</summary>
    public abstract int Version { get; }

    /// <summary>The lower-case extensions, without dots, this importer claims.</summary>
    public abstract IReadOnlySet<string> Extensions { get; }

    /// <summary>
    /// Whether this importer handles <paramref name="id"/> when nothing named an importer
    /// explicitly. The default answers from <see cref="Extensions"/>.
    /// </summary>
    public virtual bool CanHandle(AssetId id) => Extensions.Contains(id.Extension);

    /// <summary>
    /// Fills in defaults and reads each value as the type this importer wants, so that <c>1</c> and
    /// <c>1.0</c> and <c>"1"</c> become one settings map and therefore one key. A bad setting throws
    /// <see cref="FormatException"/> and the driver reports it against the asset.
    /// </summary>
    public abstract IReadOnlyDictionary<string, object?> ResolveSettings(ImportSettings settings);

    /// <summary>
    /// Every other asset whose contents change these outputs. It runs before the key is built, so it
    /// must be cheap, and it must be complete: a dependency left out is not in the key.
    /// </summary>
    public virtual Task<IReadOnlySet<AssetId>> DependenciesOfAsync(
        AssetId id, byte[] bytes, IReadOnlyDictionary<string, object?> settings) =>
        Task.FromResult<IReadOnlySet<AssetId>>(new HashSet<AssetId>());

    /// <summary>
    /// <see cref="DependenciesOfAsync"/> for an importer that has to read other files to find them.
    /// The cook calls this one. It reads nothing by default.
    /// </summary>
    public virtual Task<IReadOnlySet<AssetId>> DependenciesInAsync(
        AssetId id, byte[] bytes, IReadOnlyDictionary<string, object?> settings, IAssetSource source) =>
        DependenciesOfAsync(id, bytes, settings);

    /// <summary>Does the work.</summary>
    public abstract Task<ImportResult> ImportAsync(ImportRequest request);

    public override string ToString() => $"{Name} v{Version}";
}
