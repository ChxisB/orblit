namespace Orblit.Input;

/// <summary>Which half of a two-axis action a binding feeds.</summary>
public enum AxisPart
{
    X,
    Y,
}

/// <summary>
/// A control wired to an action. The control's value is shaped, then multiplied by
/// <see cref="Scale"/>. A key on the left of a steering axis has a scale of -1.
/// </summary>
public sealed class Binding
{
    public Binding(Control control, double scale = 1, AxisPart part = AxisPart.X, Shaping? shaping = null)
    {
        Control = control;
        Scale = scale;
        Part = part;
        Shaping = shaping ?? Shaping.None;
    }

    public Control Control { get; }

    /// <summary>Multiplies the shaped value. Negative flips the direction.</summary>
    public double Scale { get; }

    /// <summary>
    /// The half of a two-axis action this feeds. Other actions ignore it, and so does a whole
    /// stick, which feeds both.
    /// </summary>
    public AxisPart Part { get; }

    /// <summary>A dead zone and curve on top of any the pad already applied.</summary>
    public Shaping Shaping { get; }

    /// <summary>
    /// This binding on another control. The scale, part and shaping stay, so a player who rebinds
    /// a key does not change which way it steers.
    /// </summary>
    /// <remarks>
    /// A whole stick has no half of its own, so moving to or from one starts again on x. A
    /// both-ways axis carries its own sign, which a key or a half does not, so moving to or from
    /// one starts again at a scale of 1. <paramref name="scale"/> and <paramref name="part"/>
    /// replace what would stay.
    /// </remarks>
    public Binding WithControl(Control control, double? scale = null, AxisPart? part = null)
    {
        var restartsPart = (control is PadStickControl) != (Control is PadStickControl);
        var restartsScale = restartsPart || BothWays(control) != BothWays(Control);
        return new Binding(
            control,
            scale ?? (restartsScale ? 1 : Scale),
            part ?? (restartsPart ? AxisPart.X : Part),
            Shaping);
    }

    private static bool BothWays(Control control) => control is PadAxisControl { Side: AxisSide.Both };
}
