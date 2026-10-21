namespace Orblit.Asset;

/// <summary>
/// An importer could not do its job. Thrown rather than returned, because there is no
/// half-cooked asset: either the outputs are there or the cook failed.
/// </summary>
public sealed class ImportFailureException(AssetId id, string reason, object? cause = null)
    : Exception($"Could not import {id}: {reason}{(cause is null ? "" : "\n" + cause)}")
{
    public AssetId Id { get; } = id;

    public string Reason { get; } = reason;

    /// <summary>Whatever the underlying tool said, when there was one.</summary>
    public object? Cause { get; } = cause;
}
