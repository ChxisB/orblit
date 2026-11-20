using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Orblit.Editor.App.Theme;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App.Shell;

/// <summary>
/// The body of the Scene tab: a row with the add and instance buttons, the filter box and the Scene options
/// menu, over the entity tree. A row's menu holds every command with its key, and F2 renames in the row.
/// </summary>
internal sealed class ScenePane : Grid
{
    internal const double RowHeight = 36;

    // The tree starts 10 pixels under the button row in the reference.
    const double TreeTop = 10;

    readonly TreeView _outline = new();
    readonly TextBox _filter = new()
    {
        PlaceholderText = "Filter entities",
        Height = 28,
        MinHeight = 0,
        VerticalAlignment = VerticalAlignment.Center,
        InnerRightContent = Parts.SearchIcon(),
    };
    readonly Button _menu = Parts.IconButton("GuiTabMenuHl", "Scene options");
    readonly MenuItem _classNames = new() { Header = "Show Class Names", ToggleType = MenuItemToggleType.CheckBox };
    readonly Dictionary<nint, OutlineRow> _rows = [];
    readonly TreeGuides _guides;
    bool _showClassNames;
    OutlineNode? _full;
    OutlineNode? _shown;
    nint _selected;
    bool _mirroring;

    public ScenePane(EntityTools tools, MoveTools move, InstanceTool instance, KeyModifiers command)
    {
        RowDefinitions = new RowDefinitions($"{RowHeight},*");
        _menu.Flyout = Options(move, command);
        tools.RenameButton.Click += (_, _) => Rename(tools);
        _outline.Margin = new Thickness(0, TreeTop, 0, 0);
        _outline.ContextMenu = new ContextMenu
        {
            ItemsSource = new[]
            {
                PressItem("Add Child Entity...", tools.AddButton, new KeyGesture(Key.A, command)),
                PressItem("Instance Scene...", instance.OpenButton, new KeyGesture(Key.A, command | KeyModifiers.Shift)),
                PressItem("Duplicate", tools.DuplicateButton, new KeyGesture(Key.D, command)),
                PressItem("Rename...", tools.RenameButton, new KeyGesture(Key.F2)),
                PressItem("Move Up", move.UpButton, new KeyGesture(Key.Up, command)),
                PressItem("Move Down", move.DownButton, new KeyGesture(Key.Down, command)),
                PressItem("Move Under...", move.MoveUnderButton),
                PressItem("Remove", tools.RemoveButton, new KeyGesture(Key.Back)),
            },
        };
        _outline.Styles.Add(new Style(x => x.OfType<TreeViewItem>())
        {
            Setters =
            {
                new Setter(TreeViewItem.IsExpandedProperty, true),
                new Setter(TemplatedControl.PaddingProperty, new Thickness(0)),
                new Setter(ContentControl.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch),
            },
        });
        _outline.ItemTemplate = new FuncTreeDataTemplate<OutlineNode>((node, _) => _rows[node.Handle] = new OutlineRow(node, entity => EyeClicked?.Invoke(entity), _filter.Text ?? "", _showClassNames), node => node.Children);
        _outline.SelectionChanged += (_, _) =>
        {
            // The tree only shows the selection. A change the pane makes to the tree must not rewrite it.
            if (!_mirroring)
            {
                _selected = (_outline.SelectedItem as OutlineNode)?.Handle ?? 0;
                SelectionChanged?.Invoke();
            }
        };
        // TextChanged waits for a render, so the property is the one that fires at once.
        _filter.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty)
            {
                Show(_full, _selected);
            }
        };
        var bar = new Grid { ColumnDefinitions = new ColumnDefinitions("32,32,2,*,2,32"), Margin = new Thickness(2, 0, 3, 0) };
        Place(bar, tools.AddButton, 0);
        Place(bar, instance.OpenButton, 1);
        Place(bar, _filter, 3);
        Place(bar, _menu, 5);
        SetRow(_outline, 1);
        _guides = new TreeGuides(_outline);
        SetRow(_guides, 1);
        Children.Add(bar);
        Children.Add(_outline);
        Children.Add(_guides);
    }

    /// <summary>Raised when the selected entity changes, also when a new tree clears it.</summary>
    public event Action? SelectionChanged;

    /// <summary>Raised when the eye of a row is pressed, with the entity of that row.</summary>
    public event Action<OutlineNode>? EyeClicked;

    internal TreeView Outline => _outline;

    internal TreeGuides Guides => _guides;

    internal TextBox FilterBox => _filter;

    internal Button MenuButton => _menu;

    /// <summary>The row of each entity the tree has drawn, by handle.</summary>
    internal IReadOnlyDictionary<nint, OutlineRow> Rows => _rows;

    /// <summary>The selected entity of the whole tree, also while the filter hides its row. Null for none.</summary>
    public OutlineNode? Selected => _selected == 0 ? null : _full?.Find(_selected);

    /// <summary>Shows a tree under the filter and selects the entity with this handle. Zero selects nothing.</summary>
    public void Show(OutlineNode? root, nint select)
    {
        _full = root;
        _shown = OutlineFilter.Apply(root, _filter.Text ?? "");
        Choose(select, () =>
        {
            _rows.Clear();
            _outline.ItemsSource = _shown is null ? Array.Empty<OutlineNode>() : new[] { _shown };
            _outline.SelectedItem = ShownSelection();
        });
    }

    /// <summary>Selects the entity with this handle, also when the filter hides its row. Zero selects nothing.</summary>
    public void Select(nint handle) => Choose(handle, () => _outline.SelectedItem = ShownSelection());

    OutlineNode? ShownSelection() => _selected == 0 ? null : _shown?.Find(_selected);

    // The tree raises nothing while the pane sets it, because a hidden row leaves it unchanged. The pane raises
    // SelectionChanged itself when the handle differs.
    void Choose(nint handle, Action showInTree)
    {
        var changed = handle != _selected;
        _selected = handle;
        _mirroring = true;
        showInTree();
        _mirroring = false;
        if (changed)
        {
            SelectionChanged?.Invoke();
        }
    }

    // The selected row edits its name in place. A row the filter hides cannot.
    void Rename(EntityTools tools)
    {
        if (_rows.TryGetValue(_selected, out var row))
        {
            row.Rename(tools.Rename);
        }
    }

    MenuFlyout Options(MoveTools move, KeyModifiers command)
    {
        var parent = new MenuItem { Header = "Add a New Parent..." };
        parent.Click += (_, _) => _menu.RaiseEvent(new UnbuiltEventArgs("Add a New Parent is not built yet."));
        EditorTheme.MarkSoon(parent);
        // The pane keeps the flag, so the tick is right whether or not the item flips itself before this runs.
        _classNames.Click += (_, _) =>
        {
            _showClassNames = !_showClassNames;
            _classNames.IsChecked = _showClassNames;
            Show(_full, _selected);
        };
        return new MenuFlyout
        {
            Placement = PlacementMode.BottomEdgeAlignedRight,
            Items =
            {
                Heading("Order"),
                PressItem("Move Up", move.UpButton, new KeyGesture(Key.Up, command)),
                PressItem("Move Down", move.DownButton, new KeyGesture(Key.Down, command)),
                PressItem("Move Under...", move.MoveUnderButton),
                parent,
                new Separator(),
                Heading("Tree"),
                ActionItem("Expand All", () => SetExpanded(true)),
                ActionItem("Collapse All", () => SetExpanded(false)),
                _classNames,
            },
        };
    }

    // Expanding clears what the arrows set, so the pane's style opens every row again, also rows not drawn yet.
    void SetExpanded(bool expanded)
    {
        foreach (var item in _outline.GetVisualDescendants().OfType<TreeViewItem>().ToList())
        {
            if (expanded)
            {
                item.ClearValue(TreeViewItem.IsExpandedProperty);
            }
            else
            {
                item.IsExpanded = false;
            }
        }
    }

    // A heading names a group of the menu. It is off, so it takes no hover and no click.
    static MenuItem Heading(string group) => new()
    {
        IsEnabled = false,
        MinHeight = 0,
        Header = new TextBlock { Text = group.ToUpperInvariant(), FontSize = 10.5, FontWeight = FontWeight.SemiBold, LetterSpacing = 0.6, Foreground = EditorBrushes.Unit },
    };

    static MenuItem ActionItem(string header, Action run)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => run();
        return item;
    }

    // An item presses the button it stands for, so the menus and a key run the same code. The item is on while
    // the button is on.
    static MenuItem PressItem(string header, Button button, KeyGesture? key = null)
    {
        var item = ActionItem(header, () => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)));
        item.InputGesture = key;
        item.Bind(IsEnabledProperty, button.GetObservable(IsEnabledProperty));
        return item;
    }

    static void Place(Grid grid, Control child, int column)
    {
        SetColumn(child, column);
        grid.Children.Add(child);
    }
}
