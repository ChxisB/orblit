namespace Orblit.Editor.App.Shell;

/// <summary>The three places a dock can sit: the left column over and under, and the right column.</summary>
internal enum DockSlot
{
    UpperLeft,
    LowerLeft,
    Right,
}

internal static class DockSlotNames
{
    public static string Label(this DockSlot slot) => slot switch
    {
        DockSlot.UpperLeft => "Left column, top",
        DockSlot.LowerLeft => "Left column, bottom",
        _ => "Right column",
    };
}
