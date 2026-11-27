using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.VisualTree;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Shell;

/// <summary>
/// The drag of one tab. A press that moves past the slack marks where a drop would go, a release drops there, and
/// Escape or a lost pointer leaves everything as it was.
/// </summary>
// shortcut: only a pointer moves a single tab. The panel menu moves whole groups. Add moves for a tab to the menu if
// keyboard users need them.
internal sealed class TabDrag
{
    const double Slack = 6;

    readonly DockTab _tab;
    readonly DockArea _area;
    Point? _pressed;
    // The window the drag started in, while one is under way.
    TopLevel? _top;
    DropTarget? _target;
    DropMark? _mark;
    PixelPoint _screen;

    TabDrag(DockTab tab, DockArea area)
    {
        _tab = tab;
        _area = area;
    }

    internal static void Follow(DockTab tab, DockArea area)
    {
        var drag = new TabDrag(tab, area);
        tab.PointerPressed += drag.Press;
        tab.PointerMoved += drag.Move;
        tab.PointerReleased += drag.Release;
        tab.PointerCaptureLost += (_, _) => drag.End();
    }

    void Press(object? sender, PointerPressedEventArgs e)
    {
        var top = TopLevel.GetTopLevel(_tab)!;
        _pressed = e.GetCurrentPoint(_tab).Properties.IsLeftButtonPressed ? e.GetPosition(top) : null;
    }

    void Move(object? sender, PointerEventArgs e)
    {
        if (_pressed is not { } from || !e.GetCurrentPoint(_tab).Properties.IsLeftButtonPressed)
        {
            return;
        }

        var top = TopLevel.GetTopLevel(_tab)!;
        var at = e.GetPosition(top);
        if (_top is null)
        {
            var moved = at - from;
            if (Math.Abs(moved.X) < Slack && Math.Abs(moved.Y) < Slack)
            {
                return;
            }

            if (_area.Locked)
            {
                _pressed = null;
                _area.Refuse();
                return;
            }

            _top = top;
            e.Pointer.Capture(_tab);
            top.AddHandler(InputElement.KeyDownEvent, Key, RoutingStrategies.Tunnel);
        }

        _screen = top.PointToScreen(at);
        Aim(DropTarget.At(_area, _tab, _screen));
    }

    void Release(object? sender, PointerReleasedEventArgs e)
    {
        var target = _target;
        End();
        if (target is not null)
        {
            _area.Drop(_tab, target, _screen);
        }
    }

    void Key(object? sender, KeyEventArgs e)
    {
        if (e.Key == Avalonia.Input.Key.Escape)
        {
            e.Handled = true;
            End();
        }
    }

    void Aim(DropTarget? target)
    {
        if (target == _target)
        {
            return;
        }

        Unmark();
        _target = target;
        if (target is null)
        {
            return;
        }

        var layer = AdornerLayer.GetAdornerLayer(target.On)!;
        _mark = new DropMark(target.Mark, target.Words);
        AdornerLayer.SetAdornedElement(_mark, target.On);
        layer.Children.Add(_mark);
    }

    void Unmark()
    {
        (_mark?.GetVisualParent() as AdornerLayer)?.Children.Remove(_mark!);
        _mark = null;
    }

    void End()
    {
        _pressed = null;
        _top?.RemoveHandler(InputElement.KeyDownEvent, Key);
        _top = null;
        Unmark();
        _target = null;
    }
}

/// <summary>Shows where a dragged tab goes, over the control it would go to. It takes no clicks.</summary>
internal sealed class DropMark : Border
{
    public DropMark(Rect area, string words)
    {
        Words = words;
        IsHitTestVisible = false;
        HorizontalAlignment = HorizontalAlignment.Left;
        VerticalAlignment = VerticalAlignment.Top;
        Margin = new Thickness(area.X, area.Y, 0, 0);
        Width = area.Width;
        Height = area.Height;
        Background = EditorBrushes.AccentSoft;
        BorderBrush = EditorBrushes.Accent;
        BorderThickness = new Thickness(2);
        CornerRadius = EditorTheme.Corners;
        Child = new Border
        {
            Background = EditorBrushes.Popover,
            CornerRadius = EditorTheme.Corners,
            Padding = new Thickness(Gaps.Item, Gaps.Tight),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock { Text = words, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
        };
    }

    internal string Words { get; }
}
