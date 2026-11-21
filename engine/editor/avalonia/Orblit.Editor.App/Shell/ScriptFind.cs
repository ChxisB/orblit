using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Shell;

/// <summary>
/// The card over a script that finds text in it, without regard to case. Enter selects the next match and Shift and
/// Enter the one before, both going round past the ends. Escape closes the card. The keys stay in the find box, so
/// Enter can be pressed again.
/// </summary>
internal sealed class ScriptFind : Border
{
    readonly CodeBox _code;
    readonly TextBox _text = new() { PlaceholderText = "Find", Width = 200, Classes = { EditorTheme.Field } };
    readonly TextBlock _count = new() { MinWidth = 64, FontSize = 11, Foreground = EditorBrushes.TextDim, VerticalAlignment = VerticalAlignment.Center };

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
        var previous = Parts.IconButton("ArrowUp", "The match before. Shift and Enter does the same.");
        var next = Parts.IconButton("ArrowDown", "The match after. Enter does the same.");
        var close = Parts.IconButton("GuiClose", "Close. Escape does the same.");
        previous.Click += (_, _) => Find(false);
        next.Click += (_, _) => Find(true);
        close.Click += (_, _) => Close();
        Child = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            Children = { _text, _count, previous, next, close },
        };
        _text.KeyDown += OnKey;
    }

    internal TextBox Text => _text;

    /// <summary>Which match is selected out of how many, or that there is none.</summary>
    internal string Count => _count.Text ?? "";

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
        var starts = Starts(_code.Editor.Text ?? "", wanted);
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
    static List<int> Starts(string text, string wanted)
    {
        var starts = new List<int>();
        for (var at = wanted.Length == 0 ? -1 : text.IndexOf(wanted, StringComparison.OrdinalIgnoreCase); at >= 0; at = text.IndexOf(wanted, at + wanted.Length, StringComparison.OrdinalIgnoreCase))
        {
            starts.Add(at);
        }

        return starts;
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
    }
}
