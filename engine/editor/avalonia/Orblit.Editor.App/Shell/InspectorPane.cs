using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Orblit.Editor.App.Theme;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App.Shell;

/// <summary>
/// The body of the Inspector tab: a history row, a filter row and the property list. The history row names
/// the inspected node. Its arrows are off because the editor keeps no list of inspected objects.
/// </summary>
internal sealed class InspectorPane : Grid
{
    internal const double HistoryHeight = 36;

    // The box is 28 high and the list starts one pixel under it.
    const double FilterRowHeight = 29;
    const double BoxHeight = 28;

    readonly InspectorPanel _panel;
    readonly ScrollViewer _viewer;
    readonly TextBox _filter = new()
    {
        PlaceholderText = "Filter Properties",
        Height = BoxHeight,
        MinHeight = 0,
        InnerRightContent = Parts.SearchIcon(),
    };
    readonly Button _back = Parts.Tight(Parts.IconButton("Back", "Inspect the previous object"));
    readonly Button _forward = Parts.Tight(Parts.IconButton("Forward", "Inspect the next object"));
    readonly Button _dropdown = Parts.Tight(Unbuilt.Mark(Parts.IconButton("GuiDropdown", "Inspected objects"), "The list of inspected objects is not built yet."));
    readonly Button _history = Parts.Tight(Unbuilt.Mark(Parts.IconButton("GuiTabMenuHl", "Inspected objects"), "The history of inspected objects is not built yet."));
    readonly ContentControl _icon = new() { VerticalAlignment = VerticalAlignment.Center };
    readonly TextBlock _name = new() { FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center };
    readonly StackPanel _inspected = new() { Orientation = Orientation.Horizontal, Margin = new Thickness(21, 0, 0, 0), IsVisible = false };
    readonly Button _tools = Parts.Tight(Unbuilt.Mark(Parts.IconButton("Tools", "Inspector tools"), "The inspector tools are not built yet."));

    public InspectorPane(InspectorPanel panel)
    {
        _panel = panel;
        _viewer = new ScrollViewer { Content = panel };
        RowDefinitions = new RowDefinitions($"{HistoryHeight},{FilterRowHeight},*");
        _back.IsEnabled = false;
        _forward.IsEnabled = false;
        // TextChanged waits for a render, so the property is the one that fires at once.
        _filter.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty)
            {
                _panel.SetFilter(_filter.Text ?? "");
            }
        };

        _inspected.Children.AddRange([_icon, _name]);
        var history = new Grid { ColumnDefinitions = new ColumnDefinitions("24,4,24,*,24,12,24"), Margin = new Thickness(2, 0, 6, 0) };
        Place(history, _back, 0, VerticalAlignment.Center);
        Place(history, _forward, 2, VerticalAlignment.Center);
        Place(history, _inspected, 3, VerticalAlignment.Center);
        Place(history, _dropdown, 4, VerticalAlignment.Center);
        Place(history, _history, 6, VerticalAlignment.Center);
        var filter = new Grid { ColumnDefinitions = new ColumnDefinitions("*,6,24"), Margin = new Thickness(4, 0, 6, 0) };
        Place(filter, _filter, 0, VerticalAlignment.Top);
        _tools.Margin = new Thickness(0, 2, 0, 0);
        Place(filter, _tools, 2, VerticalAlignment.Top);
        SetRow(filter, 1);
        SetRow(_viewer, 2);
        Children.Add(history);
        Children.Add(filter);
        Children.Add(_viewer);
    }

    internal ScrollViewer Viewer => _viewer;

    internal TextBlock InspectedName => _name;

    internal EditorIcon InspectedIcon => (EditorIcon)_icon.Content!;

    internal Button DropdownButton => _dropdown;

    internal TextBox FilterBox => _filter;

    internal Button BackButton => _back;

    internal Button ForwardButton => _forward;

    internal Button HistoryButton => _history;

    internal Button ToolsButton => _tools;

    /// <summary>Shows the node in the property list and names it in the history row.</summary>
    public void Show(IEditorScene? scene, OutlineNode? node)
    {
        _panel.Show(scene, node);
        _inspected.IsVisible = node is not null;
        _icon.Content = node is null ? null : Parts.RowIcon(EditorIcons.ForClass(node.ClassName));
        _name.Text = node?.Name ?? "";
    }

    static void Place(Grid grid, Control child, int column, VerticalAlignment vertical)
    {
        SetColumn(child, column);
        child.VerticalAlignment = vertical;
        grid.Children.Add(child);
    }
}
