namespace Orblit.Editor.App.Shell;

/// <summary>The four places a group of panels can sit: the left and right columns, each over and under.</summary>
internal enum DockSlot
{
    UpperLeft,
    LowerLeft,
    UpperRight,
    LowerRight,
}

internal static class DockSlotNames
{
    public static bool IsLeft(this DockSlot slot) => slot is DockSlot.UpperLeft or DockSlot.LowerLeft;

    // The enum lists each side's two places together, so the other place differs in the lowest bit.
    public static DockSlot Sibling(this DockSlot slot) => (DockSlot)((int)slot ^ 1);

    public static string Label(this DockSlot slot) => slot switch
    {
        DockSlot.UpperLeft => "Left column, top",
        DockSlot.LowerLeft => "Left column, bottom",
        DockSlot.UpperRight => "Right column, top",
        _ => "Right column, bottom",
    };
}
