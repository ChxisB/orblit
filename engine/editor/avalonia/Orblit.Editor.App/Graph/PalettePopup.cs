using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Orblit.Editor.App.Shell;
using Orblit.Editor.App.Theme;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App.Graph;

/// <summary>
/// What the palette opens for: the search that fits what is being added, and what takes the chosen step and whether
/// to wire it. A wire dragged from a step joins the new step to it on Enter. With no drag, Shift and Enter join it
/// to the picked step.
/// </summary>
internal sealed record PaletteJob(Func<string, IReadOnlyList<GraphStepEntry>> Search, Action<GraphStepEntry, bool> Pick)
{
    /// <summary>The number of the step a wire was dragged from, or none.</summary>
    public int? DraggedFrom { get; init; }

    /// <summary>The number of the picked step that Shift and Enter join to, or none.</summary>
    public int? Picked { get; init; }
}

/// <summary>
/// A search box over the steps a person can add, under plain names with the engine's names beside them, and the
/// chosen step shown beside the list with what it does. Pinned steps sit at the top until there is text. The text
/// goes to the search it was opened with, and the engine picks and orders what comes back. A step whose plain name
/// holds the text comes first. One group is open at a time: the one that holds the chosen step. Choosing a step
/// closes the palette and hands the step to the pick it was opened with, and says whether to wire it.
/// </summary>
internal sealed class PalettePopup : Border
{
    const double PopupWidth = 560;
    const double PopupHeight = 420;
    const double ListWidth = 250;
    const double RowHeight = 26;
    const string PinnedGroup = "Pinned";

    readonly TextBox _text = new() { PlaceholderText = "Find a step, like wait or if", FontSize = 13, Padding = new Thickness(0), MinHeight = 0, VerticalAlignment = VerticalAlignment.Center, Classes = { EditorTheme.Bare } };
    readonly StackPanel _rows = new() { Margin = new Thickness(6) };
    readonly TextBlock _group = new() { FontSize = 11, Foreground = EditorBrushes.Unit };
    readonly TextBlock _label = new() { FontSize = 16, FontWeight = FontWeight.Bold, Foreground = EditorBrushes.Text };
    readonly TextBlock _engine = new() { FontFamily = EditorTheme.Mono, FontSize = 11, Foreground = EditorBrushes.Unit, VerticalAlignment = VerticalAlignment.Center };
    readonly StepPreview _chip = new() { Margin = new Thickness(-6, 6, 0, 0), HorizontalAlignment = HorizontalAlignment.Left };
    readonly TextBlock _about = new() { FontSize = 12, Foreground = EditorBrushes.TextSoft, TextWrapping = TextWrapping.Wrap };
    readonly TextBlock _good = new() { FontSize = 11.5, Foreground = EditorBrushes.TextDim, TextWrapping = TextWrapping.Wrap };
    readonly Button _pin = new() { Tag = "palette:pin", Focusable = false, HorizontalAlignment = HorizontalAlignment.Left, Classes = { EditorTheme.TextButton, EditorTheme.Ghost, EditorTheme.Small } };
    readonly StackPanel _preview = new() { Spacing = 6, Margin = new Thickness(18) };
    readonly StackPanel _footer = new() { Orientation = Orientation.Horizontal, Spacing = 14 };

    // shortcut: pins are kept for this graph while it is open, like the last search. Keep them in the editor settings if people want them everywhere.
    readonly List<(string Kind, string Label)> _pinned = [];
    PaletteJob _job = new(_ => [], (_, _) => { });
    IReadOnlyList<GraphStepEntry> _table = [];
    List<GraphStepEntry> _entries = [];
    int _selected = -1;
    string _remembered = "";
    bool _opening;

    public PalettePopup()
    {
        IsVisible = false;
        Width = PopupWidth;
        Height = PopupHeight;
        HorizontalAlignment = HorizontalAlignment.Left;
        VerticalAlignment = VerticalAlignment.Top;
        Background = EditorBrushes.Popover;
        BorderBrush = EditorBrushes.LineStrong;
        BorderThickness = new Thickness(1);
        CornerRadius = EditorTheme.PopoverCorners;
        ClipToBounds = true;
        Child = Layout();
        // The property changes as the text is set. The TextChanged event can come later, after a key has been handled.
        _text.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty)
            {
                Search();
            }
        };
        // Tunnel, so the arrow keys and Enter reach the popup before the text box uses them to move its caret.
        _text.AddHandler(InputElement.KeyDownEvent, OnKey, RoutingStrategies.Tunnel);
        _pin.Click += (_, _) => TogglePin();
    }

    /// <summary>Raised when the popup closes, so the canvas can take the focus back.</summary>
    public event Action? Closed;

    public bool IsOpen => IsVisible;

    internal TextBox SearchBox => _text;

    /// <summary>What the list holds, group by group: the pinned steps, then what the search found.</summary>
    internal IReadOnlyList<GraphStepEntry> Entries => _entries;

    internal int SelectedIndex => _selected;

    /// <summary>The group headings and the open group's steps, in the order they show.</summary>
    internal IEnumerable<Button> Rows => _rows.Children.OfType<Button>();

    /// <summary>The words beside the list: the chosen step's group, its names, what it does and what it is good for.</summary>
    internal string Preview => string.Join(" ", new[] { _group.Text, _label.Text, _engine.Text, _about.Text, _good.Text }.Where(text => !string.IsNullOrEmpty(text)));

    /// <summary>The step drawn beside the list, as it will be added.</summary>
    internal GraphStep? PreviewStep => _chip.Step;

    internal Button PinButton => _pin;

    /// <summary>The keys at the foot and what each one does.</summary>
    internal string Footer => string.Join(" ", _footer.GetLogicalDescendants().OfType<TextBlock>().Select(text => text.Text));

    // shortcut: the last search is kept for this graph and not for the whole editor session. Keep it in the shell if people want it across files.
    /// <summary>Opens at a point of the canvas, for the job: what to search, what takes the step and what it joins to.</summary>
    public void Open(Point at, Size within, PaletteJob job)
    {
        _job = job;
        _table = job.Search("");
        Margin = new Thickness(Math.Clamp(at.X, 0, Math.Max(0, within.Width - PopupWidth)), Math.Clamp(at.Y, 0, Math.Max(0, within.Height - PopupHeight)), 0, 0);
        _opening = true;
        _text.Text = _remembered;
        _opening = false;
        IsVisible = true;
        ShowFooter();
        Search();
        _text.Focus();
        _text.SelectAll();
    }

    public void Close()
    {
        if (!IsVisible)
        {
            return;
        }
        _remembered = _text.Text ?? "";
        IsVisible = false;
        Closed?.Invoke();
    }

    /// <summary>Chooses the selected step. An empty list chooses nothing.</summary>
    public void Pick(bool wire = false)
    {
        if (_selected < 0 || _selected >= _entries.Count)
        {
            return;
        }
        var entry = _entries[_selected];
        Close();
        _job.Pick(entry, wire);
    }

    Grid Layout()
    {
        var icon = Parts.SearchIcon();
        DockPanel.SetDock(icon, Dock.Left);
        var top = new Border
        {
            Height = 44,
            Padding = new Thickness(12, 0),
            BorderBrush = EditorBrushes.Line,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = new DockPanel { Children = { icon, _text } },
        };
        DockPanel.SetDock(top, Dock.Top);
        var list = new Border
        {
            BorderBrush = EditorBrushes.Line,
            BorderThickness = new Thickness(0, 0, 1, 0),
            Child = new DockPanel { Children = { top, new ScrollViewer { Content = _rows } } },
        };
        var names = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { _label, _engine } };
        _preview.Children.AddRange([_group, names, _chip, _about, _good, _pin]);
        var footer = new Border
        {
            Padding = new Thickness(14, 8),
            BorderBrush = EditorBrushes.Line,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Child = _footer,
        };
        var body = new Grid { ColumnDefinitions = new ColumnDefinitions($"{ListWidth},*") };
        Grid.SetColumn(_preview, 1);
        body.Children.AddRange([list, _preview]);
        Grid.SetRow(footer, 1);
        return new Grid { RowDefinitions = new RowDefinitions("*,Auto"), Children = { body, footer } };
    }

    // The foot says what Enter adds and the step a new one joins to.
    void ShowFooter()
    {
        _footer.Children.Clear();
        _footer.Children.Add(Parts.KeyHint("move", "↑", "↓"));
        if (_job.DraggedFrom is { } dragged)
        {
            _footer.Children.Add(Parts.KeyHint($"Add and join to step {dragged}", "↵"));
            return;
        }
        _footer.Children.Add(Parts.KeyHint("Add here", "↵"));
        if (_job.Picked is { } picked)
        {
            _footer.Children.Add(Parts.KeyHint($"Add and join to step {picked}", "⇧↵"));
        }
    }

    // The text box changes its text when Open sets it, and Open searches once itself.
    void Search()
    {
        if (_opening)
        {
            return;
        }
        // Grouped by first appearance, so the engine's order holds inside a group and between groups.
        _entries = [.. Found(_text.Text ?? "").GroupBy(entry => entry.Group).SelectMany(group => group)];
        Select(0);
    }

    // With no text, the pinned steps come first. With text, the steps whose plain name holds it come first, then what
    // the engine found, each once.
    IEnumerable<GraphStepEntry> Found(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            var pins = _pinned.Select(pin => _table.FirstOrDefault(entry => KeyOf(entry) == pin)).OfType<GraphStepEntry>();
            return [.. pins.Select(entry => entry with { Group = PinnedGroup }), .. _table];
        }
        var plain = _table.Where(entry => StepNames.Of(entry)?.Name.Contains(text.Trim(), StringComparison.OrdinalIgnoreCase) == true);
        return plain.Concat(_job.Search(text)).DistinctBy(KeyOf);
    }

    static (string Kind, string Label) KeyOf(GraphStepEntry entry) => (entry.Kind, entry.Label);

    void TogglePin()
    {
        var key = KeyOf(_entries[_selected]);
        if (!_pinned.Remove(key))
        {
            _pinned.Insert(0, key);
        }
        Search();
        Select(Math.Max(0, _entries.FindIndex(entry => KeyOf(entry) == key)));
    }

    void OnKey(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down:
                Select(_selected + 1);
                break;
            case Key.Up:
                Select(_selected - 1);
                break;
            case Key.Enter:
                Pick(wire: e.KeyModifiers.HasFlag(KeyModifiers.Shift));
                break;
            case Key.Escape:
                Close();
                break;
            default:
                return;
        }
        e.Handled = true;
    }

    void Select(int index)
    {
        _selected = _entries.Count == 0 ? -1 : Math.Clamp(index, 0, _entries.Count - 1);
        ShowRows();
        var entry = _selected < 0 ? null : _entries[_selected];
        _preview.IsVisible = entry is not null;
        _chip.Show(entry);
        ShowWords(entry);
        ShowPin(entry);
    }

    void ShowWords(GraphStepEntry? entry)
    {
        var row = entry is null ? null : StepNames.Of(entry);
        _group.Text = entry is null ? "" : StepNames.GroupOf(entry.Group);
        _label.Text = row?.Name ?? entry?.Label ?? "";
        _engine.Text = row?.EngineBeside ?? "";
        _about.Text = row?.About ?? "";
        _about.IsVisible = row is not null;
        _good.Text = row is null ? "" : $"Good for: {row.GoodFor}";
        _good.IsVisible = row is not null;
    }

    // Pins show only in the list before any search, so only a step that list holds can be pinned.
    void ShowPin(GraphStepEntry? entry)
    {
        if (entry is null)
        {
            _pin.IsVisible = false;
            return;
        }
        var key = KeyOf(entry);
        _pin.IsVisible = _table.Any(found => KeyOf(found) == key);
        var pinned = _pinned.Contains(key);
        var word = pinned ? "Unpin" : "Pin to the top";
        _pin.Content = word;
        new HoverCard(word, pinned ? "Takes this step off the top of the list." : "Keeps this step at the top of the list while this graph is open.").On(_pin);
    }

    // Every group shows its heading and count. Only the group of the chosen step lists its steps.
    void ShowRows()
    {
        _rows.Children.Clear();
        if (_entries.Count == 0)
        {
            _rows.Children.Add(Dim("No step matches. Try another word, like wait or if."));
            return;
        }
        var open = _entries[_selected].Group;
        var start = 0;
        foreach (var group in _entries.GroupBy(entry => entry.Group))
        {
            var first = start;
            _rows.Children.Add(Heading(group.Key, group.Count(), group.Key == open, () => Select(first)));
            for (var i = start; group.Key == open && i < start + group.Count(); i++)
            {
                _rows.Children.Add(StepRow(i));
            }
            start += group.Count();
        }
        if (!string.IsNullOrWhiteSpace(_text.Text))
        {
            _rows.Children.Add(Dim("Search looks at plain names and engine names."));
        }
    }

    static TextBlock Dim(string text) => new() { Text = text, FontSize = 11, Foreground = EditorBrushes.TextDim, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(8) };

    // Rows never take the keys, so the search box keeps typing while the pointer picks.
    static Button Heading(string group, int count, bool open, Action choose)
    {
        var chevron = new EditorIcon(open ? "GuiTreeArrowDown" : "GuiTreeArrowRight") { Width = 12, Height = 12, Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center };
        var number = new TextBlock { Text = count.ToString(System.Globalization.CultureInfo.InvariantCulture), FontSize = 11, Foreground = EditorBrushes.Unit, VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(chevron, Dock.Left);
        DockPanel.SetDock(number, Dock.Right);
        var plain = StepNames.GroupOf(group);
        var names = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        names.Children.Add(new TextBlock { Text = plain, FontSize = 12, FontWeight = FontWeight.SemiBold, Foreground = EditorBrushes.TextSoft });
        if (plain != group)
        {
            names.Children.Add(new TextBlock { Text = group, FontFamily = EditorTheme.Mono, FontSize = 10.5, Foreground = EditorBrushes.Unit, VerticalAlignment = VerticalAlignment.Center });
        }
        var button = Flat($"palette:group:{group}", new DockPanel { Children = { chevron, number, names } }, new Thickness(6, 0, 8, 0));
        button.Click += (_, _) => choose();
        return button;
    }

    Button StepRow(int index)
    {
        var entry = _entries[index];
        var row = StepNames.Of(entry);
        var button = Flat($"palette:step:{index}", Parts.Noted(row?.Name ?? entry.Label, row?.Line, row?.EngineBeside), new Thickness(22, 3, 8, 3));
        if (index == _selected)
        {
            button.Background = EditorBrushes.AccentSoft;
            // The row is new and has no place until layout has run.
            Dispatcher.UIThread.Post(() => button.BringIntoView(), DispatcherPriority.Loaded);
        }
        button.Click += (_, _) =>
        {
            _selected = index;
            Pick();
        };
        return button;
    }

    // A row with a line under its name grows to hold it.
    static Button Flat(string tag, Control content, Thickness padding) => new()
    {
        Tag = tag,
        MinHeight = RowHeight,
        Padding = padding,
        CornerRadius = new CornerRadius(6),
        Focusable = false,
        Background = Brushes.Transparent,
        HorizontalAlignment = HorizontalAlignment.Stretch,
        HorizontalContentAlignment = HorizontalAlignment.Stretch,
        VerticalContentAlignment = VerticalAlignment.Center,
        Content = content,
    };
}
