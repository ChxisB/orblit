using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Shell;

/// <summary>The rounded bar that names a class in the inspector, with that class's icon, centred.</summary>
internal sealed class ClassBar : Border
{
    const double BarHeight = 30;

    readonly ContentControl _icon = new() { VerticalAlignment = VerticalAlignment.Center };
    readonly TextBlock _title = new() { FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center };

    public ClassBar(string className = "")
    {
        Height = BarHeight;
        // The bar sits 4 pixels under what came before and 2 over its first group.
        Margin = new Thickness(0, 4, 0, 2);
        Background = EditorBrushes.ClassBar;
        CornerRadius = EditorTheme.Corners;
        Child = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
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
