using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Shell;

/// <summary>
/// The strip that heads a class in the inspector: the class icon, a title in plain words and the class name beside
/// it in small dim mono, on a tone under the panel with a line over it. It runs past the 6 pixel gutter to both
/// edges of the dock, so it reads as a heading and not a button.
/// </summary>
internal sealed class ClassBar : Border
{
    const double BarHeight = 34;

    readonly TextBlock _title = new() { FontWeight = FontWeight.SemiBold, FontSize = 12.5, VerticalAlignment = VerticalAlignment.Center };
    readonly EditorIcon _icon;
    readonly TextBlock _engine = new() { FontFamily = EditorTheme.Mono, FontSize = 10.5, Foreground = EditorBrushes.Unit, Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };

    /// <summary>A bar for the class, titled in plain words. A title that is the class name shows it once.</summary>
    public ClassBar(string className, string? title = null)
    {
        Height = BarHeight;
        // The bar sits 4 pixels under what came before and 2 over its first group.
        Margin = new Thickness(-6, 4, -6, 2);
        Padding = new Thickness(12, 0);
        Background = EditorBrushes.ClassBar;
        BorderBrush = EditorBrushes.Line;
        BorderThickness = new Thickness(0, 1, 0, 0);
        _title.Text = title ?? className;
        _engine.Text = className;
        _engine.IsVisible = _title.Text != className;
        _title.TextTrimming = TextTrimming.CharacterEllipsis;
        ToolTip.SetTip(this, _title.Text);
        _icon = Parts.RowIcon(EditorIcons.ForClass(className));
        DockPanel.SetDock(_icon, Dock.Left);
        Child = new DockPanel { Children = { _icon, new NamePair(_title, new Panel(), _engine) } };
    }

    internal string Title => _title.Text ?? "";

    /// <summary>The class name beside the title, or empty when the title is the class name.</summary>
    internal string Engine => _engine.IsVisible ? _engine.Text ?? "" : "";

    internal string IconName => _icon.IconName;
}
