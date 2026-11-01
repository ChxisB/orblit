namespace Orblit.Filament;

/// <summary>
/// An image on disk, used as one of a material's maps. A path rather than bytes, because the same
/// file is usually on many materials and the renderer loads each one once however many ask for it.
/// Two textures are equal when they name the same file in the same colour space, which is how the
/// message decides to send a file once.
/// </summary>
/// <param name="Path">
/// An absolute path to a PNG, JPEG or KTX2 file, <c>orblit:target/&lt;name&gt;</c> for what a pass
/// drew, or <c>orblit:feed/&lt;key&gt;/&lt;width&gt;x&lt;height&gt;</c> for a copy of a feed.
/// </param>
/// <param name="Srgb">
/// Whether the file holds colour. Normals, masks and roughness are numbers, and reading them as
/// colour bends every value.
/// </param>
public sealed record Texture(string Path, bool Srgb = true)
{
    /// <summary>What a target's path starts with.</summary>
    public const string TargetScheme = "orblit:target/";

    /// <summary>What a feed copy's path starts with.</summary>
    public const string FeedScheme = "orblit:feed/";

    /// <summary>The pass target this samples, or null if it is an image on disk.</summary>
    public string? Target => Path.StartsWith(TargetScheme, StringComparison.Ordinal) ? Path[TargetScheme.Length..] : null;

    /// <summary>
    /// What a pass drew, sampled as a texture. This is what makes a render target worth writing. A
    /// mirror is a pass that draws the scene from behind the glass into a target and a material
    /// that samples it. A portal, a security monitor and a rear-view mirror are the same two halves.
    /// Without this a target is a picture nothing can look at.
    /// </summary>
    /// <remarks>
    /// Addressed as a path with a scheme rather than through a second field: a texture is already
    /// "where the image comes from", and one more place it can come from is a different answer to
    /// the same question. Never sRGB, because what a pass drew is already linear and decoding it
    /// again would darken every reflection in the scene.
    /// </remarks>
    public static Texture OfTarget(string target) => new(TargetScheme + target, false);

    /// <summary>
    /// A feed, copied once a frame into a texture of its own. A screen with the video shading shows
    /// a feed without copying it. This is for the other slots: the texture can go in any map of any
    /// material. It costs one GPU copy a frame at the given size and nothing crosses to the CPU.
    /// </summary>
    /// <remarks>
    /// The size defaults to the one <see cref="FeedSource.Create(int, int)"/> was given, swapped for
    /// a feed turned by a quarter, so the texture comes out upright. A smaller size is a cheaper
    /// copy. Like a target, the copy has one level and is sampled clamped and linear, and it is
    /// never sRGB: the copy has already decoded the picture to linear light. A feed is shown by its
    /// key, so the feed has to be in the scene's <see cref="RenderScene.Feeds"/>. A video has no
    /// declared size and is not offered.
    /// </remarks>
    public static Texture OfFeed(Feed feed, int? width = null, int? height = null)
    {
        var turned = feed.Rotation % 2 == 1;
        var wide = width ?? (turned ? feed.Source.Height : feed.Source.Width);
        var tall = height ?? (turned ? feed.Source.Width : feed.Source.Height);
        return wide < 1 || tall < 1
            ? throw new ArgumentException($"A feed copy needs a size of at least 1 by 1, not {wide} by {tall}.")
            : new Texture($"{FeedScheme}{feed.Key}/{wide}x{tall}", false);
    }
}
