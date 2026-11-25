using System.Text.Json;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
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

    /// <summary>Every class a game can make an entity of, by name.</summary>
    IReadOnlyList<string> AddableClasses();

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
/// The Graph workspace, as three islands. The left one lists the project's graph scripts with a filter, over the
/// panels of the script on show. The middle one holds a tab for each open script over its canvas, with the
/// debugger under it. The right one inspects the selected step. The File menu makes and closes scripts, Go to moves
/// about the script on show and Help opens the guide. An item that is off says what to do first.
/// </summary>
internal sealed class GraphView : Border
{
    readonly ListBox _list = new() { Background = null, BorderThickness = new Thickness(0), Classes = { EditorTheme.Rows } };
    readonly TextBox _filter = new() { PlaceholderText = "Filter graph scripts", Margin = new Thickness(8, 8, 8, 6), Classes = { EditorTheme.Field } };
    readonly StackPanel _tabs = new() { Orientation = Orientation.Horizontal, Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
    readonly TextBlock _status = new() { FontSize = 12, Foreground = EditorBrushes.TextDim, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0) };
    readonly TextBlock _extends = new() { FontSize = 12, Foreground = EditorBrushes.TextDim, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
    readonly TextBlock _hint = new() { Text = "Open a graph script, or choose File, New.", Foreground = EditorBrushes.TextDim, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    readonly Border _canvasSlot = new();
    readonly Border _panelsSlot = new() { BorderBrush = EditorBrushes.Line, BorderThickness = new Thickness(0, 1, 0, 0) };
    readonly Border _inspectorSlot = new();
    readonly Control _noStep = StepInspector.Empty();
    readonly ScrollViewer _debugHost = new() { MaxHeight = 240, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    readonly Button _fileButton = MenuButton("File");
    readonly Button _gotoButton = MenuButton("Go to");
    readonly Button _helpButton = MenuButton("Help");
    readonly Button _findButton = Parts.IconButton("Search", "Find steps");
    readonly Button _saveButton = new() { Classes = { EditorTheme.TextButton, EditorTheme.Small }, VerticalAlignment = VerticalAlignment.Center };
    readonly Button _bookmarkButton = Parts.IconButton("Favorites", "Bookmark");
    readonly Button _breakpointButton = Parts.IconButton("Breakpoint", "Toggle Breakpoint");
    readonly GotoMenu _gotoMenu;
    readonly MenuFlyout _menu = new();
    readonly MenuItem _save;
    readonly MenuItem _saveAll;
    readonly MenuItem _close;
    readonly MenuItem _closeAll;
    readonly MenuItem _copyPath;
    readonly MenuItem _showInFiles;
    readonly CreateScriptForm _form = new(ScriptKind.Graph);
    readonly ITextClipboard _clipboard;
    readonly List<GraphSheet> _sheets = [];
    IReadOnlyList<string> _all = [];
    IGraphFiles? _files;
    GraphDebugger? _debugger;
    DebugPlace? _shownStop;
    GraphSheet? _current;

    public GraphView(ITextClipboard? clipboard = null, ILinkOpener? links = null)
    {
        _clipboard = clipboard ?? new WindowClipboard(this);
        _gotoMenu = new GotoMenu(() => Canvas);
        _gotoButton.Flyout = _gotoMenu;
        _helpButton.Flyout = new HelpMenu(links ?? new WindowLinks(this));
        _save = Parts.PressItem("Save", _saveButton, new KeyGesture(Key.S, KeyModifiers.Meta));
        _saveAll = Parts.PressItem("Save all", SaveAllButton, new KeyGesture(Key.S, KeyModifiers.Meta | KeyModifiers.Alt));
        _close = Parts.PressItem("Close", CloseButton, new KeyGesture(Key.W, KeyModifiers.Meta));
        _closeAll = Parts.ActionItem("Close all", CloseAll);
        _copyPath = Parts.ActionItem("Copy path", CopyPath);
        _showInFiles = Parts.PressItem(SceneTabStrip.ShowInFiles, ShowInFilesButton);
        _menu.Items.Add(Parts.ActionItem("New graph…", ShowCreate, note: "A script made of steps. You pick what it runs on."));
        _menu.Items.Add(new Separator());
        _menu.Items.Add(_save);
        _menu.Items.Add(_saveAll);
        _menu.Items.Add(new Separator());
        _menu.Items.Add(_close);
        _menu.Items.Add(_closeAll);
        _menu.Items.Add(new Separator());
        _menu.Items.Add(_copyPath);
        _menu.Items.Add(_showInFiles);
        _fileButton.Flyout = _menu;
        SaveAllButton.Click += (_, _) => Save();
        CloseButton.Click += (_, _) => CloseCurrent();
        _saveButton.Content = Parts.Keyed("Save", "⌘S");
        _saveButton.Click += (_, _) => SaveCurrent();
        _findButton.Click += (_, _) => Canvas?.FindBar.Open();
        _bookmarkButton.Click += (_, _) => Canvas?.FindBar.Run(search => search.ToggleBookmark());
        _breakpointButton.Click += (_, _) => Canvas?.FindBar.Run(search => search.ToggleBreakpoint());
        _form.Requested += Create;
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

    internal Button FindButton => _findButton;

    internal Button SaveButton => _saveButton;

    internal Button BookmarkButton => _bookmarkButton;

    internal Button BreakpointButton => _breakpointButton;

    /// <summary>Saves every open graph. The File menu and a key press it.</summary>
    internal Button SaveAllButton { get; } = new();

    /// <summary>Closes the graph on show. The File menu and a key press it.</summary>
    internal Button CloseButton { get; } = new();

    /// <summary>The window picks the graph on show in the Files panel when this is pressed.</summary>
    internal Button ShowInFilesButton { get; } = new();

    /// <summary>The class the script on show extends, as the canvas bar says it.</summary>
    internal TextBlock Extends => _extends;

    internal MenuFlyout FileMenu => _menu;

    internal CreateScriptForm Form => _form;

    internal Dialog CreateDialog => _form.Dialog;

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

    /// <summary>The res:// path of each graph script with edits.</summary>
    internal IEnumerable<string> EditedScripts => _sheets.Where(sheet => sheet.IsEdited).Select(sheet => sheet.Script);

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

    static Button MenuButton(string text) => new() { Content = text, Classes = { EditorTheme.TextButton, EditorTheme.Segment } };

    static Border Island(Control child, int column) => Place(new Border { Classes = { EditorTheme.Island }, ClipToBounds = true, Child = child }, column);

    static T Place<T>(T control, int column, int row = 0) where T : Control
    {
        Grid.SetColumn(control, column);
        Grid.SetRow(control, row);
        return control;
    }

    Grid Layout()
    {
        var list = new DockPanel();
        foreach (var top in new Control[] { Parts.Bar(_fileButton, _gotoButton, _helpButton, Parts.Right(_findButton)), _filter, new ScrollViewer { MaxHeight = 220, Content = _list } })
        {
            DockPanel.SetDock(top, Dock.Top);
            list.Children.Add(top);
        }
        list.Children.Add(_panelsSlot);

        var canvas = new DockPanel();
        var canvasBar = Parts.Bar(_tabs, _extends, Parts.Right(_saveButton), Parts.Right(_status));
        DockPanel.SetDock(canvasBar, Dock.Top);
        canvas.Children.Add(canvasBar);
        canvas.Children.Add(new Grid { Children = { _hint, _canvasSlot } });
        var middle = new Grid { RowDefinitions = new RowDefinitions("*,6,Auto") };
        middle.Children.Add(Island(canvas, 0));
        middle.Children.Add(Place(new Border { Classes = { EditorTheme.Island }, Child = _debugHost }, 0, 2));

        var tab = new Border
        {
            Height = 26,
            Padding = new Thickness(10, 0),
            CornerRadius = EditorTheme.Corners,
            Background = EditorBrushes.Line,
            Child = new TextBlock { Text = "Step", FontWeight = FontWeight.Medium, VerticalAlignment = VerticalAlignment.Center },
        };
        var inspector = new DockPanel();
        var inspectorBar = Parts.Bar(tab, Parts.Right(_breakpointButton), Parts.Right(_bookmarkButton));
        DockPanel.SetDock(inspectorBar, Dock.Top);
        inspector.Children.Add(inspectorBar);
        inspector.Children.Add(new Grid { Children = { _noStep, _inspectorSlot } });

        return new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("248,6,*,6,292"),
            Children = { Island(list, 0), Place(middle, 2), Island(inspector, 4) },
        };
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
        _debugHost.Content = new DebuggerPanel(_debugger, place => _current?.StepTitle(place));
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
    internal void Open(string? script)
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

    internal void ShowCreate() => _form.Open(_all, _files?.AddableClasses() ?? []);

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

        _form.Dialog.Close();
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
        _noStep.IsVisible = _current is null;
        _extends.Text = _current is null ? "" : $"extends {_current.Graph.Document.BaseType}";
        _saveButton.IsEnabled = _current?.IsEdited == true;
        _findButton.IsEnabled = _current is not null;
        _bookmarkButton.IsEnabled = _current is not null;
        _breakpointButton.IsEnabled = _current is not null;
        SaveAllButton.IsEnabled = IsDirty;
        CloseButton.IsEnabled = _current is not null;
        ShowInFilesButton.IsEnabled = _current is not null;
        _closeAll.IsEnabled = _sheets.Count > 0;
        _copyPath.IsEnabled = _current is not null;
        var name = _current is null ? "" : Path.GetFileNameWithoutExtension(_current.Script);
        var none = "Open a graph first.";
        Note(_save, $"Saves {name}", _current is null ? none : "Nothing new to save.");
        Note(_saveAll, "Saves every open graph", "Nothing new to save.");
        Note(_close, $"Saves {name} first, then closes it", "Nothing is open.");
        Note(_copyPath, _current?.Script ?? "", none);
        Note(_showInFiles, "Picks the file in the Files panel", none);
        _gotoMenu.Show(_current is not null);
    }

    // An item's note says what it does while it is on, and what to do first while it is off.
    static void Note(MenuItem item, string note, string why) =>
        item.Header = Parts.Noted(AutomationProperties.GetName(item) ?? "", item.IsEnabled ? note : why);
}
