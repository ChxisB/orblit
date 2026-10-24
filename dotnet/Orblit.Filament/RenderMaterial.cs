using Orblit.VectorMath;

namespace Orblit.Filament;

/// <summary>
/// How a surface answers light. Two answers rather than a catalogue: the long list of shaders a
/// renderer offers is one lighting model with different numbers in it, and the numbers live on
/// <see cref="RenderMaterial"/>. What differs is whether light is consulted at all. The order is
/// the number sent.
/// </summary>
public enum MaterialShading
{
    /// <summary>Lit by the scene.</summary>
    Lit,

    /// <summary>Draws the base colour as given, which suits anything that carries its own brightness.</summary>
    Unlit,

    /// <summary>
    /// Shows a video, unlit because a screen makes its own light. The base colour tints it, so a
    /// screen can be dimmed without touching the film.
    /// </summary>
    Video,

    /// <summary>
    /// Nothing but the shadows falling on it, for putting a rendered object onto something that is
    /// not rendered. Its blend is fixed, because the surface is see-through by definition.
    /// </summary>
    ShadowCatcher,
}

/// <summary>Whether a shading model is drawn with the parameters of an ordinary surface.</summary>
public static class MaterialShadings
{
    /// <summary>False for the two models that have a short list of their own.</summary>
    public static bool IsSurface(MaterialShading shading) => shading is MaterialShading.Lit or MaterialShading.Unlit;
}

/// <summary>
/// How a surface's pixels combine with what is already drawn. Blending is state the GPU is
/// configured with, so each mode is a separately compiled copy of the shader and switching modes
/// swaps which material an instance comes from. The order is the number sent.
/// </summary>
public enum MaterialBlend
{
    /// <summary>Alpha is ignored. The fast path, because an opaque object can be sorted and culled.</summary>
    Opaque,

    /// <summary>Alpha fades diffuse and reflections but keeps highlights at full strength, as glass does.</summary>
    Transparent,

    /// <summary>Alpha fades everything, highlights included, until the surface is gone.</summary>
    Fade,

    /// <summary>Each pixel is drawn or discarded by comparing alpha with <see cref="RenderMaterial.MaskThreshold"/>.</summary>
    Masked,

    /// <summary>Added to what is behind it, so it only brightens.</summary>
    Add,
}

/// <summary>Which side of a triangle is drawn. The order is the number sent.</summary>
public enum MaterialCulling
{
    Back,
    Front,
    None,
}

/// <summary>How a texture behaves outside nought to one. The order is the number sent.</summary>
public enum MaterialWrap
{
    Repeat,
    Clamp,
    Mirror,
}

/// <summary>How a texture is sampled between its pixels. The order is the number sent.</summary>
public enum MaterialFilter
{
    Smooth,
    Sharp,
}

/// <summary>How a second surface is mixed into the first. The order is the number sent.</summary>
public enum MaterialBlendMode
{
    None,
    Linear,
    Masked,
    MaskedDepth,
}

/// <summary>
/// Everything about how one surface looks. A value with many fields, so it is changed with
/// <c>with</c>. Objects point at a material by its <see cref="Key"/>.
/// </summary>
public sealed record RenderMaterial
{
    /// <summary>Floats a material occupies in the packed array.</summary>
    public const int Stride = 37;

    /// <summary>
    /// Maps a material holds, in the order they are sent: base colour, normal, metallic and
    /// roughness, occlusion, emissive, then the second surface's base colour and mask.
    /// </summary>
    public const int MapCount = 7;

    /// <summary>The name of each float <c>Pack</c> writes, in order, so a diagnostic can say what the shader was sent.</summary>
    internal static readonly IReadOnlyList<string> PackedNames =
    [
        "baseColour.r",
        "baseColour.g",
        "baseColour.b",
        "baseColour.a",
        "metallic",
        "roughness",
        "reflectance",
        "emissive.r",
        "emissive.g",
        "emissive.b",
        "emissiveIntensity",
        "ambientOcclusion",
        "normalScale",
        "tiling.x",
        "tiling.y",
        "offset.x",
        "offset.y",
        "maskThreshold",
        "depthBias",
        "blendMode",
        "blendAmount",
        "blendSharpness",
        "blendTiling.x",
        "blendTiling.y",
        "blendOffset.x",
        "blendOffset.y",
        "clearCoat",
        "clearCoatRoughness",
        "anisotropy",
        "sheenColour.r",
        "sheenColour.g",
        "sheenColour.b",
        "sheenRoughness",
        "windDirection.x",
        "windDirection.y",
        "windSpeed",
        "windStrength",
    ];

    /// <summary>The name of each slot of <see cref="Maps"/>, in order.</summary>
    internal static readonly IReadOnlyList<string> MapNames =
    [
        "baseColour",
        "normal",
        "metallicRoughness",
        "occlusion",
        "emissive",
        "blendBaseColour",
        "blendMask",
    ];

    private readonly Vector2? _blendTiling;
    private readonly Vector2? _blendOffset;

    public required long Key { get; init; }

    public MaterialShading Shading { get; init; } = MaterialShading.Lit;

    public MaterialBlend Blend { get; init; } = MaterialBlend.Opaque;

    public MaterialCulling Culling { get; init; } = MaterialCulling.Back;

    /// <summary>Whether the back face is lit by its own normal, which culling nothing alone does not do.</summary>
    public bool DoubleSided { get; init; }

    /// <summary>Linear RGBA.</summary>
    public Vector4 BaseColour { get; init; } = new(0.8, 0.8, 0.8, 1.0);

    public double Metallic { get; init; }

    /// <summary>Above nought, because a perfectly smooth surface flickers.</summary>
    public double Roughness { get; init; } = 0.5;

    public double Reflectance { get; init; } = 0.5;

    /// <summary>A varnish over the surface. Nought is the feature off, not a tidy default.</summary>
    public double ClearCoat { get; init; }

    public double ClearCoatRoughness { get; init; } = 0.1;

    /// <summary>Stretches highlights along the grain. Negative turns the grain the other way.</summary>
    public double Anisotropy { get; init; }

    public Vector3 SheenColour { get; init; } = Vector3.Zero();

    public double SheenRoughness { get; init; } = 0.3;

    public Wind Wind { get; init; } = Wind.None;

    /// <summary>Linear RGB.</summary>
    public Vector3 Emissive { get; init; } = Vector3.Zero();

    public double EmissiveIntensity { get; init; }

    public double AmbientOcclusion { get; init; } = 1.0;

    public double NormalScale { get; init; } = 1.0;

    public Vector2 Tiling { get; init; } = new(1.0, 1.0);

    public Vector2 Offset { get; init; } = Vector2.Zero();

    /// <summary>Alpha below this is discarded when the blend is <see cref="MaterialBlend.Masked"/>.</summary>
    public double MaskThreshold { get; init; } = 0.4;

    public bool DepthWrite { get; init; } = true;

    public double DepthBias { get; init; }

    public MaterialWrap Wrap { get; init; } = MaterialWrap.Repeat;

    public MaterialFilter Filter { get; init; } = MaterialFilter.Smooth;

    /// <summary>The key of the video a <see cref="MaterialShading.Video"/> screen shows.</summary>
    public long? Video { get; init; }

    /// <summary>Whether the screen's picture is laid across the view rather than across the surface.</summary>
    public bool ScreenMapped { get; init; }

    /// <summary>Whether a video screen is lit as well as bright.</summary>
    public bool LitScreen { get; init; }

    public MaterialBlendMode BlendMode { get; init; } = MaterialBlendMode.None;

    public double BlendAmount { get; init; }

    public double BlendSharpness { get; init; } = 8.0;

    /// <summary>How often the second surface repeats. It follows <see cref="Tiling"/> until told otherwise.</summary>
    public Vector2 BlendTiling
    {
        get => _blendTiling ?? Tiling;
        init => _blendTiling = value;
    }

    /// <summary>Where the second surface starts. It follows <see cref="Offset"/> until told otherwise.</summary>
    public Vector2 BlendOffset
    {
        get => _blendOffset ?? Offset;
        init => _blendOffset = value;
    }

    public Texture? BlendBaseColourMap { get; init; }

    public Texture? BlendMaskMap { get; init; }

    public Texture? BaseColourMap { get; init; }

    public Texture? NormalMap { get; init; }

    public Texture? MetallicRoughnessMap { get; init; }

    public Texture? OcclusionMap { get; init; }

    public Texture? EmissiveMap { get; init; }

    /// <summary>A shader somebody wrote, drawn instead of the built-in ones.</summary>
    public CustomSurface? Surface { get; init; }

    /// <summary>The maps in the order they are sent. An empty slot is null.</summary>
    public IReadOnlyList<Texture?> Maps =>
        [BaseColourMap, NormalMap, MetallicRoughnessMap, OcclusionMap, EmissiveMap, BlendBaseColourMap, BlendMaskMap];

    /// <summary>
    /// What needs a rebuild of the shader, in one number. Bit 15 says a custom surface is drawn.
    /// </summary>
    internal int Flags =>
        (int)Shading
        | ((int)Blend << 2)
        | ((int)Culling << 6)
        | ((DoubleSided ? 1 : 0) << 8)
        | ((DepthWrite ? 1 : 0) << 9)
        | ((int)Wrap << 10)
        | ((int)Filter << 12)
        | (ScreenMapped ? 1 << 13 : 0)
        | (LitScreen ? 1 << 14 : 0)
        | (Surface is not null ? 1 << 15 : 0);

    /// <summary>
    /// The material as the scene packs it for the renderer, as JSON ready values. The floats are
    /// the packed ones, so they are the 32 bit values written to the message and not a read back
    /// from the renderer.
    /// </summary>
    public OrderedDictionary<string, object?> ToJson()
    {
        var packed = new float[Stride];
        Pack(packed, 0);
        var maps = new OrderedDictionary<string, object?>();
        for (var slot = 0; slot < MapCount; slot++)
        {
            if (Maps[slot] is { } map)
            {
                maps[MapNames[slot]] = new OrderedDictionary<string, object?> { ["path"] = map.Path, ["srgb"] = map.Srgb };
            }
        }

        var values = new OrderedDictionary<string, object?>();
        for (var at = 0; at < Stride; at++)
        {
            values[PackedNames[at]] = (double)packed[at];
        }

        return new OrderedDictionary<string, object?>
        {
            ["key"] = Key,
            ["shading"] = EnumNames.Of(Shading),
            ["blend"] = EnumNames.Of(Blend),
            ["culling"] = EnumNames.Of(Culling),
            ["doubleSided"] = DoubleSided,
            ["depthWrite"] = DepthWrite,
            ["wrap"] = EnumNames.Of(Wrap),
            ["filter"] = EnumNames.Of(Filter),
            ["screenMapped"] = ScreenMapped,
            ["litScreen"] = LitScreen,
            ["video"] = Video,
            ["blendMode"] = EnumNames.Of(BlendMode),
            ["maps"] = maps,
            ["values"] = values,
        };
    }

    internal void Pack(float[] into, int at) => Packing.Floats(
        BaseColour.X, BaseColour.Y, BaseColour.Z, BaseColour.W,
        Metallic,
        Roughness,
        Reflectance,
        Emissive.X, Emissive.Y, Emissive.Z,
        EmissiveIntensity,
        AmbientOcclusion,
        NormalScale,
        Tiling.X, Tiling.Y,
        Offset.X, Offset.Y,
        MaskThreshold,
        DepthBias,
        (int)BlendMode,
        BlendAmount,
        BlendSharpness,
        BlendTiling.X, BlendTiling.Y,
        BlendOffset.X, BlendOffset.Y,
        ClearCoat,
        ClearCoatRoughness,
        Anisotropy,
        SheenColour.X, SheenColour.Y, SheenColour.Z,
        SheenRoughness,
        Wind.Direction.X, Wind.Direction.Y,
        Wind.Moves ? Wind.Speed : 0.0,
        Wind.Moves ? Wind.Strength : 0.0).CopyTo(into, at);
}
