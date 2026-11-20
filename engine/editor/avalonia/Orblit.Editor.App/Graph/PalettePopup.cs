using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Orblit.Editor.App.Shell;
using Orblit.Editor.App.Theme;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App.Graph;

/// <summary>
/// A search box over the steps a person can add, grouped as the engine groups them, with the chosen step shown
/// beside the list. The text goes to the search it was opened with, and the engine picks and orders what comes
/// back. One group is open at a time: the one that holds the chosen step. Choosing a step closes the palette and
/// hands the step to the pick it was opened with, and says whether to wire it to the selected step.
/// </summary>
internal sealed class PalettePopup : Border
{
    const double PopupWidth = 560;
    const double PopupHeight = 420;
    const double ListWidth = 250;
    const double RowHeight = 26;

    readonly TextBox _text = new() { PlaceholderText = "Search steps", FontSize = 13, Padding = new Thickness(0), MinHeight = 0, VerticalAlignment = VerticalAlignment.Center, Classes = { EditorTheme.Bare } };
    readonly StackPanel _rows = new() { Margin = new Thickness(6) };
    readonly TextBlock _group = new() { FontSize = 11, Foreground = EditorBrushes.Unit };
    readonly TextBlock _label = new() { FontSize = 16, FontWeight = FontWeight.Bold, Foreground = EditorBrushes.Text };
    readonly TextBlock _chipLabel = new() { FontSize = 12, FontWeight = FontWeight.SemiBold, Foreground = EditorBrushes.Text, VerticalAlignment = VerticalAlignment.Center };
    readonly Border _chipHeader = new() { Height = StepView.HeaderHeight, Padding = new Thickness(12, 0), CornerRadius = new CornerRadius(10, 10, 0, 0) };
    readonly StackPanel _preview = new() { Spacing = 6, Margin = new Thickness(18) };
    Func<string, IReadOnlyList<GraphStepEntry>> _search = _ => [];
    Action<GraphStepEntry, bool> _pick = (_, _) => { };
    IReadOnlyList<GraphStepEntry> _entries = [];
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
    }

    /// <summary>Raised when the popup closes, so the canvas can take the focus back.</summary>
    public event Action? Closed;

    public bool IsOpen => IsVisible;

    internal TextBox SearchBox => _text;

    /// <summary>What the search found, group by group. A group keeps the order the search gave its steps.</summary>
    internal IReadOnlyList<GraphStepEntry> Entries => _entries;

    internal int SelectedIndex => _selected;

    /// <summary>The group headings and the open group's steps, in the order they show.</summary>
    internal IEnumerable<Button> Rows => _rows.Children.OfType<Button>();

    /// <summary>The words beside the list: the chosen step's group and name.</summary>
    internal string Preview => $"{_group.Text} {_label.Text}";

    // shortcut: the last search is kept for this graph and not for the whole editor session. Keep it in the shell if people want it across files.
    /// <summary>
    /// Opens at a point of the canvas, with the search that fits what the person is adding. The pick is told
    /// whether the person asked to wire the step to the selection.
    /// </summary>
    public void Open(Point at, Size within, Func<string, IReadOnlyList<GraphStepEntry>> search, Action<GraphStepEntry, bool> pick)
    {
        _search = search;
        _pick = pick;
        Margin = new Thickness(Math.Clamp(at.X, 0, Math.Max(0, within.Width - PopupWidth)), Math.Clamp(at.Y, 0, Math.Max(0, within.Height - PopupHeight)), 0, 0);
        _opening = true;
        _text.Text = _remembered;
        _opening = false;
        IsVisible = true;
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
        _pick(entry, wire);
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
        _chipHeader.Child = _chipLabel;
        _preview.Children.AddRange([_group, _label, new Border
        {
            Width = StepView.Width,
            Margin = new Thickness(0, 12, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
            Background = EditorBrushes.Raised,
            BorderBrush = EditorBrushes.LineStrong,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Child = new StackPanel { Children = { _chipHeader, new Border { Height = 30 } } },
        }]);
        var footer = new Border
        {
            Padding = new Thickness(14, 8),
            BorderBrush = EditorBrushes.Line,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Child = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 14, Children = { Hint("↵", "add at the cursor."), Hint("⇧↵", "add and wire.") } },
        };
        var body = new Grid { ColumnDefinitions = new ColumnDefinitions($"{ListWidth},*") };
        Grid.SetColumn(_preview, 1);
        body.Children.AddRange([list, _preview]);
        Grid.SetRow(footer, 1);
        return new Grid { RowDefinitions = new RowDefinitions("*,Auto"), Children = { body, footer } };
    }

    static StackPanel Hint(string key, string what) => new()
    {
        Orientation = Orientation.Horizontal,
        Spacing = 6,
        Children = { Parts.Kbd(key), new TextBlock { Text = what, FontSize = 11, Foreground = EditorBrushes.Unit, VerticalAlignment = VerticalAlignment.Center } },
    };

    // The text box changes its text when Open sets it, and Open searches once itself.
    void Search()
    {
        if (_opening)
        {
            return;
        }
        // Grouped by first appearance, so the engine's order holds inside a group and between groups.
        _entries = [.. _search(_text.Text ?? "").GroupBy(entry => entry.Group).SelectMany(group => group)];
        Select(0);
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
        _group.Text = entry?.Group ?? "";
        _label.Text = entry?.Label ?? "";
        _chipLabel.Text = entry?.Label ?? "";
        _chipHeader.Background = entry is null ? null : StepView.CategoryBrush(entry.Group);
    }

    // Every group shows its heading and count. Only the group of the chosen step lists its steps.
    void ShowRows()
    {
        _rows.Children.Clear();
        if (_entries.Count == 0)
        {
            _rows.Children.Add(new TextBlock { Text = "Nothing matches.", FontSize = 12, Foreground = EditorBrushes.TextDim, Margin = new Thickness(8) });
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
    }

    // Rows never take the keys, so the search box keeps typing while the pointer picks.
    static Button Heading(string group, int count, bool open, Action choose)
    {
        var chevron = new EditorIcon(open ? "GuiTreeArrowDown" : "GuiTreeArrowRight") { Width = 12, Height = 12, Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center };
        var number = new TextBlock { Text = count.ToString(System.Globalization.CultureInfo.InvariantCulture), FontSize = 11, Foreground = EditorBrushes.Unit, VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(chevron, Dock.Left);
        DockPanel.SetDock(number, Dock.Right);
        var name = new TextBlock { Text = group.ToUpperInvariant(), FontSize = 10.5, FontWeight = FontWeight.SemiBold, LetterSpacing = 0.6, Foreground = EditorBrushes.Unit, VerticalAlignment = VerticalAlignment.Center };
        var button = Flat($"palette:group:{group}", new DockPanel { Children = { chevron, number, name } }, new Thickness(6, 0, 8, 0));
        button.Click += (_, _) => choose();
        return button;
    }

    Button StepRow(int index)
    {
        var text = new TextBlock { Text = _entries[index].Label, FontSize = 12.5, Foreground = EditorBrushes.Text, VerticalAlignment = VerticalAlignment.Center };
        var button = Flat($"palette:step:{index}", text, new Thickness(22, 0, 8, 0));
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

    static Button Flat(string tag, Control content, Thickness padding) => new()
    {
        Tag = tag,
        Height = RowHeight,
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
