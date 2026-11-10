using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Orblit.Editor.App.Shell;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App;

/// <summary>
/// The body of the FileSystem tab: a row with the history arrows, the path and the layout button, a filter row,
/// and the tree of the project's scene files. A double press or Enter on a scene raises it to open.
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
        PlaceholderText = "Filter Files",
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
    IReadOnlyList<string> _scenes = [];
    IReadOnlyDictionary<string, DateTime> _modified = new Dictionary<string, DateTime>();
    string _open = "";
    bool _flat;
    bool _newest;

    public ScenesPanel()
    {
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
        };
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

    /// <summary>Raised with the res:// path of the scene to open.</summary>
    public event Action<string>? Chosen;

    internal TreeView Tree => _tree;

    internal TreeGuides Guides => _guides;

    internal TextBox PathBox => _path;

    internal TextBox FilterBox => _filter;

    internal Button BackButton => _back;

    internal Button ForwardButton => _forward;

    internal Button LayoutButton => _layout;

    internal Button SortButton => _sort;

    /// <summary>Shows the scenes. The times of the files order them when the sort is newest first.</summary>
    public void Show(IReadOnlyList<string> scenes, IReadOnlyDictionary<string, DateTime> modified)
    {
        _scenes = scenes;
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

    // The project folder is selected, as in the reference.
    void Rebuild()
    {
        var text = (_filter.Text ?? "").Trim();
        var order = _newest ? FileTree.Newest(_modified) : null;
        var rows = text.Length == 0 && !_flat ? FileTree.Build(_scenes, order) : FileTree.Matches(_scenes, text, order);
        _tree.ItemsSource = rows;
        _tree.SelectedItem = rows[1];
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
        if (item is FileNode { Kind: FileKind.Scene } scene)
        {
            Chosen?.Invoke(scene.Path);
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
            Children = { highlight, Parts.RowIcon(IconOf(node.Kind), node.Kind == FileKind.Folder ? EditorBrushes.FolderIcon : null), name },
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
        FileKind.Favorites => "Favorites",
        FileKind.Folder => "Folder",
        _ => "Prefab",
    };

    static void Place(Grid grid, Control child, int column)
    {
        SetColumn(child, column);
        grid.Children.Add(child);
    }
}
