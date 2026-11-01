namespace Orblit.Filament;

/// <summary>
/// The questions <see cref="Renderer.GetCapability"/> answers. Each is asked
/// once, when the renderer starts, because none of the answers changes
/// mid-session.
/// </summary>
public enum Capability
{
    /// <summary>The <see cref="Filament.Backend"/> actually drawing, with <see cref="Backend.Default"/> resolved.</summary>
    Backend = 0,

    /// <summary>Filament's feature level the device supports, 0 to 3. Below 3 the renderer draws the slim lit surface.</summary>
    FeatureLevel,

    /// <summary>Texels on a side of the largest 2D texture.</summary>
    MaxTextureSize,

    /// <summary>Layers in the largest array texture.</summary>
    MaxArrayTextureLayers,

    /// <summary><see cref="FormatFamily"/> bits: which block-compressed families can be sampled.</summary>
    CompressedFormats,

    /// <summary>1 if an RGBA16F texture can be sampled and mipmapped.</summary>
    HalfFloatTextures,

    /// <summary>Threads work can spread across: the cores, or 1 without threading.</summary>
    WorkerThreads,

    /// <summary>Physical memory in megabytes, or 0 where the platform will not say.</summary>
    SystemMemoryMegabytes,
}

/// <summary>
/// Block-compressed texture families, as <see cref="Capability.CompressedFormats"/>
/// spells them. A family counts only when its linear and sRGB forms are both there.
/// </summary>
[Flags]
public enum FormatFamily
{
    Etc2 = 1 << 0,
    Astc = 1 << 1,
    Bc13 = 1 << 2,
    Bc45 = 1 << 3,
    Bc6H = 1 << 4,
    Bc7 = 1 << 5,
}
