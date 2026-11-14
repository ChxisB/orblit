using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Orblit.Editor.App.Theme;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App.Shell;

/// <summary>
/// The strip over the viewport: one tab for the open scene, a plus, and two buttons at the right end. The tab is
/// raised on a fine edge with its icon in the accent colour, like a chosen dock tab.
/// </summary>
internal sealed class SceneTabStrip : Grid
{
    readonly Border _tab = new()
    {
        Height = 26,
        CornerRadius = EditorTheme.Corners,
        Padding = new Thickness(10, 0, 0, 0),
        Background = EditorBrushes.Raised,
        BorderBrush = EditorBrushes.LineStrong,
        BorderThickness = new Thickness(1),
        VerticalAlignment = VerticalAlignment.Center,
        IsVisible = false,
    };
    readonly ContentControl _icon = new();
    readonly TextBlock _name = new() { VerticalAlignment = VerticalAlignment.Center };
    readonly Button _close = Parts.Tight(Parts.IconButton("GuiClose", "Close the scene"));
    readonly Button _add = Parts.IconButton("Add", "Make a new scene");
    readonly Button _menu = Unbuilt.Mark(Parts.IconButton("GuiTabMenuHl", "Scene tab options"), "The scene tab menu is not built yet.");
    readonly Button _distraction = Unbuilt.Mark(Parts.IconButton("DistractionFree", "Distraction free mode"), "Distraction free mode is not built yet.");

    public SceneTabStrip()
    {
        ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*,Auto");
        Margin = new Thickness(6, 0);
        TextElement.SetForeground(_icon, EditorBrushes.Accent);
        TextElement.SetForeground(_close, EditorBrushes.TextDim);
        _tab.Child = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 7,
            Children = { _icon, _name, _close },
        };
        _add.Margin = new Thickness(4, 0, 0, 0);
        _add.VerticalAlignment = VerticalAlignment.Center;
        var right = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 2,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _distraction, _menu },
        };
        Place(_tab, 0);
        Place(_add, 1);
        Place(right, 3);
    }

    internal Border Tab => _tab;

    internal ContentControl Icon => _icon;

    internal TextBlock Label => _name;

    internal Button CloseButton => _close;

    internal Button AddButton => _add;

    internal Button MenuButton => _menu;

    internal Button DistractionButton => _distraction;

    /// <summary>Shows the open scene as a tab with its root's icon and this label, or no tab for none.</summary>
    public void Show(OutlineNode? root, string label)
    {
        _tab.IsVisible = root is not null;
        if (root is null)
        {
            return;
        }

        // The icon draws to its own size, so a stretch would squash it.
        _icon.Content = new EditorIcon(EditorIcons.ForClass(root.ClassName)) { VerticalAlignment = VerticalAlignment.Center };
        _name.Text = label;
    }

    void Place(Control child, int column)
    {
        SetColumn(child, column);
        Children.Add(child);
    }
}
