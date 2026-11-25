using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using Microsoft.CodeAnalysis.Completion;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Shell;

/// <summary>
/// The completions at a code box's caret, in a list under it. Each row has a line on what it is, read when the row
/// is built. Ctrl+Space or a typed dot asks for them. Up and Down move, Enter and Tab put the pick in, Escape closes,
/// and typing a name narrows the list.
/// </summary>
internal sealed class CompletionPopup
{
    readonly CodeBox _code;
    readonly Func<string, int, Task<CompletionSet?>> _ask;
    readonly ListBox _list = new()
    {
        Background = null,
        BorderThickness = new Thickness(0),
        Width = 340,
        MaxHeight = 260,
        Classes = { EditorTheme.Rows },
    };

    readonly Popup _popup;
    CompletionSet? _set;
    int _asks;

    /// <summary>The ask is given the text and the caret, and answers what Roslyn offers there.</summary>
    public CompletionPopup(CodeBox code, Func<string, int, Task<CompletionSet?>> ask)
    {
        _code = code;
        _ask = ask;
        _list.ItemTemplate = new FuncDataTemplate<CompletionItem>((item, _) => item is null || _set is not { } set ? null : Row(set, item));
        _popup = new Popup
        {
            Placement = PlacementMode.AnchorAndGravity,
            PlacementAnchor = PopupAnchor.BottomLeft,
            PlacementGravity = PopupGravity.BottomRight,
            IsLightDismissEnabled = true,
            Child = new Border
            {
                Padding = new Thickness(4),
                CornerRadius = EditorTheme.Corners,
                Background = EditorBrushes.Raised,
                BorderBrush = EditorBrushes.LineStrong,
                BorderThickness = new Thickness(1),
                Child = new StackPanel
                {
                    Children =
                    {
                        _list,
                        new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Margin = new Thickness(6, 6, 6, 2), Children = { Parts.KeyHint("insert", "↵"), Parts.KeyHint("close", "esc") } },
                    },
                },
            },
        };
        code.Editor.AddHandler(InputElement.KeyDownEvent, OnKey, RoutingStrategies.Tunnel);
        // After the box has put the dot in, so the ask sees it.
        code.Editor.AddHandler(InputElement.TextInputEvent, OnTyped, RoutingStrategies.Bubble, handledEventsToo: true);
        code.CaretMoved += () =>
        {
            if (_set is not null)
            {
                Filter();
            }
        };
        // A click outside closes the list too, and a list closed that way must not come back when the caret moves.
        _popup.Closed += (_, _) => _set = null;
        _list.DoubleTapped += (_, _) => Asking = Insert();
    }

    /// <summary>Sits in the code box's grid cell, so the list takes the editor's theme.</summary>
    internal Popup Popup => _popup;

    internal ListBox List => _list;

    /// <summary>The last ask or insert, for a test to wait on.</summary>
    internal Task Asking { get; private set; } = Task.CompletedTask;

    // The line is read off the UI thread when the row first shows, so a long list reads only the rows on screen.
    static DockPanel Row(CompletionSet set, CompletionItem item)
    {
        var kind = item.Tags.FirstOrDefault() ?? "";
        var detail = new TextBlock { Text = kind.ToLowerInvariant(), FontSize = 11, Foreground = EditorBrushes.TextDim, Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(detail, Dock.Right);
        var name = new TextBlock { Text = item.DisplayText + item.DisplayTextSuffix, FontFamily = EditorTheme.Mono, FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
        var line = new TextBlock { FontSize = 11, Foreground = EditorBrushes.TextDim, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(28, 0, 0, 0) };
        line.AttachedToVisualTree += async (_, _) => line.Text = await Task.Run(() => set.LineOf(item));
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { Parts.RowIcon(IconOf(kind)), name } };
        return new DockPanel { Children = { detail, new StackPanel { Children = { row, line } } } };
    }

    // The outline's icons, where Roslyn's kind has one.
    static string IconOf(string kind) => kind switch
    {
        "Method" or "ExtensionMethod" => "MemberMethod",
        "Property" or "Field" => "MemberProperty",
        "Constant" or "EnumMember" => "MemberConstant",
        "Event" => "MemberSignal",
        "Keyword" => "Keyword",
        "Local" or "Parameter" or "RangeVariable" => "LocalVariable",
        "Enum" => "Enum",
        _ => "Script",
    };

    void OnKey(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space && e.KeyModifiers == KeyModifiers.Control)
        {
            e.Handled = true;
            Asking = Ask();
            return;
        }

        if (!_popup.IsOpen)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Up or Key.Down:
                var last = _list.ItemCount - 1;
                _list.SelectedIndex = Math.Clamp(_list.SelectedIndex + (e.Key == Key.Up ? -1 : 1), 0, last);
                _list.ScrollIntoView(_list.SelectedIndex);
                break;
            case Key.Enter or Key.Tab:
                Asking = Insert();
                break;
            case Key.Escape:
                Close();
                break;
            default:
                return;
        }

        e.Handled = true;
    }

    void OnTyped(object? sender, TextInputEventArgs e)
    {
        if (e.Text == ".")
        {
            Asking = Ask();
        }
    }

    async Task Ask()
    {
        var ask = ++_asks;
        var set = await _ask(_code.Editor.Text ?? "", _code.Editor.CaretIndex);
        // An answer that comes after a later ask is for a caret that has moved on.
        if (ask == _asks)
        {
            _set = set;
            Filter();
        }
    }

    // The list keeps the items whose name starts with what was typed since the list's start. Any other key closes it.
    void Filter()
    {
        var text = _code.Editor.Text ?? "";
        var caret = _code.Editor.CaretIndex;
        if (_set is null || caret < _set.Start || caret > text.Length)
        {
            Close();
            return;
        }

        var typed = text[_set.Start..caret];
        var shown = _set.Items
            .Where(item => item.FilterText.StartsWith(typed, StringComparison.OrdinalIgnoreCase))
            .OrderBy(item => item.SortText, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (!typed.All(c => char.IsLetterOrDigit(c) || c == '_') || shown.Count == 0)
        {
            Close();
            return;
        }

        _list.ItemsSource = shown;
        _list.SelectedIndex = 0;
        if (_code.Editor.GetVisualDescendants().OfType<TextPresenter>().FirstOrDefault() is { } presenter)
        {
            _popup.PlacementTarget = presenter;
            _popup.PlacementRect = presenter.TextLayout.HitTestTextPosition(caret);
        }

        _popup.IsOpen = true;
    }

    // Roslyn's change is for the text it was asked about. What was typed since sits inside the change's span, so the
    // span grows by it. The change goes in as one edit, so one undo takes it out.
    async Task Insert()
    {
        if (_set is not { } set || _list.SelectedItem is not CompletionItem item)
        {
            return;
        }

        Close();
        var change = await set.ChangeOf(item);
        var editor = _code.Editor;
        var span = change.TextChange.Span;
        editor.SelectionStart = span.Start;
        editor.SelectionEnd = Math.Max(span.Start, span.End + (editor.Text ?? "").Length - set.Length);
        editor.SelectedText = change.TextChange.NewText ?? item.DisplayText;
        if (change.NewPosition is { } caret)
        {
            editor.CaretIndex = caret;
        }
    }

    void Close()
    {
        _set = null;
        _popup.IsOpen = false;
    }
}
