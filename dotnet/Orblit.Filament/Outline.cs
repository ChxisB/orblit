namespace Orblit.Filament;

/// <summary>
/// What becomes of the parts of an outlined object that something else hides. An object selected
/// behind a wall should still be findable, and it should be plain that it is behind the wall.
/// </summary>
public enum OutlineOccluded
{
    /// <summary>Drawn like the visible edge. The cheapest style, since nothing has to be worked out.</summary>
    Shown,

    /// <summary>Drawn at <see cref="Outline.OccludedOpacity"/>.</summary>
    Faint,

    /// <summary>Faint, and broken into dashes <see cref="Outline.Dash"/> pixels long.</summary>
    Dashed,

    /// <summary>Not drawn: only the visible edge is outlined.</summary>
    Hidden,
}

/// <summary>
/// A line drawn round the silhouettes of some of the scene's objects, which is how an editor
/// shows its selection. It is drawn after the frame is finished, so the colour lands on the
/// screen as given and never enters a temporal history. One object may be the primary, drawn in
/// <see cref="PrimaryColour"/>. It costs nothing while <see cref="Keys"/> is empty and
/// <see cref="Primary"/> is null. Only scene objects can be outlined.
/// </summary>
public sealed record Outline
{
    /// <summary>Floats the settings take in the message.</summary>
    public const int Stride = 14;

    /// <summary>The widest outline drawn, in pixels. Both passes look a fixed distance in the worst case, so this bounds the cost.</summary>
    public const double MaxWidth = 16;

    /// <summary>A deep orange for the selection.</summary>
    public static readonly DisplayColour DefaultColour = DisplayColour.FromArgb(0xFFF15800);

    /// <summary>A lighter orange for the active object.</summary>
    public static readonly DisplayColour DefaultPrimaryColour = DisplayColour.FromArgb(0xFFFFAA40);

    /// <summary>Nothing outlined.</summary>
    public static readonly Outline None = new();

    /// <summary>The objects outlined, by key, in the order given. Keys the scene lacks are ignored.</summary>
    public IReadOnlyList<long> Keys { get; init; } = [];

    /// <summary>The active object. Outlined whether or not it is also in <see cref="Keys"/>.</summary>
    public long? Primary { get; init; }

    public DisplayColour Colour { get; init; } = DefaultColour;

    public DisplayColour PrimaryColour { get; init; } = DefaultPrimaryColour;

    /// <summary>In pixels of the frame, up to <see cref="MaxWidth"/>. A fraction gives the outer pixel partial cover.</summary>
    public double Width { get; init; } = 2;

    public OutlineOccluded Occluded { get; init; } = OutlineOccluded.Dashed;

    /// <summary>How opaque a hidden edge is, nought to one, for the faint and dashed styles.</summary>
    public double OccludedOpacity { get; init; } = 0.4;

    /// <summary>How long a dash is, in pixels, for the dashed style.</summary>
    public double Dash { get; init; } = 6;

    /// <summary>Whether this outlines nothing, and so costs nothing.</summary>
    public bool IsEmpty => Keys.Count == 0 && Primary is null;

    /// <summary>
    /// The keys as they travel: the active one first, then the rest, each once. The renderer needs
    /// only the count of active keys at the front, and a count is one number where a flag per key
    /// is a second array.
    /// </summary>
    internal long[] PackedKeys
    {
        get
        {
            var active = Primary;
            var rest = Keys.Where(key => key != active).Distinct();
            return [.. active is { } first ? rest.Prepend(first) : rest];
        }
    }

    /// <summary>
    /// The settings: the selection's colour and the active object's as RGBA, the width, the hidden
    /// style, its opacity, the dash length, whether a key at the front is active, and one spare.
    /// </summary>
    internal float[] Packed => Packing.Floats(
        Colour.R, Colour.G, Colour.B, Colour.A,
        PrimaryColour.R, PrimaryColour.G, PrimaryColour.B, PrimaryColour.A,
        Math.Clamp(Width, 0, MaxWidth),
        (int)Occluded,
        Math.Clamp(OccludedOpacity, 0, 1),
        Dash < 0 ? 0 : Dash,
        Primary is null ? 0 : 1,
        0);
}
