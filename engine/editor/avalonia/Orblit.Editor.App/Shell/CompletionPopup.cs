using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.VisualTree;
using Microsoft.CodeAnalysis.Completion;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Shell;

/// <summary>
/// The completions at a code box's caret, in a list under it. Ctrl+Space or a typed dot asks for them. Up and Down
/// move, Enter and Tab put the pick in, Escape closes, and typing a name narrows the list.
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

    /// <summary>The ask is given the text and the caret, and answers what Roslyn offers there.</summary>
    public CompletionPopup(CodeBox code, Func<string, int, Task<CompletionSet?>> ask)
    {
        _code = code;
        _ask = ask;
        _list.ItemTemplate = new FuncDataTemplate<CompletionItem>((item, _) => item is null ? null : Row(item));
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
                Child = _list,
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
        _list.DoubleTapped += (_, _) => Asking = Insert();
    }

    /// <summary>Sits in the code box's grid cell, so the list takes the editor's theme.</summary>
    internal Popup Popup => _popup;

    internal ListBox List => _list;

    /// <summary>The last ask or insert, for a test to wait on.</summary>
    internal Task Asking { get; private set; } = Task.CompletedTask;

    static DockPanel Row(CompletionItem item)
    {
        var kind = item.Tags.FirstOrDefault() ?? "";
        var detail = new TextBlock { Text = kind.ToLowerInvariant(), FontSize = 11, Foreground = EditorBrushes.TextDim, Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(detail, Dock.Right);
        var name = new TextBlock { Text = item.DisplayText + item.DisplayTextSuffix, FontFamily = EditorTheme.Mono, FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { Parts.RowIcon(IconOf(kind)), name } };
        return new DockPanel { Children = { detail, row } };
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
        _set = await _ask(_code.Editor.Text ?? "", _code.Editor.CaretIndex);
        Filter();
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

    // The pick replaces what was typed since the list's start, as typing it would, so one undo takes it out.
    async Task Insert()
    {
        if (_set is not { } set || _list.SelectedItem is not CompletionItem item)
        {
            return;
        }

        Close();
        var text = await set.TextOf(item);
        var editor = _code.Editor;
        editor.SelectionStart = set.Start;
        editor.SelectionEnd = Math.Max(editor.CaretIndex, set.Start);
        editor.SelectedText = text;
    }

    void Close()
    {
        _set = null;
        _popup.IsOpen = false;
    }
}
