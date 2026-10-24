using System.Collections.Frozen;

namespace Orblit.Input;

/// <summary>
/// How one model of pad maps onto <see cref="PadButton"/> and <see cref="PadAxis"/>. The kernel
/// already normalises most pads to one set of codes, so a layout is mostly a name, the labels
/// printed on the buttons, and the odd pad whose axes sit somewhere else.
/// </summary>
public sealed class PadLayout
{
    /// <summary>The d-pad as a hat, one axis for each direction pair. A hat is not an axis here, and reads as four buttons.</summary>
    public const int HatX = 0x10;

    public const int HatY = 0x11;

    public static readonly IReadOnlyDictionary<int, PadButton> StandardButtons = new Dictionary<int, PadButton>
    {
        [0x130] = PadButton.South,
        [0x131] = PadButton.East,
        [0x133] = PadButton.North,
        [0x134] = PadButton.West,
        [0x136] = PadButton.LeftShoulder,
        [0x137] = PadButton.RightShoulder,
        [0x138] = PadButton.LeftTrigger,
        [0x139] = PadButton.RightTrigger,
        [0x13a] = PadButton.Select,
        [0x13b] = PadButton.Start,
        [0x13c] = PadButton.Guide,
        [0x13d] = PadButton.LeftStick,
        [0x13e] = PadButton.RightStick,

        // Some drivers report the d-pad as four buttons rather than as a hat.
        [0x220] = PadButton.DpadUp,
        [0x221] = PadButton.DpadDown,
        [0x222] = PadButton.DpadLeft,
        [0x223] = PadButton.DpadRight,
    }.ToFrozenDictionary();

    public static readonly IReadOnlyDictionary<int, PadAxis> StandardAxes = new Dictionary<int, PadAxis>
    {
        [0x00] = PadAxis.LeftX,
        [0x01] = PadAxis.LeftY,
        [0x03] = PadAxis.RightX,
        [0x04] = PadAxis.RightY,
        [0x02] = PadAxis.LeftTrigger,
        [0x05] = PadAxis.RightTrigger,
    }.ToFrozenDictionary();

    /// <summary>An older pad reports its right stick on the axes a modern one uses for triggers.</summary>
    public static readonly IReadOnlyDictionary<int, PadAxis> SwappedAxes = new Dictionary<int, PadAxis>
    {
        [0x00] = PadAxis.LeftX,
        [0x01] = PadAxis.LeftY,
        [0x02] = PadAxis.RightX,
        [0x05] = PadAxis.RightY,
    }.ToFrozenDictionary();

    // Valve, Microsoft, Sony and Nintendo, keyed by (vendor << 16) | product. The positions are
    // the kernel's, so what a row says is the name and what is printed on the buttons.
    private static readonly FrozenDictionary<long, PadLayout> Table = new Dictionary<long, PadLayout>
    {
        [0x28de1205] = new(name: "Steam Deck"),
        [0x28de11ff] = new(name: "Steam Controller"),
        [0x28de1102] = new(name: "Steam Controller"),
        [0x045e028e] = new(name: "Xbox 360 Controller"),
        [0x045e02d1] = new(name: "Xbox One Controller"),
        [0x045e02ea] = new(name: "Xbox One S Controller"),
        [0x045e0b12] = new(name: "Xbox Series Controller"),
        [0x045e0b13] = new(name: "Xbox Series Controller"),
        [0x054c05c4] = new(name: "DualShock 4", labels: PadLabels.PlayStation),
        [0x054c09cc] = new(name: "DualShock 4", labels: PadLabels.PlayStation),
        [0x054c0ce6] = new(name: "DualSense", labels: PadLabels.PlayStation),
        [0x054c0df2] = new(name: "DualSense Edge", labels: PadLabels.PlayStation),
        [0x057e2009] = new(name: "Switch Pro Controller", labels: PadLabels.Nintendo),
        [0x057e2017] = new(name: "Switch Online SNES Controller", labels: PadLabels.Nintendo),
    }.ToFrozenDictionary();

    // In the order they are tried. "x-box" comes before "xbox" only because it was written so.
    private static readonly (string Part, PadLayout Layout)[] ByName =
    [
        ("steam deck", new(name: "Steam Deck")),
        ("steam controller", new(name: "Steam Controller")),
        ("x-box", new(name: "Xbox Controller")),
        ("xbox", new(name: "Xbox Controller")),
        ("dualsense", new(name: "DualSense", labels: PadLabels.PlayStation)),
        ("dualshock", new(name: "DualShock", labels: PadLabels.PlayStation)),
        ("nintendo", new(name: "Nintendo Controller", labels: PadLabels.Nintendo)),
    ];

    // F2: the Dart named parameters count as the options object.
    public PadLayout(
        string name = "Standard",
        PadLabels? labels = null,
        IReadOnlyDictionary<int, PadButton>? buttons = null,
        IReadOnlyDictionary<int, PadAxis>? axes = null,
        Shaping? sticks = null,
        Shaping? triggers = null)
    {
        Name = name;
        Labels = labels ?? PadLabels.Xbox;
        Buttons = buttons ?? StandardButtons;
        Axes = axes ?? StandardAxes;
        Sticks = sticks ?? Shaping.None;
        Triggers = triggers ?? Shaping.Triggers;
    }

    public string Name { get; }

    public PadLabels Labels { get; }

    /// <summary>Button codes to buttons. A code with no entry is ignored.</summary>
    public IReadOnlyDictionary<int, PadButton> Buttons { get; }

    /// <summary>Axis codes to axes. A code with no entry is ignored.</summary>
    public IReadOnlyDictionary<int, PadAxis> Axes { get; }

    /// <summary>The shaping this layout wants for its sticks. <see cref="Shaping.None"/> means use the one <see cref="Pads"/> was given.</summary>
    public Shaping Sticks { get; }

    public Shaping Triggers { get; }

    /// <summary>
    /// Chooses a layout: by vendor and product when the pad is in the table, then by a word in its
    /// name, then by what its axes say, and otherwise the standard one.
    /// </summary>
    public static PadLayout Of(PadIdentity identity, EvdevCapabilities? capabilities = null)
    {
        if (Table.TryGetValue(((long)identity.Vendor << 16) | (long)identity.Product, out var keyed))
        {
            return keyed;
        }

        var name = Lower(identity.Name);
        foreach (var (part, layout) in ByName)
        {
            if (name.Contains(part, StringComparison.Ordinal))
            {
                return layout;
            }
        }

        // A pad with no right-stick axes but with the two a trigger would use is an older pad
        // putting its right stick there. Decided from what the device says it has rather than
        // from a table, so it is right for a pad nobody has ever seen.
        var has = capabilities?.Axes;
        if (has is not null && !has.Contains(0x03) && !has.Contains(0x04) && has.Contains(0x02) && has.Contains(0x05))
        {
            return new PadLayout(name: "Standard (right stick on Z)", axes: SwappedAxes);
        }

        return new PadLayout();
    }

    public override string ToString() => $"PadLayout({Name})";

    /// <summary>
    /// Lower-cases as Dart does, which folds the dotted capital I to an i and the Kelvin sign to a
    /// k. Which of those <see cref="string.ToLowerInvariant"/> does depends on the runtime's data.
    /// </summary>
    private static string Lower(string text) =>
        text.ToLowerInvariant().Replace((char)0x130, 'i').Replace((char)0x212A, 'k');
}
