using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Reactive;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Shell;

/// <summary>
/// The text area of the Script workspace: C# in its colors, line numbers in a gutter, and the caret's line lit.
/// Breakpoints are dots left of the numbers, and the line a debugged game stopped on is lit too.
/// The text box draws its text clear. A copy in colour lies over it and takes no clicks, so the caret and the
/// selection stay the text box's own.
/// </summary>
internal sealed class CodeBox : Border
{
    internal const double LineHeight = 21.25;

    const double TextSize = 12.5;

    // The column the breakpoint marks take. A click in it sets or clears the line's breakpoint.
    const double MarkColumn = 20;
    const double MarkSize = 10;
    const double NumberWidth = 30;
    const double NumberGap = 14;
    const double NumberSize = 12;
    const double Inset = 10;

    // The graph's breakpoint and current-step colors, so a stop looks the same in both workspaces.
    static readonly IBrush s_mark = new ImmutableSolidColorBrush(Color.Parse("#FF5C6C"));
    static readonly IBrush s_paused = new ImmutableSolidColorBrush(Color.Parse("#F2D34F"));

    // The colour of each kind, in the order of CodeKind. Plain text keeps the layer's colour.
    static readonly IBrush?[] s_colours =
    [
        null,
        EditorBrushes.CodeKeyword,
        EditorBrushes.CodeType,
        EditorBrushes.CodeString,
        EditorBrushes.CodeNumber,
        EditorBrushes.CodeComment,
        EditorBrushes.CodeFunction,
        EditorBrushes.CodeAttribute,
    ];

    // shortcut: one text box holds the whole file and every edit colors it all again, so a file of many thousands
    // of lines types slowly. Use a virtualized editor then.
    readonly TextBox _editor = new()
    {
        AcceptsReturn = true,
        AcceptsTab = true,
        TextWrapping = TextWrapping.NoWrap,
        FontFamily = EditorTheme.Mono,
        FontSize = TextSize,
        LineHeight = LineHeight,
        Padding = new Thickness(0),
        BorderThickness = new Thickness(0),
        Background = null,
        Foreground = Brushes.Transparent,
        SelectionForegroundBrush = Brushes.Transparent,
        CaretBrush = EditorBrushes.Text,
        Classes = { EditorTheme.Bare },
    };

    readonly TextBlock _colours = new()
    {
        FontFamily = EditorTheme.Mono,
        FontSize = TextSize,
        LineHeight = LineHeight,
        Foreground = EditorBrushes.Text,
        IsHitTestVisible = false,
    };

    readonly TextBlock _gutter = Numbers(EditorBrushes.Disabled);
    readonly TextBlock _number = Numbers(EditorBrushes.TextSoft);
    readonly Border _band = new()
    {
        Height = LineHeight,
        VerticalAlignment = VerticalAlignment.Top,
        Background = EditorBrushes.CodeLine,
        BorderBrush = EditorBrushes.Accent,
        BorderThickness = new Thickness(2, 0, 0, 0),
    };

    readonly Border _pausedLine = new()
    {
        Height = LineHeight,
        VerticalAlignment = VerticalAlignment.Top,
        Background = new ImmutableSolidColorBrush(Color.Parse("#2EF2D34F")),
        BorderBrush = s_paused,
        BorderThickness = new Thickness(2, 0, 0, 0),
        IsVisible = false,
    };

    readonly Canvas _marks = new() { Width = MarkColumn, HorizontalAlignment = HorizontalAlignment.Left, Background = Brushes.Transparent, Cursor = new Cursor(StandardCursorType.Hand) };
    readonly ScrollViewer _scroll;

    public CodeBox()
    {
        Background = EditorBrushes.Code;
        _number.VerticalAlignment = VerticalAlignment.Top;
        var text = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), Margin = new Thickness(0, Inset) };
        Grid.SetColumnSpan(_band, 2);
        Grid.SetColumnSpan(_pausedLine, 2);
        Grid.SetColumn(_editor, 1);
        Grid.SetColumn(_colours, 1);
        text.Children.AddRange([_band, _pausedLine, _gutter, _number, _marks, _editor, _colours]);
        ToolTip.SetTip(_marks, "Set or clear a breakpoint");
        _marks.PointerPressed += (_, e) =>
        {
            ClickMark(e.GetPosition(_marks).Y);
            e.Handled = true;
        };
        _scroll = new ScrollViewer { Content = text, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto };
        // A short file would leave the box narrower than the area, and a click beside it would reach nothing.
        _scroll.GetObservable(ScrollViewer.ViewportProperty).Subscribe(new AnonymousObserver<Size>(viewport => text.MinWidth = viewport.Width));
        _editor.GetObservable(TextBox.TextProperty).Subscribe(new AnonymousObserver<string?>(_ => Paint()));
        _editor.GetObservable(TextBox.CaretIndexProperty).Subscribe(new AnonymousObserver<int>(_ => ShowCaret()));
        Child = _scroll;
    }

    /// <summary>Raised when the caret moves, by a key, a click or an edit.</summary>
    public event Action? CaretMoved;

    /// <summary>Raised with the line, counted from one, whose breakpoint mark was clicked.</summary>
    public event Action<int>? MarkClicked;

    /// <summary>The breakpoint dots, one for each marked line.</summary>
    internal Canvas Marks => _marks;

    /// <summary>The band that lights the line a debugged game stopped on.</summary>
    internal Border PausedLine => _pausedLine;

    internal TextBox Editor => _editor;

    internal TextBlock Gutter => _gutter;

    /// <summary>The colored copy of the text that lies over the text box.</summary>
    internal TextBlock Colors => _colours;

    /// <summary>The band that lights the caret's line.</summary>
    internal Border CaretLine => _band;

    /// <summary>The caret's line and column, both counted from one.</summary>
    internal (int Line, int Column) Caret
    {
        get
        {
            var text = _editor.Text ?? "";
            var at = Math.Min(_editor.CaretIndex, text.Length);
            var start = at == 0 ? 0 : text.LastIndexOf('\n', at - 1) + 1;
            return (text.AsSpan(0, at).Count('\n') + 1, at - start + 1);
        }
    }

    /// <summary>Puts the caret at this place of the text, gives the box the keys and scrolls its line into view.</summary>
    public void GoTo(int index)
    {
        _editor.CaretIndex = index;
        _editor.Focus();
        ShowLineOf(index);
    }

    /// <summary>Selects this much of the text from this place and scrolls its line into view. The keys stay where they are.</summary>
    public void Select(int start, int length)
    {
        // A caret write drops the selection, so it goes first.
        _editor.CaretIndex = start;
        _editor.SelectionStart = start;
        _editor.SelectionEnd = start + length;
        ShowLineOf(start);
    }

    /// <summary>Marks the lines that hold a breakpoint, and lights the line the game stopped on. Null lights none.</summary>
    public void ShowMarks(IReadOnlySet<int> lines, int? paused)
    {
        _marks.Children.Clear();
        _marks.Children.AddRange(lines.Select(Mark));
        _pausedLine.IsVisible = paused is not null;
        _pausedLine.Margin = new Thickness(0, ((paused ?? 1) - 1) * LineHeight, 0, 0);
    }

    /// <summary>A click this far down the mark column, which names the line there. Past the last line names none.</summary>
    internal void ClickMark(double y)
    {
        var line = (int)(y / LineHeight) + 1;
        if (line >= 1 && line <= (_editor.Text ?? "").Count(c => c == '\n') + 1)
        {
            MarkClicked?.Invoke(line);
        }
    }

    static Ellipse Mark(int line)
    {
        var dot = new Ellipse { Width = MarkSize, Height = MarkSize, Fill = s_mark };
        Canvas.SetLeft(dot, (MarkColumn - MarkSize) / 2);
        Canvas.SetTop(dot, ((line - 1) * LineHeight) + ((LineHeight - MarkSize) / 2));
        return dot;
    }

    void ShowLineOf(int index)
    {
        var text = _editor.Text ?? "";
        var top = text.AsSpan(0, Math.Min(index, text.Length)).Count('\n') * LineHeight;
        var shown = _scroll.Offset.Y;
        if (top < shown || top + LineHeight > shown + _scroll.Viewport.Height)
        {
            // A third of the way down, so the lines above the member show too.
            _scroll.Offset = new Vector(_scroll.Offset.X, Math.Max(0, top - (_scroll.Viewport.Height / 3)));
        }
    }

    static TextBlock Numbers(IBrush tint) => new()
    {
        MinWidth = NumberWidth,
        Margin = new Thickness(MarkColumn, 0, NumberGap, 0),
        FontFamily = EditorTheme.Mono,
        FontSize = NumberSize,
        LineHeight = LineHeight,
        TextAlignment = TextAlignment.Right,
        Foreground = tint,
        IsHitTestVisible = false,
    };

    void Paint()
    {
        var text = _editor.Text ?? "";
        _gutter.Text = string.Join('\n', Enumerable.Range(1, text.Count(c => c == '\n') + 1));
        _colours.Inlines!.Clear();
        _colours.Inlines.AddRange(CodeColours.Read(text).Select(span => Paint(text, span)));
        ShowCaret();
    }

    static Run Paint(string text, CodeSpan span)
    {
        var run = new Run(text.Substring(span.Start, span.Length));
        if (s_colours[(int)span.Kind] is { } colour)
        {
            run.Foreground = colour;
        }

        return run;
    }

    void ShowCaret()
    {
        var line = Caret.Line;
        _band.Margin = new Thickness(0, (line - 1) * LineHeight, 0, 0);
        _number.Margin = new Thickness(MarkColumn, (line - 1) * LineHeight, NumberGap, 0);
        _number.Text = line.ToString(System.Globalization.CultureInfo.InvariantCulture);
        CaretMoved?.Invoke();
    }
}
