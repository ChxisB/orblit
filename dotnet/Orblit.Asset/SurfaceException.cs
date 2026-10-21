namespace Orblit.Asset;

/// <summary>
/// Something in a surface source that the translator refuses, said at the line the author wrote it on.
/// </summary>
public sealed class SurfaceException(SurfaceOrigin origin, string reason) : Exception($"{origin}: {reason}")
{
    /// <summary>Where the author wrote it.</summary>
    public SurfaceOrigin Origin { get; } = origin;

    /// <summary>What is wrong, without the place.</summary>
    public string Reason { get; } = reason;
}
