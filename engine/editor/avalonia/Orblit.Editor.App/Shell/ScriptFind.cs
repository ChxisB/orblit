using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Shell;

/// <summary>
/// The card over a script that finds text in it. Case counts only while Aa is on, and Option and C turns it on or
/// off. Enter selects the next match and Shift and Enter the one before, both going round past the ends. Escape
/// closes the card. The keys stay in the find box, so Enter can be pressed again.
/// </summary>
internal sealed class ScriptFind : Border
{
    readonly CodeBox _code;
    readonly TextBox _text = new() { PlaceholderText = "Find", Width = 200, Classes = { EditorTheme.Field } };
    readonly TextBlock _count = new() { MinWidth = 64, FontSize = 11, Foreground = EditorBrushes.TextDim, VerticalAlignment = VerticalAlignment.Center };
    readonly ToggleButton _case = new() { Content = "Aa", FontSize = 11.5, Padding = new Thickness(6, 3), MinWidth = 0, MinHeight = 0, BorderThickness = new Thickness(0) };

    public ScriptFind(CodeBox code)
    {
        _code = code;
        IsVisible = false;
        Background = EditorBrushes.Raised;
        BorderBrush = EditorBrushes.LineStrong;
        BorderThickness = new Thickness(1);
        CornerRadius = EditorTheme.Corners;
        Padding = new Thickness(6, 4);
        Margin = new Thickness(0, 8, 16, 0);
        HorizontalAlignment = HorizontalAlignment.Right;
        VerticalAlignment = VerticalAlignment.Top;
        var previous = new HoverCard("Previous match", "Selects the match before this one.", "⇧↵").On(Parts.IconButton("ArrowUp", ""));
        var next = new HoverCard("Next match", "Selects the match after this one.", "↵").On(Parts.IconButton("ArrowDown", ""));
        var close = new HoverCard("Close", "Hides the find bar and goes back to the code.", "esc").On(new Button { Content = Parts.Kbd("esc"), Padding = new Thickness(2), MinWidth = 0, MinHeight = 0, BorderThickness = new Thickness(0), Background = null });
        new HoverCard("Match case", "Speed and speed count as different.", "⌥C").On(_case);
        previous.Click += (_, _) => Find(false);
        next.Click += (_, _) => Find(true);
        close.Click += (_, _) => Close();
        _case.IsCheckedChanged += (_, _) => Refind();
        Child = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            Children = { _text, _count, _case, previous, next, close },
        };
        _text.KeyDown += OnKey;
    }

    internal TextBox Text => _text;

    /// <summary>Which match is selected out of how many, or that there is none.</summary>
    internal string Count => _count.Text ?? "";

    /// <summary>Aa, which makes case count.</summary>
    internal ToggleButton CaseButton => _case;

    /// <summary>Shows the card with the find box's text selected and the keys in it.</summary>
    public void Open()
    {
        IsVisible = true;
        _text.Focus();
        _text.SelectAll();
    }

    /// <summary>Selects the first match after the selection, or the last one before it, and counts the matches.</summary>
    internal void Find(bool forward)
    {
        var wanted = _text.Text ?? "";
        var starts = Starts(_code.Editor.Text ?? "", wanted, _case.IsChecked == true ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);
        if (starts.Count == 0)
        {
            _count.Text = wanted.Length == 0 ? "" : "No match";
            return;
        }

        var editor = _code.Editor;
        var (from, to) = (Math.Min(editor.SelectionStart, editor.SelectionEnd), Math.Max(editor.SelectionStart, editor.SelectionEnd));
        var at = forward ? starts.FindIndex(start => start >= to) : starts.FindLastIndex(start => start < from);
        if (at < 0)
        {
            at = forward ? 0 : starts.Count - 1;
        }

        _code.Select(starts[at], wanted.Length);
        _count.Text = $"{at + 1} of {starts.Count}";
    }

    // Matches do not overlap, so the next one starts at or after the end of the one selected.
    static List<int> Starts(string text, string wanted, StringComparison rule)
    {
        var starts = new List<int>();
        for (var at = wanted.Length == 0 ? -1 : text.IndexOf(wanted, rule); at >= 0; at = text.IndexOf(wanted, at + wanted.Length, rule))
        {
            starts.Add(at);
        }

        return starts;
    }

    // The selection shrinks to its start, so a match there is found again under the new rule.
    void Refind()
    {
        var editor = _code.Editor;
        _code.Select(Math.Min(editor.SelectionStart, editor.SelectionEnd), 0);
        Find(true);
    }

    void Close()
    {
        IsVisible = false;
        _count.Text = "";
        _code.Editor.Focus();
    }

    void OnKey(object? sender, KeyEventArgs e)
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
        else if (e.Key == Key.C && e.KeyModifiers == KeyModifiers.Alt)
        {
            _case.IsChecked = _case.IsChecked != true;
            e.Handled = true;
        }
    }
}
