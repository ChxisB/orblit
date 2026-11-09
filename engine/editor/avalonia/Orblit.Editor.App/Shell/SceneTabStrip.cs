using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Orblit.Editor.App.Theme;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App.Shell;

/// <summary>
/// The strip over the toolbar: one tab for the open scene, a plus, and two menu buttons at the right end.
/// The tab has the colour of the toolbar, so the two join.
/// </summary>
internal sealed class SceneTabStrip : Grid
{
    readonly Border _tab = new() { Background = EditorBrushes.Panel, IsVisible = false };
    readonly ContentControl _icon = new();
    readonly TextBlock _name = new()
    {
        Foreground = new SolidColorBrush(EditorColors.Accent),
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(0, 0, 4, 0),
    };
    readonly Button _close = Parts.Tight(Parts.IconButton("GuiClose", "Close the scene"));
    readonly Button _add = Parts.Tight(Parts.IconButton("Add", "Make a new scene"));
    readonly Button _menu = Parts.Tight(Unbuilt.Mark(Parts.IconButton("GuiTabMenuHl", "Scene tab options"), "The scene tab menu is not built yet."));
    readonly Button _distraction = Parts.Tight(Unbuilt.Mark(Parts.IconButton("DistractionFree", "Distraction free mode"), "Distraction free mode is not built yet."));

    public SceneTabStrip()
    {
        ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*,Auto");
        _tab.Child = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 5,
            Margin = new Thickness(16, 0, 12, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _icon, _name, _close },
        };
        _add.Margin = new Thickness(8, 0, 0, 0);
        var right = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(0, 0, 4, 0),
            Children = { _menu, _distraction },
        };
        Place(_tab, 0);
        Place(_add, 1);
        Place(right, 3);
        _add.VerticalAlignment = VerticalAlignment.Center;
        right.VerticalAlignment = VerticalAlignment.Center;
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
