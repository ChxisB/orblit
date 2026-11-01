using Orblit.Filament;
using Orblit.Scene;
using Orblit.VectorMath;

namespace Orblit.Stage;

/// <summary>
/// Turns a resolved material into the one the renderer draws with.
/// The one place the names in a <c>.omat</c> meet the renderer's own fields. It is a plain list
/// rather than anything reflective on purpose: a parameter that is added to
/// <see cref="MaterialFields"/> and not handled here is a parameter that silently does nothing, and
/// a list is the only shape where that shows up as a test failure rather than as a surface that looks
/// slightly wrong.
/// </summary>
public static class MaterialView
{
    // Which map slots hold a colour somebody picked, and therefore arrive sRGB. The other four are
    // measurements, a direction, a roughness, an occlusion, and decoding those as though they were
    // colours bends every one of them.
    private static readonly HashSet<string> ColourMaps = ["baseColour", "emissive", "blendBaseColour"];

    /// <summary>
    /// <paramref name="locate"/> turns a project relative asset path into one the renderer can open.
    /// Anything it cannot place is left out rather than guessed at.
    /// </summary>
    public static RenderMaterial From(ResolvedMaterial resolved, long key, Func<string, string?>? locate = null)
    {
        var read = new Reader(resolved, locate);

        // The renderer's own defaults stand in for anything the chain never set, so that a material
        // which says nothing draws the same as no material at all.
        var fallback = new RenderMaterial { Key = 0 };
        var material = new RenderMaterial
        {
            Key = key,
            Shading = read.Choice("shading", fallback.Shading),
            Blend = read.Choice("blend", fallback.Blend),
            Culling = read.Choice("culling", fallback.Culling),
            DoubleSided = read.Flag("doubleSided", fallback.DoubleSided),
            BaseColour = read.Rgba("baseColour", fallback.BaseColour),
            Metallic = read.Number("metallic", fallback.Metallic),
            Roughness = read.Number("roughness", fallback.Roughness),
            Reflectance = read.Number("reflectance", fallback.Reflectance),
            ClearCoat = read.Number("clearCoat", fallback.ClearCoat),
            ClearCoatRoughness = read.Number("clearCoatRoughness", fallback.ClearCoatRoughness),
            Anisotropy = read.Number("anisotropy", fallback.Anisotropy),
            SheenColour = read.Rgb("sheenColour", fallback.SheenColour),
            SheenRoughness = read.Number("sheenRoughness", fallback.SheenRoughness),
            Wind = read.Air(),
            Emissive = read.Rgb("emissive", fallback.Emissive),
            EmissiveIntensity = read.Number("emissiveIntensity", fallback.EmissiveIntensity),
            AmbientOcclusion = read.Number("ambientOcclusion", fallback.AmbientOcclusion),
            NormalScale = read.Number("normalScale", fallback.NormalScale),
            Tiling = read.Pair("tiling", fallback.Tiling),
            Offset = read.Pair("offset", fallback.Offset),
            MaskThreshold = read.Number("maskThreshold", fallback.MaskThreshold),
            DepthWrite = read.Flag("depthWrite", fallback.DepthWrite),
            DepthBias = read.Number("depthBias", fallback.DepthBias),
            Wrap = read.Choice("wrap", fallback.Wrap),
            Filter = read.Choice("filter", fallback.Filter),
            ScreenMapped = read.Flag("screenMapped", fallback.ScreenMapped),
            LitScreen = read.Flag("litScreen", fallback.LitScreen),
            BlendMode = read.Choice("blendMode", fallback.BlendMode),
            BlendAmount = read.Number("blendAmount", fallback.BlendAmount),
            BlendSharpness = read.Number("blendSharpness", fallback.BlendSharpness),
            BaseColourMap = read.Map("baseColour"),
            NormalMap = read.Map("normal"),
            MetallicRoughnessMap = read.Map("metallicRoughness"),
            OcclusionMap = read.Map("occlusion"),
            EmissiveMap = read.Map("emissive"),
            BlendBaseColourMap = read.Map("blendBaseColour"),
            BlendMaskMap = read.Map("blendMask"),
        };

        return read.Blended(material);
    }

    private sealed class Reader(ResolvedMaterial resolved, Func<string, string?>? locate)
    {
        public T Choice<T>(string name, T fallback)
            where T : struct, Enum => Values.Named<T>(resolved.Choice(name)) ?? fallback;

        public bool Flag(string name, bool fallback) => resolved.Flag(name) ?? fallback;

        public double Number(string name, double fallback) => resolved.Number(name) ?? fallback;

        public Vector4 Rgba(string name, Vector4 fallback) => resolved.Numbers(name) is { } n ? new Vector4(n[0], n[1], n[2], n[3]) : fallback;

        public Vector3 Rgb(string name, Vector3 fallback) => resolved.Numbers(name) is { } n ? new Vector3(n[0], n[1], n[2]) : fallback;

        public Vector2 Pair(string name, Vector2 fallback) => resolved.Numbers(name) is { } n ? new Vector2(n[0], n[1]) : fallback;

        // Still air unless the material says otherwise, and stated as one value so that a material
        // setting only a speed still gets the default strength.
        public Wind Air()
        {
            var bearing = resolved.Number("windBearing");
            var speed = resolved.Number("windSpeed");
            var strength = resolved.Number("windStrength");
            return bearing is null && speed is null && strength is null
                ? Orblit.Filament.Wind.None
                : new Wind { Bearing = bearing ?? 0, Speed = speed ?? 0, Strength = strength ?? 1 };
        }

        public Texture? Map(string slot)
        {
            if (!resolved.Maps.TryGetValue(slot, out var path))
            {
                return null;
            }

            var found = locate is null ? path : locate(path);
            return found is null ? null : new Texture(found, ColourMaps.Contains(slot));
        }

        // The second surface follows the first until the material says otherwise, so stating
        // nothing must leave it following. Setting it from the default would pin it.
        public RenderMaterial Blended(RenderMaterial material)
        {
            if (resolved.Numbers("blendTiling") is { } tiling)
            {
                material = material with { BlendTiling = new Vector2(tiling[0], tiling[1]) };
            }

            return resolved.Numbers("blendOffset") is { } offset
                ? material with { BlendOffset = new Vector2(offset[0], offset[1]) }
                : material;
        }
    }
}
