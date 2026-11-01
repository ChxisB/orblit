namespace Orblit.Input;

/// <summary>
/// The edges and gestures of one button action across frames. Time comes in with each call, so a
/// replay or a test sets the clock.
/// </summary>
internal sealed class ButtonTrack
{
    // False from the moment the action is cut off until its control is seen up. A key still held
    // when a menu closes would otherwise press the action the player never meant to press.
    private bool _armed = true;
    private bool _holdSent;
    private bool _doublePress;
    private TimeSpan _downAt = TimeSpan.Zero;
    private TimeSpan? _tapEnded;

    internal bool IsDown { get; private set; }

    /// <summary>Went down in the last advance.</summary>
    internal bool WasPressed { get; private set; }

    /// <summary>Came up in the last advance, or was cut off while down.</summary>
    internal bool WasReleased { get; private set; }

    /// <summary>Passed the hold time in the last advance. True once per press.</summary>
    internal bool WasHeld { get; private set; }

    /// <summary>Came up in the last advance after a short press.</summary>
    internal bool WasTapped { get; private set; }

    /// <summary>Went down in the last advance soon after a tap came up.</summary>
    internal bool WasDoubleTapped { get; private set; }

    /// <summary>
    /// Moves to <paramref name="now"/>. <paramref name="changed"/> is true when what the action is
    /// bound to is not what it was last time, which includes a context hiding it. A press in
    /// progress is cut off then and is not a tap. The control may have been held before the binding
    /// appeared, so it has to be seen up before it can press.
    /// </summary>
    internal void Advance(bool down, bool changed, TimeSpan now, Gestures gestures)
    {
        WasPressed = WasReleased = WasHeld = WasTapped = WasDoubleTapped = false;
        if (changed && down)
        {
            _armed = false;
        }
        else if (!down)
        {
            _armed = true;
        }

        var next = down && _armed;
        if (next && !IsDown)
        {
            Begin(now, gestures);
        }
        else if (!next && IsDown)
        {
            End(now, gestures, changed);
        }
        else if (next)
        {
            CheckHold(now, gestures);
        }

        IsDown = next;
    }

    private void Begin(TimeSpan now, Gestures gestures)
    {
        WasPressed = true;
        _downAt = now;
        _holdSent = false;
        _doublePress = _tapEnded is { } ended && now - ended <= gestures.DoubleTap;
        WasDoubleTapped = _doublePress;
    }

    private void End(TimeSpan now, Gestures gestures, bool cut)
    {
        WasReleased = true;
        WasTapped = !cut && now - _downAt <= gestures.Tap;
        // The press that made a double tap does not start another.
        _tapEnded = WasTapped && !_doublePress ? now : null;
    }

    private void CheckHold(TimeSpan now, Gestures gestures)
    {
        if (_holdSent || now - _downAt < gestures.Hold)
        {
            return;
        }

        _holdSent = true;
        WasHeld = true;
    }
}
