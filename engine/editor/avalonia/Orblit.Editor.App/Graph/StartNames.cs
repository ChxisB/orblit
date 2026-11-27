namespace Orblit.Editor.App.Graph;

/// <summary>A virtual a graph can start from, in plain words: what happens, when it runs, and the group it sits in.</summary>
internal sealed record StartName(string Engine, string Sentence, string Line, string Group);

/// <summary>
/// The plain words of the starts people use most. A virtual with no row here goes by its engine name, under All
/// starts, A to Z.
/// </summary>
internal static class StartNames
{
    // The order is the order of the start menu. A list of cases, so it is data.
    static readonly StartName[] s_rows =
    [
        new("_frame", "Every frame", "About 60 times a second. For movement and timers.", "Common"),
        new("_start", "The game starts", "Once, when the thing is ready.", "Common"),
        new("_enter_stage", "It enters the scene", "When the thing is added.", "Common"),
        new("_exit_stage", "It leaves the scene", "When the thing is removed.", "Common"),
        new("_input", "A key or click happens", "Every key, click and touch.", "Keys and clicks"),
        new("_shortcut_input", "A shortcut is pressed", "Only keys set up as shortcuts.", "Keys and clicks"),
        new("_unhandled_input", "Nothing else used it", "Input that menus and buttons let through.", "Keys and clicks"),
        new("_step", "Every physics step", "A steady 60 times a second. For things that bump.", "Physics"),
        new("_get_configuration_warnings", "Editor warnings", "Shows a warning on this thing in the Scene panel.", "In the editor"),
    ];

    internal static IReadOnlyList<StartName> Rows => s_rows;

    /// <summary>The row for this virtual, or none.</summary>
    internal static StartName? Of(string engine) => s_rows.FirstOrDefault(row => row.Engine == engine);
}
