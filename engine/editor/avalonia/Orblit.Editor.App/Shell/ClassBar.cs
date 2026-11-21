using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Shell;

/// <summary>
/// The strip that heads a class in the inspector: the class icon and name on a tone under the panel, with a line
/// over it. It runs past the 6 pixel gutter to both edges of the dock, so it reads as a heading and not a button.
/// </summary>
internal sealed class ClassBar : Border
{
    const double BarHeight = 34;

    readonly ContentControl _icon = new() { VerticalAlignment = VerticalAlignment.Center };
    readonly TextBlock _title = new() { FontWeight = FontWeight.SemiBold, FontSize = 12.5, VerticalAlignment = VerticalAlignment.Center };

    public ClassBar(string className = "")
    {
        Height = BarHeight;
        // The bar sits 4 pixels under what came before and 2 over its first group.
        Margin = new Thickness(-6, 4, -6, 2);
        Padding = new Thickness(12, 0);
        Background = EditorBrushes.ClassBar;
        BorderBrush = EditorBrushes.Line;
        BorderThickness = new Thickness(0, 1, 0, 0);
        Child = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Children = { _icon, _title },
        };
        Show(className);
    }

    internal string Title => _title.Text ?? "";

    internal string IconName => ((EditorIcon)_icon.Content!).IconName;

    public void Show(string className)
    {
        _title.Text = className;
        _icon.Content = Parts.RowIcon(EditorIcons.ForClass(className));
    }
}
