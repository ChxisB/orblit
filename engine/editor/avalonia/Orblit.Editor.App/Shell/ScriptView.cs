using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Reactive;
using Orblit.Editor.App.Graph;
using Orblit.Editor.App.Theme;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App.Shell;

/// <summary>A graph script open for editing. Disposing it lets the engine drop its copy, so an unsaved edit is gone.</summary>
internal interface IGraphFile : IGraphSource, IScriptDeclarations, IDisposable
{
    // The canvas and the panels each declare these, with the same meaning, so a file is both. The file
    // repeats them to give one member for a caller that holds the file.
    new GraphFunction? Function { get; }

    new event Action? Changed;

    new GraphEditResult SetStepParameter(int id, string parameter, string valueText);

    /// <summary>Writes the script to its file. Throws <see cref="IOException"/> when it cannot be written.</summary>
    void Save();
}

/// <summary>What the script workspace needs from the project: its C# files and their text, and its graph scripts.</summary>
internal interface IScriptFiles
{
    /// <summary>The res:// path of every C# file of the project.</summary>
    IReadOnlyList<string> Scripts();

    /// <summary>The res:// path of every graph script of the project.</summary>
    IReadOnlyList<string> GraphScripts();

    /// <summary>Opens a graph script. Throws <see cref="IOException"/> when the engine cannot load it.</summary>
    IGraphFile OpenGraph(string resourcePath);

    /// <summary>The text of a script. Throws <see cref="IOException"/> when it cannot be read.</summary>
    string ReadScript(string resourcePath);

    /// <summary>Replaces the text of a script. Throws <see cref="IOException"/> when it cannot be written.</summary>
    void WriteScript(string resourcePath, string text);
}

/// <summary>
/// The Script workspace: the project's C# files and graph scripts in a list. A C# file opens in a text box with line
/// numbers and a graph script opens on a canvas. It reads and writes the files and does not compile or run them.
/// </summary>
internal sealed class ScriptView : Border
{
    static readonly FontFamily s_mono = new("Menlo, Consolas, DejaVu Sans Mono");

    // shortcut: one text box holds the whole file, so a file of many thousands of lines types slowly. Use a virtualized editor then.
    readonly TextBox _editor = new()
    {
        AcceptsReturn = true,
        AcceptsTab = true,
        TextWrapping = TextWrapping.NoWrap,
        FontFamily = s_mono,
        Padding = new Thickness(0),
        BorderThickness = new Thickness(0),
        Background = null,
    };

    readonly TextBlock _gutter = new() { FontFamily = s_mono, TextAlignment = TextAlignment.Right, Foreground = EditorBrushes.TabInactive, Margin = new Thickness(8, 0) };
    readonly ListBox _list = new() { Background = null, BorderThickness = new Thickness(0), Width = 240 };
    readonly TextBlock _name = new() { Text = "Pick a script.", FontWeight = FontWeight.Bold, VerticalAlignment = VerticalAlignment.Center };
    readonly TextBlock _status = new() { Foreground = EditorBrushes.TabInactive, VerticalAlignment = VerticalAlignment.Center };
    readonly Button _save = new() { Content = "Save", Padding = new Thickness(12, 4), MinHeight = 0, IsEnabled = false };
    readonly ScrollViewer _text;
    readonly Border _graphHost = new() { IsVisible = false };
    readonly Border _panelsHost = new() { IsVisible = false, Width = 320, BorderThickness = new Thickness(1, 0, 0, 0), BorderBrush = EditorBrushes.TabInactive };
    IScriptFiles? _files;
    IGraphFile? _graphFile;
    ScriptPanels? _panels;
    StepInspector? _inspector;
    string? _open;
    string _saved = "";
    bool _graphEdited;

    public ScriptView()
    {
        Background = EditorBrushes.Panel;
        _editor.IsEnabled = false;
        _editor.GetObservable(TextBox.TextProperty).Subscribe(new AnonymousObserver<string?>(_ => ShowText()));
        _list.SelectionChanged += (_, _) => Choose(_list.SelectedItem as string);
        _save.Click += (_, _) => Save();
        var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Margin = new Thickness(12, 8) };
        header.Children.Add(_name);
        header.Children.Add(_save);
        header.Children.Add(_status);
        var text = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        Grid.SetColumn(_editor, 1);
        text.Children.Add(_gutter);
        text.Children.Add(_editor);
        _text = new ScrollViewer { Content = text, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto };
        // A short file would leave the box narrower than the area, and a click beside it would reach nothing.
        _text.GetObservable(ScrollViewer.ViewportProperty).Subscribe(new AnonymousObserver<Size>(viewport => text.MinWidth = viewport.Width));
        var body = new DockPanel();
        DockPanel.SetDock(header, Dock.Top);
        body.Children.Add(header);
        // The text spans both columns. A graph script has the canvas on the left and its panels on the right.
        var area = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        Grid.SetColumnSpan(_text, 2);
        Grid.SetColumn(_panelsHost, 1);
        area.Children.Add(_text);
        area.Children.Add(_graphHost);
        area.Children.Add(_panelsHost);
        body.Children.Add(area);
        var panel = new DockPanel();
        DockPanel.SetDock(_list, Dock.Left);
        panel.Children.Add(_list);
        panel.Children.Add(body);
        Child = panel;
    }

    internal ListBox List => _list;

    internal TextBox Editor => _editor;

    internal TextBlock Gutter => _gutter;

    internal TextBlock Heading => _name;

    internal TextBlock Status => _status;

    internal Button SaveButton => _save;

    /// <summary>The canvas of the open graph script, or null when a C# file or nothing is open.</summary>
    internal GraphCanvas? Canvas => _graphHost.Child as GraphCanvas;

    /// <summary>The panels of the open graph script, or null when a C# file or nothing is open.</summary>
    internal ScriptPanels? Panels => _panels;

    /// <summary>The inspector of the selected step of the open graph script, or null when a C# file or nothing is open.</summary>
    internal StepInspector? Inspector => _inspector;

    /// <summary>Whether the open file differs from what is on disk: the text in the box, or a graph the engine changed.</summary>
    public bool IsDirty => _graphFile is not null ? _graphEdited : _open is not null && _editor.Text != _saved;

    /// <summary>Starts from this project's files, with no script open.</summary>
    public void Show(IScriptFiles files)
    {
        _files = files;
        CloseGraph();
        _open = null;
        _saved = "";
        _editor.Text = "";
        _editor.IsEnabled = false;
        _name.Text = "Pick a script.";
        _status.Text = "";
        Refresh();
    }

    /// <summary>Writes the open script when it has edits. Answers whether the file holds the text now.</summary>
    public bool Save()
    {
        if (!IsDirty)
        {
            return true;
        }

        try
        {
            if (_graphFile is not null)
            {
                _graphFile.Save();
                _graphEdited = false;
            }
            else
            {
                var text = _editor.Text ?? "";
                _files!.WriteScript(_open!, text);
                _saved = text;
            }

            ShowText();
            _status.Text = $"Saved {_open}.";
            return true;
        }
        catch (Exception error) when (Failed(error))
        {
            _status.Text = error.Message;
            return false;
        }
    }

    /// <summary>Counts the text as saved, for a close that drops the edits.</summary>
    public void Discard()
    {
        _saved = _editor.Text ?? "";
        _graphEdited = false;
        ShowText();
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

    void Refresh()
    {
        IReadOnlyList<string> paths = _files is null ? [] : [.. _files.Scripts(), .. _files.GraphScripts()];
        // The list keeps a selection that the new source still holds, which would open that script by itself.
        _list.SelectedItem = null;
        _list.ItemsSource = paths;
        _list.SelectedItem = _open is not null && paths.Contains(_open) ? _open : null;
    }

    // Choosing another script writes the open one first, as opening a scene does. A failure keeps the open one.
    void Choose(string? path)
    {
        if (path is null || path == _open || _files is null)
        {
            return;
        }

        if (!Save())
        {
            _list.SelectedItem = _open;
            return;
        }

        try
        {
            Open(_files, path);
            _status.Text = "";
            ShowText();
        }
        catch (Exception error) when (Failed(error))
        {
            _status.Text = error.Message;
            _list.SelectedItem = _open;
        }
    }

    void Open(IScriptFiles files, string path)
    {
        if (path.EndsWith(".ograph", StringComparison.Ordinal))
        {
            var file = files.OpenGraph(path);
            CloseGraph();
            _graphFile = file;
            _graphEdited = false;
            file.Changed += () =>
            {
                _graphEdited = true;
                ShowText();
            };
            _panels = new ScriptPanels(file);
            _inspector = new StepInspector(file);
            ShowCanvas(file, _inspector);
            // The canvas shows the function it is given when it is made, so another function needs a new canvas.
            _panels.FunctionOpened += _ => ShowCanvas(file, _inspector);
            var side = new Grid { RowDefinitions = new RowDefinitions("*,Auto") };
            Grid.SetRow(_inspector, 1);
            side.Children.Add(_panels);
            side.Children.Add(_inspector);
            _panelsHost.Child = side;
            _graphHost.IsVisible = true;
            _panelsHost.IsVisible = true;
            _text.IsVisible = false;
        }
        else
        {
            var text = files.ReadScript(path);
            CloseGraph();
            _editor.Text = text;
            _editor.IsEnabled = true;
            _saved = text;
        }

        _open = path;
        _name.Text = path;
    }

    // A new canvas has nothing selected, so the inspector is cleared with it.
    void ShowCanvas(IGraphFile file, StepInspector inspector)
    {
        var canvas = new GraphCanvas(file);
        canvas.SelectionChanged += inspector.Show;
        _graphHost.Child = canvas;
        inspector.Show(null);
    }

    void CloseGraph()
    {
        _panels = null;
        _inspector = null;
        _graphFile?.Dispose();
        _graphFile = null;
        _graphEdited = false;
        _graphHost.Child = null;
        _graphHost.IsVisible = false;
        _panelsHost.Child = null;
        _panelsHost.IsVisible = false;
        _text.IsVisible = true;
    }

    void ShowText()
    {
        var lines = (_editor.Text ?? "").Count(c => c == '\n') + 1;
        _gutter.Text = string.Join('\n', Enumerable.Range(1, lines));
        _save.IsEnabled = IsDirty;
    }
}
