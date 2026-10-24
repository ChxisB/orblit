namespace Orblit.Filament;

/// <summary>How a shadow's edge is worked out.</summary>
public enum ShadowKind
{
    /// <summary>
    /// A fixed pattern of samples around each pixel. The edge is as soft as the pattern is wide
    /// and no softer, wherever the caster is. It is the cheapest, and good for a scene that is
    /// mostly sunlight.
    /// </summary>
    Sharp,

    /// <summary>
    /// The penumbra grows with the gap between the caster and what it lands on, which is what a
    /// real shadow does: sharp where a chair leg meets the floor and soft under the seat. It draws
    /// the same picture as <see cref="Area"/> on Filament 1.76. This asks Filament for the
    /// contact hardening filter, whose own header marks it deprecated and falls it back to the
    /// other one. The two kinds came out byte identical across 1.92 million pixels. It stays a
    /// name of its own because it says what is wanted and not which technique happens to serve it.
    /// </summary>
    Soft,

    /// <summary>
    /// The penumbra is worked out from how big the light actually is, so a strip light and a bare
    /// bulb do not cast the same shadow. It is the dearest of the four, and the only one where the
    /// light's size means anything.
    /// </summary>
    Area,

    /// <summary>
    /// Shadows kept as a distribution and not a depth, so they can be blurred and filtered like any
    /// other texture. Very soft, very cheap to filter, and prone to light leaking through thin walls.
    /// </summary>
    Variance,
}

/// <summary>
/// Everything about shadows that is not a property of one light. Holding a shadow map still
/// between frames is not here. Filament redraws every map every frame, and the machinery that
/// would keep one is inside a header that is not public, so a scene of a hundred static lights
/// pays for a hundred maps every frame. It would have to be Filament's change.
/// </summary>
public sealed record ShadowSettings
{
    /// <summary>
    /// The one switch. Off is not shadow maps drawn cheaply. It is no shadow pass at all, which on
    /// a scene of any size is the largest saving available.
    /// </summary>
    public bool Enabled { get; init; } = true;

    public ShadowKind Kind { get; init; } = ShadowKind.Sharp;

    /// <summary>How many pixels across the shadow map is. Doubling it quarters the smallest thing a shadow can show and quadruples what it costs.</summary>
    public int MapSize { get; init; } = 1024;

    /// <summary>
    /// How many maps the distance is divided between, so that things near the camera get a map to
    /// themselves. It is the most effective shadow setting there is. One cascade over a hundred
    /// metres puts about a centimetre in each pixel, and four cascades put a millimetre in the first.
    /// </summary>
    public int Cascades { get; init; } = 2;

    /// <summary>How far shadows are drawn, in metres. Nought means as far as the camera sees, which is often much further than anybody needs.</summary>
    public double Distance { get; init; }

    /// <summary>
    /// How the cascades are spread between near and far, from evenly at nought to logarithmically
    /// at one. Half is the usual compromise, and what a perspective camera wants.
    /// </summary>
    public double Lambda { get; init; } = 0.5;

    /// <summary>How far a surface is pushed away from its own shadow along the light. Too little and it shadows itself in stripes.</summary>
    public double ConstantBias { get; init; } = 0.001;

    /// <summary>How far a surface is pushed away from its own shadow along its normal. Too much and a shadow floats away from what casts it.</summary>
    public double NormalBias { get; init; } = 1.0;

    /// <summary>
    /// Whether the shadow map is locked to the world and not to the camera. It is steadier, because
    /// an edge stops crawling as the camera moves, at the cost of resolution, because a map that
    /// cannot rotate with the camera has to be big enough for every angle.
    /// </summary>
    public bool Stable { get; init; }

    /// <summary>
    /// Whether short range shadows are traced in screen space as well. It catches what a shadow
    /// map cannot: the contact between a foot and the ground. It leaves a grain of its own on a
    /// large flat floor seen at a grazing angle far from the camera, where the march finds the
    /// floor itself. That is Filament's artefact and shortening the contact distance does not
    /// remove it, so this is off by default and worth turning on for a close, cluttered scene
    /// and not for an open one.
    /// </summary>
    public bool Contact { get; init; }

    /// <summary>
    /// How wide the penumbra is, for <see cref="ShadowKind.Soft"/> and <see cref="ShadowKind.Area"/>.
    /// It is a multiplier over the physical answer and not a size. One is what the light's own
    /// radius says and everything else is a mood.
    /// </summary>
    public double Softness { get; init; } = 1.0;

    /// <summary>
    /// Where each cascade hands over to the next, as rising fractions of the distance, one fewer
    /// than the cascades. Null lets the lambda place them. It is worth setting by hand when the
    /// scene says where the detail is. A list that does not rise, or is too short, is ignored and
    /// not half used.
    /// </summary>
    public IReadOnlyList<double>? Splits { get; init; }

    /// <summary>
    /// For <see cref="ShadowKind.Area"/>, how much faster the penumbra widens with the gap between
    /// caster and receiver. One is physical. Above it exaggerates the contact hardening and below
    /// it evens the edge out.
    /// </summary>
    public double SoftnessFalloff { get; init; } = 1.0;

    /// <summary>How far a contact shadow is traced from each pixel, in metres. Longer finds more.</summary>
    public double ContactDistance { get; init; } = 0.3;

    /// <summary>How many steps a contact shadow takes. Fewer steps miss thin things.</summary>
    public int ContactSteps { get; init; } = 8;

    /// <summary>The settings only <see cref="ShadowKind.Variance"/> reads.</summary>
    public VarianceShadows Variance { get; init; } = new();

    // The renderer's block lays these out in three runs with the resolution and the lighting
    // between them, so each run is its own member.

    internal float[] PackedBlock => Packing.Floats(
        Packing.Flag(Enabled), (int)Kind, MapSize, Cascades, Distance, Lambda, ConstantBias, NormalBias, Flags, Softness);

    // Nought where no split is given, which the renderer reads as let the lambda place them. A
    // split at nought would be a cascade of no depth.
    internal float[] PackedSplits => Packing.Floats(Split(0), Split(1), Split(2));

    internal float[] PackedTail => Packing.Floats(
        SoftnessFalloff, Variance.Anisotropy, Variance.Blur, Variance.LightBleedReduction, Variance.Samples,
        ContactDistance, ContactSteps);

    private int Flags =>
        (Stable ? 1 : 0)
        | (Contact ? 2 : 0)
        | (Variance.HighPrecision ? 4 : 0)
        | (Variance.Mipmapping ? 8 : 0)
        | (Variance.Exponential ? 16 : 0);

    private double Split(int i) => Splits?.ElementAtOrDefault(i) ?? 0;
}

/// <summary>
/// How variance shadow maps are blurred and filtered. A variance map keeps the mean and the spread
/// of depth and not the depth itself, which lets it be blurred like a picture and makes it leak
/// light through a thin wall, because a spread says nothing about what is in the middle of it.
/// Every dial here is a trade between those two.
/// </summary>
public sealed record VarianceShadows
{
    /// <summary>How wide the blur is, in texels. Nought is none.</summary>
    public double Blur { get; init; }

    /// <summary>Anisotropic filtering of the map as a power of two. Nought is off, and each step doubles how steep a surface can be before its shadow smears.</summary>
    public int Anisotropy { get; init; }

    /// <summary>Multisampling of the map itself: one, two or four.</summary>
    public int Samples { get; init; } = 1;

    /// <summary>How much of a leak is cut away, nought to one. Higher removes light bleeding through thin walls and hardens every edge with it.</summary>
    public double LightBleedReduction { get; init; } = 0.15;

    // No floor under the variance is exposed. Filament 1.76 marks its minimum variance scale
    // deprecated and says outright that it has no effect, and a dial that moves and changes
    // nothing is worse than no dial.

    /// <summary>Thirty-two bits a channel and not sixteen. Fewer artefacts over a long shadow distance, at twice the memory.</summary>
    public bool HighPrecision { get; init; }

    /// <summary>Whether the map has mipmaps, so a distant shadow is filtered and not aliased.</summary>
    public bool Mipmapping { get; init; }

    /// <summary>Exponential variance: fewer leaks, at a second pair of channels.</summary>
    public bool Exponential { get; init; }
}
