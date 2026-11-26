using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Orblit.Editor.App.Shell;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App;

/// <summary>
/// The Add a script popover. It picks a graph or a C# script, then makes a new one under a name or uses one the
/// project has. A row shows the script's path, because the editor does not know the class a script extends. While
/// the main button is off, its card says what to do first. Enter presses it.
/// </summary>
internal sealed class ScriptTool : StackPanel
{
    const string NameRule = "Letters, numbers and _ only.";

    // The order is the order of the kinds in the row. A list of cases, so it is data.
    static readonly (string Word, string Line, string Suffix, string None)[] s_kinds =
    [
        ("Graph", "Join steps with wires. No code needed.", ".ograph", "No graph scripts yet."),
        ("C#", "Write code. Best if you know a little programming.", ".cs", "No C# scripts yet."),
    ];

    readonly Segments _kind = new([.. s_kinds.Select(kind => kind.Word)]);
    readonly TextBlock _kindLine = new() { FontSize = 11, Foreground = EditorBrushes.TextDim, TextWrapping = TextWrapping.Wrap };
    readonly Segments _source = new("Make a new one", "Use one I have");
    readonly TextBox _name = new() { FontFamily = EditorTheme.Mono, Classes = { EditorTheme.Field } };
    readonly TextBlock _saved = new() { FontSize = 11, Foreground = EditorBrushes.TextDim };
    readonly TextBlock _fault = Parts.FaultLine();
    readonly StackPanel _make;
    readonly ListBox _list = new() { MaxHeight = 180, Classes = { EditorTheme.Field } };
    readonly TextBlock _noneLine = new() { FontSize = 12, Foreground = EditorBrushes.TextDim, VerticalAlignment = VerticalAlignment.Center };
    readonly Button _makeInstead = new() { Content = "Make a new one", Classes = { EditorTheme.TextButton, EditorTheme.Ghost, EditorTheme.Small } };
    readonly StackPanel _none;
    readonly Button _cancel = new() { Content = "Cancel", Classes = { EditorTheme.TextButton, EditorTheme.Ghost }, Margin = new Thickness(0, 0, Gaps.Item, 0) };
    readonly Button _add = new() { Content = Parts.Keyed("Add the script", "↵"), Classes = { EditorTheme.TextButton, EditorTheme.Primary } };
    IReadOnlyList<string>[] _paths = [[], []];
    string _entity = "";
    string _suggested = "";

    public ScriptTool()
    {
        Width = 340;
        Head = new CardHead("Add a script", "") { EngineName = "Attach Script" };
        _make = Parts.Labeled("Name", _name, _fault, _saved);
        _none = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Gaps.Item, Children = { _noneLine, _makeInstead } };
        DockPanel.SetDock(_add, Dock.Right);
        DockPanel.SetDock(_cancel, Dock.Right);
        Children.Add(Head);
        Children.Add(new Border
        {
            Padding = new Thickness(Gaps.Dialog, Gaps.Group),
            Child = new StackPanel
            {
                Spacing = Gaps.Group,
                Children = { Parts.Labeled("Kind", _kind, _kindLine), Parts.Labeled("Script", _source), _make, _list, _none },
            },
        });
        Children.Add(new Border
        {
            Padding = new Thickness(Gaps.Dialog, Gaps.Group),
            BorderBrush = EditorBrushes.Line,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Child = new DockPanel { LastChildFill = false, Children = { _add, _cancel } },
        });
        _kind.Changed += ShowKind;
        _source.Changed += Check;
        _makeInstead.Click += (_, _) => _source.SelectedIndex = 0;
        _list.SelectionChanged += (_, _) => Check();
        // TextChanged waits for a render, so the property is the one that fires at once.
        _name.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty)
            {
                Check();
            }
        };
        _add.Click += (_, _) => Add();
        Head.CloseButton.Click += (_, _) => Cancelled?.Invoke();
        _cancel.Click += (_, _) => Cancelled?.Invoke();
        // A popover is no dialog, so Enter is caught here.
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter && _add.IsEnabled)
            {
                e.Handled = true;
                Add();
            }
        };
    }

    /// <summary>Raised with the res:// path of a script the project has, to attach.</summary>
    public event Action<string>? Requested;

    /// <summary>Raised with the res:// path of a script to make and attach, and whether it is a graph.</summary>
    public event Action<string, bool>? Making;

    /// <summary>Raised by Cancel and by the cross.</summary>
    public event Action? Cancelled;

    internal CardHead Head { get; }

    internal Segments Kind => _kind;

    internal TextBlock KindLine => _kindLine;

    internal Segments Source => _source;

    internal TextBox NameBox => _name;

    /// <summary>The line under the name that says where the new script is saved.</summary>
    internal TextBlock SavedLine => _saved;

    internal ListBox List => _list;

    internal TextBlock None => _noneLine;

    internal Button MakeInsteadButton => _makeInstead;

    internal Button CancelButton => _cancel;

    internal Button AddButton => _add;

    bool IsGraph => _kind.SelectedIndex == 0;

    bool IsMaking => _source.SelectedIndex == 0;

    string Typed => (_name.Text ?? "").Trim();

    string NewPath => $"{FileTree.Root}{Typed}{s_kinds[_kind.SelectedIndex].Suffix}";

    /// <summary>
    /// Names the thing that runs the script and lists the scripts of each kind. It starts on a new graph named
    /// after the thing, and nothing is chosen in the list.
    /// </summary>
    public void Show(string entity, IReadOnlyList<string> scripts, IReadOnlyList<string> graphs)
    {
        _entity = string.Concat(entity.Where(c => char.IsAsciiLetterOrDigit(c) || c == '_'));
        Head.Heading.Text = $"Add a script to {entity}";
        Head.Why.Text = $"A script tells {entity} what to do while the game runs.";
        _paths = [graphs, scripts];
        _kind.SelectedIndex = 0;
        _source.SelectedIndex = 0;
        _name.Text = "";
        _suggested = "";
        ShowKind();
    }

    // The name follows the kind while it is still the one offered.
    void ShowKind()
    {
        var kind = s_kinds[_kind.SelectedIndex];
        _kindLine.Text = kind.Line;
        if (Typed == _suggested)
        {
            _suggested = $"{_entity}{(IsGraph ? "Graph" : "Script")}";
            _name.Text = _suggested;
        }
        var paths = _paths[_kind.SelectedIndex];
        _list.ItemsSource = paths;
        _list.SelectedIndex = -1;
        _noneLine.Text = kind.None;
        Check();
    }

    // Make waits for a name that keeps the rule and is no script yet. Use waits for a pick.
    void Check()
    {
        var kind = s_kinds[_kind.SelectedIndex];
        var empty = _paths[_kind.SelectedIndex].Count == 0;
        _make.IsVisible = IsMaking;
        _list.IsVisible = !IsMaking && !empty;
        _none.IsVisible = !IsMaking && empty;
        var badName = Typed.Length > 0 && !(Typed.All(c => char.IsAsciiLetterOrDigit(c) || c == '_') && !char.IsAsciiDigit(Typed[0]));
        var taken = _paths[_kind.SelectedIndex].Contains(NewPath);
        var fault = badName ? NameRule : taken ? $"{Typed}{kind.Suffix} already exists. Pick another name." : "";
        _name.Classes.Set(EditorTheme.Bad, fault != "");
        _fault.Text = fault;
        _fault.IsVisible = fault != "";
        _saved.Text = Typed == "" ? "" : $"Saved as {NewPath}";
        var why = IsMaking
            ? Typed == "" ? "Give it a name first." : fault != "" ? fault : null
            : empty ? $"{kind.None} Make a new one instead." : _list.SelectedItem is null ? "Pick a script in the list first." : null;
        _add.IsEnabled = why is null;
        new HoverCard("Add the script", why ?? "Puts the script on this thing.", "↵").On(_add);
    }

    void Add()
    {
        if (IsMaking)
        {
            Making?.Invoke(NewPath, IsGraph);
        }
        else if (_list.SelectedItem is string path)
        {
            Requested?.Invoke(path);
        }
    }
}
