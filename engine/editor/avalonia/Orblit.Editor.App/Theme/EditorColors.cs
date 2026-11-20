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

    // The fill of a worded button that is off, a step over the panel, so it reads as a button but not as one to press.
    public static readonly Color DisabledFill = Color.Parse("#14171D");

    // The one accent. Its soft step fills a lit tool and a chosen row, so accent text on it stays readable.
    public static readonly Color Accent = Color.Parse("#E5893F");
    public static readonly Color AccentSoft = Color.Parse("#29E5893F");

    public static readonly Color AxisX = Color.Parse("#FF6B78");
    public static readonly Color AxisY = Color.Parse("#8BD450");
    public static readonly Color AxisZ = Color.Parse("#5AA2FF");

    public static readonly Color Warn = Color.Parse("#F2B33D");

    // A job that went through and one that did not. A danger button takes the fault in a lighter text and a faint edge.
    public static readonly Color Done = Color.Parse("#4CC38A");
    public static readonly Color Fault = Color.Parse("#FF5C6C");
    public static readonly Color FaultText = Color.Parse("#FF8A96");
    public static readonly Color FaultEdge = Color.Parse("#55FF5C6C");

    // The ring round a field with a fault, as the soft accent rings a focused one.
    public static readonly Color FaultSoft = Color.Parse("#22FF5C6C");

    // The colour of a 3D class and a 2D class where a dialog marks which kind a root or a base is.
    public static readonly Color Spatial = Color.Parse("#F27D8A");
    public static readonly Color Planar = Color.Parse("#6FA8FF");

    // A scene file where a list mixes it with other rows, such as the command palette.
    public static readonly Color SceneIcon = Color.Parse("#F0A365");

    // Text on an accent fill. White on the orange reads too thin.
    public static readonly Color OnAccent = Color.Parse("#1B0F06");

    // A key cap on an accent button. A grey cap would dull the orange, so this darkens what is under it.
    public static readonly Color Shade = Color.Parse("#1F000000");

    // The shade over the editor behind a dialog. The design draws none, so this one is ours.
    public static readonly Color Veil = Color.Parse("#8C05070A");

    // The tree icons keep the colour they are drawn in, and a folder is that colour times the accent.
    public static readonly Color TreeIcon = Color.Parse("#C9CFD8");
    public static readonly Color FolderIcon = Color.Parse("#B56F35");

    // The lines that join a tree row to its parent. The lit ones follow the path to the selected row.
    public static readonly Color Guide = Line;
    public static readonly Color GuideStub = LineStrong;
    public static readonly Color GuideLit = Color.Parse("#3A414C");
}
