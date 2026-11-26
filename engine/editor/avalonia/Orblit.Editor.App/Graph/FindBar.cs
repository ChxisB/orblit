using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Orblit.Editor.App.Shell;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Graph;

/// <summary>
/// The bar that finds steps by their names and numbers, goes to a step by its number and walks the bookmarks. Each
/// button's card gives its key. Enter in the find box shows the next match and Shift and Enter the previous one. Escape
/// closes the bar.
/// </summary>
internal sealed class FindBar : Border
{
    const string GotoHint = "Type a step number, like 12. Problems and Output name steps by number.";

    readonly GraphSearch _search;
    readonly Action _closed;
    readonly TextBox _text = new() { Tag = "find:text", PlaceholderText = "Find a step, like print", MinWidth = 170, MinHeight = 0, Padding = new Thickness(4, 2) };
    readonly TextBox _goto = new() { Tag = "find:goto", PlaceholderText = "Step number", MinWidth = 100, MinHeight = 0, Padding = new Thickness(4, 2) };
    readonly TextBlock _status = new() { Tag = "find:status", FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0) };
    readonly StackPanel _gotoRow;

    public FindBar(GraphSearch search, Action closed, KeyModifiers command)
    {
        _search = search;
        _closed = closed;
        IsVisible = false;
        Background = EditorBrushes.Panel;
        BorderBrush = EditorBrushes.Accent;
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(4);
        Padding = new Thickness(8, 4);
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Bottom;
        Margin = new Thickness(12);
        var row = new WrapPanel { MaxWidth = 760 };
        row.Children.Add(_text);
        row.Children.Add(_status);
        AddButton(row, ("previous", "Back", "⇧↵"), "Shows the match before the picked step.", () => Find(false));
        AddButton(row, ("next", "Next", "↵"), "Shows the match after the picked step.", () => Find(true));
        row.Children.Add(Parts.Rule());
        AddButton(row, ("open_goto", "Go to number", Parts.KeyText(new KeyGesture(Key.L, command))), "Shows the step with the number you type.", OpenGoto);
        row.Children.Add(Parts.Rule());
        AddButton(row, ("bookmark", "Bookmark", Parts.KeyText(new KeyGesture(Key.F2, command))), "Marks the picked step, or takes its mark off.", search.ToggleBookmark);
        row.Children.Add(new TextBlock { Text = "Bookmarks", FontSize = 12, Foreground = EditorBrushes.TextDim, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 2, 0) });
        AddButton(row, ("bookmark_previous", "Back", "⇧F2"), "Shows the bookmarked step before the picked one.", () => search.StepBookmark(false));
        AddButton(row, ("bookmark_next", "Next", "F2"), "Shows the bookmarked step after the picked one.", () => search.StepBookmark(true));
        row.Children.Add(Parts.Rule());
        AddButton(row, ("close", "Close", "Esc"), "Hides the bar.", Close);
        var hint = new TextBlock { Text = GotoHint, FontSize = 11, Foreground = EditorBrushes.TextDim, TextWrapping = TextWrapping.Wrap, MaxWidth = 420, VerticalAlignment = VerticalAlignment.Center };
        _gotoRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, IsVisible = false, Margin = new Thickness(0, 4, 0, 0), Children = { _goto, hint } };
        Child = new StackPanel { Children = { row, _gotoRow } };
        search.Reported += line => _status.Text = line;
        _text.KeyDown += OnTextKey;
        _goto.KeyDown += OnGotoKey;
    }

    /// <summary>The line the last command reported.</summary>
    internal string Status => _status.Text ?? "";

    /// <summary>Whether the row with the step number box shows.</summary>
    internal bool ShowsGoto => _gotoRow.IsVisible;

    /// <summary>The commands the bar runs. A command run from a menu says its answer on the bar, so the bar shows too.</summary>
    internal void Run(Action<GraphSearch> command)
    {
        IsVisible = true;
        command(_search);
    }

    /// <summary>Shows the bar and puts the cursor in the find box.</summary>
    public void Open()
    {
        IsVisible = true;
        _gotoRow.IsVisible = false;
        _text.Focus();
        _text.SelectAll();
    }

    /// <summary>Shows the bar with the step number box and its hint, and puts the cursor there.</summary>
    public void OpenGoto()
    {
        IsVisible = true;
        _gotoRow.IsVisible = true;
        _goto.Focus();
        _goto.SelectAll();
    }

    void Close()
    {
        IsVisible = false;
        _closed();
    }

    void Find(bool forward) => _search.Find(_text.Text ?? "", forward);

    static void AddButton(WrapPanel row, (string Id, string Word, string Key) name, string line, Action run)
    {
        var button = new Button
        {
            Tag = $"find:{name.Id}",
            Content = name.Word,
            VerticalAlignment = VerticalAlignment.Center,
            Classes = { EditorTheme.TextButton, EditorTheme.Ghost, EditorTheme.Small },
        };
        button.Click += (_, _) => run();
        new HoverCard(name.Word, line, name.Key).On(button);
        row.Children.Add(button);
    }

    void OnTextKey(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Find((e.KeyModifiers & KeyModifiers.Shift) == 0);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }

    void OnGotoKey(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            _search.GoTo(_goto.Text ?? "");
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }
}
