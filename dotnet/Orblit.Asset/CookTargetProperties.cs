using System.Diagnostics.CodeAnalysis;

namespace Orblit.Asset;

/// <summary>
/// Reads <see cref="CookTarget.Properties"/> with the names the built-in importers agree on, so that a missing or
/// mistyped property is caught once, here, rather than differently in each importer.
/// </summary>
[SuppressMessage("Usage", "CA2208:Instantiate argument exceptions correctly", Justification = "The error names the target property that is wrong, as the Dart error does, and a caller reads that name.")]
public static class CookTargetProperties
{
    /// <summary>Which compressed texture families to write. Empty means the caller did not say.</summary>
    public static IReadOnlyList<string> TextureFamilies(this CookTarget target)
    {
        if (target.Properties.GetValueOrDefault("textureFamilies") is not { } value)
        {
            return [];
        }

        if (value is not IEnumerable<object?> families)
        {
            throw new ArgumentException(
                $"must be a list of family names, one of [{string.Join(", ", TextureFamily.All)}]: {value}",
                "textureFamilies");
        }

        return [.. families.Select(family => family as string ?? throw new InvalidCastException($"{family} is not a family name."))];
    }

    /// <summary>The largest texture the device will accept, or null for no limit.</summary>
    public static long? MaxTextureSize(this CookTarget target)
    {
        if (target.Properties.GetValueOrDefault("maxTextureSize") is not { } value)
        {
            return null;
        }

        return value is long size and > 0
            ? size
            : throw new ArgumentException($"must be a positive whole number of pixels: {value}", "maxTextureSize");
    }

    /// <summary>
    /// Whether the device samples half-float textures, which decides whether an environment map can stay in
    /// floating point or has to be encoded.
    /// </summary>
    public static bool HalfFloatTextures(this CookTarget target) =>
        target.Properties.GetValueOrDefault("halfFloatTextures") is true;
}
