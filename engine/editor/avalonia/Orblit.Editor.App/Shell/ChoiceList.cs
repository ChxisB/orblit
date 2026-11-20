using Avalonia;
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
}

/// <summary>
/// A search field over the choices whose name holds it. A click or the arrows light a choice. Enter goes on to the
/// dialog that holds the list, which runs its main action on the lit choice.
/// </summary>
internal sealed class ChoiceList : StackPanel
{
    // A row with a line under its name is taller, and a tree row steps in by depth as the scene tree does.
    const double LinedRow = 40;
    const double TreeRow = 26;
    const double Step = 16;

    readonly StackPanel _rows = new();
    readonly List<(Button Row, EditorIcon Icon)> _drawn = [];
    IReadOnlyList<Choice> _all = [];

    public ChoiceList(string placeholder)
    {
        SearchBox = new TextBox { PlaceholderText = placeholder, InnerLeftContent = Parts.FieldIcon("Lens", EditorBrushes.TextDim), Classes = { EditorTheme.Field } };
        Children.Add(SearchBox);
        Children.Add(new ScrollViewer { MaxHeight = 320, Margin = new Thickness(0, 8, 0, 0), Content = _rows });
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

    /// <summary>The place of the lit choice in <see cref="Shown"/>, or -1 for none.</summary>
    internal int Lit { get; private set; } = -1;

    public Choice? Picked => Lit < 0 ? null : Shown[Lit];

    /// <summary>Shows these choices under an empty search, with the first one that is on lit.</summary>
    public void Show(IReadOnlyList<Choice> choices)
    {
        _all = choices;
        SearchBox.Text = "";
        Fill();
    }

    void Fill()
    {
        var query = SearchBox.Text?.Trim() ?? "";
        Shown = [.. _all.Where(choice => choice.Name.Contains(query, StringComparison.OrdinalIgnoreCase))];
        _rows.Children.Clear();
        _drawn.Clear();
        Lit = -1;
        if (Shown.Count == 0)
        {
            _rows.Children.Add(new TextBlock { Text = "Nothing matches.", FontSize = 12, Foreground = EditorBrushes.TextDim, Margin = new Thickness(8) });
        }

        foreach (var choice in Shown)
        {
            var drawn = Row(choice, _drawn.Count);
            _drawn.Add(drawn);
            _rows.Children.Add(drawn.Row);
        }

        Light(Next(-1, 1));
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
        var icon = new EditorIcon(choice.Icon) { Width = 14, Height = 14, Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
        TextElement.SetForeground(icon, choice.Tint ?? EditorBrushes.TextDim);
        var name = new TextBlock { Text = choice.Name, VerticalAlignment = VerticalAlignment.Center };
        Control words = choice.Line == "" ? name : new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Children = { name, new TextBlock { Text = choice.Line, FontSize = 11, Foreground = EditorBrushes.TextDim } },
        };
        DockPanel.SetDock(icon, Dock.Left);
        var row = new Button
        {
            Height = choice.Line == "" ? TreeRow : LinedRow,
            Padding = new Thickness(6 + (Step * choice.Depth), 0, 8, 0),
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
        row.Click += (_, _) => Light(index);
        return (row, icon);
    }
}
