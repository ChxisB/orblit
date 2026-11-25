using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Reactive;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Orblit.Editor.App.Theme;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App.Shell;

/// <summary>
/// The body of the Scene tab: a row with the add and instance buttons, the filter box and the Scene options
/// menu, over the entity tree. A row's menu holds every command with its key and a note on what it does to the
/// picked thing, or why it is off. F2 renames in the row.
/// Command click adds or drops an entity and Shift click picks a range. The entity picked last is the selected
/// one, which the gizmo and the row commands act on.
/// </summary>
internal sealed class ScenePane : Grid
{
    internal const double RowHeight = 36;

    // The tree starts 10 pixels under the button row in the reference.
    const double TreeTop = 10;

    readonly TreeView _outline = new() { SelectionMode = SelectionMode.Multiple };
    readonly TextBox _filter = new()
    {
        PlaceholderText = "Filter entities",
        Height = 28,
        MinHeight = 0,
        VerticalAlignment = VerticalAlignment.Center,
        InnerRightContent = Parts.SearchIcon(),
    };
    readonly Button _menu = new HoverCard("Scene options", "Open or close every branch, and pick what the list shows.").On(Parts.IconButton("GuiTabMenuHl", ""));
    readonly Dictionary<nint, OutlineRow> _rows = [];
    readonly TreeGuides _guides;
    readonly ThingItem[] _thingMenu;
    readonly string _undoKey;
    bool _showClassNames;
    bool _onlyMatches = true;
    OutlineNode? _full;
    OutlineNode? _shown;
    nint _selected;
    // The entities picked with the selected one, in the tree's order.
    List<nint> _others = [];
    bool _mirroring;

    public ScenePane(EntityTools tools, MoveTools move, InstanceTool instance, KeyModifiers command)
    {
        RowDefinitions = new RowDefinitions($"{RowHeight},*");
        _menu.Flyout = Options();
        _undoKey = Parts.KeyText(new KeyGesture(Key.Z, command));
        tools.RenameButton.Click += (_, _) => Rename(tools);
        _outline.Margin = new Thickness(0, TreeTop, 0, 0);
        var addKey = new KeyGesture(Key.A, command);
        var addSceneKey = new KeyGesture(Key.A, command | KeyModifiers.Shift);
        var add = new HoverCard("Add a thing…", "Puts a camera, light, shape or anything else in the scene.", Parts.KeyText(addKey));
        add.SaysWhyOff(add.On(tools.AddButton), "Open a scene first.");
        var addScene = new HoverCard("Add a scene inside…", "Reuse a scene you already made.", Parts.KeyText(addSceneKey));
        addScene.SaysWhyOff(addScene.On(instance.OpenButton), "Open a scene first.");
        _thingMenu =
        [
            new(tools.AddButton, addKey),
            new(instance.OpenButton, addSceneKey),
            new(tools.DuplicateButton, new KeyGesture(Key.D, command)),
            new(tools.RenameButton, new KeyGesture(Key.F2)),
            new(move.UpButton, new KeyGesture(Key.Up, command)),
            new(move.DownButton, new KeyGesture(Key.Down, command)),
            new(move.MoveUnderButton, null),
            new(tools.NewParentButton, null),
            new(tools.RemoveButton, new KeyGesture(Key.Back)),
        ];
        _outline.ContextMenu = new ContextMenu { ItemsSource = _thingMenu };
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
        _outline.SelectionChanged += (_, e) =>
        {
            // The tree only shows the selection. A change the pane makes to the tree must not rewrite it.
            if (!_mirroring)
            {
                Pick(e.AddedItems.OfType<OutlineNode>().LastOrDefault());
                DescribeMenu();
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
        SetRow(Empty, 1);
        Children.Add(bar);
        Children.Add(_outline);
        Children.Add(_guides);
        Children.Add(Empty);
    }

    /// <summary>What the pane says while no scene is open.</summary>
    internal StackPanel Empty { get; } = Parts.EmptyState("SceneFile", "No scene is open", "Open a scene to list the things in it here.");

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

    /// <summary>The items of a row's right-click menu, in order.</summary>
    internal IReadOnlyList<MenuItem> ThingMenu => _thingMenu;

    /// <summary>The selected entity of the whole tree, also while the filter hides its row. Null for none.</summary>
    public OutlineNode? Selected => _selected == 0 ? null : _full?.Find(_selected);

    /// <summary>Every picked entity, with the selected one last. Empty for none.</summary>
    public IReadOnlyList<OutlineNode> Picked => Selected is { } selected ? [.. _others.Select(handle => _full!.Find(handle)).OfType<OutlineNode>(), selected] : [];

    /// <summary>Shows a tree under the filter and selects the entity with this handle. Zero selects nothing.</summary>
    public void Show(OutlineNode? root, nint select)
    {
        _full = root;
        Empty.IsVisible = root is null;
        _shown = _onlyMatches ? OutlineFilter.Apply(root, _filter.Text ?? "") : root;
        Choose(select, () =>
        {
            _rows.Clear();
            _outline.ItemsSource = _shown is null ? Array.Empty<OutlineNode>() : new[] { _shown };
            ShowPicked();
        });
    }

    /// <summary>Selects the entity with this handle, also when the filter hides its row. Zero selects nothing.</summary>
    public void Select(nint handle) => Choose(handle, ShowPicked);

    // The tree holds only the rows the filter shows. Setting its one item clears the others first.
    void ShowPicked()
    {
        _outline.SelectedItem = _selected == 0 ? null : _shown?.Find(_selected);
        foreach (var other in _others.Select(handle => _shown?.Find(handle)).OfType<OutlineNode>())
        {
            _outline.SelectedItems!.Add(other);
        }
    }

    // The entity just added leads. When one is dropped, the lead stays if it is still picked.
    void Pick(OutlineNode? added)
    {
        List<nint> picked = [.. _outline.SelectedItems!.OfType<OutlineNode>().Select(node => node.Handle)];
        _selected = added?.Handle ?? (picked.Contains(_selected) ? _selected : picked.LastOrDefault());
        _others = [.. picked.Where(handle => handle != _selected)];
    }

    // The tree raises nothing while the pane sets it, because a hidden row leaves it unchanged. The pane raises
    // SelectionChanged itself when the selection differs. The same entity chosen again, as after an edit, keeps
    // the others still in the scene.
    void Choose(nint handle, Action showInTree)
    {
        List<nint> kept = handle != 0 && handle == _selected ? [.. _others.Where(other => _full?.Find(other) is not null)] : [];
        var changed = handle != _selected || kept.Count != _others.Count;
        _selected = handle;
        _others = kept;
        _mirroring = true;
        showInTree();
        _mirroring = false;
        DescribeMenu();
        if (changed)
        {
            SelectionChanged?.Invoke();
        }
    }

    // The selected row edits its name in place. A row the filter hides cannot.
    void Rename(EntityTools tools)
    {
        // A row added a moment ago has no control until the tree's next layout.
        if (!_rows.ContainsKey(_selected))
        {
            _outline.UpdateLayout();
        }

        if (_rows.TryGetValue(_selected, out var row))
        {
            row.Rename(tools.Rename);
        }
    }

    // The menu names the picked thing. Add goes into the top thing when nothing is picked, so it names that.
    void DescribeMenu()
    {
        var picked = Selected;
        var name = picked?.Name ?? "";
        var top = picked is not null && picked == _full;
        var parent = picked is null || top ? null : MoveTools.ParentOf(_full!, picked);
        var pickFirst = _full is null ? "Open a scene first." : "Pick a thing first.";
        var whole = top ? "The top thing can't go inside another." : pickFirst;
        _thingMenu[0].Say($"Add inside {(picked ?? _full)?.Name}…", "A camera, light, shape or anything else", "Open a scene first.");
        _thingMenu[1].Say("Add a scene inside…", "Reuse a scene you already made", "Open a scene first.");
        _thingMenu[2].Say("Duplicate", "Makes a copy right below it", top ? $"{name} is the whole scene. Copy the scene file in Files instead." : pickFirst);
        _thingMenu[3].Say("Rename", null, pickFirst);
        _thingMenu[4].Say("Move up", null, top ? "The top thing has nothing above it." : parent is null ? pickFirst : $"{name} is already first.");
        _thingMenu[5].Say("Move down", null, top ? "The top thing has nothing below it." : parent is null ? pickFirst : $"{name} is already last.");
        _thingMenu[6].Say("Move into…", $"Put {name} inside another thing", whole);
        _thingMenu[7].Say("Wrap in a new thing…", $"Puts {name} inside a new empty point", whole);
        _thingMenu[8].Say("Delete", $"Undo with {_undoKey}", top ? "A scene always needs a top thing." : pickFirst);
    }

    MenuFlyout Options() => new()
    {
        Placement = PlacementMode.BottomEdgeAlignedRight,
        Items =
        {
            Parts.ActionItem("Open all", () => SetExpanded(true), note: "Shows every thing inside every other"),
            Parts.ActionItem("Close all", () => SetExpanded(false), note: "Shows only the top level"),
            new Separator(),
            Toggle("Show engine names", "Adds the engine's kind, like MeshInstance3D, beside each name", false, on => _showClassNames = on),
            Toggle("Show only what matches the search", "Hides the rest while you type", true, on => _onlyMatches = on),
        },
    };

    // The pane keeps the flag, so the tick is right whether or not the item flips itself before this runs.
    MenuItem Toggle(string name, string note, bool on, Action<bool> set)
    {
        var item = Parts.ActionItem(name, () => { }, note: note);
        item.ToggleType = MenuItemToggleType.CheckBox;
        item.IsChecked = on;
        item.Click += (_, _) =>
        {
            on = !on;
            item.IsChecked = on;
            set(on);
            Show(_full, _selected);
        };
        return item;
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

    static void Place(Grid grid, Control child, int column)
    {
        SetColumn(child, column);
        grid.Children.Add(child);
    }

    // An item that presses its button, so a key and the menu run the same code. Its name and note follow the
    // picked thing, and while the button is off the note says why.
    sealed class ThingItem : MenuItem
    {
        readonly TextBlock _name = new();
        readonly TextBlock _note = new() { FontSize = 11, Foreground = EditorBrushes.TextDim };
        string? _does;
        string _why = "";

        public ThingItem(Button button, KeyGesture? key)
        {
            Header = new StackPanel { Spacing = 1, Children = { _name, _note } };
            InputGesture = key;
            Click += (_, _) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            this.Bind(IsEnabledProperty, button.GetObservable(IsEnabledProperty));
            this.GetObservable(IsEnabledProperty).Subscribe(new AnonymousObserver<bool>(_ => ShowNote()));
        }

        protected override Type StyleKeyOverride => typeof(MenuItem);

        internal string Label => _name.Text ?? "";

        /// <summary>What the item does, or why it is off.</summary>
        internal string Note => _note.Text ?? "";

        public void Say(string name, string? does, string why)
        {
            _name.Text = name;
            AutomationProperties.SetName(this, name);
            _does = does;
            _why = why;
            ShowNote();
        }

        void ShowNote()
        {
            _note.Text = IsEnabled ? _does : _why;
            _note.IsVisible = !string.IsNullOrEmpty(_note.Text);
        }
    }
}
