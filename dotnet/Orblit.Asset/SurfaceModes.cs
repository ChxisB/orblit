namespace Orblit.Asset;

/// <summary>What the <c>render_mode</c> lines asked for, as the material file keys.</summary>
internal sealed class SurfaceModes
{
    // Each mode sets every field of its family, so the last mode of a family wins whichever order they are written in.
    private static readonly Dictionary<string, Action<SurfaceModes>> Known = new()
    {
        ["blend_mix"] = m =>
        {
            m.Blending = "transparent";
            m.Premultiply = true;
        },
        ["blend_add"] = m =>
        {
            m.Blending = "add";
            m.Premultiply = true;
        },
        ["blend_mul"] = m =>
        {
            m.Blending = "multiply";
            m.Premultiply = false;
        },
        ["blend_premul_alpha"] = m =>
        {
            m.Blending = "transparent";
            m.Premultiply = false;
        },
        ["cull_back"] = m =>
        {
            m.Culling = "back";
            m.DoubleSided = false;
        },
        ["cull_front"] = m =>
        {
            m.Culling = "front";
            m.DoubleSided = false;
        },
        ["cull_disabled"] = m =>
        {
            m.Culling = "none";
            m.DoubleSided = true;
        },
        ["depth_draw_opaque"] = m => m.DepthWrite = null,
        ["depth_draw_always"] = m => m.DepthWrite = true,
        ["depth_draw_never"] = m => m.DepthWrite = false,
        ["depth_test_default"] = m => m.DepthCulling = null,
        ["depth_test_disabled"] = m => m.DepthCulling = false,
        ["unshaded"] = m => m.Unshaded = true,
    };

    /// <summary>Filament's <c>blending</c> value.</summary>
    public string Blending { get; private set; } = "opaque";

    /// <summary>Whether the colour is multiplied by its alpha before it is written, which Filament's blending modes expect.</summary>
    public bool Premultiply { get; private set; }

    public bool Unshaded { get; private set; }

    public string? Culling { get; private set; }

    public bool DoubleSided { get; private set; }

    public bool? DepthWrite { get; private set; }

    public bool? DepthCulling { get; private set; }

    /// <summary>Adds one mode, or says it is not one this translator has.</summary>
    public void Add(string mode, SurfaceOrigin at)
    {
        if (!Known.TryGetValue(mode, out var apply))
        {
            throw new SurfaceException(at, $"render mode `{mode}` is not supported");
        }

        apply(this);
    }
}
