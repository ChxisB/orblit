using System.Diagnostics.CodeAnalysis;

namespace Orblit.Asset;

/// <summary>
/// What a texture is being used as. This matters more than it looks. A base colour map is sRGB and a
/// roughness map is not, so the two cannot share a page: decoding one as the other darkens or washes out
/// every texel. Each role also cooks differently. A normal map wants the two-channel path, occlusion wants
/// a single channel, and colour wants neither.
/// </summary>
public enum TextureRole
{
    BaseColour,
    MetallicRoughness,
    Normal,
    Occlusion,
    Emissive,
}

/// <summary>What each <see cref="TextureRole"/> carries.</summary>
public static class TextureRoleTools
{
    private static readonly int[] White = [255, 255, 255, 255];
    private static readonly int[] Flat = [128, 128, 255, 255];

    /// <summary>Whether the page holding this role is stored sRGB-encoded.</summary>
    public static bool Srgb(this TextureRole role) => role is TextureRole.BaseColour or TextureRole.Emissive;

    /// <summary>
    /// What an unused texel of this role's page should be, as RGBA8. Not arbitrary: each one is the value glTF
    /// samples when a material has no map in that slot at all, so a material handed a slot it never had draws
    /// exactly as it did before. White for everything multiplicative (a missing emissive map means white times
    /// an emissive factor of nought, not black times nothing) and flat for a normal.
    /// </summary>
    [SuppressMessage("Maintainability", "CA1502:Avoid excessive complexity", Justification = "F2: a list of cases is data.")]
    public static IReadOnlyList<int> Blank(this TextureRole role) => role switch
    {
        TextureRole.BaseColour => White,
        TextureRole.MetallicRoughness => White,
        TextureRole.Normal => Flat,
        TextureRole.Occlusion => White,
        TextureRole.Emissive => White,
        _ => throw new InvalidOperationException($"{role} is not a texture role."),
    };

    /// <summary>The role's name as a message gives it, which is Dart's enum name.</summary>
    [SuppressMessage("Maintainability", "CA1502:Avoid excessive complexity", Justification = "F2: a list of cases is data.")]
    public static string Name(this TextureRole role) => role switch
    {
        TextureRole.BaseColour => "baseColour",
        TextureRole.MetallicRoughness => "metallicRoughness",
        TextureRole.Normal => "normal",
        TextureRole.Occlusion => "occlusion",
        TextureRole.Emissive => "emissive",
        _ => throw new InvalidOperationException($"{role} is not a texture role."),
    };

    /// <summary>
    /// The suffix its page gets, so <c>atlas0.basecolour.ktx2</c> says what it holds without anyone opening it.
    /// </summary>
    [SuppressMessage("Maintainability", "CA1502:Avoid excessive complexity", Justification = "F2: a list of cases is data.")]
    public static string Suffix(this TextureRole role) => role switch
    {
        TextureRole.BaseColour => "basecolour",
        TextureRole.MetallicRoughness => "metallicroughness",
        TextureRole.Normal => "normal",
        TextureRole.Occlusion => "occlusion",
        TextureRole.Emissive => "emissive",
        _ => throw new InvalidOperationException($"{role} is not a texture role."),
    };
}
