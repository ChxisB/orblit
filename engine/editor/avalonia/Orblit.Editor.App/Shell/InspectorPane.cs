using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Orblit.Editor.App.Theme;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App.Shell;

/// <summary>
/// The body of the Inspector tab: a history row, a filter row and the property list. The history row walks back
/// and forward through the objects shown, and its chip names the one shown and lists the recent ones. Another
/// scene starts a new history.
/// </summary>
internal sealed class InspectorPane : Grid
{
    internal const double HistoryHeight = 36;

    // The box is 28 high and the list starts one pixel under it.
    const double FilterRowHeight = 29;
    const double BoxHeight = 28;

    // A chip on a row of its own is 38 high. Here it shares the 36 pixel row with the arrows.
    const double ChipHeight = 32;

    readonly InspectorPanel _panel;
    readonly ScrollViewer _viewer;
    readonly TextBox _filter = new()
    {
        PlaceholderText = "Filter properties",
        Height = BoxHeight,
        MinHeight = 0,
        InnerRightContent = Parts.SearchIcon(),
    };
    readonly Button _back = Parts.Tight(Parts.IconButton("Back", "Inspect the previous object"));
    readonly Button _forward = Parts.Tight(Parts.IconButton("Forward", "Inspect the next object"));
    readonly ContentControl _icon = new() { VerticalAlignment = VerticalAlignment.Center };
    readonly TextBlock _name = new() { FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center };
    readonly TextBlock _class = new() { FontSize = 11, Foreground = EditorBrushes.TextDim, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
    // The class fills what the icon and the name leave, so a long chain trims before the chevron.
    readonly DockPanel _inspected = new() { HorizontalSpacing = 6, IsVisible = false };
    readonly MenuFlyout _recent = new();
    readonly Button _chip;
    readonly Button _tools = Parts.Tight(Parts.IconButton("Tools", "Inspector tools"));
    readonly List<Inspected> _seen = [];
    int _at = -1;
    string? _file;

    public InspectorPane(InspectorPanel panel)
    {
        _panel = panel;
        _viewer = new ScrollViewer { Content = panel };
        RowDefinitions = new RowDefinitions($"{HistoryHeight},{FilterRowHeight},*");
        _chip = ObjectChip(_inspected, _recent);
        _back.IsEnabled = false;
        _forward.IsEnabled = false;
        _back.Click += (_, _) => Step(-1);
        _forward.Click += (_, _) => Step(1);
        _recent.Opening += (_, _) => FillRecent();
        _tools.Flyout = Tools();
        // TextChanged waits for a render, so the property is the one that fires at once.
        _filter.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty)
            {
                _panel.SetFilter(_filter.Text ?? "");
            }
        };

        _inspected.Children.AddRange([_icon, _name, _class]);
        var history = new Grid { ColumnDefinitions = new ColumnDefinitions("24,4,24,8,*"), Margin = new Thickness(2, 0, 6, 0) };
        Place(history, _back, 0, VerticalAlignment.Center);
        Place(history, _forward, 2, VerticalAlignment.Center);
        Place(history, _chip, 4, VerticalAlignment.Center);
        var filter = new Grid { ColumnDefinitions = new ColumnDefinitions("*,6,24"), Margin = new Thickness(4, 0, 6, 0) };
        Place(filter, _filter, 0, VerticalAlignment.Top);
        _tools.Margin = new Thickness(0, 2, 0, 0);
        Place(filter, _tools, 2, VerticalAlignment.Top);
        SetRow(filter, 1);
        SetRow(_viewer, 2);
        SetRow(Empty, 2);
        Children.Add(history);
        Children.Add(filter);
        Children.Add(_viewer);
        Children.Add(Empty);
    }

    internal ScrollViewer Viewer => _viewer;

    /// <summary>What the pane says while nothing is picked.</summary>
    internal StackPanel Empty { get; } = Parts.EmptyState("ListSelect", "Nothing selected", "Click a thing in the view or the Scene panel to change its settings here.");

    internal TextBlock InspectedName => _name;

    internal EditorIcon InspectedIcon => (EditorIcon)_icon.Content!;

    internal TextBlock InspectedClass => _class;

    internal Button Chip => _chip;

    internal MenuFlyout Recent => _recent;

    internal TextBox FilterBox => _filter;

    internal Button BackButton => _back;

    internal Button ForwardButton => _forward;

    internal Button ToolsButton => _tools;

    /// <summary>Raised with the handle of an object the history goes back to.</summary>
    public event Action<nint>? Picked;

    /// <summary>
    /// Shows the picked entities in the property list, names the selected one, which is last, or counts them in the
    /// chip, and adds the selected one to the history.
    /// </summary>
    public void Show(IEditorScene? scene, IReadOnlyList<OutlineNode> picked)
    {
        var node = picked.Count == 0 ? null : picked[^1];
        _panel.Show(scene, picked);
        _inspected.IsVisible = node is not null;
        Empty.IsVisible = node is null;
        _icon.Content = node is null ? null : Parts.RowIcon(EditorIcons.ForClass(node.ClassName));
        _name.Text = picked.Count > 1 ? $"{picked.Count} selected" : node?.Name ?? "";
        _class.Text = string.Join(" › ", _panel.Chain);
        Remember(scene?.SceneFile, node);
    }

    // The object at the cursor shown again, as after a drag or a step back, adds nothing. A new one drops the
    // steps forward of the cursor, as a browser does.
    void Remember(string? file, OutlineNode? node)
    {
        if (file != _file)
        {
            _seen.Clear();
            _at = -1;
            _file = file;
        }
        if (node is not null && (_at < 0 || _seen[_at].Handle != node.Handle))
        {
            _seen.RemoveRange(_at + 1, _seen.Count - _at - 1);
            _seen.Add(new Inspected(node.Handle, node.Name, node.ClassName));
            _at = _seen.Count - 1;
        }
        ShowSteps();
    }

    void ShowSteps()
    {
        _back.IsEnabled = _at > 0;
        _forward.IsEnabled = _at < _seen.Count - 1;
    }

    // An object deleted since leaves nothing selected, and the cursor still moves past it.
    void Step(int by)
    {
        _at += by;
        ShowSteps();
        Picked?.Invoke(_seen[_at].Handle);
    }

    // Newest first, each object once.
    void FillRecent()
    {
        _recent.Items.Clear();
        foreach (var seen in Enumerable.Reverse(_seen).DistinctBy(seen => seen.Handle))
        {
            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                Children = { new TextBlock { Text = seen.Name }, new TextBlock { Text = seen.ClassName, FontSize = 11, Foreground = EditorBrushes.TextDim, VerticalAlignment = VerticalAlignment.Center } },
            };
            var item = new MenuItem { Header = row, Icon = Parts.RowIcon(EditorIcons.ForClass(seen.ClassName)) };
            item.Click += (_, _) => Picked?.Invoke(seen.Handle);
            _recent.Items.Add(item);
        }
    }

    MenuFlyout Tools() => new()
    {
        Items =
        {
            Parts.ActionItem("Expand All", () => _panel.FoldAll(true)),
            Parts.ActionItem("Collapse All", () => _panel.FoldAll(false)),
            new Separator(),
            Parts.ActionItem("Copy Properties", _panel.CopyProperties),
            Parts.ActionItem("Paste Properties", _panel.PasteProperties),
            Parts.ActionItem("Make Sub-Resources Unique", _panel.MakeResourcesUnique),
        },
    };

    // The object chip: the icon, the name and the class in a quieter voice on a raised tone, with a
    // chevron that says it opens the list.
    static Button ObjectChip(Control inspected, MenuFlyout recent)
    {
        var chevron = new EditorIcon("GuiDropdown") { VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(chevron, Dock.Right);
        var chip = new Button
        {
            Height = ChipHeight,
            MinHeight = 0,
            MinWidth = 0,
            Padding = new Thickness(10, 0),
            Background = EditorBrushes.Raised,
            BorderBrush = EditorBrushes.LineStrong,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Center,
            Content = new DockPanel { Children = { chevron, inspected } },
            Flyout = recent,
        };
        ToolTip.SetTip(chip, "Inspected objects");
        return chip;
    }

    readonly record struct Inspected(nint Handle, string Name, string ClassName);

    static void Place(Grid grid, Control child, int column, VerticalAlignment vertical)
    {
        SetColumn(child, column);
        child.VerticalAlignment = vertical;
        grid.Children.Add(child);
    }
}
