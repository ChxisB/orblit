namespace Orblit.Input;

/// <summary>
/// The buttons a pad is described in. Named by position, not by what is printed on them, so
/// <see cref="South"/> is the bottom face button on every pad. <see cref="PadLabels"/> says what to call it.
/// </summary>
public enum PadButton
{
    South,
    East,
    West,
    North,
    LeftShoulder,
    RightShoulder,
    LeftTrigger,
    RightTrigger,
    Select,
    Start,
    Guide,
    LeftStick,
    RightStick,
    DpadUp,
    DpadDown,
    DpadLeft,
    DpadRight,
}

/// <summary>The analogue axes. Positive is up and to the right, and a trigger runs from 0 to 1.</summary>
public enum PadAxis
{
    LeftX,
    LeftY,
    RightX,
    RightY,
    LeftTrigger,
    RightTrigger,
}

/// <summary>A pair of axes read together.</summary>
public enum PadStick
{
    Left,
    Right,
}

/// <summary>A trigger, by hand.</summary>
public enum PadSide
{
    Left,
    Right,
}
