using Avalonia;
using Avalonia.Controls;
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
/// The body of the FileSystem tab: a row with the history arrows, the path and the layout button, a filter row,
/// and the tree of the project's scenes, C# scripts and graph scripts, each tinted by kind. A double press or Enter
/// on a file raises it to open, and a right click opens the file menu.
/// </summary>
internal sealed class ScenesPanel : Grid
{
    const double PathRowHeight = 36;
    const double BoxHeight = 28;

    // The tree starts 14 pixels under the filter in the reference.
    const double TreeTop = 14;

    const string ListTip = "Show the files as a list";
    const string TreeTip = "Show the files as a tree";
    const string NewestTip = "Sort the files newest first";
    const string NameTip = "Sort the files by name";

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
        PlaceholderText = "Filter files",
        Height = BoxHeight,
        MinHeight = 0,
        VerticalAlignment = VerticalAlignment.Center,
        InnerRightContent = Parts.SearchIcon(),
    };
    readonly Button _back = Parts.Tight(Parts.IconButton("Back", "Go back"));
    readonly Button _forward = Parts.Tight(Parts.IconButton("Forward", "Go forward"));
    readonly Button _layout = Parts.Tight(Parts.IconButton("Panels1", ListTip));
    readonly Button _sort = Parts.Tight(Parts.IconButton("Sort", NewestTip));
    readonly TreeGuides _guides;
    readonly ITextClipboard _clipboard;
    IReadOnlyList<string> _files = [];
    IReadOnlyDictionary<string, DateTime> _modified = new Dictionary<string, DateTime>();
    string _open = "";
    bool _flat;
    bool _newest;

    public ScenesPanel(KeyModifiers command, ITextClipboard? clipboard = null)
    {
        _clipboard = clipboard ?? new WindowClipboard(this);
        RowDefinitions = new RowDefinitions($"{PathRowHeight},{BoxHeight},*");
        _back.IsEnabled = false;
        _forward.IsEnabled = false;
        foreach (var arrow in new[] { _back, _forward, _layout })
        {
            arrow.VerticalAlignment = VerticalAlignment.Top;
            arrow.Margin = new Thickness(0, 5, 0, 0);
        }

        _layout.Click += (_, _) =>
        {
            _flat = !_flat;
            ToolTip.SetTip(_layout, _flat ? TreeTip : ListTip);
            Rebuild();
        };
        _sort.Click += (_, _) =>
        {
            _newest = !_newest;
            ToolTip.SetTip(_sort, _newest ? NameTip : NewestTip);
            Rebuild();
        };
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
        _tree.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                Activate(_tree.SelectedItem);
            }
            else if (e.Key == Key.C && e.KeyModifiers == (command | KeyModifiers.Alt))
            {
                CopyPath();
            }
        };
        _tree.ContextMenu = FileMenu(command);
        _tree.SelectionChanged += (_, _) => ShowSelected();
        // TextChanged waits for a render, so the property is the one that fires at once.
        _filter.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty)
            {
                Rebuild();
            }
        };
        var pathRow = new Grid { ColumnDefinitions = new ColumnDefinitions("24,24,6,*,6,24"), Margin = new Thickness(2, 0, 7, 0) };
        Place(pathRow, _back, 0);
        Place(pathRow, _forward, 1);
        Place(pathRow, _path, 3);
        Place(pathRow, _layout, 5);
        var filterRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,6,24"), Margin = new Thickness(4, 0, 7, 0) };
        Place(filterRow, _filter, 0);
        Place(filterRow, _sort, 2);
        SetRow(filterRow, 1);
        SetRow(_tree, 2);
        _guides = new TreeGuides(_tree);
        SetRow(_guides, 2);
        Children.Add(pathRow);
        Children.Add(filterRow);
        Children.Add(_tree);
        Children.Add(_guides);
    }

    /// <summary>Raised with the res:// path of the file to open.</summary>
    public event Action<string>? Chosen;

    /// <summary>Raised when the file menu asks for a new graph script.</summary>
    public event Action? NewGraphRequested;

    /// <summary>Opens the selected file. It is on while a file is selected.</summary>
    internal Button OpenButton { get; } = new();

    /// <summary>Copies the res:// path of the selected row. It is on while a row is selected.</summary>
    internal Button CopyPathButton { get; } = new();

    internal TreeView Tree => _tree;

    internal TreeGuides Guides => _guides;

    internal TextBox PathBox => _path;

    internal TextBox FilterBox => _filter;

    internal Button BackButton => _back;

    internal Button ForwardButton => _forward;

    internal Button LayoutButton => _layout;

    internal Button SortButton => _sort;

    /// <summary>Shows the files. Their times order them when the sort is newest first.</summary>
    public void Show(IReadOnlyList<string> files, IReadOnlyDictionary<string, DateTime> modified)
    {
        _files = files;
        _modified = modified;
        Rebuild();
    }

    /// <summary>Names the open scene in the accent colour. An empty path means the open scene has no file.</summary>
    public void MarkOpen(string resourcePath)
    {
        _open = resourcePath;
        foreach (var name in _tree.GetVisualDescendants().OfType<TextBlock>().Where(text => text.DataContext is FileNode))
        {
            Tint(name, (FileNode)name.DataContext!);
        }
    }

    // The first row is selected, as in the reference: the project folder, or the first file of a list.
    void Rebuild()
    {
        var text = (_filter.Text ?? "").Trim();
        var order = _newest ? FileTree.Newest(_modified) : null;
        var rows = text.Length == 0 && !_flat ? FileTree.Build(_files, order) : FileTree.Matches(_files, text, order);
        _tree.ItemsSource = rows;
        _tree.SelectedItem = rows.FirstOrDefault();
        ShowSelected();
    }

    void ShowSelected()
    {
        CopyPathButton.IsEnabled = _tree.SelectedItem is FileNode;
        OpenButton.IsEnabled = _tree.SelectedItem is FileNode { Kind: not FileKind.Folder };
    }

    // The file menu acts on the selected row. What the editor cannot do to a file yet says so.
    ContextMenu FileMenu(KeyModifiers command)
    {
        OpenButton.Click += (_, _) => Activate(_tree.SelectedItem);
        CopyPathButton.Click += (_, _) => CopyPath();
        return new ContextMenu
        {
            ItemsSource = new Control[]
            {
                Parts.PressItem("Open", OpenButton),
                Parts.SoonItem("Show in Finder", _tree),
                Parts.PressItem("Copy Path", CopyPathButton, new KeyGesture(Key.C, command | KeyModifiers.Alt)),
                new Separator(),
                Parts.SoonItem("Rename...", _tree),
                Parts.SoonItem("Duplicate...", _tree),
                Parts.SoonItem("Move To...", _tree),
                new Separator(),
                Parts.SoonItem("New Script...", _tree),
                Parts.ActionItem("New Graph Script...", () => NewGraphRequested?.Invoke()),
                Parts.SoonItem("New Folder...", _tree),
                new Separator(),
                Parts.SoonItem("Delete", _tree),
            },
        };
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

    Control Row(FileNode node)
    {
        var name = new TextBlock { Text = node.Name, VerticalAlignment = VerticalAlignment.Center };
        Tint(name, node);
        var highlight = new RowHighlight();
        SetColumn(name, 1);
        SetColumnSpan(highlight, 2);
        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            Margin = new Thickness(-EditorTheme.RowOverhang, 0, 0, 0),
            Children = { highlight, Parts.RowIcon(IconOf(node.Kind), TintOf(node.Kind)), name },
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

    static string IconOf(FileKind kind) => kind switch
    {
        FileKind.Folder => "Folder",
        FileKind.Scene => "SceneFile",
        FileKind.Script => "CodeFile",
        _ => "GraphFile",
    };

    static IBrush TintOf(FileKind kind) => kind switch
    {
        FileKind.Folder => EditorBrushes.FolderIcon,
        FileKind.Scene => EditorBrushes.SceneIcon,
        FileKind.Script => EditorBrushes.ScriptIcon,
        _ => EditorBrushes.GraphIcon,
    };

    static void Place(Grid grid, Control child, int column)
    {
        SetColumn(child, column);
        grid.Children.Add(child);
    }
}
