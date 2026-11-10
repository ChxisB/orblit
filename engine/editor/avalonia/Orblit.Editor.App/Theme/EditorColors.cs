using Avalonia.Media;

namespace Orblit.Editor.App.Theme;

/// <summary>The editor's colors. Each value was measured on the reference editor, see editor-parity REFERENCE.md.</summary>
internal static class EditorColors
{
    public static readonly Color Window = Color.Parse("#141414");
    public static readonly Color Panel = Color.Parse("#2a2a2a");
    public static readonly Color Field = Color.Parse("#1c1c1c");
    public static readonly Color InspectorField = Color.Parse("#212121");
    public static readonly Color ActiveTool = Color.Parse("#424242");
    public static readonly Color SelectedRow = Color.Parse("#494949");
    public static readonly Color Text = Color.Parse("#c5c5c5");
    public static readonly Color TextDim = Color.Parse("#7a7a7a");
    public static readonly Color Accent = Color.Parse("#559eff");
    // The tree icons keep the colour they are drawn in, and a folder is that colour times the accent.
    public static readonly Color TreeIcon = Color.Parse("#e0e0e0");
    public static readonly Color FolderIcon = Color.Parse("#7ea8e1");
    public static readonly Color TabInactive = Color.Parse("#959595");
    public static readonly Color BarSeparator = Color.Parse("#191919");
    public static readonly Color OutputDot = Color.Parse("#9d8d5c");
    public static readonly Color AxisX = Color.Parse("#7b3d49");
    public static readonly Color AxisY = Color.Parse("#6f8341");
    public static readonly Color AxisZ = Color.Parse("#436488");
    public static readonly Color Unit = Color.Parse("#606060");
    public static readonly Color ClassBar = Color.Parse("#393939");
    public static readonly Color Chip = Color.Parse("#3d3d3d");
    // The lines that join a tree row to its parent. The lit ones follow the path to the selected row.
    public static readonly Color Guide = Color.Parse("#343434");
    public static readonly Color GuideStub = Color.Parse("#3f3f3f");
    public static readonly Color GuideLit = Color.Parse("#545454");

    // Not measured: the reference was captured with the pointer away. Close to the panel so it reads as a nudge.
    public static readonly Color Hover = Color.Parse("#363636");
    public static readonly Color Disabled = Color.Parse("#6e6e6e");
}
