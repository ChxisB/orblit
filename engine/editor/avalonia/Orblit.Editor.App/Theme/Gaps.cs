namespace Orblit.Editor.App.Theme;

/// <summary>
/// The one spacing scale, on a 4 pixel step. A margin, a padding or a gap takes one of these, or 0. A number of its
/// own is only for placing something by geometry, with a comment that says what it lines up with.
/// </summary>
internal static class Gaps
{
    /// <summary>Between the parts of a joined group, such as tabs.</summary>
    public const double Joined = 2;

    /// <summary>Between an icon and its word, and inside a small mark.</summary>
    public const double Tight = 4;

    /// <summary>Between items.</summary>
    public const double Item = 8;

    /// <summary>Between groups, and inside panels. The narrow side docks pad Item, so names keep their room.</summary>
    public const double Group = 12;

    /// <summary>Inside dialogs.</summary>
    public const double Dialog = 16;

    /// <summary>Between the sections of a dialog or a page.</summary>
    public const double Section = 24;
}
