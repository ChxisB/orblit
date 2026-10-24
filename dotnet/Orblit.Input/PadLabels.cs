namespace Orblit.Input;

/// <summary>What is printed on the four face buttons. Positions never change, and the letters do.</summary>
public sealed class PadLabels(string south, string east, string west, string north)
{
    public static PadLabels Xbox { get; } = new("A", "B", "X", "Y");

    public static PadLabels PlayStation { get; } = new("Cross", "Circle", "Square", "Triangle");

    public static PadLabels Nintendo { get; } = new("B", "A", "Y", "X");

    public string South { get; } = south;

    public string East { get; } = east;

    public string West { get; } = west;

    public string North { get; } = north;

    /// <summary>The label for a face button, or the button's own name for any other.</summary>
    public string Of(PadButton button) => button switch
    {
        PadButton.South => South,
        PadButton.East => East,
        PadButton.West => West,
        PadButton.North => North,
        _ => EnumNames.Of(button),
    };
}
