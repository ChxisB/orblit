using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace Orblit.Editor.App.Theme;

/// <summary>The fills that a panel or a strip needs, made once from <see cref="EditorColors"/>.</summary>
internal static class EditorBrushes
{
    public static readonly IBrush Window = new ImmutableSolidColorBrush(EditorColors.Window);
    public static readonly IBrush Panel = new ImmutableSolidColorBrush(EditorColors.Panel);
    public static readonly IBrush Floating = new ImmutableSolidColorBrush(EditorColors.Floating);
    public static readonly IBrush Field = new ImmutableSolidColorBrush(EditorColors.Field);
    public static readonly IBrush Popover = new ImmutableSolidColorBrush(EditorColors.Popover);
    public static readonly IBrush Raised = new ImmutableSolidColorBrush(EditorColors.Raised);
    public static readonly IBrush Lifted = new ImmutableSolidColorBrush(EditorColors.Lifted);
    public static readonly IBrush Line = new ImmutableSolidColorBrush(EditorColors.Line);
    public static readonly IBrush LineStrong = new ImmutableSolidColorBrush(EditorColors.LineStrong);
    public static readonly IBrush Hover = new ImmutableSolidColorBrush(EditorColors.Hover);
    public static readonly IBrush Accent = new ImmutableSolidColorBrush(EditorColors.Accent);
    public static readonly IBrush AccentSoft = new ImmutableSolidColorBrush(EditorColors.AccentSoft);
    public static readonly IBrush Text = new ImmutableSolidColorBrush(EditorColors.Text);
    public static readonly IBrush TextSoft = new ImmutableSolidColorBrush(EditorColors.TextSoft);
    public static readonly IBrush TextDim = new ImmutableSolidColorBrush(EditorColors.TextDim);
    public static readonly IBrush Disabled = new ImmutableSolidColorBrush(EditorColors.Disabled);
    public static readonly IBrush DisabledFill = new ImmutableSolidColorBrush(EditorColors.DisabledFill);
    public static readonly IBrush Warn = new ImmutableSolidColorBrush(EditorColors.Warn);
    public static readonly IBrush Done = new ImmutableSolidColorBrush(EditorColors.Done);
    public static readonly IBrush DoneSoft = new ImmutableSolidColorBrush(EditorColors.DoneSoft);
    public static readonly IBrush Fault = new ImmutableSolidColorBrush(EditorColors.Fault);
    public static readonly IBrush FaultText = new ImmutableSolidColorBrush(EditorColors.FaultText);
    public static readonly IBrush FaultEdge = new ImmutableSolidColorBrush(EditorColors.FaultEdge);
    public static readonly IBrush FaultSoft = new ImmutableSolidColorBrush(EditorColors.FaultSoft);
    public static readonly IBrush OnAccent = new ImmutableSolidColorBrush(EditorColors.OnAccent);
    public static readonly IBrush Shade = new ImmutableSolidColorBrush(EditorColors.Shade);
    public static readonly IBrush Spatial = new ImmutableSolidColorBrush(EditorColors.Spatial);
    public static readonly IBrush Planar = new ImmutableSolidColorBrush(EditorColors.Planar);
    public static readonly IBrush Veil = new ImmutableSolidColorBrush(EditorColors.Veil);
    public static readonly IBrush TreeIcon = new ImmutableSolidColorBrush(EditorColors.TreeIcon);
    public static readonly IBrush FolderIcon = new ImmutableSolidColorBrush(EditorColors.FolderIcon);
    public static readonly IBrush SceneIcon = new ImmutableSolidColorBrush(EditorColors.SceneIcon);
    public static readonly IBrush ScriptIcon = new ImmutableSolidColorBrush(EditorColors.ScriptIcon);
    public static readonly IBrush GraphIcon = new ImmutableSolidColorBrush(EditorColors.GraphIcon);
    public static readonly IBrush Unit = new ImmutableSolidColorBrush(EditorColors.Unit);
    public static readonly IBrush ClassBar = new ImmutableSolidColorBrush(EditorColors.ClassBar);
    public static readonly IBrush Guide = new ImmutableSolidColorBrush(EditorColors.Guide);
    public static readonly IBrush GuideStub = new ImmutableSolidColorBrush(EditorColors.GuideStub);
    public static readonly IBrush GuideLit = new ImmutableSolidColorBrush(EditorColors.GuideLit);
    public static readonly IBrush Ruler = new ImmutableSolidColorBrush(EditorColors.Ruler);
    public static readonly IBrush RulerEdge = new ImmutableSolidColorBrush(EditorColors.RulerEdge);
    public static readonly IBrush RulerTick = new ImmutableSolidColorBrush(EditorColors.RulerTick);
    public static readonly IBrush RulerMajor = new ImmutableSolidColorBrush(EditorColors.RulerMajor);
    public static readonly IBrush GridLine = new ImmutableSolidColorBrush(EditorColors.GridLine);
    public static readonly IBrush GridAxisX = new ImmutableSolidColorBrush(EditorColors.GridAxisX);
    public static readonly IBrush GridAxisY = new ImmutableSolidColorBrush(EditorColors.GridAxisY);
    public static readonly IBrush Stage = new ImmutableSolidColorBrush(EditorColors.Stage);
    public static readonly IBrush Code = new ImmutableSolidColorBrush(EditorColors.Code);
    public static readonly IBrush CodeLine = new ImmutableSolidColorBrush(EditorColors.CodeLine);
    public static readonly IBrush CodeKeyword = new ImmutableSolidColorBrush(EditorColors.CodeKeyword);
    public static readonly IBrush CodeType = new ImmutableSolidColorBrush(EditorColors.CodeType);
    public static readonly IBrush CodeString = new ImmutableSolidColorBrush(EditorColors.CodeString);
    public static readonly IBrush CodeNumber = new ImmutableSolidColorBrush(EditorColors.CodeNumber);
    public static readonly IBrush CodeComment = new ImmutableSolidColorBrush(EditorColors.CodeComment);
    public static readonly IBrush CodeFunction = new ImmutableSolidColorBrush(EditorColors.CodeFunction);
    public static readonly IBrush CodeAttribute = new ImmutableSolidColorBrush(EditorColors.CodeAttribute);
}
