namespace Orblit.Asset;

/// <summary>
/// Where an asset stands with the cook, worked out without cooking it. It is <see cref="CookStatus"/> asked in
/// advance rather than reported afterwards, and a separate name on purpose: <c>Cooked</c> here means "already in
/// the cache and up to date", where <see cref="CookStatus.Cooked"/> means "an importer just ran".
/// </summary>
public enum CookState
{
    /// <summary>
    /// The cache holds outputs for exactly this source, these dependencies, these settings and this target.
    /// A build would do nothing.
    /// </summary>
    Cooked,

    /// <summary>Never cooked, or cooked from something that has since changed. A build would run the importer.</summary>
    Stale,

    /// <summary>
    /// It cannot be cooked as things stand. Either working out what to do with it failed here and now, or a cook
    /// tried this exact work before and the importer could not do it. A build would report this asset and carry
    /// on with the rest. It is still worth trying: importers fail for reasons that are not in the key, so a cook
    /// always runs one rather than believing this.
    /// </summary>
    Failed,

    /// <summary>Nothing claims it. A README and an <c>.import.json</c> both land here, and neither is a problem.</summary>
    Ignored,
}
