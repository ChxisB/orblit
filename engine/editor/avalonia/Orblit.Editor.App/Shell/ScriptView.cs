using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Shapes;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Orblit.Editor.App.Graph;
using Orblit.Editor.App.Theme;
using Orblit.Editor.Bridge;

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

    /// <summary>The path of each assembly a script can use besides the .NET runtime, such as the engine's C# API.</summary>
    IReadOnlyList<string> ScriptReferences();

    /// <summary>The classes a new script can extend, by name.</summary>
    IReadOnlyList<string> AddableClasses();

    /// <summary>
    /// Writes a new script that extends the class. Throws <see cref="ArgumentException"/> for a name that is no class
    /// name and <see cref="IOException"/> when the file exists or cannot be written.
    /// </summary>
    void CreateScript(string resourcePath, string baseType);

    /// <summary>The C# side of a game Play started under netcoredbg. A breakpoint is a script's res:// path and a line.</summary>
    IDebugChannel Csharp { get; }
}

/// <summary>
/// The Script workspace, on two islands. The left one lists the project's C# files over the members of the open
/// one. The right one holds the open file in color, with a status line under it. It reads and writes the files, and
/// Check for problems builds them to list what is wrong. A click left of a line number sets a breakpoint, and a game
/// that stops on one opens its file at the line. Graph scripts have their own workspace.
/// </summary>
internal sealed class ScriptView : Border
{
    internal const double ListWidth = 248;

    const double BoxHeight = 28;
    const double DotSize = 6;

    // Wide enough for most file names. A longer one is cut, and the path beside the tab gives it whole.
    const double WidestTab = 240;

    readonly CodeBox _code = new();
    readonly ScriptFind _finder;
    readonly CreateScriptForm _form = new(ScriptKind.Csharp);
    readonly Button _new = Parts.Dress(new Button(), "ScriptCreate", new HoverCard("New script", NewLine));
    // The column is too narrow for a third word, so Sort keeps its icon alone.
    readonly Button _sort = SortCard(descending: false).On(Parts.IconButton("Sort", ""));
    readonly Button _find = new();
    readonly Button _check = new();
    readonly Button _close = Parts.Tight(Parts.IconButton("GuiClose", ""));
    readonly Button _start = Choice("New script", null, EditorTheme.Primary, NewLine);
    readonly Button _findScript;
    readonly KeyModifiers _command;
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
        PlaceholderText = "Find a script",
        Height = BoxHeight,
        MinHeight = 0,
        Margin = new Thickness(Gaps.Item),
        InnerRightContent = Parts.SearchIcon(),
    };

    readonly TextBlock _name = new() { Text = "Pick a script.", FontFamily = EditorTheme.Mono, FontSize = 11.5, Foreground = EditorBrushes.TextDim, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(Gaps.Item, 0) };
    readonly TextBlock _tabName = new() { FontWeight = FontWeight.Medium, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
    readonly Ellipse _tabDot = Dot();
    readonly Border _tab;
    readonly TextBlock _status = new() { VerticalAlignment = VerticalAlignment.Center };
    readonly TextBlock _caret = StatusText();
    readonly TextBlock _indent = StatusText();
    readonly Control _empty;
    readonly Button _save = new() { Classes = { EditorTheme.TextButton, EditorTheme.Primary, EditorTheme.Small }, IsEnabled = false, VerticalAlignment = VerticalAlignment.Center };
    readonly Button _problems = new() { Classes = { EditorTheme.TextButton, EditorTheme.Ghost, EditorTheme.Small }, VerticalAlignment = VerticalAlignment.Center };
    readonly TextBlock _problemsText = StatusText();
    readonly TextBlock _errors = new() { Foreground = EditorBrushes.FaultText, MaxWidth = 520, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
    // A pause in typing, so Roslyn reads the text once a burst of keys is done.
    readonly DispatcherTimer _errorsAfter = new() { Interval = TimeSpan.FromMilliseconds(600) };
    readonly CompletionPopup _completions;
    IReadOnlyList<ScriptProblem> _found = [];
    ScriptCompletion? _completion;
    IScriptFiles? _files;
    GraphDebugger? _debugger;
    (string Path, int Line)? _paused;
    IReadOnlyList<string> _paths = [];
    string? _open;
    string _saved = "";
    bool _shownDirty;
    bool _descending;

    public ScriptView(KeyModifiers command)
    {
        _command = command;
        _findScript = Choice("Find a script", Parts.KeyText(new KeyGesture(Key.K, command)), EditorTheme.Ghost, "Type part of a name to open it.");
        _empty = NoScript();
        _finder = new ScriptFind(_code);
        _completions = new CompletionPopup(_code, Complete);
        _errorsAfter.Tick += async (_, _) =>
        {
            // Roslyn reads every script and reference, and a timer's fault would end the editor. The line says what failed.
            try
            {
                await ShowErrors();
            }
            catch (Exception error) when (Failed(error) || error is BadImageFormatException)
            {
                _errors.Text = error.Message;
            }
        };
        _list.ItemTemplate = new FuncDataTemplate<string>((path, _) => path is null ? null : ScriptRow(path));
        var save = new HoverCard("Save", "Writes the script to its file.", Parts.KeyText(new KeyGesture(Key.S, command)));
        save.SaysWhyOff(Parts.Dress(_save, "Save", save), "Nothing new to save.");
        var find = new HoverCard("Find", "Finds words in this script.", Parts.KeyText(new KeyGesture(Key.F, command)));
        find.SaysWhyOff(Parts.Dress(_find, "Search", find), "Open a script first.");
        var check = new HoverCard("Check for problems", "Saves, then builds the project's C# and lists what is wrong.", Parts.KeyText(CheckKey));
        check.SaysWhyOff(Parts.Dress(_check, "ImportCheck", check), "A check is running.");
        new HoverCard("Close", "Saves the script, then closes it.").On(_close);
        new HoverCard("Problems", "Lists what the last check found. With nothing found, it checks again.").On(_problems);
        _tab = new Border
        {
            Height = 26,
            Padding = new Thickness(Gaps.Item, 0),
            CornerRadius = EditorTheme.Corners,
            Background = EditorBrushes.Raised,
            BorderBrush = EditorBrushes.LineStrong,
            BorderThickness = new Thickness(1),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left,
            IsVisible = false,
            MaxWidth = WidestTab,
            Child = TabParts(),
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
        _code.MarkClicked += line =>
        {
            if (_open is not null)
            {
                Breakpoints.Toggle(_open, line);
            }
        };
        Breakpoints.Changed += (_, _, _) => ShowMarks();
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
        _start.Click += (_, _) => _form.Open(_paths, _files?.AddableClasses() ?? []);
        _check.Click += async (_, _) => await Check();
        _close.Click += (_, _) => Close();
        _form.Requested += Create;
        _sort.Click += (_, _) => Sort();
        _find.Click += (_, _) => _finder.Open();
        _problems.Click += async (_, _) => await ListOrCheck();
        // The count shows once a check has run.
        _problems.IsVisible = false;
        var lists = new FoldingColumn("Scripts", "CodeFile", Dock.Left, Lists);
        Columns = [lists];
        var editor = EditorIsland();
        Grid.SetColumn(editor, 2);
        Child = new Grid { ColumnDefinitions = new ColumnDefinitions($"{ListWidth},6,*"), Children = { lists, editor } };
        ShowCaret();
    }

    /// <summary>The columns that fold to a rail: the script list.</summary>
    internal IReadOnlyList<FoldingColumn> Columns { get; }

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

    internal Button CheckButton => _check;

    /// <summary>The cross on the tab of the open script.</summary>
    internal Button CloseButton => _close;

    /// <summary>New script in the middle while no script is open.</summary>
    internal Button StartButton => _start;

    /// <summary>Find a script in the middle while no script is open. The window opens Search.</summary>
    internal Button FindScriptButton => _findScript;

    /// <summary>What the middle shows while no script is open.</summary>
    internal Control Empty => _empty;

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

    internal CompletionPopup Completions => _completions;

    /// <summary>The first error Roslyn finds in the open text, after a pause in typing.</summary>
    internal TextBlock ErrorsText => _errors;

    // A C# breakpoint is a script's res:// path and a line, the shape of a graph breakpoint, so the graph's set serves.
    internal GraphBreakpoints Breakpoints { get; } = new();

    /// <summary>The C# session in the graph debugger's panel, for the Debugger tab while a game runs under netcoredbg.</summary>
    internal DebuggerPanel? DebugPanel { get; private set; }

    /// <summary>Whether a game runs under netcoredbg, so <see cref="DebugPanel"/> has a session to show.</summary>
    internal bool IsDebugging => _debugger?.State.Connected == true;

    /// <summary>Raised when the game stopped in a script of the project, which this has opened at the line.</summary>
    public event Action? Paused;

    /// <summary>Whether the text in the box differs from what is on disk.</summary>
    public bool IsDirty => _open is not null && _code.Editor.Text != _saved;

    /// <summary>The res:// path of the open script, or none.</summary>
    internal string? OpenPath => _open;

    /// <summary>How many lines of the open script differ from the file.</summary>
    internal int ChangedLines => LinesChanged(_saved, _code.Editor.Text ?? "");

    /// <summary>
    /// The lines one text adds or drops against the other, whichever is more, so an edited line counts once.
    /// shortcut: lines are matched as a bag, so a moved line does not count. Diff in order if that misleads.
    /// </summary>
    internal static int LinesChanged(string saved, string text)
    {
        var left = saved.Split('\n').GroupBy(line => line).ToDictionary(group => group.Key, group => group.Count());
        var added = 0;
        foreach (var line in text.Split('\n'))
        {
            if (left.GetValueOrDefault(line) > 0)
            {
                left[line]--;
            }
            else
            {
                added++;
            }
        }

        return Math.Max(added, left.Values.Sum());
    }

    /// <summary>Starts from this project's files, with no script open.</summary>
    public void Show(IScriptFiles files)
    {
        _files = files;
        _open = null;
        _saved = "";
        // shortcut: C# breakpoints are not saved, so another project starts with none. Save them as the graph's are when asked.
        Breakpoints.Load(null);
        _paused = null;
        _debugger?.Dispose();
        _debugger = new GraphDebugger(files.Csharp, Breakpoints);
        _debugger.Changed += ShowStop;
        DebugPanel = new DebuggerPanel(_debugger, _ => null, _command);
        _code.Editor.Text = "";
        ShowOpen();
        _status.Text = "";
        _errors.Text = "";
        Refresh();
    }

    /// <summary>Takes in what the game under netcoredbg has said since the last poll.</summary>
    public void PollDebugger() => _debugger?.Poll();

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

    const string NewLine = "Makes a C# script in this project.";

    static HoverCard SortCard(bool descending) => new(descending ? "Sort the scripts A to Z" : "Sort the scripts Z to A", "Turns the order of the list round.");

    // A worded button for the middle, with its key when it has one.
    static Button Choice(string word, string? key, string look, string line) =>
        new HoverCard(word, line, key).On(new Button { Classes = { EditorTheme.TextButton, look }, Content = key is null ? word : Parts.Keyed(word, key) });

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
        return $"Spaces {(widths.Count > 0 ? widths.Min() : 4)}";
    }

    static Ellipse Dot() => new() { Width = DotSize, Height = DotSize, Fill = EditorBrushes.Accent, VerticalAlignment = VerticalAlignment.Center, IsVisible = false };

    // The close button and the dot keep their places, and a long file name is cut between them.
    DockPanel TabParts()
    {
        var icon = Parts.RowIcon("CodeFile", EditorBrushes.ScriptIcon);
        icon.Margin = new Thickness(0, 0, Gaps.Tight, 0);
        _tabDot.Margin = _close.Margin = new Thickness(Gaps.Item, 0, 0, 0);
        DockPanel.SetDock(icon, Dock.Left);
        DockPanel.SetDock(_close, Dock.Right);
        DockPanel.SetDock(_tabDot, Dock.Right);
        return new DockPanel { Children = { icon, _close, _tabDot, _tabName } };
    }

    // The place in the file comes first. What was said and what the check found share the rest, and are cut.
    DockPanel StatusLine()
    {
        Control[] parts = [_caret, _indent, StatusText("UTF-8"), _status, _errors];
        foreach (var part in parts)
        {
            part.Margin = new Thickness(part == _caret ? 0 : Gaps.Group, 0, 0, 0);
            DockPanel.SetDock(part, Dock.Left);
        }

        _status.MaxWidth = 240;
        _status.TextTrimming = TextTrimming.CharacterEllipsis;
        Parts.TipItsText(_status);
        _errors.HorizontalAlignment = HorizontalAlignment.Left;
        var line = new DockPanel();
        line.Children.AddRange(parts);
        return line;
    }

    static TextBlock StatusText(string text = "") => new() { Text = text, VerticalAlignment = VerticalAlignment.Center };

    // A strip like a dock's: the same height as one, with a line under it.
    static Border Bar(Control left, Control right)
    {
        DockPanel.SetDock(right, Dock.Right);
        return new Border
        {
            Height = DockTabs.StripHeight,
            Padding = new Thickness(Gaps.Item, DockTabs.StripInset),
            BorderBrush = EditorBrushes.Line,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = new DockPanel { Children = { right, left } },
        };
    }

    static StackPanel Row(params Control[] items)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Gaps.Tight, VerticalAlignment = VerticalAlignment.Center };
        row.Children.AddRange(items);
        return row;
    }

    static DockPanel MemberRow(ScriptMember member)
    {
        var detail = new TextBlock { Text = member.Detail, FontSize = 11, Foreground = EditorBrushes.TextDim, Margin = new Thickness(Gaps.Item, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(detail, Dock.Right);
        var icon = Parts.RowIcon(member.Icon);
        DockPanel.SetDock(icon, Dock.Left);
        var name = Parts.Trimmed(member.Name, member.Name);
        (name.FontFamily, name.FontSize) = (EditorTheme.Mono, 12);
        return new DockPanel { Children = { detail, icon, name } };
    }

    Border Lists(Button collapse)
    {
        var tab = new Border
        {
            Height = 26,
            Padding = new Thickness(Gaps.Item, 0),
            CornerRadius = EditorTheme.Corners,
            Background = EditorBrushes.Line,
            Child = new TextBlock { Text = "Scripts", FontWeight = FontWeight.Medium, VerticalAlignment = VerticalAlignment.Center },
        };
        var tools = Row(_new, _sort);
        tools.Spacing = Gaps.Joined;
        tools.Classes.Add(EditorTheme.Joined);
        var heading = new Border
        {
            Padding = new Thickness(Gaps.Group, Gaps.Group, Gaps.Group, Gaps.Item),
            BorderBrush = EditorBrushes.Line,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Child = new TextBlock { Text = "In this script", FontSize = 11, FontWeight = FontWeight.SemiBold, Foreground = EditorBrushes.TextDim },
        };
        var grid = new Grid { RowDefinitions = new RowDefinitions($"{DockTabs.StripHeight},Auto,*,Auto,*") };
        Place(grid, Bar(tab, Row(tools, collapse)), 0);
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
            Padding = new Thickness(Gaps.Item, Gaps.Joined),
            CornerRadius = new CornerRadius(4),
            Background = EditorBrushes.Line,
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock { Text = "C#", FontSize = 10.5, FontWeight = FontWeight.SemiBold, Foreground = EditorBrushes.TextSoft },
        };
        // The buttons are measured first, then the tab, and the path gets what is left.
        var tools = Row(badge, _find, _save, _check);
        tools.Spacing = Gaps.Item;
        DockPanel.SetDock(_tab, Dock.Left);
        DockPanel.SetDock(_problems, Dock.Right);
        var status = new Border
        {
            Height = 28,
            Padding = new Thickness(Gaps.Group, 0, Gaps.Tight, 0),
            BorderBrush = EditorBrushes.Line,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Child = new DockPanel { Children = { _problems, StatusLine() } },
        };
        TextElement.SetFontSize(status, 11);
        TextElement.SetForeground(status, EditorBrushes.TextDim);
        var grid = new Grid { RowDefinitions = new RowDefinitions($"{DockTabs.StripHeight},*,Auto") };
        Place(grid, Bar(new DockPanel { Children = { _tab, _name } }, tools), 0);
        Place(grid, _code, 1);
        Place(grid, _empty, 1);
        Place(grid, _finder, 1);
        Place(grid, _completions.Popup, 1);
        Place(grid, status, 2);
        // The text area's corners would square off the island's lower ones.
        return new Border { Classes = { EditorTheme.Island }, ClipToBounds = true, Child = grid };
    }

    // What scripts are, and two ways in.
    StackPanel NoScript()
    {
        var mark = new EditorIcon("CodeFile") { Width = 28, Height = 28, HorizontalAlignment = HorizontalAlignment.Center };
        TextElement.SetForeground(mark, EditorBrushes.Disabled);
        return new StackPanel
        {
            Width = 360,
            Spacing = Gaps.Group,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                mark,
                new TextBlock { Text = "No script is open", FontSize = 15, FontWeight = FontWeight.SemiBold, HorizontalAlignment = HorizontalAlignment.Center },
                new TextBlock
                {
                    Text = "Scripts tell things what to do. Pick one from the list, or make one.",
                    Foreground = EditorBrushes.TextSoft,
                    TextWrapping = TextWrapping.Wrap,
                    TextAlignment = TextAlignment.Center,
                },
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = Gaps.Item, HorizontalAlignment = HorizontalAlignment.Center, Children = { _start, _findScript } },
            },
        };
    }

    static void Place(Grid grid, Control child, int row)
    {
        Grid.SetRow(child, row);
        grid.Children.Add(child);
    }

    void Refresh()
    {
        _paths = _files?.Scripts() ?? [];
        _completion = null;
        ShowList();
    }

    // Built on first use, and again after the files change, because it reads every script.
    ScriptCompletion Completion() => _completion ??= ScriptCompletion.For(_files!);

    // The workspace is read here, because the files answer only on the UI thread. Roslyn works off it.
    Task<CompletionSet?> Complete(string text, int caret)
    {
        if (_open is not { } path || _files is null)
        {
            return Task.FromResult<CompletionSet?>(null);
        }

        var completion = Completion();
        return Task.Run(() => completion.Complete(path, text, caret));
    }

    internal async Task ShowErrors()
    {
        _errorsAfter.Stop();
        if (_open is not { } path || _files is null)
        {
            _errors.Text = "";
            return;
        }

        var completion = Completion();
        var text = _code.Editor.Text ?? "";
        var found = await Task.Run(() => completion.Problems(path, text));
        // Another script or more typing since the ask makes the answer stale. The next pause asks again.
        if (path != _open || text != _code.Editor.Text)
        {
            return;
        }

        var more = found.Count > 1 ? $" {found.Count - 1} more." : "";
        _errors.Text = found.Count == 0 ? "" : $"Line {found[0].Line}: {found[0].Text}{more}";
        ToolTip.SetTip(_errors, found.Count == 0 ? null : string.Join("\n", found.Select(problem => $"Line {problem.Line}: {problem.Text}")));
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
            // The workspace holds the last script as it was read, before the save.
            _completion = null;
            _errors.Text = "";
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
        SortCard(_descending).On(_sort);
        ShowList();
    }

    /// <summary>Writes the open script, then closes it. A script that cannot be written stays open.</summary>
    internal void Close()
    {
        if (_open is null || !Save())
        {
            return;
        }

        _open = null;
        _saved = "";
        _errors.Text = "";
        _code.Editor.Text = "";
        ShowOpen();
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
        ToolTip.SetTip(_tab, _open);
        _name.Text = _open ?? "Pick a script.";
        ToolTip.SetTip(_name, _open);
        ShowMarks();
    }

    // A stop in a project script opens it at the line once. Selecting another level opens that one, and going on clears the light.
    void ShowStop()
    {
        var place = PausedPlace(_debugger!.State);
        if (place == _paused)
        {
            return;
        }

        _paused = place;
        if (place is { } at)
        {
            Choose(at.Path);
            if (_open == at.Path)
            {
                _code.GoTo(IndexOf(_code.Editor.Text ?? "", at.Line, 1));
            }

            Paused?.Invoke();
        }

        ShowMarks();
    }

    static (string Path, int Line)? PausedPlace(DebugState state) =>
        state.Stopped && state.Level < state.Stack.Count && state.Stack[state.Level] is var frame && frame.Source.StartsWith(FileTree.Root, StringComparison.Ordinal)
            ? (frame.Source, frame.Line)
            : null;

    void ShowMarks() => _code.ShowMarks(Breakpoints.Steps(_open ?? ""), _paused is { } at && at.Path == _open ? at.Line : null);

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
        _errorsAfter.Stop();
        if (_open is not null)
        {
            _errorsAfter.Start();
        }

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
        _check.IsEnabled = false;
        _problems.IsVisible = true;
        ShowProblems("Checking…", null, EditorBrushes.TextDim);
        try
        {
            _found = await _files.CheckScripts();
            ShowFound();
        }
        catch (Exception error) when (Failed(error))
        {
            _found = [];
            ShowProblems("Not checked", null, EditorBrushes.TextDim);
            Say(error.Message, EditorBrushes.FaultText);
        }

        _problems.IsEnabled = true;
        _check.IsEnabled = true;
    }

    /// <summary>
    /// The list of what the last check found under a count of each kind, with Check again under it. Each problem
    /// says what is wrong, then where, with the compiler's code beside it in dim mono.
    /// </summary>
    KeyGesture CheckKey => new(Key.B, _command | KeyModifiers.Shift);

    internal MenuFlyout ProblemList()
    {
        var list = new MenuFlyout { Placement = PlacementMode.TopEdgeAlignedRight };
        list.Items.Add(Parts.MenuHeading(Counts()));
        foreach (var problem in _found)
        {
            var icon = new EditorIcon(problem.IsError ? "FaultCircle" : "WarnTriangle") { Width = 14, Height = 14 };
            TextElement.SetForeground(icon, problem.IsError ? EditorBrushes.Fault : EditorBrushes.Warn);
            var header = Parts.Noted(problem.Words, problem.Line > 0 ? $"{FileName(problem.Path)}, line {problem.Line}." : null, problem.Code == "" ? null : problem.Code);
            header.MaxWidth = 560;
            var item = new MenuItem
            {
                Header = header,
                Icon = icon,
                // A problem with no place in a project file has nowhere to go.
                IsEnabled = problem.Line > 0 && problem.Path.StartsWith(FileTree.Root, StringComparison.Ordinal),
            };
            AutomationProperties.SetName(item, problem.Words);
            item.Click += (_, _) => GoTo(problem);
            list.Items.Add(item);
        }

        list.Items.Add(new Separator());
        list.Items.Add(Parts.PressItem("Check again", _check, CheckKey, "Saves, then builds again."));
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
        if (_found.Count == 0)
        {
            ShowProblems("No problems", "Tick", EditorBrushes.Done);
            return;
        }

        var errors = _found.Any(problem => problem.IsError);
        ShowProblems(Count(_found.Count, "problem"), errors ? "FaultCircle" : "WarnTriangle", errors ? EditorBrushes.FaultText : EditorBrushes.Warn);
    }

    string Counts()
    {
        var errors = _found.Count(problem => problem.IsError);
        return $"{Count(errors, "error")}, {Count(_found.Count - errors, "warning")}";
    }

    static string Count(int count, string word) => count == 1 ? $"1 {word}" : $"{count} {word}s";

    void ShowProblems(string text, string? icon, IBrush tint)
    {
        _problemsText.Text = text;
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Gaps.Tight };
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
        dot.Margin = new Thickness(Gaps.Item, 0, 0, 0);
        DockPanel.SetDock(dot, Dock.Right);
        var kind = new TextBlock { Text = "C#", FontFamily = EditorTheme.Mono, FontSize = 10.5, Foreground = EditorBrushes.Unit, Margin = new Thickness(Gaps.Item, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(kind, Dock.Right);
        return new DockPanel
        {
            Children =
            {
                dot,
                kind,
                Parts.RowIcon("CodeFile", EditorBrushes.ScriptIcon),
                Parts.Trimmed(FileName(path), path),
            },
        };
    }
}
