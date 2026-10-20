using Orblit.VectorMath;

namespace Orblit.Terrain;

/// <summary>
/// What covers one texel of ground: which texture sets, how they mix, how they lie, and whether
/// the ground is there at all. Thirty-two bits, the same thirty-two the cover map stores and the
/// shader reads.
/// <code>
/// 31 ... 27  base set        0-31
/// 26 ... 22  overlay set     0-31
/// 21 ... 14  blend           0-255, how much of the overlay shows
/// 13 ... 10  angle           0-15, sixteenths of a turn
///  9 ...  7  scale           a code, see Scale
///  6 ...  3  (unused, kept zero)
///         2  hole
///         1  navigation
///         0  automatic
/// </code>
/// A value, not an object: it is a <c>uint</c> wearing names, so a map of a million of them is a
/// <c>uint[]</c> and nothing more. Every change hands back a new one.
/// </summary>
public readonly record struct Cover(uint Word)
{
    /// <summary>The most sets a terrain can have. Ids run 0 to 31.</summary>
    public const int SetCount = 32;

    /// <summary>How many steps the blend has between all base and all overlay.</summary>
    public const int BlendSteps = 255;

    /// <summary>How many steps a whole turn of the angle has.</summary>
    public const int AngleSteps = 16;

    /// <summary>Plain ground under set 0, placed by hand.</summary>
    public static readonly Cover Plain = new(0);

    /// <summary>
    /// Ground whose sets are chosen by slope and height, see <see cref="AutoCover"/>. What a new
    /// region is covered with, so untouched terrain is not one texture.
    /// </summary>
    public static readonly Cover Auto = new(1);

    /// <summary>
    /// The scales a cover can take, in percent: how much bigger than its set's own size a texture
    /// lies. Negative is smaller.
    /// </summary>
    public static IReadOnlyList<int> Scales { get; } = [0, 20, 40, 60, 80, -60, -40, -20];

    /// <summary>The set underneath.</summary>
    public int Base => (int)((Word >> 27) & 0x1F);

    /// <summary>The set laid over <see cref="Base"/>, showing by <see cref="Blend"/>.</summary>
    public int Overlay => (int)((Word >> 22) & 0x1F);

    /// <summary>How much of <see cref="Overlay"/> shows, 0 to <see cref="BlendSteps"/>.</summary>
    public int BlendStep => (int)((Word >> 14) & 0xFF);

    /// <summary>How much of <see cref="Overlay"/> shows, 0 to 1.</summary>
    public double Blend => (double)BlendStep / BlendSteps;

    /// <summary>How far the textures are turned, in sixteenths of a turn.</summary>
    public int Angle => (int)((Word >> 10) & 0xF);

    /// <summary>How far the textures are turned, anticlockwise seen from above.</summary>
    public double Radians => Angle * (2 * Math.PI / AngleSteps);

    /// <summary>The scale as it is stored: an index into <see cref="Scales"/>.</summary>
    public int ScaleCode => (int)((Word >> 7) & 0x7);

    /// <summary>How much bigger than its set's own size the texture lies, in percent.</summary>
    public int Scale => Scales[ScaleCode];

    /// <summary>
    /// What a texture coordinate is multiplied by for <see cref="Scale"/>: a texture lying twenty
    /// percent bigger is read at 0.8.
    /// </summary>
    public double UvMultiplier => 1 - (Scale / 100.0);

    /// <summary>
    /// No ground here. The triangles that touch this texel are not drawn, and nothing stands on
    /// them.
    /// </summary>
    public bool Hole => ((Word >> 2) & 1) == 1;

    /// <summary>Whether a navigation build should treat this texel as walkable ground.</summary>
    public bool Navigation => ((Word >> 1) & 1) == 1;

    /// <summary>
    /// Whether the sets here come from slope and height rather than from <see cref="Base"/>,
    /// <see cref="Overlay"/> and <see cref="Blend"/>.
    /// </summary>
    public bool Automatic => (Word & 1) == 1;

    /// <summary>
    /// A cover from its parts. Out-of-range ids and steps wrap into their bits rather than
    /// spilling into the next field; a <paramref name="blend"/> is clamped to 0-1 and a
    /// <paramref name="scale"/> snaps to the nearest of <see cref="Scales"/>.
    /// </summary>
    public static Cover Of(
        int @base = 0,
        int overlay = 0,
        double blend = 0,
        int angle = 0,
        int scale = 0,
        bool hole = false,
        bool navigation = false,
        bool automatic = false) => new(
            ((uint)(@base & 0x1F) << 27) |
            ((uint)(overlay & 0x1F) << 22) |
            ((uint)BlendStepOf(blend) << 14) |
            ((uint)(angle & 0xF) << 10) |
            ((uint)ScaleCodeOf(scale) << 7) |
            (hole ? 4u : 0u) |
            (navigation ? 2u : 0u) |
            (automatic ? 1u : 0u));

    public Cover WithBase(int @base) => WithField(27, 0x1F, @base);

    public Cover WithOverlay(int overlay) => WithField(22, 0x1F, overlay);

    public Cover WithBlend(double blend) => WithField(14, 0xFF, BlendStepOf(blend));

    public Cover WithAngle(int angle) => WithField(10, 0xF, angle);

    public Cover WithScale(int scale) => WithField(7, 0x7, ScaleCodeOf(scale));

    public Cover WithHole(bool hole) => WithField(2, 1, hole ? 1 : 0);

    public Cover WithNavigation(bool navigation) => WithField(1, 1, navigation ? 1 : 0);

    public Cover WithAutomatic(bool automatic) => WithField(0, 1, automatic ? 1 : 0);

    private Cover WithField(int shift, int mask, int value) =>
        new((Word & ~((uint)mask << shift)) | ((uint)(value & mask) << shift));

    private static int BlendStepOf(double blend) =>
        (int)Doubles.Round(Doubles.Clamp(blend, 0.0, 1.0) * BlendSteps);

    // The code for the nearest of Scales. Steps of twenty, so the code is the step count taken
    // round eight: 20 is 1, -20 is 7.
    private static int ScaleCodeOf(int percent)
    {
        var step = (int)Doubles.Round(Math.Clamp(percent, -60, 80) / 20.0);
        return ((step % 8) + 8) % 8;
    }
}
