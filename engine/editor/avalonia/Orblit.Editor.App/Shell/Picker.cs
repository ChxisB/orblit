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
/// One row of a picker: an icon, a name with the engine's name beside it, a dim note under them, and what it is and
/// its key at the right. Run is what a pick does.
/// </summary>
internal sealed record PickerRow(string Group, string Icon, string Name, Action Run)
{
    public IBrush? Tint { get; init; }

    public string Note { get; init; } = "";

    public string Key { get; init; } = "";

    /// <summary>What the row is, such as "3D scene" or "C# script".</summary>
    public string Kind { get; init; } = "";

    /// <summary>The engine's name for it, where that differs. A query finds a row by it too.</summary>
    public string Engine { get; init; } = "";
}

/// <summary>
/// A card at the top of the window over a search box and the rows that match it, under a heading for each group.
/// The arrows move the light, Enter runs the lit row, and Escape or a click outside the card closes it.
/// It waits hidden until <see cref="Open"/>.
/// </summary>
internal sealed class Picker : Border
{
    readonly TextBox _search;
    readonly StackPanel _rows = new() { Margin = new Thickness(6) };
    readonly List<Button> _buttons = [];
    Func<string, IReadOnlyList<PickerRow>> _find = _ => [];

    public Picker(string name, TextBox search, Control top, Control footer)
    {
        _search = search;
        var body = new StackPanel { Children = { top, new ScrollViewer { MaxHeight = 360, Content = _rows }, footer } };
        Card = new Border
        {
            Width = 440,
            Margin = new Thickness(0, 72, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Background = EditorBrushes.Popover,
            BorderBrush = EditorBrushes.LineStrong,
            BorderThickness = new Thickness(1),
            CornerRadius = EditorTheme.PopoverCorners,
            BoxShadow = BoxShadows.Parse("0 20 50 0 #8C000000, 0 2 6 0 #59000000"),
            Child = body,
        };
        AutomationProperties.SetName(Card, name);
        // A clear fill takes the clicks outside the card, so one of them can close it.
        Background = Brushes.Transparent;
        IsVisible = false;
        Child = Card;
        search.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty)
            {
                Show();
            }
        };
        // The search box moves its caret on the arrows, so the card takes them on the way down.
        AddHandler(KeyDownEvent, OnKey, RoutingStrategies.Tunnel);
        PointerPressed += (_, e) =>
        {
            if (e.Source == this)
            {
                Close();
            }
        };
    }

    internal Border Card { get; }

    internal TextBox SearchBox => _search;

    internal IReadOnlyList<PickerRow> Shown { get; private set; } = [];

    internal IReadOnlyList<Button> RowButtons => _buttons;

    internal int Lit { get; private set; }

    /// <summary>The rows whose name or engine name holds the query, in their order.</summary>
    internal static IReadOnlyList<PickerRow> Matching(IEnumerable<PickerRow> rows, string query) =>
        [.. rows.Where(row => row.Name.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase) || row.Engine.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase))];

    /// <summary>Shows the card with this text in the search, the caret at its end. <paramref name="find"/> gives the rows for a query.</summary>
    public void Open(Func<string, IReadOnlyList<PickerRow>> find, string text = "")
    {
        _find = find;
        _search.Text = text;
        _search.CaretIndex = text.Length;
        Show();
        IsVisible = true;
        _search.Focus();
    }

    public void Close() => IsVisible = false;

    void Show()
    {
        Shown = _find(_search.Text ?? "");
        Lit = 0;
        _rows.Children.Clear();
        _buttons.Clear();
        if (Shown.Count == 0)
        {
            _rows.Children.Add(new TextBlock { Text = "Nothing matches. Try fewer letters, or > for actions.", FontSize = 12, Foreground = EditorBrushes.TextDim, Margin = new Thickness(8, 8) });
            return;
        }

        var group = "";
        foreach (var row in Shown)
        {
            if (row.Group != group && row.Group != "")
            {
                _rows.Children.Add(Heading(row.Group));
            }

            group = row.Group;
            var button = Row(row);
            _buttons.Add(button);
            _rows.Children.Add(button);
        }

        Light(0);
    }

    void OnKey(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Up when Shown.Count > 0:
                Light(Math.Max(Lit - 1, 0));
                break;
            case Key.Down when Shown.Count > 0:
                Light(Math.Min(Lit + 1, Shown.Count - 1));
                break;
            case Key.Enter when Shown.Count > 0:
                Run(Shown[Lit]);
                break;
            case Key.Escape:
                Close();
                break;
            default:
                return;
        }

        e.Handled = true;
    }

    void Light(int index)
    {
        _buttons[Lit].Background = Brushes.Transparent;
        Lit = index;
        _buttons[Lit].Background = EditorBrushes.AccentSoft;
        _buttons[Lit].BringIntoView();
    }

    // The card goes first, so a row that opens a dialog or a scene does not leave it over the result.
    void Run(PickerRow row)
    {
        Close();
        row.Run();
    }

    internal static TextBlock Heading(string group) => new()
    {
        Text = group.ToUpperInvariant(),
        FontSize = 10.5,
        FontWeight = FontWeight.SemiBold,
        LetterSpacing = 0.6,
        Foreground = EditorBrushes.Unit,
        Margin = new Thickness(8, 7, 8, 4),
    };

    // Rows never take the keys, so the search box keeps typing while the pointer picks.
    Button Row(PickerRow row)
    {
        var icon = new EditorIcon(row.Icon) { Width = 15, Height = 15, Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center };
        TextElement.SetForeground(icon, row.Tint ?? EditorBrushes.TextDim);
        var right = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            Margin = new Thickness(10, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new TextBlock { Text = row.Kind, FontSize = 11, Foreground = EditorBrushes.TextDim, VerticalAlignment = VerticalAlignment.Center },
                new TextBlock { Text = row.Key, FontFamily = EditorTheme.Mono, FontSize = 11, Foreground = EditorBrushes.Unit, VerticalAlignment = VerticalAlignment.Center },
            },
        };
        DockPanel.SetDock(icon, Dock.Left);
        DockPanel.SetDock(right, Dock.Right);
        var words = Parts.Noted(row.Name, row.Note == "" ? null : row.Note, row.Engine == "" ? null : row.Engine);
        words.VerticalAlignment = VerticalAlignment.Center;
        var button = new Button
        {
            MinHeight = 34,
            Padding = new Thickness(8, 4),
            CornerRadius = new CornerRadius(6),
            Focusable = false,
            Background = Brushes.Transparent,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Center,
            Content = new DockPanel { Children = { icon, right, words } },
        };
        button.Click += (_, _) => Run(row);
        return button;
    }
}
