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
    }

    static IconDrawing Line(string path) =>
        new(24, 24, [new(path) { Stroke = IconShape.Themed, StrokeWidth = 1.75, Cap = PenLineCap.Round, Join = PenLineJoin.Round }]);

    public static bool Has(string name) => s_icons.ContainsKey(name);

    /// <summary>The icon a class draws. A class with no icon of its own draws the one of Entity.</summary>
    public static string ForClass(string className) => Has(className) ? className : "Entity";

    public static IconDrawing Get(string name) =>
        s_icons.TryGetValue(name, out var drawing) ? drawing : throw new ArgumentException($"No icon is named {name}.", nameof(name));
}
