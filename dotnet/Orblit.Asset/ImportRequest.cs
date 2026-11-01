namespace Orblit.Asset;

/// <summary>
/// The one asset an importer has been asked to turn into bytes, and everything it is allowed to
/// look at while doing so.
/// </summary>
public sealed class ImportRequest
{
    /// <summary>The asset being cooked.</summary>
    public required AssetId Id { get; init; }

    /// <summary>Its bytes, already read. The driver has read them anyway to hash them.</summary>
    public required byte[] Bytes { get; init; }

    /// <summary>
    /// The settings for this asset, already resolved: every default filled in and every value read
    /// as the type the importer declared.
    /// </summary>
    public required IReadOnlyDictionary<string, object?> Settings { get; init; }

    /// <summary>What machine the bytes are for.</summary>
    public required CookTarget Target { get; init; }

    /// <summary>
    /// Where to read anything else from. Only the ids <see cref="Importer.DependenciesOfAsync"/>
    /// returned may be read: anything else is a file the key does not cover, so the cache would
    /// serve stale bytes forever.
    /// </summary>
    public required IAssetSource Source { get; init; }
}

/// <summary>What came out of an importer.</summary>
public sealed class ImportResult
{
    /// <summary>The files produced, by the name each is filed under inside the cooked entry.</summary>
    public required IReadOnlyDictionary<string, byte[]> Outputs { get; init; }

    /// <summary>
    /// Anything a person should see. Not errors: an importer that cannot proceed throws
    /// <see cref="ImportFailureException"/>.
    /// </summary>
    public IReadOnlyList<string> Notes { get; init; } = [];
}
