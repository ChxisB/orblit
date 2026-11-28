using Avalonia.Media;

namespace Orblit.Editor.App.Theme;

/// <summary>The editor's colors. Every brush and style in the shell is made from these.</summary>
internal static class EditorColors
{
    // Surfaces, darkest first. The ground shows in the gaps between the islands.
    public static readonly Color Window = Color.Parse("#0C0908");
    public static readonly Color Field = Color.Parse("#100C0A");
    public static readonly Color ClassBar = Color.Parse("#130F0C");
    public static readonly Color Panel = Color.Parse("#17120F");

    // The island fill at 90 percent, for the pills that float over the viewport.
    public static readonly Color Floating = Color.Parse("#E617120F");

    public static readonly Color Popover = Color.Parse("#1C1612");
    public static readonly Color Raised = Color.Parse("#221B16");
    public static readonly Color Lifted = Color.Parse("#2F251F");

    // An island's edge and the fill under a pointer are one tone, so a hover reads as the edge coming forward.
    public static readonly Color Line = Color.Parse("#261E19");
    public static readonly Color Hover = Line;
    public static readonly Color LineStrong = Color.Parse("#32271F");

    public static readonly Color Text = Color.Parse("#F3EEEA");
    public static readonly Color TextSoft = Color.Parse("#C4B8AE");
    public static readonly Color TextDim = Color.Parse("#9B8D82");

    public static readonly Color Unit = Color.Parse("#7F7067");
    public static readonly Color Disabled = Color.Parse("#5A4D45");

    // The fill of a worded button that is off, a step over the panel, so it reads as a button but not as one to press.
    public static readonly Color DisabledFill = Color.Parse("#1A1411");

    // The one accent. Its soft step fills a lit tool and a chosen row, so accent text on it stays readable.
    public static readonly Color Accent = Color.Parse("#FF7A00");
    public static readonly Color AccentSoft = Color.Parse("#29FF7A00");

    public static readonly Color AxisX = Color.Parse("#FF6B78");
    public static readonly Color AxisY = Color.Parse("#8BD450");
    public static readonly Color AxisZ = Color.Parse("#5AA2FF");

    public static readonly Color Warn = Color.Parse("#FFC81E");

    // A job that went through and one that did not. A danger button takes the fault in a lighter text and a faint edge.
    public static readonly Color Done = Color.Parse("#4CC38A");
    public static readonly Color DoneSoft = Color.Parse("#1F4CC38A");
    public static readonly Color Fault = Color.Parse("#FF4D6D");
    public static readonly Color FaultText = Color.Parse("#FF8FA3");
    public static readonly Color FaultEdge = Color.Parse("#55FF4D6D");

    // The ring round a field with a fault, as the soft accent rings a focused one.
    public static readonly Color FaultSoft = Color.Parse("#22FF4D6D");

    // The colour of a 3D class and a 2D class where a dialog marks which kind a root or a base is.
    public static readonly Color Spatial = Color.Parse("#F27D8A");
    public static readonly Color Planar = Color.Parse("#6FA8FF");

    // A scene file where a list mixes it with other rows, such as the command palette.
    public static readonly Color SceneIcon = Color.Parse("#F0A365");

    // A C# script and a graph script where a list mixes them with scenes, such as the Files dock.
    public static readonly Color ScriptIcon = Color.Parse("#8DB8FF");
    public static readonly Color GraphIcon = Color.Parse("#7FD4C1");

    // Text on an accent fill. White on the orange reads too thin.
    public static readonly Color OnAccent = Color.Parse("#1A0A00");

    // A key cap on an accent button. A gray cap would dull the orange, so this darkens what is under it.
    public static readonly Color Shade = Color.Parse("#1F000000");

    // The shade over the editor behind a dialog. The design draws none, so this one is ours.
    public static readonly Color Veil = Color.Parse("#8C0A0605");

    // The tree icons keep the colour they are drawn in, and a folder is that colour times the accent.
    public static readonly Color TreeIcon = Color.Parse("#D6CCC4");
    public static readonly Color FolderIcon = Color.Parse("#D66200");

    // The lines that join a tree row to its parent. The lit ones follow the path to the selected row.
    public static readonly Color Guide = Line;
    public static readonly Color GuideStub = LineStrong;
    public static readonly Color GuideLit = Color.Parse("#4A3C33");

    // The rulers of the 2D view. A major tick is as strong as off text, and the labels take the unit colour.
    public static readonly Color Ruler = Color.Parse("#120E0C");
    public static readonly Color RulerEdge = Color.Parse("#2C221C");
    public static readonly Color RulerTick = GuideLit;
    public static readonly Color RulerMajor = Disabled;

    // The grid of the 2D view, with its two world axes in the axis colors at the grid's strength.
    public static readonly Color GridLine = Color.Parse("#662E241E");
    public static readonly Color GridAxisX = Color.Parse("#66FF6B78");
    public static readonly Color GridAxisY = Color.Parse("#668BD450");

    // What fills the 2D view while it says the scene has nothing for it.
    public static readonly Color Stage = Color.Parse("#18130F");

    // The script text area sits deeper than its island, and the line with the caret lifts by a trace of white.
    public static readonly Color Code = Color.Parse("#110D0B");
    public static readonly Color CodeLine = Color.Parse("#08FFFFFF");

    // The colors of C# text. A type shares the graph mark's teal and a function the script mark's blue.
    public static readonly Color CodeKeyword = Color.Parse("#FF9E64");
    public static readonly Color CodeType = GraphIcon;
    public static readonly Color CodeString = Color.Parse("#B5D67A");
    public static readonly Color CodeNumber = Warn;
    public static readonly Color CodeComment = Color.Parse("#6F625A");
    public static readonly Color CodeFunction = ScriptIcon;
    public static readonly Color CodeAttribute = Color.Parse("#C792EA");
}
