using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace Orblit.Editor.App.Theme;

/// <summary>The fills that a panel or a strip needs, made once from <see cref="EditorColors"/>.</summary>
internal static class EditorBrushes
{
    public static readonly IBrush Window = new ImmutableSolidColorBrush(EditorColors.Window);
    public static readonly IBrush Panel = new ImmutableSolidColorBrush(EditorColors.Panel);
    public static readonly IBrush Field = new ImmutableSolidColorBrush(EditorColors.Field);
    public static readonly IBrush SelectedRow = new ImmutableSolidColorBrush(EditorColors.SelectedRow);
    public static readonly IBrush Hover = new ImmutableSolidColorBrush(EditorColors.Hover);
    public static readonly IBrush Accent = new ImmutableSolidColorBrush(EditorColors.Accent);
    public static readonly IBrush Text = new ImmutableSolidColorBrush(EditorColors.Text);
    public static readonly IBrush TreeIcon = new ImmutableSolidColorBrush(EditorColors.TreeIcon);
    public static readonly IBrush FolderIcon = new ImmutableSolidColorBrush(EditorColors.FolderIcon);
    public static readonly IBrush TabInactive = new ImmutableSolidColorBrush(EditorColors.TabInactive);
    public static readonly IBrush BarSeparator = new ImmutableSolidColorBrush(EditorColors.BarSeparator);
    public static readonly IBrush OutputDot = new ImmutableSolidColorBrush(EditorColors.OutputDot);
    public static readonly IBrush AxisX = new ImmutableSolidColorBrush(EditorColors.AxisX);
    public static readonly IBrush AxisY = new ImmutableSolidColorBrush(EditorColors.AxisY);
    public static readonly IBrush AxisZ = new ImmutableSolidColorBrush(EditorColors.AxisZ);
    public static readonly IBrush Unit = new ImmutableSolidColorBrush(EditorColors.Unit);
    public static readonly IBrush ClassBar = new ImmutableSolidColorBrush(EditorColors.ClassBar);
    public static readonly IBrush Chip = new ImmutableSolidColorBrush(EditorColors.Chip);
    public static readonly IBrush ActiveTool = new ImmutableSolidColorBrush(EditorColors.ActiveTool);
    public static readonly IBrush Guide = new ImmutableSolidColorBrush(EditorColors.Guide);
    public static readonly IBrush GuideStub = new ImmutableSolidColorBrush(EditorColors.GuideStub);
    public static readonly IBrush GuideLit = new ImmutableSolidColorBrush(EditorColors.GuideLit);
}
