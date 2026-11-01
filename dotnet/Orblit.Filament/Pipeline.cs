namespace Orblit.Filament;

/// <summary>
/// How much of everything a machine is asked to do. These are not different pipelines. There is
/// one pipeline and these are settings of it, so a scene that looks right on a laptop is the same
/// scene, drawn the same way, with smaller numbers. Nothing appears or disappears between tiers,
/// so nothing has to be authored twice.
/// </summary>
public enum Detail
{
    /// <summary>Enough to be playable on anything. One shadow cascade at half the map size, no multisampling, and a frame allowed to shrink to hold its rate.</summary>
    Low,

    /// <summary>The default. Two cascades, a kilobyte map and a fixed resolution.</summary>
    Medium,

    /// <summary>Three cascades, soft shadows and four samples of multisampling.</summary>
    High,

    /// <summary>Everything on and nothing shrinking. Four cascades at four kilobytes, area shadows and a high precision colour buffer.</summary>
    Ultra,
}

/// <summary>
/// How many pixels are actually drawn before the frame is shown. It is the most direct dial there
/// is, because drawing three quarters as many pixels is drawing three quarters as much.
/// </summary>
public sealed record Resolution
{
    /// <summary>A fixed fraction of the view. One is every pixel.</summary>
    public double Scale { get; init; } = 1.0;

    /// <summary>
    /// Whether the renderer is allowed to move the scale between the minimum and the maximum to
    /// hold its frame rate. This is asked for and not yet granted. The renderer pins the scale at
    /// the maximum and draws every frame at that size, because a scale that moves between frames
    /// leaves most of the buffer undrawn. Measured on Metal, seven frames in nine had the scene
    /// squeezed into a corner and the rest of the buffer showing whatever was in that memory.
    /// Filament only varies the scale where the time a frame takes can be measured accurately, and
    /// no frame rate is aimed at yet. So this is a request the renderer may refuse, and not a
    /// switch. To trade sharpness for headroom today, give a scale below one, which is honoured
    /// exactly.
    /// </summary>
    public bool Adaptive { get; init; }

    /// <summary>The low end of the range adaptive would move between. Until it can, it is unused.</summary>
    public double MinScale { get; init; } = 0.5;

    /// <summary>The high end of the range adaptive would move between, and the size every frame is drawn at until then.</summary>
    public double MaxScale { get; init; } = 1.0;

    /// <summary>How much the upscale is sharpened on the way back. Nought is a plain stretch.</summary>
    public double Sharpness { get; init; } = 0.9;

    // A fixed scale is the adaptive one with nowhere to go, which is one path in the renderer
    // and not two that have to agree.
    internal float[] Packed => Packing.Floats(
        Packing.Flag(Adaptive), Adaptive ? MinScale : Scale, Adaptive ? MaxScale : Scale, Sharpness);
}

/// <summary>
/// How the renderer works out which lights reach a pixel. The view is cut into cells running away
/// from the camera, each light is put in the cells it reaches, and a pixel consults its own cell,
/// so a lamp at the far end of a corridor costs nothing at this end. The cells are fine near the
/// camera and coarse far away, and these two numbers anchor that. A grid that stops at a hundred
/// metres in a scene whose lights are two hundred metres out puts every distant light in one cell.
/// </summary>
public sealed record Lighting
{
    /// <summary>Where the cells start, in metres from the camera. Nearer than this is one cell.</summary>
    public double ClusterNear { get; init; } = 5;

    /// <summary>Where they stop, in metres. Everything beyond is one cell, so this wants to be about as far as the furthest light that matters.</summary>
    public double ClusterFar { get; init; } = 100;

    internal float[] Packed => Packing.Floats(ClusterNear, ClusterFar);
}

/// <summary>
/// How large textures are loaded, and how fast they reach the GPU. Both default to the device's
/// own, which the renderer works out from the same measurements the device profile is made of.
/// </summary>
public sealed record TextureLimits
{
    /// <summary>
    /// The widest or tallest a texture is loaded at, in texels. A texture with mipmaps larger than
    /// this leaves out its largest levels until it fits and never uploads them, and a picture
    /// without any is halved as it is decoded. Textures already loaded keep the size they were
    /// loaded at. Null is the device's own.
    /// </summary>
    public int? MaxSize { get; init; }

    /// <summary>
    /// How much texture data a frame may hand the GPU, in kilobytes. Uploading is work the frame
    /// waits for, so spread under this, four hundred textures arrive over several frames, the
    /// smallest levels of each first. A frame always uploads at least one level, however large, or
    /// a level larger than this would never go. Null is the device's own, which is measured and
    /// follows what frames cost while textures arrive. A number is held exactly.
    /// </summary>
    public int? UploadKilobytes { get; init; }

    // Nought is the device's own, which the renderer works out itself.
    internal float[] Packed => Packing.Floats(Math.Clamp(MaxSize ?? 0, 0, 1 << 16), Math.Clamp(UploadKilobytes ?? 0, 0, 1 << 24));
}

/// <summary>
/// The screen a frame is being drawn for. Only what the display itself settles, which is less than
/// a quality preset. How many pixels there are, and what shape they are in, are facts about the
/// panel, and how hard to push the machine behind it is not.
/// </summary>
/// <param name="Width">The panel's width in pixels.</param>
/// <param name="Height">The panel's height in pixels.</param>
/// <param name="Detail">Which of the four named settings suits it.</param>
public sealed record Display(int Width, int Height, Detail Detail = Detail.Medium)
{
    /// <summary>
    /// A Steam Deck's own screen. It is 1280 by 800, which is 16 by 10 and not the 16 by 9 almost
    /// everything assumes, and a little over a million pixels.
    /// </summary>
    public static Display Handheld => new(1280, 800);

    /// <summary>
    /// Width over height. Worth reading and not assuming, because a camera framed for 16 by 9 and
    /// shown on a 16 by 10 panel is either stretched or cropped.
    /// </summary>
    public double Aspect => Height == 0 ? 1 : (double)Width / Height;

    /// <summary>How many pixels a frame covers.</summary>
    public long Pixels => (long)Width * Height;
}

/// <summary>
/// How a frame gets drawn. One pipeline and not a choice of them. Every frame goes the same way:
/// shadow maps, then the depth the whole frame is sorted and occluded by, then the opaque objects
/// lit, then the sky behind what is left, then everything see through back to front, then the
/// image work. The order is not negotiable, because a renderer that lets it be negotiated has to
/// be correct for every ordering and can therefore optimise for none of them. What is negotiable
/// is how much of each step happens, which is what this describes. <see cref="Detail"/> gives
/// four named settings of those dials, and everything can also be set by hand. There is no way to
/// add a pass on purpose. That is left open until there is a real second user for it, because a
/// plugin surface invented before anybody has plugged anything into it is a guess.
/// </summary>
public sealed record Pipeline
{
    /// <summary>
    /// Floats <see cref="Packed"/> holds. The renderer names every offset in its shadows header
    /// and a test compares this number against the one there, because a block a float short
    /// reads a nought where a dial should be, and for the contact distance that is a shadow
    /// traced nowhere.
    /// </summary>
    public const int Stride = 30;

    public ShadowSettings Shadows { get; init; } = new();

    public Resolution Resolution { get; init; } = new();

    public Lighting Lighting { get; init; } = new();

    public TextureLimits Textures { get; init; } = new();

    /// <summary>
    /// How many samples an edge is worked out from, before any of the image work happens. One is
    /// none. This runs while the frame is drawn and costs memory bandwidth on everything, and the
    /// anti aliasing in the post process runs on the finished image and costs almost nothing.
    /// Multisampling is sharper where they disagree, which is why both exist.
    /// </summary>
    public int Samples { get; init; } = 1;

    /// <summary>
    /// Whether the frame is kept at sixteen bits a channel and not eleven. It is worth it for a
    /// scene with a very bright light and a very dark shadow in the same shot, where the cheaper
    /// format bands in the falloff. It costs half as much again in bandwidth for every pass.
    /// </summary>
    public bool Precise { get; init; }

    /// <summary>Whether what the camera cannot see is skipped. Off only to find out whether a missing object was culled or never there.</summary>
    public bool Culling { get; init; } = true;

    /// <summary>Whether see through surfaces bend what is behind them.</summary>
    public bool Refraction { get; init; } = true;

    /// <summary>The pipeline at one of the four named settings.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Maintainability", "CA1502", Justification = "F2: a list of cases is data.")]
    public static Pipeline At(Detail detail) => detail switch
    {
        Detail.Low => new Pipeline
        {
            Shadows = new ShadowSettings { MapSize = 512, Cascades = 1, Distance = 40 },
            Resolution = new Resolution { Adaptive = true, MinScale = 0.5 },
        },
        Detail.Medium => new Pipeline { Shadows = new ShadowSettings { MapSize = 1024, Cascades = 2, Distance = 80 } },
        Detail.High => new Pipeline
        {
            Shadows = new ShadowSettings { Kind = ShadowKind.Soft, MapSize = 2048, Cascades = 3, Contact = true },
            Samples = 4,
        },
        Detail.Ultra => new Pipeline
        {
            Shadows = new ShadowSettings { Kind = ShadowKind.Area, MapSize = 4096, Cascades = 4, Contact = true, Stable = true },
            Samples = 4,
            Precise = true,
        },
        _ => throw new ArgumentOutOfRangeException(nameof(detail), detail, "Not a named setting."),
    };

    /// <summary>
    /// The pipeline at a named setting, with the dials a display's own size decides moved to suit
    /// it. Only three change, because only three depend on how many pixels there are and not on
    /// how fast the machine is. The shadow map is matched to the frame: a map is only as useful as
    /// the screen pixels it is stretched over, so above a megapixel and a half the named setting
    /// is left alone, and below it the map comes down one step and the cascades with it. The floor
    /// under adaptive resolution rises to 0.7, because half scale on a 1280 by 800 panel is
    /// visibly soft. Multisampling comes off, because the machines that drive panels this size are
    /// the ones whose memory bandwidth it spends. It does not measure the machine or target a
    /// frame rate, and it offers no rung between the four settings.
    /// </summary>
    public static Pipeline ForDisplay(Display display)
    {
        var pipeline = At(display.Detail);
        if (display.Pixels > 1500000)
        {
            return pipeline;
        }

        var shadows = pipeline.Shadows;
        return pipeline with
        {
            Shadows = shadows with
            {
                MapSize = Math.Clamp(shadows.MapSize / 2, 512, 4096),
                Cascades = shadows.Cascades > 2 ? shadows.Cascades - 1 : shadows.Cascades,
            },
            Samples = 1,
            Resolution = pipeline.Resolution.Adaptive ? pipeline.Resolution with { MinScale = 0.7 } : pipeline.Resolution,
        };
    }

    /// <summary>Every number, in the order the renderer reads them.</summary>
    internal float[] Packed =>
    [
        .. Shadows.PackedBlock,
        .. Resolution.Packed,
        .. Packing.Floats(Samples, (Precise ? 1 : 0) + (Culling ? 2 : 0) + (Refraction ? 4 : 0)),
        .. Lighting.Packed,
        .. Shadows.PackedSplits,
        .. Shadows.PackedTail,
        .. Textures.Packed,
    ];
}
