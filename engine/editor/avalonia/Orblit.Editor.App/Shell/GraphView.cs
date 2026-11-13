using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Orblit.Editor.App.Graph;
using Orblit.Editor.App.Theme;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App.Shell;

/// <summary>A graph script open for editing. Disposing it lets the engine drop its copy, so an unsaved edit is gone.</summary>
internal interface IGraphFile : IGraphSource, IScriptDeclarations, IGraphSnapshots, IDisposable
{
    // The canvas and the panels each declare these, with the same meaning, so a file is both. The file
    // repeats them to give one member for a caller that holds the file.
    new GraphFunction? Function { get; }

    new event Action? Changed;

    new GraphEditResult SetStepParameter(int id, string parameter, string valueText);

    /// <summary>Writes the script to its file. Throws <see cref="IOException"/> when it cannot be written.</summary>
    void Save();
}

/// <summary>What the Graph workspace needs from the project: its graph scripts, a way to make one, and the link to the game.</summary>
internal interface IGraphFiles
{
    /// <summary>The res:// path of every graph script of the project.</summary>
    IReadOnlyList<string> GraphScripts();

    /// <summary>Opens a graph script. Throws <see cref="IOException"/> when the engine cannot load it.</summary>
    IGraphFile OpenGraph(string resourcePath);

    /// <summary>
    /// Writes an empty graph script that extends a class. Throws <see cref="IOException"/> when the file exists or cannot be
    /// written, and <see cref="ArgumentException"/> when the path is no graph script file or the class is not one.
    /// </summary>
    void CreateGraph(string resourcePath, string baseType);

    /// <summary>The link to the game Play runs, which reports where it stopped and takes the commands that go on.</summary>
    IDebugChannel Debug { get; }

    /// <summary>The saved breakpoints of the project's graph scripts, or null when it has none. Throws <see cref="IOException"/> when it cannot be read.</summary>
    string? ReadBreakpoints();

    /// <summary>Replaces the saved breakpoints. Throws <see cref="IOException"/> when it cannot be written.</summary>
    void WriteBreakpoints(string text);
}

/// <summary>
/// The Graph workspace: the project's graph scripts in a list with a filter, a tab for each one open, and for the one
/// on show its canvas, side panels and the debugger. The File menu makes and closes scripts, Goto moves about the
/// script on show and Help opens the guide.
/// </summary>
internal sealed class GraphView : Border
{
    readonly ListBox _list = new() { Background = null, BorderThickness = new Thickness(0) };
    readonly TextBox _filter = new() { PlaceholderText = "Filter graph scripts", Margin = new Thickness(6) };
    readonly StackPanel _tabs = new() { Orientation = Orientation.Horizontal, Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
    readonly TextBlock _status = new() { Foreground = EditorBrushes.TabInactive, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0) };
    readonly TextBlock _hint = new() { Text = "Open a graph script, or choose File, New.", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    readonly Border _canvasSlot = new();
    readonly Border _panelsSlot = new();
    readonly Border _inspectorSlot = new();
    readonly Border _side = new() { IsVisible = false, Width = 320, BorderThickness = new Thickness(1, 0, 0, 0), BorderBrush = EditorBrushes.TabInactive };
    readonly ScrollViewer _debugHost = new() { MaxHeight = 240, Padding = new Thickness(10, 6), HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    readonly Button _fileButton = new() { Content = "File", Padding = new Thickness(12, 4), MinHeight = 0 };
    readonly Button _gotoButton = new() { Content = "Goto", Padding = new Thickness(12, 4), MinHeight = 0 };
    readonly Button _helpButton = new() { Content = "Help", Padding = new Thickness(12, 4), MinHeight = 0 };
    readonly GotoMenu _gotoMenu;
    readonly MenuFlyout _menu = new();
    readonly MenuItem _save;
    readonly MenuItem _saveAll;
    readonly MenuItem _close;
    readonly MenuItem _closeAll;
    readonly MenuItem _copyPath;
    readonly CreateGraphForm _form = new();
    readonly Flyout _createFlyout;
    readonly ITextClipboard _clipboard;
    readonly List<GraphSheet> _sheets = [];
    IReadOnlyList<string> _all = [];
    IGraphFiles? _files;
    GraphDebugger? _debugger;
    DebugPlace? _shownStop;
    GraphSheet? _current;

    public GraphView(ITextClipboard? clipboard = null, ILinkOpener? links = null)
    {
        Background = EditorBrushes.Panel;
        _clipboard = clipboard ?? new WindowClipboard(this);
        _gotoMenu = new GotoMenu(() => Canvas);
        _gotoButton.Flyout = _gotoMenu;
        _helpButton.Flyout = new HelpMenu(links ?? new WindowLinks(this));
        _createFlyout = new Flyout { Content = _form, Placement = PlacementMode.BottomEdgeAlignedLeft };
        _save = Item("Save", SaveCurrent);
        _saveAll = Item("Save All", () => Save());
        _close = Item("Close", CloseCurrent);
        _closeAll = Item("Close All", CloseAll);
        _copyPath = Item("Copy Path", CopyPath);
        _menu.Items.Add(Item("New...", ShowCreate));
        _menu.Items.Add(new Separator());
        _menu.Items.Add(_save);
        _menu.Items.Add(_saveAll);
        _menu.Items.Add(new Separator());
        _menu.Items.Add(_close);
        _menu.Items.Add(_closeAll);
        _menu.Items.Add(new Separator());
        _menu.Items.Add(_copyPath);
        // The FileSystem dock lists scenes only, so there is nothing for it to select yet.
        _menu.Items.Add(Item("Show in FileSystem", () => _fileButton.RaiseEvent(new UnbuiltEventArgs("The FileSystem dock does not list graph scripts yet."))));
        _fileButton.Flyout = _menu;
        _form.Requested += Create;
        _form.Cancelled += _createFlyout.Hide;
        _list.SelectionChanged += (_, _) => Open(_list.SelectedItem as string);
        // TextChanged waits for a render, so the property is the one that fires at once.
        _filter.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty)
            {
                ShowList();
            }
        };
        Child = Layout();
        ShowState();
    }

    internal ListBox List => _list;

    internal TextBox Filter => _filter;

    internal TextBlock Status => _status;

    internal TextBlock Hint => _hint;

    internal Button FileButton => _fileButton;

    internal GotoMenu GotoMenu => _gotoMenu;

    internal Button HelpButton => _helpButton;

    internal MenuFlyout FileMenu => _menu;

    internal CreateGraphForm Form => _form;

    internal Flyout CreateFlyout => _createFlyout;

    /// <summary>One tab for each open graph script, in the order they were opened.</summary>
    internal IEnumerable<GraphTab> Tabs => _tabs.Children.Cast<GraphTab>();

    /// <summary>The breakpoints of every graph script opened here. They outlive the canvas that shows them, and the project keeps them.</summary>
    internal GraphBreakpoints Breakpoints { get; } = new();

    /// <summary>The res:// path of the graph script on show, or null when none is open.</summary>
    internal string? Current => _current?.Script;

    /// <summary>The canvas of the graph script on show, or null when none is open.</summary>
    internal GraphCanvas? Canvas => _current?.Canvas;

    /// <summary>The panel of the debug session, or null before the files are shown.</summary>
    internal DebuggerPanel? DebugPanel => _debugHost.Content as DebuggerPanel;

    /// <summary>The panels of the graph script on show, or null when none is open.</summary>
    internal ScriptPanels? Panels => _current?.Panels;

    /// <summary>The inspector of the selected step of the graph script on show, or null when none is open.</summary>
    internal StepInspector? Inspector => _current?.Inspector;

    /// <summary>Whether an open graph script has edits that the file does not hold.</summary>
    public bool IsDirty => _sheets.Any(sheet => sheet.IsEdited);

    /// <summary>Starts from this project's files, with no script open.</summary>
    public void Show(IGraphFiles files)
    {
        _files = files;
        StartDebugger(files);
        foreach (var sheet in _sheets)
        {
            sheet.Dispose();
        }

        _sheets.Clear();
        _tabs.Children.Clear();
        Select(null);
        LoadBreakpoints(files);
        Refresh();
    }

    /// <summary>Takes in what the running game said. A stop on a step of a graph script opens the script and shows the step.</summary>
    public void PollDebugger() => _debugger?.Poll();

    /// <summary>Writes every open script that has edits. Answers whether all the files hold their graphs now.</summary>
    public bool Save()
    {
        var saved = true;
        foreach (var sheet in _sheets)
        {
            saved &= Save(sheet);
        }

        return saved;
    }

    /// <summary>Counts every open graph as saved, for a close that drops the edits.</summary>
    public void Discard()
    {
        foreach (var sheet in _sheets)
        {
            sheet.MarkSaved();
        }

        ShowState();
    }

    // The list is read again each time the workspace shows, so a file made since is listed.
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsVisibleProperty && IsVisible)
        {
            Refresh();
        }
    }

    static bool Failed(Exception error) => error is IOException or UnauthorizedAccessException;

    static MenuItem Item(string header, Action action)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => action();
        return item;
    }

    Grid Layout()
    {
        var left = new DockPanel { Width = 240 };
        DockPanel.SetDock(_filter, Dock.Top);
        left.Children.Add(_filter);
        left.Children.Add(_list);
        var header = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(6, 4) };
        header.Children.Add(_fileButton);
        header.Children.Add(_gotoButton);
        header.Children.Add(_helpButton);
        header.Children.Add(_tabs);
        header.Children.Add(_status);
        // The right column stacks the script's panels, the debugger and the step inspector.
        var column = new Grid { RowDefinitions = new RowDefinitions("*,Auto,Auto") };
        Grid.SetRow(_debugHost, 1);
        Grid.SetRow(_inspectorSlot, 2);
        column.Children.Add(_panelsSlot);
        column.Children.Add(_debugHost);
        column.Children.Add(_inspectorSlot);
        _side.Child = column;
        var area = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        Grid.SetColumn(_side, 1);
        area.Children.Add(_hint);
        area.Children.Add(_canvasSlot);
        area.Children.Add(_side);
        var body = new DockPanel();
        DockPanel.SetDock(header, Dock.Top);
        body.Children.Add(header);
        body.Children.Add(area);
        var root = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        Grid.SetColumn(body, 1);
        root.Children.Add(left);
        root.Children.Add(body);
        return root;
    }

    // The project keeps the breakpoints, so a person's stops are still there when the editor opens again. A file that
    // cannot be read leaves none and says why. Showing the files again must not write each change twice.
    void LoadBreakpoints(IGraphFiles files)
    {
        Breakpoints.Changed -= SaveBreakpoints;
        Breakpoints.Changed += SaveBreakpoints;
        try
        {
            Breakpoints.Load(files.ReadBreakpoints());
        }
        catch (JsonException)
        {
            _status.Text = "The saved breakpoints could not be read.";
        }
        catch (Exception error) when (Failed(error))
        {
            _status.Text = error.Message;
        }
    }

    void SaveBreakpoints(string source, int step, bool isSet)
    {
        try
        {
            _files!.WriteBreakpoints(Breakpoints.ToJson());
        }
        catch (Exception error) when (Failed(error))
        {
            _status.Text = $"The breakpoints were not saved. {error.Message}";
        }
    }

    void StartDebugger(IGraphFiles files)
    {
        _debugger?.Dispose();
        _shownStop = null;
        _debugger = new GraphDebugger(files.Debug, Breakpoints);
        _debugger.Changed += ShowStop;
        _debugHost.Content = new DebuggerPanel(_debugger);
    }

    // The view moves to a stop once. A poll that only brings values leaves the view where the person put it.
    void ShowStop()
    {
        var place = _debugger!.Current;
        if (place != _shownStop && place is { } at)
        {
            Reveal(at);
        }

        _shownStop = place;
        _current?.ShowStop(place);
    }

    void Reveal(DebugPlace at)
    {
        if (_files is null || !_files.GraphScripts().Contains(at.Script))
        {
            return;
        }

        Open(at.Script);
        if (_current?.Script == at.Script)
        {
            _current.Panels.Reveal(at.Function, at.Step);
        }
    }

    void Refresh()
    {
        _all = _files?.GraphScripts() ?? [];
        ShowList();
    }

    void ShowList()
    {
        var filter = _filter.Text?.Trim() ?? "";
        var shown = _all.Where(path => path.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();
        // The list keeps a selection that the new source still holds, which would open that script by itself.
        _list.SelectedItem = null;
        _list.ItemsSource = shown;
        _list.SelectedItem = _current is { } sheet && shown.Contains(sheet.Script) ? sheet.Script : null;
    }

    // A script that is open already is shown again with its edits. One that is not is loaded and gets a tab.
    void Open(string? script)
    {
        if (script is null || script == _current?.Script || _files is null)
        {
            return;
        }

        var sheet = _sheets.Find(open => open.Script == script);
        if (sheet is null)
        {
            try
            {
                sheet = new GraphSheet(script, _files.OpenGraph(script), Breakpoints, _debugger!);
            }
            catch (Exception error) when (Failed(error))
            {
                _status.Text = error.Message;
                _list.SelectedItem = _current?.Script;
                return;
            }

            AddTab(sheet);
        }

        _status.Text = "";
        Select(sheet);
    }

    void AddTab(GraphSheet sheet)
    {
        var tab = new GraphTab(sheet.Script);
        tab.Chosen += () => Select(sheet);
        tab.CloseRequested += () => Close(sheet);
        sheet.Edited += ShowState;
        _sheets.Add(sheet);
        _tabs.Children.Add(tab);
    }

    void Select(GraphSheet? sheet)
    {
        _current = sheet;
        _list.SelectedItem = sheet?.Script;
        _canvasSlot.Child = sheet?.Host;
        _panelsSlot.Child = sheet?.Panels;
        _inspectorSlot.Child = sheet?.Inspector;
        sheet?.ShowStop(_debugger?.Current);
        ShowState();
    }

    bool Save(GraphSheet sheet)
    {
        if (!sheet.IsEdited)
        {
            return true;
        }

        try
        {
            sheet.Save();
            _status.Text = $"Saved {sheet.Script}.";
            ShowState();
            return true;
        }
        catch (Exception error) when (Failed(error))
        {
            _status.Text = error.Message;
            return false;
        }
    }

    void SaveCurrent()
    {
        if (_current is { } sheet)
        {
            Save(sheet);
        }
    }

    // A close writes the script first, as opening a scene does. A failed write keeps the script open.
    void Close(GraphSheet sheet)
    {
        if (!Save(sheet))
        {
            return;
        }

        var index = _sheets.IndexOf(sheet);
        _sheets.Remove(sheet);
        _tabs.Children.RemoveAt(index);
        if (_current == sheet)
        {
            Select(_sheets.Count == 0 ? null : _sheets[Math.Min(index, _sheets.Count - 1)]);
        }

        sheet.Dispose();
        ShowList();
    }

    void CloseCurrent()
    {
        if (_current is { } sheet)
        {
            Close(sheet);
        }
    }

    void CloseAll()
    {
        foreach (var sheet in _sheets.ToList())
        {
            Close(sheet);
        }
    }

    void CopyPath()
    {
        if (_current is { } sheet)
        {
            _ = _clipboard.SetText(sheet.Script);
            _status.Text = $"Copied {sheet.Script}.";
        }
    }

    void ShowCreate()
    {
        _form.Reset();
        _createFlyout.ShowAt(_fileButton);
    }

    void Create(string path, string baseType)
    {
        if (_files is null)
        {
            return;
        }

        try
        {
            _files.CreateGraph(path, baseType);
        }
        catch (Exception error) when (Failed(error) || error is ArgumentException)
        {
            _form.Say(error.Message);
            return;
        }

        _createFlyout.Hide();
        Refresh();
        Open(path);
        _status.Text = $"Created {path}.";
    }

    // The tabs show which script is on show and which have edits, and the menu offers what the state allows.
    void ShowState()
    {
        // A tab is added and removed with its sheet, so the two lists run in step.
        foreach (var (tab, sheet) in Tabs.Zip(_sheets))
        {
            tab.Show(sheet == _current, sheet.IsEdited);
        }

        _hint.IsVisible = _current is null;
        _side.IsVisible = _current is not null;
        _save.IsEnabled = _current?.IsEdited == true;
        _saveAll.IsEnabled = IsDirty;
        _close.IsEnabled = _current is not null;
        _closeAll.IsEnabled = _sheets.Count > 0;
        _copyPath.IsEnabled = _current is not null;
        _gotoMenu.Show(_current is not null);
    }
}
