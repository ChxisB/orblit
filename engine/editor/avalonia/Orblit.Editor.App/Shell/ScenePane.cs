using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Threading;
using Orblit.Editor.App.Theme;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App.Shell;

/// <summary>
/// The body of the Scene tab: a row with the add and instance buttons, the filter box and the menu, over the
/// entity tree. The add and menu buttons open the tool rows.
/// </summary>
internal sealed class ScenePane : Grid
{
    internal const double RowHeight = 36;

    // The tree starts 10 pixels under the button row in the reference.
    const double TreeTop = 10;

    readonly TreeView _outline = new();
    readonly TextBox _filter = new()
    {
        PlaceholderText = "Filter Nodes",
        Height = 28,
        MinHeight = 0,
        VerticalAlignment = VerticalAlignment.Center,
        InnerRightContent = Parts.SearchIcon(),
    };
    readonly Button _add = Parts.IconButton("Add", "Add an entity");
    readonly Button _instance = Parts.IconButton("Instance", "Instance a scene as a child of the selected entity");
    readonly Button _menu = Parts.IconButton("GuiTabMenuHl", "Scene options");
    readonly Flyout _flyout = new();
    readonly TreeGuides _guides;
    OutlineNode? _full;
    OutlineNode? _shown;
    nint _selected;
    bool _mirroring;

    public ScenePane(EntityTools tools, MoveTools move)
    {
        RowDefinitions = new RowDefinitions($"{RowHeight},*");
        _flyout.Content = new StackPanel { Children = { tools, move } };
        // The flyout moves focus itself while it opens, so the name box takes it back on a later turn.
        _flyout.Opened += (_, _) => Dispatcher.UIThread.Post(() => tools.NameBox.Focus(), DispatcherPriority.Background);
        _add.Flyout = _flyout;
        _menu.Flyout = _flyout;
        tools.Changed += _ => _flyout.Hide();
        move.Changed += _ => _flyout.Hide();
        _outline.Margin = new Thickness(0, TreeTop, 0, 0);
        _outline.ContextMenu = BuildMenu(tools, move);
        _outline.Styles.Add(new Style(x => x.OfType<TreeViewItem>())
        {
            Setters =
            {
                new Setter(TreeViewItem.IsExpandedProperty, true),
                new Setter(TemplatedControl.PaddingProperty, new Thickness(0)),
                new Setter(ContentControl.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch),
            },
        });
        _outline.ItemTemplate = new FuncTreeDataTemplate<OutlineNode>((node, _) => new OutlineRow(node, entity => EyeClicked?.Invoke(entity)), node => node.Children);
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
        Place(bar, _add, 0);
        Place(bar, _instance, 1);
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

    internal Button AddButton => _add;

    internal Button InstanceButton => _instance;

    internal Button MenuButton => _menu;

    internal Flyout Flyout => _flyout;

    /// <summary>The selected entity of the whole tree, also while the filter hides its row. Null for none.</summary>
    public OutlineNode? Selected => _selected == 0 ? null : _full?.Find(_selected);

    /// <summary>Shows a tree under the filter and selects the entity with this handle. Zero selects nothing.</summary>
    public void Show(OutlineNode? root, nint select)
    {
        _full = root;
        _shown = OutlineFilter.Apply(root, _filter.Text ?? "");
        Choose(select, () =>
        {
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

    // An item presses the button it stands for, so the menu, the tool row and a shortcut run the same code. Add
    // and Rename need a name, so they open the tool row. Reparenting stays in the tool row.
    ContextMenu BuildMenu(EntityTools tools, MoveTools move) => new()
    {
        ItemsSource = new[]
        {
            Item("Add Child Entity...", tools.AddButton, () => _flyout.ShowAt(_add)),
            PressItem("Duplicate", tools.DuplicateButton),
            Item("Rename...", tools.RenameButton, () => _flyout.ShowAt(_add)),
            PressItem("Move Up", move.UpButton),
            PressItem("Move Down", move.DownButton),
            PressItem("Remove", tools.RemoveButton),
        },
    };

    static MenuItem PressItem(string header, Button button) =>
        Item(header, button, () => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)));

    // The item is on while the button it stands for is on.
    static MenuItem Item(string header, Button enabledWith, Action run)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => run();
        item.Bind(IsEnabledProperty, enabledWith.GetObservable(IsEnabledProperty));
        return item;
    }

    static void Place(Grid grid, Control child, int column)
    {
        SetColumn(child, column);
        grid.Children.Add(child);
    }
}
