using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Shell;

/// <summary>
/// One thing a picker offers: an icon and a name, a dim line under the name, and a depth that indents it as a
/// tree row. An off choice shows its line as the reason and cannot be lit.
/// </summary>
internal sealed record Choice(string Icon, string Name)
{
    public string Line { get; init; } = "";

    public IBrush? Tint { get; init; }

    public bool Off { get; init; }

    public int Depth { get; init; }

    /// <summary>The engine's name for it, in small dim mono after the name. None when the two are the same.</summary>
    public string? Engine { get; init; }

    /// <summary>The heading it sits under. None draws no heading.</summary>
    public ChoiceGroup? Group { get; init; }

    /// <summary>Whether its name, its engine name or its line holds the text.</summary>
    public bool Holds(string text) =>
        Name.Contains(text, StringComparison.OrdinalIgnoreCase)
        || (Engine?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false)
        || Line.Contains(text, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// A heading over choices. A folded group shows one row with its note in place of its choices, until that row is
/// pressed. A search looks through its choices all the same.
/// </summary>
internal sealed record ChoiceGroup(string Name)
{
    public string Note { get; init; } = "";

    public bool Folded { get; init; }
}

/// <summary>
/// A search field over the choices whose name, engine name or line holds it. A click or the arrows light a choice.
/// Enter goes on to the dialog that holds the list, which runs its main action on the lit choice. Choices in
/// groups sit under their headings, and a search puts every match under one Matches heading.
/// </summary>
internal sealed class ChoiceList : StackPanel
{
    // A row with a line under its name is taller, and a tree row steps in by depth as the scene tree does.
    const double LinedRow = 40;
    const double TreeRow = 26;
    const double Step = 16;

    internal const string Matches = "Matches";

    readonly StackPanel _rows = new() { Spacing = Gaps.Joined, Classes = { EditorTheme.Joined } };
    readonly List<(Button Row, EditorIcon Icon)> _drawn = [];
    readonly List<Button> _folds = [];
    readonly HashSet<ChoiceGroup> _unfolded = [];
    readonly DockPanel _top;
    readonly DockPanel _body;
    IReadOnlyList<Choice> _all = [];

    public ChoiceList(string placeholder)
    {
        SearchBox = new TextBox { PlaceholderText = placeholder, InnerLeftContent = Parts.LeadIcon("Lens"), Classes = { EditorTheme.Field } };
        _top = new DockPanel { Children = { SearchBox } };
        _body = new DockPanel { Margin = new Thickness(0, Gaps.Item, 0, 0), Children = { new ScrollViewer { MaxHeight = 320, Content = _rows } } };
        Children.Add(_top);
        Children.Add(_body);
        // TextChanged waits for a render, so the property is the one that fires at once.
        SearchBox.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty)
            {
                Fill();
            }
        };
        // The search box moves its caret on the arrows, so the list takes them on the way down.
        AddHandler(KeyDownEvent, OnKey, RoutingStrategies.Tunnel);
    }

    /// <summary>Raised when the lit choice changes, also to none.</summary>
    public event Action? PickedChanged;

    internal TextBox SearchBox { get; }

    internal IReadOnlyList<Choice> Shown { get; private set; } = [];

    internal IEnumerable<Button> RowButtons => _drawn.Select(drawn => drawn.Row);

    /// <summary>The rows that stand for folded groups. Pressing one shows its choices.</summary>
    internal IReadOnlyList<Button> FoldRows => _folds;

    /// <summary>A control at the right of the search box, such as one that says where the pick goes.</summary>
    public Control? Beside
    {
        get;
        init
        {
            field = value;
            PutRight(_top, value);
        }
    }

    /// <summary>A panel at the right of the list, such as one that tells about the lit choice.</summary>
    public Control? Aside
    {
        get;
        init
        {
            field = value;
            PutRight(_body, value);
        }
    }

    /// <summary>The place of the lit choice in <see cref="Shown"/>, or -1 for none.</summary>
    internal int Lit { get; private set; } = -1;

    public Choice? Picked => Lit < 0 ? null : Shown[Lit];

    /// <summary>Shows these choices under an empty search, with the first one that is on lit.</summary>
    public void Show(IReadOnlyList<Choice> choices)
    {
        _all = choices;
        _unfolded.Clear();
        SearchBox.Text = "";
        Fill();
    }

    static void PutRight(DockPanel panel, Control? control)
    {
        if (control is not null)
        {
            DockPanel.SetDock(control, Dock.Right);
            panel.Children.Insert(0, control);
        }
    }

    void Fill()
    {
        var query = SearchBox.Text?.Trim() ?? "";
        var matching = _all.Where(choice => choice.Holds(query)).ToList();
        // A search puts its matches under one heading, wherever their groups are.
        var matches = query != "" && matching.Any(choice => choice.Group is not null) ? new ChoiceGroup(Matches) : null;
        _rows.Children.Clear();
        _drawn.Clear();
        _folds.Clear();
        Lit = -1;
        if (matching.Count == 0)
        {
            _rows.Children.Add(new TextBlock { Text = "Nothing matches.", FontSize = 12, Foreground = EditorBrushes.TextDim, Margin = new Thickness(Gaps.Item) });
        }

        Shown = Draw(matching, matches);
        Light(Next(-1, 1));
    }

    // Draws each choice under its group's heading. A folded group draws its fold row in place of its choices.
    List<Choice> Draw(List<Choice> choices, ChoiceGroup? matches)
    {
        var shown = new List<Choice>();
        ChoiceGroup? heading = null;
        foreach (var choice in choices)
        {
            var group = matches ?? choice.Group;
            var folded = group is { Folded: true } && !_unfolded.Contains(group);
            if (group is not null && group != heading)
            {
                heading = group;
                _rows.Children.Add(folded ? Fold(group) : Picker.Heading(group.Name));
            }

            if (!folded)
            {
                var drawn = Row(choice, shown.Count);
                shown.Add(choice);
                _drawn.Add(drawn);
                _rows.Children.Add(drawn.Row);
            }
        }

        return shown;
    }

    // Pressing the row opens the group and lights its first choice.
    Button Fold(ChoiceGroup group)
    {
        var chevron = new EditorIcon("GuiTreeArrowRight") { Width = 12, Height = 12, VerticalAlignment = VerticalAlignment.Center };
        TextElement.SetForeground(chevron, EditorBrushes.TextDim);
        DockPanel.SetDock(chevron, Dock.Right);
        var words = Parts.Noted(group.Name, group.Note == "" ? null : group.Note);
        words.VerticalAlignment = VerticalAlignment.Center;
        var row = new Button
        {
            Height = LinedRow,
            Margin = new Thickness(0, Gaps.Tight, 0, 0),
            Padding = new Thickness(Gaps.Item, 0),
            CornerRadius = new CornerRadius(6),
            Focusable = false,
            Background = Brushes.Transparent,
            Foreground = EditorBrushes.Text,
            BorderBrush = EditorBrushes.Line,
            BorderThickness = new Thickness(0, 1, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Center,
            Content = new DockPanel { Children = { chevron, words } },
        };
        AutomationProperties.SetName(row, group.Name);
        row.Click += (_, _) =>
        {
            _unfolded.Add(group);
            Fill();
            Light(Shown.ToList().FindIndex(choice => choice.Group == group));
        };
        _folds.Add(row);
        return row;
    }

    void OnKey(object? sender, KeyEventArgs e)
    {
        var step = e.Key switch { Key.Up => -1, Key.Down => 1, _ => 0 };
        if (step == 0)
        {
            return;
        }

        e.Handled = true;
        if (Next(Lit, step) is var next and >= 0)
        {
            Light(next);
        }
    }

    // The next choice that is on from a place in a direction, or -1 when there is none.
    int Next(int from, int step)
    {
        for (var at = from + step; at >= 0 && at < Shown.Count; at += step)
        {
            if (!Shown[at].Off)
            {
                return at;
            }
        }

        return -1;
    }

    void Light(int index)
    {
        Paint(Lit, false);
        Lit = index;
        Paint(Lit, true);
        PickedChanged?.Invoke();
    }

    // The lit row takes the soft accent, an accent icon and white words.
    void Paint(int index, bool lit)
    {
        if (index < 0)
        {
            return;
        }

        var (row, icon) = _drawn[index];
        row.Background = lit ? EditorBrushes.AccentSoft : Brushes.Transparent;
        row.Foreground = lit ? Brushes.White : EditorBrushes.Text;
        TextElement.SetForeground(icon, lit ? EditorBrushes.Accent : Shown[index].Tint ?? EditorBrushes.TextDim);
        if (lit)
        {
            row.BringIntoView();
        }
    }

    // Rows never take the keys, so the search box keeps typing while the pointer picks.
    (Button Row, EditorIcon Icon) Row(Choice choice, int index)
    {
        var icon = new EditorIcon(choice.Icon) { Width = 14, Height = 14, Margin = new Thickness(0, 0, Gaps.Item, 0), VerticalAlignment = VerticalAlignment.Center };
        TextElement.SetForeground(icon, choice.Tint ?? EditorBrushes.TextDim);
        var words = Parts.Noted(choice.Name, choice.Line == "" ? null : choice.Line, choice.Engine);
        words.VerticalAlignment = VerticalAlignment.Center;
        DockPanel.SetDock(icon, Dock.Left);
        var row = new Button
        {
            Height = choice.Line == "" ? TreeRow : LinedRow,
            Padding = new Thickness(Gaps.Item + (Step * choice.Depth), 0, Gaps.Item, 0),
            CornerRadius = new CornerRadius(6),
            Focusable = false,
            IsEnabled = !choice.Off,
            Background = Brushes.Transparent,
            Foreground = EditorBrushes.Text,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Center,
            Content = new DockPanel { Children = { icon, words } },
        };
        AutomationProperties.SetName(row, choice.Name);
        row.Click += (_, _) => Light(index);
        return (row, icon);
    }
}
