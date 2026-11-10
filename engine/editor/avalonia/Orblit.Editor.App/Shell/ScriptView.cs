using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Reactive;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Shell;

/// <summary>What the script workspace needs from the project: its C# files and their text.</summary>
internal interface IScriptFiles
{
    /// <summary>The res:// path of every C# file of the project.</summary>
    IReadOnlyList<string> Scripts();

    /// <summary>The text of a script. Throws <see cref="IOException"/> when it cannot be read.</summary>
    string ReadScript(string resourcePath);

    /// <summary>Replaces the text of a script. Throws <see cref="IOException"/> when it cannot be written.</summary>
    void WriteScript(string resourcePath, string text);
}

/// <summary>
/// The Script workspace: the project's C# files in a list and the open one in a text box with line numbers.
/// It reads and writes the files and does not compile or run them.
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
    IScriptFiles? _files;
    string? _open;
    string _saved = "";

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
        var scroll = new ScrollViewer { Content = text, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto };
        // A short file would leave the box narrower than the area, and a click beside it would reach nothing.
        scroll.GetObservable(ScrollViewer.ViewportProperty).Subscribe(new AnonymousObserver<Size>(viewport => text.MinWidth = viewport.Width));
        var body = new DockPanel();
        DockPanel.SetDock(header, Dock.Top);
        body.Children.Add(header);
        body.Children.Add(scroll);
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

    /// <summary>Whether the text in the box differs from the file it was read from.</summary>
    public bool IsDirty => _open is not null && _editor.Text != _saved;

    /// <summary>Starts from this project's files, with no script open.</summary>
    public void Show(IScriptFiles files)
    {
        _files = files;
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
            var text = _editor.Text ?? "";
            _files!.WriteScript(_open!, text);
            _saved = text;
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
        var paths = _files?.Scripts() ?? [];
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
            var text = _files.ReadScript(path);
            _editor.Text = text;
            _editor.IsEnabled = true;
            _open = path;
            _saved = text;
            _name.Text = path;
            _status.Text = "";
            ShowText();
        }
        catch (Exception error) when (Failed(error))
        {
            _status.Text = error.Message;
            _list.SelectedItem = _open;
        }
    }

    void ShowText()
    {
        var lines = (_editor.Text ?? "").Count(c => c == '\n') + 1;
        _gutter.Text = string.Join('\n', Enumerable.Range(1, lines));
        _save.IsEnabled = IsDirty;
    }
}
