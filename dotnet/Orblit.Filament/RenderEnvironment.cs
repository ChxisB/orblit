namespace Orblit.Filament;

/// <summary>
/// The light a place brings with it, and the backdrop it is seen against. A photograph of
/// somewhere real, turned into light: a chrome ball reflects the room it stands in, a matte
/// surface picks up the colour of the wall beside it, and a shadow is filled by the sky. Once it
/// is built it costs nothing per frame, because the lighting is a cube map that is sampled. A
/// value with many fields, so it is changed with <c>with</c>.
/// </summary>
public sealed record RenderEnvironment
{
    /// <summary>Floats the environment occupies in the packed array.</summary>
    public const int Stride = 4;

    /// <summary>Nothing: the scene is lit by its lights and its flat ambient alone.</summary>
    public static readonly RenderEnvironment None = new();

    /// <summary>
    /// Where the light comes from: a prefiltered radiance cube map baked ahead of time, or an
    /// equirectangular picture to filter here. The blurrier levels are the reflection and the
    /// spherical harmonics are the diffuse light.
    /// </summary>
    public string? Radiance { get; init; }

    /// <summary>
    /// The backdrop: a baked cube map or a picture, drawn sharper than the reflections. Separate
    /// from <see cref="Radiance"/> because the backdrop is what the camera sees and the radiance is
    /// what surfaces sample. One without the other is legitimate.
    /// </summary>
    public string? Skybox { get; init; }

    /// <summary>How bright the place is, in lux, the unit every other light is stated in.</summary>
    public double Intensity { get; init; } = 30000;

    /// <summary>How far the environment is turned about the vertical, in radians.</summary>
    public double Rotation { get; init; }

    /// <summary>Whether the backdrop is drawn as well as sampled.</summary>
    public bool ShowSkybox { get; init; } = true;

    /// <summary>
    /// The side of the reflection cube map a picture is filtered at, in texels, or nought for the
    /// device's own choice. Baked cube maps are the size they were baked at, so it does not apply.
    /// </summary>
    public int Size { get; init; }

    /// <summary>Whether either image is named. An empty path names nothing.</summary>
    public bool IsSet => !string.IsNullOrEmpty(Radiance) || !string.IsNullOrEmpty(Skybox);

    /// <summary>
    /// The light and the backdrop of one equirectangular picture, filtered at run time with no
    /// bake. The path is named as both the radiance and the skybox. The other settings are changed
    /// with <c>with</c>.
    /// </summary>
    public static RenderEnvironment FromImage(string image) => new() { Radiance = image, Skybox = image };

    internal float[] Packed => Packing.Floats(Intensity, Rotation, Packing.Flag(ShowSkybox), Size);
}
