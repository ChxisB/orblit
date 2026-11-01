namespace Orblit.Asset;

/// <summary>An asset that was asked for and is not there.</summary>
public sealed class AssetNotFoundException(AssetId id, string? reason = null)
    : Exception(reason is null ? $"There is no asset \"{id}\"." : $"There is no asset \"{id}\": {reason}.")
{
    /// <summary>The asset that was asked for.</summary>
    public AssetId Id { get; } = id;

    /// <summary>
    /// Why, when the source knows something more useful than that it is not there: that the whole
    /// directory is missing, say.
    /// </summary>
    public string? Reason { get; } = reason;
}
