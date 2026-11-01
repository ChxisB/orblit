namespace Orblit.Filament;

/// <summary>
/// Where a mobile application's assets sit in its package, for readers that seek and so never hold
/// a large asset in memory.
/// </summary>
/// <remarks>
/// A desktop keeps a bundled asset as a file beside the executable, where a plain path finds it.
/// A package that keeps its assets inside one archive can only hand over all of an asset as bytes,
/// which for a game pack of hundreds of megabytes is the whole pack in memory before the first
/// asset is drawn. Asking where the asset sits in the archive instead lets a reader seek to
/// <see cref="AssetFile.Offset"/> in <see cref="AssetFile.Path"/> and read up to
/// <see cref="AssetFile.Length"/>.
/// </remarks>
/// <param name="source">
/// What knows the package, or null where there is nothing to ask: every platform whose assets are
/// plain files.
/// </param>
// shortcut: no package source ships yet, so every platform answers null, when mobile hosts arrive (decision 71).
public sealed class AssetFiles(IAssetFileSource? source)
{
    /// <summary>Where <paramref name="key"/> sits, or null where there is nothing to ask or the package has no such asset.</summary>
    public AssetFile? Of(string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        return source?.Find(key);
    }

    /// <summary>Lets go of what <see cref="Of"/> kept open for <paramref name="key"/>, and says whether it held anything.</summary>
    public bool Release(string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        return source?.Release(key) ?? false;
    }
}

/// <summary>A package that can say where an asset sits in it. A failure other than "no such asset" is thrown.</summary>
public interface IAssetFileSource
{
    /// <summary>The asset's place, or null for a key the package does not have.</summary>
    AssetFile? Find(string key);

    /// <summary>Lets go of the descriptor kept open for <paramref name="key"/>, and says whether there was one.</summary>
    bool Release(string key);
}
