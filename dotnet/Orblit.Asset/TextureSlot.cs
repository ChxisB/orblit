namespace Orblit.Asset;

/// <summary>One texture slot on one material.</summary>
public sealed class TextureSlot(TextureRole role, GltfMap owner, string key, GltfMap info)
{
    public TextureRole Role { get; } = role;

    /// <summary>The material this slot belongs to.</summary>
    public GltfMap Owner { get; } = owner;

    /// <summary>Where the slot lives (<c>baseColorTexture</c> and the rest), for messages.</summary>
    public string Key { get; } = key;

    /// <summary>The <c>textureInfo</c> object itself: <c>{index, texCoord, extensions?}</c>.</summary>
    public GltfMap Info { get; } = info;

    public int Texture => GltfJson.IntOr(Info, "index", -1);

    public int TexCoord => GltfJson.IntOr(Info, "texCoord", 0);
}
