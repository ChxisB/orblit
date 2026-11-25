using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Orblit.Editor.App.Graph;
using Orblit.Editor.App.Shell;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App;

/// <summary>
/// The body of the Files tab: a row with the path and the Tree or List switch, a row with the filter and the sort
/// menu, and the tree of the project's files, each tinted by kind and named in plain words. A double press or
/// Enter on a file raises it to open, and a right click opens a menu of what fits the picked row.
/// </summary>
internal sealed class ScenesPanel : Grid
{
    const double PathRowHeight = 36;
    const double BoxHeight = 28;

    // The tree starts 14 pixels under the filter in the reference.
    const double TreeTop = 14;

    // The sorts the menu offers: the word the button shows, the name of the item and its note.
    static readonly (string Word, string Item, string Note)[] s_sorts =
    [
        ("Name", "Name", "A to Z"),
        ("Newest", "Newest first", "The last file you changed goes on top"),
        ("Kind", "Kind", "Scenes, then scripts, then the rest"),
    ];

    readonly TreeView _tree = new() { Margin = new Thickness(0, TreeTop, 0, 0) };
    readonly HashSet<string> _opened = [];
    readonly TextBox _path = new()
    {
        Text = FileTree.Root,
        IsReadOnly = true,
        Height = BoxHeight,
        MinHeight = 0,
        VerticalAlignment = VerticalAlignment.Top,
        Margin = new Thickness(0, 3, 0, 0),
    };
    readonly TextBox _filter = new()
    {
        PlaceholderText = "Find a file",
        Height = BoxHeight,
        MinHeight = 0,
        VerticalAlignment = VerticalAlignment.Center,
        InnerRightContent = Parts.SearchIcon(),
    };
    // The switch is two pixels taller than the path box, so it sits a pixel higher to share its middle.
    readonly Segments _layout = new("Tree", "List") { VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 2, 0, 0) };
    readonly TextBlock _sortWord = new() { Text = s_sorts[0].Word, VerticalAlignment = VerticalAlignment.Center };
    readonly Button _sort;
    readonly MenuItem[] _sorts;
    readonly StackPanel _noMatch = Parts.EmptyState("Search", "", "Search looks at file names in every folder.");
    readonly ContextMenu _menu = new();
    readonly TreeGuides _guides;
    readonly ITextClipboard _clipboard;
    readonly KeyModifiers _command;
    IReadOnlyList<string> _files = [];
    IReadOnlyDictionary<string, DateTime> _modified = new Dictionary<string, DateTime>();
    Func<string, string> _sceneKind = _ => "";
    string _open = "";
    int _order;

    public ScenesPanel(KeyModifiers command, ITextClipboard? clipboard = null)
    {
        _command = command;
        _clipboard = clipboard ?? new WindowClipboard(this);
        RowDefinitions = new RowDefinitions($"{PathRowHeight},{BoxHeight},*");
        _sorts = [.. s_sorts.Select((sort, at) => Parts.ActionItem(sort.Item, () => SortBy(at), note: sort.Note))];
        _sort = SortMenu(_sortWord, _sorts);
        _layout.Changed += Rebuild;
        _tree.Styles.Add(new Style(x => x.OfType<TreeViewItem>())
        {
            Setters =
            {
                new Setter(TemplatedControl.PaddingProperty, new Thickness(0)),
                new Setter(ContentControl.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch),
            },
        });
        _tree.ItemTemplate = new FuncTreeDataTemplate<FileNode>((node, _) => Row(node), node => node.Children);
        _tree.AddHandler(TreeViewItem.ExpandedEvent, (_, e) => Remember(e.Source, true));
        _tree.AddHandler(TreeViewItem.CollapsedEvent, (_, e) => Remember(e.Source, false));
        // The row that took the press, not the selected one: a press on an arrow or on empty space opens nothing.
        _tree.DoubleTapped += (_, e) => Activate((e.Source as Visual)?.FindAncestorOfType<TreeViewItem>(true)?.DataContext);
        _tree.KeyDown += (_, e) => PressKey(e);
        _tree.ContextMenu = _menu;
        _menu.Opening += (_, e) => e.Cancel = _tree.SelectedItem is null;
        _tree.SelectionChanged += (_, _) => ShowSelected();
        // TextChanged waits for a render, so the property is the one that fires at once.
        _filter.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty)
            {
                Rebuild();
            }
        };
        _filter.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape && _filter.Text is { Length: > 0 })
            {
                e.Handled = true;
                _filter.Text = "";
            }
        };
        ClearSearchButton.Click += (_, _) => _filter.Text = "";
        _noMatch.Children.Add(ClearSearchButton);
        _noMatch.VerticalAlignment = VerticalAlignment.Top;
        _guides = new TreeGuides(_tree);
        Lay();
    }

    /// <summary>Raised with the res:// path of the file to open.</summary>
    public event Action<string>? Chosen;

    /// <summary>Raised when the file menu asks for a new graph script.</summary>
    public event Action? NewGraphRequested;

    /// <summary>Raised with the res:// path of the folder the new scene goes in.</summary>
    public event Action<string>? NewSceneRequested;

    /// <summary>Raised with the res:// path of the scene the game starts in.</summary>
    public event Action<string>? PlayRequested;

    /// <summary>Raised with the res:// path of the scene to put a copy of inside the open one.</summary>
    public event Action<string>? UseRequested;

    /// <summary>Empties the filter. It shows while no file matches.</summary>
    internal Button ClearSearchButton { get; } = new()
    {
        Content = Parts.Keyed("Clear the search", "esc"),
        HorizontalAlignment = HorizontalAlignment.Center,
        Classes = { EditorTheme.TextButton, EditorTheme.Small },
    };

    internal TreeView Tree => _tree;

    internal TreeGuides Guides => _guides;

    internal TextBox PathBox => _path;

    internal TextBox FilterBox => _filter;

    internal Segments LayoutChoice => _layout;

    internal Button SortButton => _sort;

    internal IReadOnlyList<MenuItem> SortItems => _sorts;

    /// <summary>What shows in place of the tree when the filter matches no file.</summary>
    internal StackPanel NoMatch => _noMatch;

    /// <summary>
    /// Shows the files. Their times order them when the sort is newest first, and the kind of each scene, such as
    /// 3D, goes in its row.
    /// </summary>
    public void Show(IReadOnlyList<string> files, IReadOnlyDictionary<string, DateTime> modified, Func<string, string> sceneKind)
    {
        _files = files;
        _modified = modified;
        _sceneKind = sceneKind;
        Rebuild();
    }

    /// <summary>Names the open scene in the accent colour. An empty path means the open scene has no file.</summary>
    public void MarkOpen(string resourcePath)
    {
        _open = resourcePath;
        foreach (var name in _tree.GetVisualDescendants().OfType<TextBlock>().Where(text => text.Tag is FileNode))
        {
            Tint(name, (FileNode)name.Tag!);
        }

        ShowMenu();
    }

    void Lay()
    {
        var pathRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,6,Auto"), Margin = new Thickness(4, 0, 7, 0) };
        Place(pathRow, _path, 0);
        Place(pathRow, _layout, 2);
        var filterRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,6,Auto"), Margin = new Thickness(4, 0, 7, 0) };
        Place(filterRow, _filter, 0);
        Place(filterRow, _sort, 2);
        SetRow(filterRow, 1);
        SetRow(_tree, 2);
        SetRow(_guides, 2);
        SetRow(_noMatch, 2);
        Children.AddRange([pathRow, filterRow, _tree, _guides, _noMatch]);
    }

    // The word of the sort in use, and an arrow that says a menu opens.
    static Button SortMenu(TextBlock word, MenuItem[] sorts)
    {
        var chevron = new EditorIcon("GuiDropdown") { VerticalAlignment = VerticalAlignment.Center };
        TextElement.SetForeground(chevron, EditorBrushes.TextDim);
        foreach (var item in sorts)
        {
            item.ToggleType = MenuItemToggleType.Radio;
        }

        sorts[0].IsChecked = true;
        Control[] items = [Parts.MenuHeading("Sort by"), .. sorts];
        var button = new Button
        {
            Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { word, chevron } },
            VerticalAlignment = VerticalAlignment.Center,
            Classes = { EditorTheme.TextButton, EditorTheme.Ghost, EditorTheme.Small },
            Flyout = new MenuFlyout { ItemsSource = items },
        };
        return new HoverCard("Sort by", "Picks the order of the files in each folder. Folders stay on top.").On(button);
    }

    void SortBy(int order)
    {
        _order = order;
        _sortWord.Text = s_sorts[order].Word;
        foreach (var (item, at) in _sorts.Select((item, at) => (item, at)))
        {
            item.IsChecked = at == order;
        }

        Rebuild();
    }

    IComparer<string>? Order() => _order switch
    {
        1 => FileTree.Newest(_modified),
        2 => FileTree.ByKind,
        _ => null,
    };

    // The first row starts selected: the project folder, or the first file of a list.
    void Rebuild()
    {
        var text = (_filter.Text ?? "").Trim();
        var rows = text.Length == 0 && _layout.SelectedIndex == 0 ? FileTree.Build(_files, Order()) : FileTree.Matches(_files, text, Order());
        _tree.ItemsSource = rows;
        _tree.SelectedItem = rows.FirstOrDefault();
        _noMatch.IsVisible = rows.Count == 0 && text.Length > 0;
        // The title is the second line of the empty state, under its mark.
        ((TextBlock)_noMatch.Children[1]).Text = $"No file matches \"{text}\"";
        ShowSelected();
    }

    void ShowSelected()
    {
        ShowMenu();
        // An imported file opens in the Import tab, which sits beside the tree, so a click is enough.
        if (_tree.SelectedItem is FileNode { Kind: FileKind.Imported } imported)
        {
            Chosen?.Invoke(imported.Path);
        }
    }

    void ShowMenu() => _menu.ItemsSource = _tree.SelectedItem is FileNode node ? MenuFor(node) : [];

    // The menu holds what fits the picked row. A folder makes new things in it. A file opens, and a scene also
    // starts the game or goes inside the open scene.
    Control[] MenuFor(FileNode node)
    {
        var copy = Parts.ActionItem("Copy path", CopyPath, new KeyGesture(Key.C, _command | KeyModifiers.Alt), $"Copies {node.Path}");
        if (node.Kind == FileKind.Folder)
        {
            return
            [
                Parts.MenuHeading("Make"),
                Parts.ActionItem("New scene…", () => NewSceneRequested?.Invoke(node.Path), note: "A new scene in this folder"),
                Parts.ActionItem("New graph…", () => NewGraphRequested?.Invoke(), note: "A script made of steps"),
                new Separator(),
                copy,
            ];
        }

        var open = Parts.ActionItem("Open", () => Activate(node), new KeyGesture(Key.Enter), node.Kind == FileKind.Imported ? "Shows how it comes in" : $"Opens {node.Name}");
        return node.Kind == FileKind.Scene ? [open, .. SceneItems(node), new Separator(), copy] : [open, new Separator(), copy];
    }

    // shortcut: Play from this scene has no key, because the window's key plays the open scene. Give it one if people ask.
    MenuItem[] SceneItems(FileNode scene)
    {
        var self = scene.Path == _open;
        var use = Parts.ActionItem("Use inside the open scene", () => UseRequested?.Invoke(scene.Path), note: self ? $"You are in {scene.Name}. A scene can't hold itself." : "Same as Add a scene inside");
        use.IsEnabled = !self;
        return [Parts.ActionItem("Play from this scene", () => PlayRequested?.Invoke(scene.Path), note: "Starts the game here"), use];
    }

    void PressKey(KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Activate(_tree.SelectedItem);
        }
        else if (e.Key == Key.C && e.KeyModifiers == (_command | KeyModifiers.Alt))
        {
            CopyPath();
        }
    }

    // shortcut: a clipboard that fails says nothing. Raise the fault to the status line when one is seen.
    void CopyPath()
    {
        if (_tree.SelectedItem is FileNode node)
        {
            _ = _clipboard.SetText(node.Path);
        }
    }

    void Remember(object? source, bool open)
    {
        if (source is TreeViewItem { DataContext: FileNode node })
        {
            _ = open ? _opened.Add(node.Path) : _opened.Remove(node.Path);
        }
    }

    void Activate(object? item)
    {
        if (item is FileNode { Kind: not FileKind.Folder } file)
        {
            Chosen?.Invoke(file.Path);
        }
    }

    void Tint(TextBlock name, FileNode node)
    {
        if (node.Kind == FileKind.Scene && node.Path == _open)
        {
            name.Foreground = EditorBrushes.Accent;
        }
        else
        {
            name.ClearValue(TextBlock.ForegroundProperty);
        }
    }

    // The name carries its row, so marking the open scene tints the name and not the kind beside it.
    Control Row(FileNode node)
    {
        var name = new TextBlock { Text = node.Name, Tag = node, VerticalAlignment = VerticalAlignment.Center };
        Tint(name, node);
        var label = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { name } };
        if (node.Kind != FileKind.Folder && Search.KindText(node.Path, _sceneKind(node.Path)) is { Length: > 0 } kind)
        {
            label.Children.Add(new TextBlock { Text = kind, FontSize = 11, Foreground = EditorBrushes.TextDim, VerticalAlignment = VerticalAlignment.Center });
        }

        var highlight = new RowHighlight();
        SetColumn(label, 1);
        SetColumnSpan(highlight, 2);
        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            Margin = new Thickness(-EditorTheme.RowOverhang, 0, 0, 0),
            Children = { highlight, Parts.RowIcon(IconOf(node.Kind), TintOf(node.Kind)), label },
        };
        // A nested row's item does not raise the tree's ContainerPrepared, so the row opens its own item.
        row.AttachedToVisualTree += (_, _) =>
        {
            if (row.FindAncestorOfType<TreeViewItem>() is { } item)
            {
                item.IsExpanded = node.Path == FileTree.Root || _opened.Contains(node.Path);
            }
        };
        return row;
    }

    internal static string IconOf(FileKind kind) => kind switch
    {
        FileKind.Folder => "Folder",
        FileKind.Scene => "SceneFile",
        FileKind.Script => "CodeFile",
        FileKind.Imported => "ImportArrow",
        _ => "GraphFile",
    };

    internal static IBrush TintOf(FileKind kind) => kind switch
    {
        FileKind.Folder => EditorBrushes.FolderIcon,
        FileKind.Scene => EditorBrushes.SceneIcon,
        FileKind.Script => EditorBrushes.ScriptIcon,
        FileKind.Imported => EditorBrushes.TreeIcon,
        _ => EditorBrushes.GraphIcon,
    };

    static void Place(Grid grid, Control child, int column)
    {
        SetColumn(child, column);
        grid.Children.Add(child);
    }
}
