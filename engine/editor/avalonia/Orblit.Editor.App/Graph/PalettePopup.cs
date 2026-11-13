using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.VisualTree;
using Orblit.Editor.App.Theme;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App.Graph;

/// <summary>
/// A search box over a list of the steps a person can add. The text goes to the search it was opened with, and
/// the engine picks and orders what comes back. It lists what it is given and adds nothing: choosing an entry
/// closes it and hands the entry to the pick it was opened with.
/// </summary>
internal sealed class PalettePopup : Border
{
    const double PopupWidth = 320;
    const double ListHeight = 260;
    const double PopupHeight = ListHeight + 44;

    readonly TextBox _text = new() { PlaceholderText = "Search steps", MinHeight = 0, Padding = new Thickness(6, 4), FontSize = 12 };
    readonly ListBox _list = new() { MaxHeight = ListHeight, FontSize = 12 };
    Func<string, IReadOnlyList<GraphStepEntry>> _search = _ => [];
    Action<GraphStepEntry> _pick = _ => { };
    IReadOnlyList<GraphStepEntry> _entries = [];
    string _remembered = "";
    bool _opening;

    public PalettePopup()
    {
        IsVisible = false;
        Width = PopupWidth;
        HorizontalAlignment = HorizontalAlignment.Left;
        VerticalAlignment = VerticalAlignment.Top;
        Background = EditorBrushes.Panel;
        BorderBrush = EditorBrushes.BarSeparator;
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(4);
        Padding = new Thickness(6);
        _list.ItemTemplate = new FuncDataTemplate<GraphStepEntry>((entry, _) => Row(entry));
        var stack = new StackPanel { Spacing = 6 };
        stack.Children.Add(_text);
        stack.Children.Add(_list);
        Child = stack;
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
        _list.Tapped += OnTapped;
    }

    /// <summary>Raised when the popup closes, so the canvas can take the focus back.</summary>
    public event Action? Closed;

    public bool IsOpen => IsVisible;

    internal TextBox SearchBox => _text;

    /// <summary>What the list shows now, in the order the search gave it.</summary>
    internal IReadOnlyList<GraphStepEntry> Entries => _entries;

    internal int SelectedIndex => _list.SelectedIndex;

    // shortcut: the last search is kept for this graph and not for the whole editor session. Keep it in the shell if people want it across files.
    /// <summary>Opens at a point of the canvas, with the search that fits what the person is adding.</summary>
    public void Open(Point at, Size within, Func<string, IReadOnlyList<GraphStepEntry>> search, Action<GraphStepEntry> pick)
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

    /// <summary>Chooses the selected entry. An empty list chooses nothing.</summary>
    public void Pick()
    {
        if (_list.SelectedIndex < 0 || _list.SelectedIndex >= _entries.Count)
        {
            return;
        }
        var entry = _entries[_list.SelectedIndex];
        Close();
        _pick(entry);
    }

    static StackPanel Row(GraphStepEntry entry)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        row.Children.Add(new TextBlock { Text = entry.Label });
        row.Children.Add(new TextBlock { Text = entry.Group, Foreground = EditorBrushes.TabInactive, FontSize = 11, VerticalAlignment = VerticalAlignment.Center });
        return row;
    }

    // The text box changes its text when Open sets it, and Open searches once itself.
    void Search()
    {
        if (_opening)
        {
            return;
        }
        _entries = _search(_text.Text ?? "");
        _list.ItemsSource = _entries;
        _list.SelectedIndex = _entries.Count > 0 ? 0 : -1;
    }

    void OnKey(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down:
                Select(_list.SelectedIndex + 1);
                break;
            case Key.Up:
                Select(_list.SelectedIndex - 1);
                break;
            case Key.Enter:
                Pick();
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
        if (_entries.Count > 0)
        {
            _list.SelectedIndex = Math.Clamp(index, 0, _entries.Count - 1);
            _list.ScrollIntoView(_list.SelectedIndex);
        }
    }

    // A tap on a row chooses it. A tap on the scroll bar or the gap below the rows chooses nothing.
    void OnTapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        if ((e.Source as Visual)?.FindAncestorOfType<ListBoxItem>() is not null)
        {
            Pick();
        }
    }
}
