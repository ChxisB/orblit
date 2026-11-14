using Avalonia.Media;

namespace Orblit.Editor.App.Theme;

/// <summary>The editor's colors. Each value comes from the Ember Graphite design, decision 280.</summary>
internal static class EditorColors
{
    // Surfaces, darkest first. The ground shows in the gaps between the islands.
    public static readonly Color Window = Color.Parse("#0A0C10");
    public static readonly Color Field = Color.Parse("#0D1015");
    public static readonly Color ClassBar = Color.Parse("#0F1217");
    public static readonly Color Panel = Color.Parse("#12151B");

    // The island fill at 90 percent, for the pills that float over the viewport.
    public static readonly Color Floating = Color.Parse("#E612151B");

    public static readonly Color Popover = Color.Parse("#161A21");
    public static readonly Color Raised = Color.Parse("#1A1E26");
    public static readonly Color Lifted = Color.Parse("#242A35");

    // An island's edge and the fill under a pointer are one tone, so a hover reads as the edge coming forward.
    public static readonly Color Line = Color.Parse("#1E232C");
    public static readonly Color Hover = Line;
    public static readonly Color LineStrong = Color.Parse("#262C37");

    public static readonly Color Text = Color.Parse("#E8EBF0");
    public static readonly Color TextSoft = Color.Parse("#AAB2BF");
    public static readonly Color TextDim = Color.Parse("#858F9D");

    // A panel tab whose panel is not built yet, a step under the dim text.
    public static readonly Color Unbuilt = Color.Parse("#5C6573");
    public static readonly Color Unit = Color.Parse("#6B7482");
    public static readonly Color Disabled = Color.Parse("#4A525F");

    // The one accent. Its soft step fills a lit tool and a chosen row, so accent text on it stays readable.
    public static readonly Color Accent = Color.Parse("#E5893F");
    public static readonly Color AccentSoft = Color.Parse("#29E5893F");

    public static readonly Color AxisX = Color.Parse("#FF6B78");
    public static readonly Color AxisY = Color.Parse("#8BD450");
    public static readonly Color AxisZ = Color.Parse("#5AA2FF");

    public static readonly Color Warn = Color.Parse("#F2B33D");

    // The tree icons keep the colour they are drawn in, and a folder is that colour times the accent.
    public static readonly Color TreeIcon = Color.Parse("#C9CFD8");
    public static readonly Color FolderIcon = Color.Parse("#B56F35");

    // The lines that join a tree row to its parent. The lit ones follow the path to the selected row.
    public static readonly Color Guide = Line;
    public static readonly Color GuideStub = LineStrong;
    public static readonly Color GuideLit = Color.Parse("#3A414C");
}
