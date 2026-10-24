using Orblit.VectorMath;

namespace Orblit.Filament;

/// <summary>
/// How the image is turned from light into pixels. A renderer works in light, with values far
/// above one, because the sun is thousands of times brighter than a lamp and both are in the same
/// frame. A screen takes numbers between nought and one. Tone mapping decides how the first
/// becomes the second, and it is the setting that most decides whether a scene looks like a
/// photograph or like a game.
/// </summary>
public enum ToneMapping
{
    /// <summary>The one to use. Keeps colour in the highlights instead of letting everything bright slide to white.</summary>
    Filmic,

    /// <summary>The academy's transform. More contrast and saturation than filmic, and what most films are graded through.</summary>
    Aces,

    /// <summary>The academy's transform with the worst of its hue shifts taken out.</summary>
    AcesLegacy,

    /// <summary>Straight through, clipped at one. For looking at what the renderer produced and not at what it looks like.</summary>
    Linear,

    /// <summary>Nothing at all, not even the clip. For debugging a value.</summary>
    None,
}

/// <summary>How the jagged edges are dealt with.</summary>
public enum AntiAliasing
{
    /// <summary>None. Every edge is a staircase.</summary>
    Off,

    /// <summary>One cheap pass over the finished image. It costs almost nothing and softens the picture slightly.</summary>
    Fxaa,

    /// <summary>Samples spread across frames. The best looking of the three, and the one that smears if something moves quickly.</summary>
    Temporal,
}

/// <summary>
/// Light spilling out of the bright parts of the image. It is what a real lens does with light it
/// cannot contain, and without it a bright thing on screen is only as bright as white.
/// </summary>
public sealed record Bloom
{
    public bool Enabled { get; init; }

    /// <summary>How much of it is added back, from nothing to all bloom and no image.</summary>
    public double Strength { get; init; } = 0.1;

    /// <summary>How far the glow spreads, as the number of times the image is halved. More is a wider, softer spill.</summary>
    public int Levels { get; init; } = 6;

    /// <summary>
    /// Whether only what is brighter than white blooms. On, a lamp glows and a white wall does
    /// not. Off, everything glows a little, which is a look and not a simulation.
    /// </summary>
    public bool Threshold { get; init; } = true;

    /// <summary>The streaks and ghosts a real lens adds around a bright light.</summary>
    public bool LensFlare { get; init; }

    public double FlareStrength { get; init; } = 0.02;

    internal float[] Packed =>
        Packing.Floats(Packing.Flag(Enabled), Strength, Levels, Packing.Flag(Threshold), Packing.Flag(LensFlare), FlareStrength);
}

/// <summary>
/// What is out of focus. A camera has one distance in focus, and everything else is blurred by
/// how far it is from that. The strength is the aperture, which is on the camera itself, so this
/// is only whether it is applied.
/// </summary>
public sealed record DepthOfField
{
    public bool Enabled { get; init; }

    /// <summary>How far away the sharp plane is, in metres.</summary>
    public double FocusDistance { get; init; } = 10;

    /// <summary>How much bigger the blur is than the physical answer. One is the lens, and more is a film.</summary>
    public double BlurScale { get; init; } = 1;

    /// <summary>A cap on how blurred the near half gets, in pixels of circle of confusion.</summary>
    public double MaxForeground { get; init; } = 1;

    /// <summary>A cap on how blurred the far half gets, which stops a background dissolving.</summary>
    public double MaxBackground { get; init; } = 1;

    internal float[] Packed =>
        Packing.Floats(Packing.Flag(Enabled), FocusDistance, BlurScale, MaxForeground, MaxBackground);
}

/// <summary>The corners going dark.</summary>
public sealed record Vignette
{
    public bool Enabled { get; init; }

    /// <summary>Where the darkening starts, from the middle out.</summary>
    public double MidPoint { get; init; } = 0.5;

    /// <summary>Round or rectangular. Nought is the shape of the screen and one is a circle.</summary>
    public double Roundness { get; init; } = 0.5;

    /// <summary>How soft the edge of the darkening is.</summary>
    public double Feather { get; init; } = 0.5;

    /// <summary>What the corners go towards. Black almost always.</summary>
    public Vector3 Colour { get; init; } = Vector3.Zero();

    internal float[] Packed =>
        Packing.Floats(Packing.Flag(Enabled), MidPoint, Roundness, Feather, Colour.X, Colour.Y, Colour.Z);
}

/// <summary>
/// Contact shadows: the darkening where two surfaces meet. It tells somebody a box is standing on
/// the floor and not floating a centimetre above it.
/// </summary>
public sealed record Occlusion
{
    public bool Enabled { get; init; }

    /// <summary>
    /// How far from a point the renderer looks for something blocking the light, in metres. Small
    /// is creases, and large is whole rooms getting darker.
    /// </summary>
    public double Radius { get; init; } = 0.3;

    /// <summary>How dark it goes.</summary>
    public double Strength { get; init; } = 1;

    /// <summary>
    /// How far a surface has to be from itself before it counts as blocking itself. It stops a flat
    /// wall shadowing its own texture.
    /// </summary>
    public double Bias { get; init; } = 0.0005;

    /// <summary>Nought to three: how many samples. Three is worth it on a still shot and not on a game.</summary>
    public int Quality { get; init; } = 1;

    /// <summary>Whether the occlusion also bends the direction light arrives from, so a crease reads as a crease and not a smudge.</summary>
    public bool BentNormals { get; init; }

    internal float[] Packed =>
        Packing.Floats(Packing.Flag(Enabled), Radius, Strength, Bias, Quality, Packing.Flag(BentNormals));
}

/// <summary>
/// Reflections worked out from what is already on screen. They are cheap next to anything that
/// traces rays, and limited in exactly the way that implies: a puddle reflects the wall behind it
/// and not the sky above the camera.
/// </summary>
public sealed record Reflections
{
    public bool Enabled { get; init; }

    /// <summary>
    /// How thick the renderer assumes everything is, since a depth buffer says where a surface is
    /// and not how deep it goes.
    /// </summary>
    public double Thickness { get; init; } = 0.1;

    public double Bias { get; init; } = 0.01;

    /// <summary>How far a reflection is followed, in metres.</summary>
    public double MaxDistance { get; init; } = 3;

    /// <summary>How many pixels a step covers. Bigger is faster and blockier.</summary>
    public double Stride { get; init; } = 2;

    internal float[] Packed =>
        Packing.Floats(Packing.Flag(Enabled), Thickness, Bias, MaxDistance, Stride);
}

/// <summary>
/// The colour the whole image is pushed towards. Grading is where a scene stops looking like data
/// and starts looking like it was shot on something. The settings are the ones a colourist uses.
/// </summary>
public sealed record Grading
{
    public bool Enabled { get; init; }

    /// <summary>
    /// How light becomes pixels. It applies whether or not the rest of the grading does, because
    /// something has to decide and filmic is a better default than a clip.
    /// </summary>
    public ToneMapping ToneMapping { get; init; } = ToneMapping.Filmic;

    /// <summary>In stops. One is twice the light.</summary>
    public double Exposure { get; init; }

    public double Contrast { get; init; } = 1;

    public double Saturation { get; init; } = 1;

    /// <summary>Saturation that leaves what is already colourful alone, which keeps skin from going orange when a scene is pushed.</summary>
    public double Vibrance { get; init; } = 1;

    /// <summary>White balance. Negative is cooler and positive is warmer.</summary>
    public double Temperature { get; init; }

    /// <summary>The other axis of white balance. Negative is magenta and positive is green.</summary>
    public double Tint { get; init; }

    /// <summary>A multiplier for each channel in the darkest range of the image.</summary>
    public Vector3 Shadows { get; init; } = Vector3.All(1);

    /// <summary>A multiplier for each channel in the middle range of the image.</summary>
    public Vector3 Midtones { get; init; } = Vector3.All(1);

    /// <summary>A multiplier for each channel in the brightest range of the image.</summary>
    public Vector3 Highlights { get; init; } = Vector3.All(1);

    internal float[] Packed => Packing.Floats(
        Packing.Flag(Enabled), (int)ToneMapping, Exposure, Contrast, Saturation, Vibrance, Temperature, Tint,
        Shadows.X, Shadows.Y, Shadows.Z,
        Midtones.X, Midtones.Y, Midtones.Z,
        Highlights.X, Highlights.Y, Highlights.Z);
}

/// <summary>
/// Everything done to the image after the scene is drawn. One value and not a dozen settings
/// scattered over the scene, because these are decisions about the picture and not about what is
/// in it, and because a look somebody arrives at is a thing they want to save and copy to another
/// scene. Everything is off by default, since a renderer that arrives with bloom on is one where
/// the first question is how to turn it off.
/// </summary>
public sealed record PostProcess
{
    /// <summary>Floats <see cref="Packed"/> holds.</summary>
    public const int Stride = 49;

    /// <summary>The one switch over all of it, for comparing against the raw image.</summary>
    public bool Enabled { get; init; } = true;

    public AntiAliasing AntiAliasing { get; init; } = AntiAliasing.Fxaa;

    public Bloom Bloom { get; init; } = new();

    public DepthOfField DepthOfField { get; init; } = new();

    public Vignette Vignette { get; init; } = new();

    public Occlusion Occlusion { get; init; } = new();

    public Reflections Reflections { get; init; } = new();

    public Grading Grading { get; init; } = new();

    /// <summary>
    /// Whether a little noise is added to hide banding. It is on by default, because eight bits a
    /// channel is not enough for a smooth gradient and the banding is the most visible fault in a
    /// dark scene.
    /// </summary>
    public bool Dithering { get; init; } = true;

    /// <summary>
    /// Every number, in the order the renderer reads them. One flat array and not a map, because
    /// this crosses to native code on every frame.
    /// </summary>
    internal float[] Packed =>
    [
        .. Packing.Floats(Packing.Flag(Enabled), (int)AntiAliasing, Packing.Flag(Dithering)),
        .. Bloom.Packed,
        .. DepthOfField.Packed,
        .. Vignette.Packed,
        .. Occlusion.Packed,
        .. Reflections.Packed,
        .. Grading.Packed,
    ];
}
