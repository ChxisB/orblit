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
        s_icons["FolderLine"] = Line("M3.5 7A1.5 1.5 0 0 1 5 5.5h4l2 2h8A1.5 1.5 0 0 1 20.5 9v8.5A1.5 1.5 0 0 1 19 19H5a1.5 1.5 0 0 1-1.5-1.5z");
    }

    static IconDrawing Line(string path) =>
        new(24, 24, [new(path) { Stroke = IconShape.Themed, StrokeWidth = 1.75, Cap = PenLineCap.Round, Join = PenLineJoin.Round }]);

    public static bool Has(string name) => s_icons.ContainsKey(name);

    /// <summary>The icon a class draws. A class with no icon of its own draws the one of Entity.</summary>
    public static string ForClass(string className) => Has(className) ? className : "Entity";

    public static IconDrawing Get(string name) =>
        s_icons.TryGetValue(name, out var drawing) ? drawing : throw new ArgumentException($"No icon is named {name}.", nameof(name));
}
