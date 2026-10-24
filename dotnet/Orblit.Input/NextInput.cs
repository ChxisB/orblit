namespace Orblit.Input;

/// <summary>What <see cref="NextInput"/> needs to see of the devices.</summary>
public interface IPressedControls
{
    /// <summary>
    /// Every control pressed or pushed halfway or more now. Keys come first, then pad buttons,
    /// sticks, pad axes and touch regions, so that pushing a stick is read as the stick before it
    /// is read as one of its axes. An axis is listed as the half that was pushed.
    /// </summary>
    IEnumerable<Control> Active { get; }
}

/// <summary>
/// Waits for the player to press something, for a rebinding screen. Whatever is already down when
/// it starts is ignored until it comes up, so the key that opened the screen is not taken as the
/// answer. A stick axis is taken as the half the player pushed, so "steer left" gets the left push.
/// </summary>
public sealed class NextInput(ActionKind kind)
{
    private readonly HashSet<Control> _ignored = [];
    private bool _started;

    /// <summary>What the answer has to suit.</summary>
    public ActionKind Kind { get; } = kind;

    /// <summary>The first suitable control pressed since it started. Null until then.</summary>
    public Control? Captured { get; private set; }

    /// <summary>Looks at one more frame. Does nothing once something is captured.</summary>
    public void Update(IPressedControls source)
    {
        if (Captured is not null)
        {
            return;
        }

        var active = source.Active.ToList();
        if (!_started)
        {
            _started = true;
            _ignored.UnionWith(active);
            return;
        }

        _ignored.IntersectWith(active);
        Captured = active.FirstOrDefault(control => !_ignored.Contains(control) && Kind.Accepts(control));
    }
}
