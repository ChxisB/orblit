using Avalonia.Media;

namespace Orblit.Editor.App.Theme;

/// <summary>The editor's icons by file name, generated from <c>engine/editor/icons</c>.</summary>
internal static partial class EditorIcons
{
    // The toast and dialog marks are drawn on the design's 24 pixel grid, so they are added here and not generated.
    // A static constructor runs after the generated table is filled.
    static EditorIcons()
    {
        s_icons["Tick"] = Line("M5 12.5l4.5 4.5L19 7.5");
        s_icons["InfoCircle"] = Line("M12 3.5a8.5 8.5 0 1 0 0 17a8.5 8.5 0 1 0 0-17zM12 11v5.5M12 7.8h.01");
        s_icons["FaultCircle"] = Line("M12 3.5a8.5 8.5 0 1 0 0 17a8.5 8.5 0 1 0 0-17zM9 9l6 6M15 9l-6 6");
        s_icons["WarnTriangle"] = Line("M12 4l9 16H3zM12 10v4.5M12 17.2h.01");
        s_icons["Cube"] = Line("M12 3l8 4.5v9L12 21l-8-4.5v-9zM4 7.5l8 4.5 8-4.5M12 12v9");
        s_icons["Square"] = Line("M5.5 5.5h13v13h-13z");
        s_icons["Ring"] = Line("M12 6a6 6 0 1 0 0 12a6 6 0 1 0 0-12z");
        s_icons["Lens"] = Line("M10.5 4a6.5 6.5 0 1 0 0 13a6.5 6.5 0 1 0 0-13zM20 20l-4.8-4.8");
        s_icons["SceneFile"] = Line("M4 9.5h16v10H4zM4 9.5l1.5-5h13l1.5 5M9 4.5l-1.5 5M14.5 4.5l-1.5 5");
        s_icons["FolderLine"] = Line("M3.5 7A1.5 1.5 0 0 1 5 5.5h4l2 2h8A1.5 1.5 0 0 1 20.5 9v8.5A1.5 1.5 0 0 1 19 19H5a1.5 1.5 0 0 1-1.5-1.5z");
        s_icons["CodeFile"] = Line("M9 7.5L4.5 12 9 16.5M15 7.5l4.5 4.5-4.5 4.5");
        s_icons["GraphFile"] = Line("M4 5h6v5H4zM14 14h6v5h-6zM10 7.5h2.5a2 2 0 0 1 2 2V14");
        s_icons["ImportArrow"] = Line("M12 4v11M7.5 10.5L12 15l4.5-4.5M5 19.5h14");
        s_icons["Pulse"] = Line("M3 12h3.5l2.5-6 4 12 2.5-6H21");
        // Ours: the design draws no empty History tab.
        s_icons["Clock"] = Line("M12 3.5a8.5 8.5 0 1 0 0 17a8.5 8.5 0 1 0 0-17zM12 7.5V12l3 2");
        s_icons["FocusFrame"] = Line("M4 9V4h5M15 4h5v5M20 15v5h-5M9 20H4v-5M12 10.5a1.5 1.5 0 1 0 0 3a1.5 1.5 0 1 0 0-3z");
        s_icons["Book"] = Line("M4 5.5h5.5A2.5 2.5 0 0 1 12 8v11a2 2 0 0 0-2-2H4zM20 5.5h-5.5A2.5 2.5 0 0 0 12 8v11a2 2 0 0 1 2-2h6z");
        s_icons["Triangle"] = Line("M12 5l8 14H4z");
        s_icons["ResetTurn"] = Line("M4.5 12a7.5 7.5 0 1 0 2.2-5.3M4.5 4v4h4");
        s_icons["Past"] = Line("M4 12a8 8 0 1 0 2.4-5.7M4 4v4h4M12 8v4l3 2");
        s_icons["Bin"] = Line("M5 7h14M10 7V4.5h4V7M7 7l1 13h8l1-13");
        s_icons["StepArrow"] = Line("M5 12h14M13 6l6 6-6 6");
    }

    static IconDrawing Line(string path) =>
        new(24, 24, [new(path) { Stroke = IconShape.Themed, StrokeWidth = 1.75, Cap = PenLineCap.Round, Join = PenLineJoin.Round }]);

    public static bool Has(string name) => s_icons.ContainsKey(name);

    /// <summary>The icon a class draws. A class with no icon of its own draws the one of Entity.</summary>
    public static string ForClass(string className) => Has(className) ? className : "Entity";

    public static IconDrawing Get(string name) =>
        s_icons.TryGetValue(name, out var drawing) ? drawing : throw new ArgumentException($"No icon is named {name}.", nameof(name));
}
