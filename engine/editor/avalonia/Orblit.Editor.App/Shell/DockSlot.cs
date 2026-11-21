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
        DockSlot.UpperLeft => "top",
        DockSlot.LowerLeft => "bottom",
        _ => "right",
    };
}
