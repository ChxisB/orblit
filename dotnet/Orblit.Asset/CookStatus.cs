namespace Orblit.Asset;

/// <summary>How one asset came out of a cook.</summary>
public enum CookStatus
{
    /// <summary>An importer ran and the cache now holds what it wrote.</summary>
    Cooked,

    /// <summary>The cache already held it: nothing ran.</summary>
    Cached,

    /// <summary>
    /// No importer claimed the asset, so it was left alone. A README and an <c>.import.json</c> both land here,
    /// and neither is a problem.
    /// </summary>
    Skipped,

    /// <summary>Something threw. <see cref="CookResult.Error"/> says what.</summary>
    Failed,
}
