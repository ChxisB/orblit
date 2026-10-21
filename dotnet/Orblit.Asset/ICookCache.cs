namespace Orblit.Asset;

/// <summary>
/// Cooked results, filed by what made them. A rebuild with nothing changed cooks nothing, and
/// changing one texture recooks that texture and nothing else.
/// </summary>
/// <remarks>
/// A miss is never wrong, only slow. Everything here may answer "no" whenever it is not certain: an
/// entry whose bytes have been evicted, an index it could not read, a store on a disk that has gone
/// away. A hit has to be right, so an entry is only reported when every one of its outputs is still
/// there to be read.
/// </remarks>
public interface ICookCache
{
    /// <summary>What <paramref name="key"/> cooked to last time, or null when this cache cannot say.</summary>
    Task<CookedAsset?> LookUpAsync(CookKey key);

    /// <summary>
    /// Files the outputs of cooking <paramref name="key"/>, and reports them back as they will be
    /// read. Storing the same key twice is not an error.
    /// </summary>
    Task<CookedAsset> StoreAsync(CookKey key, IReadOnlyDictionary<string, byte[]> outputs);

    /// <summary>The bytes of one output, or null when they are no longer there.</summary>
    Task<byte[]?> ReadAsync(ContentHash hash);

    /// <summary>
    /// What cooking <paramref name="key"/> failed with last time, or null when nothing is remembered
    /// about it. Null is always allowed: a cache that has forgotten a failure says the asset is
    /// merely uncooked.
    /// </summary>
    Task<CookFailure?> LookUpFailureAsync(CookKey key);

    /// <summary>
    /// Remembers that cooking <paramref name="key"/> did not work. Recording is not refusing:
    /// <see cref="LookUpAsync"/> is unaffected and no cook consults this before working.
    /// </summary>
    Task RecordFailureAsync(CookKey key, string reason);
}
