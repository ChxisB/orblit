using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Shapes;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Shell;

/// <summary>What the Script workspace needs from the project: its C# files and their text.</summary>
internal interface IScriptFiles
{
    /// <summary>The res:// path of every C# file of the project.</summary>
    IReadOnlyList<string> Scripts();

    /// <summary>The text of a script. Throws <see cref="IOException"/> when it cannot be read.</summary>
    string ReadScript(string resourcePath);

    /// <summary>Replaces the text of a script. Throws <see cref="IOException"/> when it cannot be written.</summary>
    void WriteScript(string resourcePath, string text);

    /// <summary>Builds the project's C# and answers each error and warning. Throws <see cref="IOException"/> when it cannot build.</summary>
    Task<IReadOnlyList<ScriptProblem>> CheckScripts();

    /// <summary>The classes a new script can extend, by name.</summary>
    IReadOnlyList<string> AddableClasses();

    /// <summary>
    /// Writes a new script that extends the class. Throws <see cref="ArgumentException"/> for a name that is no class
    /// name and <see cref="IOException"/> when the file exists or cannot be written.
    /// </summary>
    void CreateScript(string resourcePath, string baseType);
}

/// <summary>
/// The Script workspace, on two islands. The left one lists the project's C# files over the outline of the open one.
/// The right one holds the open file in colour, with a status line under it. It reads and writes the files, and the
/// status line builds them to list their problems. It does not run them. Graph scripts have their own workspace.
/// </summary>
internal sealed class ScriptView : Border
{
    internal const double ListWidth = 248;

    const double BoxHeight = 28;
    const double DotSize = 6;

    readonly CodeBox _code = new();
    readonly ScriptFind _finder;
    readonly CreateScriptForm _form = new(ScriptKind.Csharp);
    readonly Button _new = Parts.IconButton("ScriptCreate", "New script");
    readonly Button _sort = Parts.IconButton("Sort", SortTip(descending: false));
    readonly Button _find = Parts.IconButton("Search", "Find in the script");
    readonly ListBox _list = new() { Background = null, BorderThickness = new Thickness(0), Classes = { EditorTheme.Rows } };
    readonly ListBox _outline = new()
    {
        Background = null,
        BorderThickness = new Thickness(0),
        Classes = { EditorTheme.Rows },
        // The list builds a row with no member while it recycles one.
        ItemTemplate = new FuncDataTemplate<ScriptMember>((member, _) => member is null ? null : MemberRow(member)),
    };

    readonly TextBox _filter = new()
    {
        PlaceholderText = "Filter scripts",
        Height = BoxHeight,
        MinHeight = 0,
        Margin = new Thickness(8),
        InnerRightContent = Parts.SearchIcon(),
    };

    readonly TextBlock _name = new() { Text = "Pick a script.", FontFamily = EditorTheme.Mono, FontSize = 11.5, Foreground = EditorBrushes.TextDim, VerticalAlignment = VerticalAlignment.Center };
    readonly TextBlock _tabName = new() { FontWeight = FontWeight.Medium, VerticalAlignment = VerticalAlignment.Center };
    readonly Ellipse _tabDot = Dot();
    readonly Border _tab;
    readonly TextBlock _status = new() { VerticalAlignment = VerticalAlignment.Center };
    readonly TextBlock _caret = StatusText();
    readonly TextBlock _indent = StatusText();
    readonly Control _empty = Parts.EmptyState("CodeFile", "No script open", "Pick a script on the left to read and edit it.");
    readonly Button _save = new() { Classes = { EditorTheme.TextButton, EditorTheme.Primary, EditorTheme.Small }, IsEnabled = false, VerticalAlignment = VerticalAlignment.Center };
    readonly Button _problems = new() { Classes = { EditorTheme.TextButton, EditorTheme.Ghost, EditorTheme.Small }, VerticalAlignment = VerticalAlignment.Center };
    readonly TextBlock _problemsText = StatusText();
    IReadOnlyList<ScriptProblem> _found = [];
    IScriptFiles? _files;
    IReadOnlyList<string> _paths = [];
    string? _open;
    string _saved = "";
    bool _shownDirty;
    bool _descending;

    public ScriptView()
    {
        _finder = new ScriptFind(_code);
        _list.ItemTemplate = new FuncDataTemplate<string>((path, _) => path is null ? null : ScriptRow(path));
        _save.Content = Parts.Keyed("Save", "⌘S");
        _tab = new Border
        {
            Height = 26,
            Padding = new Thickness(10, 0),
            CornerRadius = EditorTheme.Corners,
            Background = EditorBrushes.Raised,
            BorderBrush = EditorBrushes.LineStrong,
            BorderThickness = new Thickness(1),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left,
            IsVisible = false,
            Child = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 7, Children = { Parts.RowIcon("CodeFile", EditorBrushes.ScriptIcon), _tabName, _tabDot } },
        };
        _code.Editor.IsEnabled = false;
        _find.IsEnabled = false;
        _code.Editor.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty)
            {
                ShowText();
            }
        };
        _code.CaretMoved += ShowCaret;
        _filter.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty)
            {
                ShowList();
            }
        };
        _list.SelectionChanged += (_, _) => Choose(_list.SelectedItem as string);
        _outline.SelectionChanged += (_, _) => GoToMember();
        _save.Click += (_, _) => Save();
        _new.Click += (_, _) => _form.Open(_paths, _files?.AddableClasses() ?? []);
        _form.Requested += Create;
        _sort.Click += (_, _) => Sort();
        _find.Click += (_, _) => _finder.Open();
        _problems.Click += async (_, _) => await ListOrCheck();
        ToolTip.SetTip(_problems, "Build the project's C# and list its errors and warnings");
        ShowProblems("Check problems", null, EditorBrushes.TextDim);
        var lists = Lists();
        var editor = EditorIsland();
        Grid.SetColumn(editor, 2);
        Child = new Grid { ColumnDefinitions = new ColumnDefinitions($"{ListWidth},6,*"), Children = { lists, editor } };
        ShowCaret();
    }

    internal ListBox List => _list;

    internal TextBox Editor => _code.Editor;

    internal TextBlock Gutter => _code.Gutter;

    internal CodeBox Code => _code;

    internal TextBox Filter => _filter;

    internal ListBox Outline => _outline;

    /// <summary>The tab of the open script, over the text.</summary>
    internal Border Tab => _tab;

    internal Dialog CreateDialog => _form.Dialog;

    internal CreateScriptForm Form => _form;

    internal Button NewButton => _new;

    internal Button SortButton => _sort;

    internal Button FindButton => _find;

    internal ScriptFind Finder => _finder;

    /// <summary>The open script's path, or a line asking for one.</summary>
    internal TextBlock Heading => _name;

    /// <summary>What the last save or open said, such as a refused write.</summary>
    internal TextBlock Status => _status;

    internal TextBlock CaretText => _caret;

    internal TextBlock IndentText => _indent;

    internal Button SaveButton => _save;

    internal Button ProblemsButton => _problems;

    internal string ProblemsText => _problemsText.Text ?? "";

    /// <summary>Whether the text in the box differs from what is on disk.</summary>
    public bool IsDirty => _open is not null && _code.Editor.Text != _saved;

    /// <summary>Starts from this project's files, with no script open.</summary>
    public void Show(IScriptFiles files)
    {
        _files = files;
        _open = null;
        _saved = "";
        _code.Editor.Text = "";
        ShowOpen();
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
            var text = _code.Editor.Text ?? "";
            _files!.WriteScript(_open!, text);
            _saved = text;
            ShowText();
            Say($"Saved {_open}.", EditorBrushes.TextDim);
            return true;
        }
        catch (Exception error) when (Failed(error))
        {
            Say(error.Message, EditorBrushes.FaultText);
            return false;
        }
    }

    /// <summary>Counts the text as saved, for a close that drops the edits.</summary>
    public void Discard()
    {
        _saved = _code.Editor.Text ?? "";
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

    static string SortTip(bool descending) => descending ? "Sort the scripts A to Z" : "Sort the scripts Z to A";

    static string FileName(string path) => path.StartsWith("res://", StringComparison.Ordinal) ? path["res://".Length..] : path;

    // The width of one step of indent: tabs when a line starts with one, else the fewest spaces any line starts with.
    static string Indent(string text)
    {
        var lines = text.Split('\n').Where(line => line.Trim().Length > 0).ToList();
        if (lines.Any(line => line[0] == '\t'))
        {
            return "Tabs";
        }

        var widths = lines.Select(line => line.Length - line.TrimStart(' ').Length).Where(width => width > 0).ToList();
        return $"Spaces: {(widths.Count > 0 ? widths.Min() : 4)}";
    }

    static Ellipse Dot() => new() { Width = DotSize, Height = DotSize, Fill = EditorBrushes.Accent, VerticalAlignment = VerticalAlignment.Center, IsVisible = false };

    static TextBlock StatusText(string text = "") => new() { Text = text, VerticalAlignment = VerticalAlignment.Center };

    // A strip like a dock's: a 26 pixel row with 6 pixels around it and a line under it.
    static Border Bar(Control left, Control right)
    {
        DockPanel.SetDock(right, Dock.Right);
        return new Border
        {
            Height = DockTabs.StripHeight,
            Padding = new Thickness(6),
            BorderBrush = EditorBrushes.Line,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = new DockPanel { Children = { right, left } },
        };
    }

    static StackPanel Row(params Control[] items)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        row.Children.AddRange(items);
        return row;
    }

    static DockPanel MemberRow(ScriptMember member)
    {
        var detail = new TextBlock { Text = member.Detail, FontSize = 11, Foreground = EditorBrushes.TextDim, Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(detail, Dock.Right);
        return new DockPanel
        {
            Children =
            {
                detail,
                Row(Parts.RowIcon(member.Icon), new TextBlock { Text = member.Name, FontFamily = EditorTheme.Mono, FontSize = 12, VerticalAlignment = VerticalAlignment.Center }),
            },
        };
    }

    Border Lists()
    {
        var tab = new Border
        {
            Height = 26,
            Padding = new Thickness(10, 0),
            CornerRadius = EditorTheme.Corners,
            Background = EditorBrushes.Line,
            Child = new TextBlock { Text = "Scripts", FontWeight = FontWeight.Medium, VerticalAlignment = VerticalAlignment.Center },
        };
        var tools = Row(_new, _sort);
        var heading = new Border
        {
            Padding = new Thickness(14, 14, 14, 6),
            BorderBrush = EditorBrushes.Line,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Child = new TextBlock { Text = "Outline", FontSize = 11, FontWeight = FontWeight.SemiBold, Foreground = EditorBrushes.TextDim },
        };
        var grid = new Grid { RowDefinitions = new RowDefinitions($"{DockTabs.StripHeight},Auto,*,Auto,*") };
        Place(grid, Bar(tab, tools), 0);
        Place(grid, _filter, 1);
        Place(grid, _list, 2);
        Place(grid, heading, 3);
        Place(grid, _outline, 4);
        return new Border { Classes = { EditorTheme.Island }, Child = grid };
    }

    Border EditorIsland()
    {
        var badge = new Border
        {
            Padding = new Thickness(6, 2),
            CornerRadius = new CornerRadius(4),
            Background = EditorBrushes.Line,
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock { Text = "C#", FontSize = 10.5, FontWeight = FontWeight.SemiBold, Foreground = EditorBrushes.TextSoft },
        };
        var tools = Row(_name, new Border { Width = 8 }, badge, new Border { Width = 4 }, _find, new Border { Width = 4 }, _save);
        DockPanel.SetDock(_problems, Dock.Right);
        var line = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Children = { _caret, _indent, StatusText("UTF-8"), _status } };
        var status = new Border
        {
            Height = 28,
            Padding = new Thickness(12, 0, 4, 0),
            BorderBrush = EditorBrushes.Line,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Child = new DockPanel { Children = { _problems, line } },
        };
        TextElement.SetFontSize(status, 11);
        TextElement.SetForeground(status, EditorBrushes.TextDim);
        var grid = new Grid { RowDefinitions = new RowDefinitions($"{DockTabs.StripHeight},*,Auto") };
        Place(grid, Bar(_tab, tools), 0);
        Place(grid, _code, 1);
        Place(grid, _empty, 1);
        Place(grid, _finder, 1);
        Place(grid, status, 2);
        // The text area's corners would square off the island's lower ones.
        return new Border { Classes = { EditorTheme.Island }, ClipToBounds = true, Child = grid };
    }

    static void Place(Grid grid, Control child, int row)
    {
        Grid.SetRow(child, row);
        grid.Children.Add(child);
    }

    void Refresh()
    {
        _paths = _files?.Scripts() ?? [];
        ShowList();
    }

    void ShowList()
    {
        var filter = (_filter.Text ?? "").Trim();
        var shown = _paths.Where(path => FileName(path).Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();
        // The project lists its scripts A to Z, so Z to A is the list turned round.
        if (_descending)
        {
            shown.Reverse();
        }

        // The list keeps a selection that the new source still holds, which would open that script by itself.
        _list.SelectedItem = null;
        _list.ItemsSource = shown;
        _list.SelectedItem = _open is not null && shown.Contains(_open) ? _open : null;
        _shownDirty = IsDirty;
    }

    // Choosing another script writes the open one first, as opening a scene does. A failure keeps the open one.
    internal void Choose(string? path)
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
            _saved = text;
            _open = path;
            _code.Editor.Text = text;
            _code.Editor.CaretIndex = 0;
            // Saving the last script lists the files again, which selects that one.
            _list.SelectedItem = path;
            ShowOpen();
            Say("", EditorBrushes.TextDim);
            ShowText();
        }
        catch (Exception error) when (Failed(error))
        {
            Say(error.Message, EditorBrushes.FaultText);
            _list.SelectedItem = _open;
        }
    }

    void Sort()
    {
        _descending = !_descending;
        ToolTip.SetTip(_sort, SortTip(_descending));
        ShowList();
    }

    void Create(string path, string baseType)
    {
        if (_files is null)
        {
            return;
        }

        try
        {
            _files.CreateScript(path, baseType);
        }
        catch (Exception error) when (Failed(error) || error is ArgumentException)
        {
            _form.Say(error.Message);
            return;
        }

        _form.Dialog.Close();
        Refresh();
        Choose(path);
        if (_open == path)
        {
            Say($"Created {path}.", EditorBrushes.TextDim);
        }
    }

    void ShowOpen()
    {
        _code.Editor.IsEnabled = _open is not null;
        _find.IsEnabled = _open is not null;
        _finder.IsVisible &= _open is not null;
        _empty.IsVisible = _open is null;
        _tab.IsVisible = _open is not null;
        _tabName.Text = _open is null ? "" : FileName(_open);
        _name.Text = _open ?? "Pick a script.";
    }

    void Say(string text, IBrush tint)
    {
        _status.Text = text;
        _status.Foreground = tint;
    }

    void ShowText()
    {
        var text = _code.Editor.Text ?? "";
        _save.IsEnabled = IsDirty;
        _tabDot.IsVisible = IsDirty;
        _indent.Text = Indent(text);
        _outline.ItemsSource = ScriptMember.Read(text);
        if (IsDirty != _shownDirty)
        {
            ShowList();
        }
    }

    void ShowCaret()
    {
        var (line, column) = _code.Caret;
        _caret.Text = $"Line {line}, column {column}";
    }

    void GoToMember()
    {
        if (_outline.SelectedItem is not ScriptMember member)
        {
            return;
        }

        // Cleared, so a second click on the same member goes there again.
        _outline.SelectedItem = null;
        _code.GoTo(member.Index);
    }

    /// <summary>Writes the open script, then builds the project's C# and shows the count of what the build found.</summary>
    internal async Task Check()
    {
        if (_files is null || !Save())
        {
            return;
        }

        _problems.IsEnabled = false;
        ShowProblems("Checking…", null, EditorBrushes.TextDim);
        try
        {
            _found = await _files.CheckScripts();
            ShowFound();
        }
        catch (Exception error) when (Failed(error))
        {
            _found = [];
            ShowProblems("Check problems", null, EditorBrushes.TextDim);
            Say(error.Message, EditorBrushes.FaultText);
        }

        _problems.IsEnabled = true;
    }

    /// <summary>The list of what the last check found, with Check again under it.</summary>
    internal MenuFlyout ProblemList()
    {
        var list = new MenuFlyout { Placement = PlacementMode.TopEdgeAlignedRight };
        foreach (var problem in _found)
        {
            var place = problem.Line > 0 ? $"{FileName(problem.Path)}:{problem.Line}  " : "";
            var icon = new EditorIcon(problem.IsError ? "FaultCircle" : "WarnTriangle") { Width = 14, Height = 14 };
            TextElement.SetForeground(icon, problem.IsError ? EditorBrushes.Fault : EditorBrushes.Warn);
            var item = new MenuItem
            {
                Header = new TextBlock { Text = place + problem.Text, MaxWidth = 560, TextWrapping = TextWrapping.Wrap },
                Icon = icon,
                // A problem with no place in a project file has nowhere to go.
                IsEnabled = problem.Line > 0 && problem.Path.StartsWith(FileTree.Root, StringComparison.Ordinal),
            };
            item.Click += (_, _) => GoTo(problem);
            list.Items.Add(item);
        }

        list.Items.Add(new Separator());
        var again = new MenuItem { Header = "Check again" };
        again.Click += async (_, _) => await Check();
        list.Items.Add(again);
        return list;
    }

    /// <summary>Opens the problem's script with the caret at its line and column.</summary>
    internal void GoTo(ScriptProblem problem)
    {
        Choose(problem.Path);
        if (_open == problem.Path)
        {
            _code.GoTo(IndexOf(_code.Editor.Text ?? "", problem.Line, problem.Column));
        }
    }

    // The index of a line and column, both counted from one. A line past the end is the end of the text.
    static int IndexOf(string text, int line, int column)
    {
        var start = 0;
        for (var at = 1; at < line; at++)
        {
            var end = text.IndexOf('\n', start);
            if (end < 0)
            {
                return text.Length;
            }

            start = end + 1;
        }

        return Math.Min(start + Math.Max(column - 1, 0), text.Length);
    }

    // A click lists what the last check found. With nothing to list it checks.
    async Task ListOrCheck()
    {
        if (_found.Count > 0)
        {
            ProblemList().ShowAt(_problems);
            return;
        }

        await Check();
    }

    void ShowFound()
    {
        var errors = _found.Count(problem => problem.IsError);
        var warnings = _found.Count - errors;
        if (_found.Count == 0)
        {
            ShowProblems("No problems", "Tick", EditorBrushes.Done);
            return;
        }

        var text = $"{Count(errors, "error")}, {Count(warnings, "warning")}";
        ShowProblems(text, errors > 0 ? "FaultCircle" : "WarnTriangle", errors > 0 ? EditorBrushes.FaultText : EditorBrushes.Warn);
    }

    static string Count(int count, string word) => count == 1 ? $"1 {word}" : $"{count} {word}s";

    void ShowProblems(string text, string? icon, IBrush tint)
    {
        _problemsText.Text = text;
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5 };
        if (icon is not null)
        {
            row.Children.Add(new EditorIcon(icon) { Width = 13, Height = 13, VerticalAlignment = VerticalAlignment.Center });
        }

        // Detached first, because the last row still holds the text.
        (_problemsText.Parent as Panel)?.Children.Remove(_problemsText);
        row.Children.Add(_problemsText);
        _problems.Content = row;
        _problems.Foreground = tint;
    }

    // The open script's row carries a dot while it has edits.
    Control ScriptRow(string path)
    {
        var dot = Dot();
        dot.IsVisible = path == _open && IsDirty;
        dot.Margin = new Thickness(8, 0, 0, 0);
        DockPanel.SetDock(dot, Dock.Right);
        return new DockPanel
        {
            Children =
            {
                dot,
                Row(Parts.RowIcon("CodeFile", EditorBrushes.ScriptIcon), new TextBlock { Text = FileName(path), TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center }),
            },
        };
    }
}
