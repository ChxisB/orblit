using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Graph;

/// <summary>
/// The bar that finds steps by their text, goes to a step by its id and walks the bookmarks. Enter in the find
/// box shows the next match and Shift and Enter the previous one. Escape closes the bar.
/// </summary>
internal sealed class FindBar : Border
{
    readonly GraphSearch _search;
    readonly Action _closed;
    readonly TextBox _text = new() { Tag = "find:text", PlaceholderText = "Find steps", MinWidth = 150, MinHeight = 0, Padding = new Thickness(4, 2) };
    readonly TextBox _goto = new() { Tag = "find:goto", PlaceholderText = "Go to step id", MinWidth = 100, MinHeight = 0, Padding = new Thickness(4, 2) };
    readonly TextBlock _status = new() { Tag = "find:status", FontSize = 12, VerticalAlignment = VerticalAlignment.Center, MinWidth = 90 };

    public FindBar(GraphSearch search, Action closed)
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
        var row = new WrapPanel { MaxWidth = 640 };
        row.Children.Add(_text);
        AddButton(row, "previous", "Previous", "The match before the selected step", () => Find(false));
        AddButton(row, "next", "Next", "The match after the selected step", () => Find(true));
        row.Children.Add(_goto);
        AddButton(row, "bookmark", "Bookmark", "Mark the selected step, or take its mark off", search.ToggleBookmark);
        AddButton(row, "bookmark_previous", "Prev mark", "The bookmarked step before the selected one", () => search.StepBookmark(false));
        AddButton(row, "bookmark_next", "Next mark", "The bookmarked step after the selected one", () => search.StepBookmark(true));
        AddButton(row, "close", "Close", "Close the bar. Escape does the same.", Close);
        row.Children.Add(_status);
        Child = row;
        search.Reported += line => _status.Text = line;
        _text.KeyDown += OnTextKey;
        _goto.KeyDown += OnGotoKey;
    }

    /// <summary>The line the last command reported.</summary>
    internal string Status => _status.Text ?? "";

    /// <summary>Shows the bar and puts the cursor in the find box.</summary>
    public void Open()
    {
        IsVisible = true;
        _text.Focus();
        _text.SelectAll();
    }

    void Close()
    {
        IsVisible = false;
        _closed();
    }

    void Find(bool forward) => _search.Find(_text.Text ?? "", forward);

    static void AddButton(WrapPanel row, string id, string label, string tip, Action run)
    {
        var button = PanelFields.Small($"find:{id}", label, run);
        ToolTip.SetTip(button, tip);
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
