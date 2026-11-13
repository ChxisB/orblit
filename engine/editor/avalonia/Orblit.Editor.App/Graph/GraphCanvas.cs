using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using Orblit.Editor.App.Shell;
using Orblit.Editor.App.Theme;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App.Graph;

/// <summary>
/// The canvas for one graph script: the steps and wires of a function, panned and zoomed, with a bar for the
/// engine's refusals, a box to type a value or a title into, and a minimap. It shows what the source holds
/// and changes nothing itself. Every edit is a call to the source, and the canvas draws what comes back.
/// </summary>
internal sealed class GraphCanvas : Grid
{
    const double EditorHeight = 22;
    const double EditorMinWidth = 60;
    const string ValueHint = "Variant text, for example 2.5, true or \"name\". Enter keeps it and Escape drops it.";

    readonly IGraphSource _source;
    readonly GraphScene _scene = new();
    readonly GraphViewport _viewport = new();
    readonly GraphEdits _edits;
    readonly GraphGestures _gestures;
    readonly GraphSurface _surface;
    readonly GraphClipboard _clipboard;
    readonly PasteChoiceBar _pasteBar;
    readonly ArrangeBar _arrangeBar;
    readonly FindBar _findBar;
    readonly GraphMinimap _minimap;
    readonly PalettePopup _palette = new();
    readonly TextBox _editor = new() { IsVisible = false, MinHeight = 0, Padding = new Thickness(4, 0), FontSize = 11, VerticalContentAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
    readonly TextBlock _notice = new() { Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
    readonly Border _noticeBar;
    readonly TextBlock _empty = new() { Text = "This script has no function yet.", Foreground = EditorBrushes.TabInactive, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, IsVisible = false };
    readonly Button _frame = Parts.IconButton("GraphFrame", "Frame the selected steps. Ctrl or Command and G does the same.");
    readonly Button _fit = Parts.IconButton("ZoomReset", "Show every step");
    readonly Avalonia.Controls.Primitives.ToggleButton _curved = Parts.IconToggle("Curve", "Curved wires. Off draws straight wires.");
    readonly Avalonia.Controls.Primitives.ToggleButton _map = Parts.IconToggle("Grid", "Minimap");
    Action<string>? _commit;
    string _original = "";
    bool _fitted;
    int? _selected;
    int? _revealed;

    /// <param name="source">What the canvas shows and edits.</param>
    /// <param name="clipboard">Where copied steps go. The window's clipboard when none is given.</param>
    public GraphCanvas(IGraphSource source, ITextClipboard? clipboard = null)
    {
        _source = source;
        _edits = new GraphEdits(source, _scene);
        _gestures = new GraphGestures(_scene, _viewport, _edits);
        _surface = new GraphSurface(_scene, _viewport, _gestures, _edits);
        _clipboard = new GraphClipboard(source, _scene, _edits, clipboard ?? new WindowClipboard(this));
        _pasteBar = new PasteChoiceBar(_clipboard);
        _arrangeBar = new ArrangeBar(new GraphArranger(_scene, _edits), Select, _edits);
        _findBar = new FindBar(new GraphSearch(_scene, Reveal, Redraw), () => _surface.Focus());
        _surface.Shortcuts = Shortcuts();
        _minimap = new GraphMinimap(_scene, _viewport, _surface) { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 12, 12) };
        _noticeBar = new Border { Background = new SolidColorBrush(Color.Parse("#c04a3a")), Padding = new Thickness(10, 4), CornerRadius = new CornerRadius(4), IsVisible = false, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(12), Child = _notice };
        Build();
        Wire();
        Reload();
    }

    internal GraphScene Scene => _scene;

    internal GraphViewport Viewport => _viewport;

    internal GraphGestures Gestures => _gestures;

    internal GraphEdits Edits => _edits;

    internal GraphSurface Surface => _surface;

    internal GraphClipboard Clipboard => _clipboard;

    internal PasteChoiceBar PasteBar => _pasteBar;

    internal ArrangeBar ArrangeBar => _arrangeBar;

    internal FindBar FindBar => _findBar;

    internal GraphMinimap Minimap => _minimap;

    internal PalettePopup Palette => _palette;

    internal TextBox Editor => _editor;

    internal Border NoticeBar => _noticeBar;

    internal string NoticeText => _notice.Text ?? "";

    internal Button FrameButton => _frame;

    internal Button FitButton => _fit;

    internal Avalonia.Controls.Primitives.ToggleButton CurvedToggle => _curved;

    internal Avalonia.Controls.Primitives.ToggleButton MapToggle => _map;

    /// <summary>Raised when the one selected step changes, with its id. Null when no step or more than one is selected.</summary>
    public event Action<int?>? SelectionChanged;

    /// <summary>Whether a value or title box is open.</summary>
    internal bool IsEditing => _commit is not null;

    /// <summary>Builds the views again from what the source holds.</summary>
    public void Reload()
    {
        _scene.Load(_source.Function, _source.Warnings);
        _empty.IsVisible = _source.Function is null;
        SyncTools();
        FitOnce();
        Redraw();
        NotifySelection();
    }

    /// <summary>The control with this id, such as <c>paste:reuse</c>. Null when the canvas has none.</summary>
    internal T? Find<T>(string id) where T : Control => this.GetVisualDescendants().OfType<T>().FirstOrDefault(control => (control.Tag as string) == id);

    /// <summary>Selects exactly these steps. An id the function does not have is skipped.</summary>
    internal void Select(IEnumerable<int> ids)
    {
        _scene.Clear();
        foreach (var id in ids.Where(id => _scene.Find(id) is not null))
        {
            _scene.SelectStep(id, true);
        }

        SyncTools();
        Redraw();
        NotifySelection();
    }

    /// <summary>Selects one step and puts it in the middle of the view. An id the function does not have changes nothing.</summary>
    public void Reveal(int stepId)
    {
        if (_scene.Find(stepId) is null)
        {
            return;
        }

        _scene.Clear();
        _scene.SelectStep(stepId, false);
        _revealed = stepId;
        CenterOnRevealed();
        SyncTools();
        Redraw();
        NotifySelection();
    }

    /// <summary>Puts a text box over a place on the canvas. Accepting it passes the text to the commit.</summary>
    internal void BeginEdit(Rect graphRect, string text, Action<string> commit)
    {
        EndEdit(accept: true);
        var top = _viewport.ToScreen(graphRect.TopLeft);
        _editor.Margin = new Thickness(top.X, top.Y, 0, 0);
        _editor.Width = Math.Max(graphRect.Width * _viewport.Zoom, EditorMinWidth);
        _editor.Height = Math.Max(graphRect.Height * _viewport.Zoom, EditorHeight);
        _original = text;
        _editor.Text = text;
        _editor.IsVisible = true;
        _commit = commit;
        _editor.Focus();
        _editor.SelectAll();
    }

    /// <summary>Closes the text box. Accepting passes a changed text on, and the same text is not an edit.</summary>
    internal void EndEdit(bool accept)
    {
        var commit = _commit;
        _commit = null;
        _editor.IsVisible = false;
        var text = _editor.Text ?? "";
        if (accept && commit is not null && text != _original)
        {
            commit(text);
        }
        _surface.Focus();
    }

    void Build()
    {
        var tools = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 8, 12, 0) };
        _curved.IsChecked = true;
        _map.IsChecked = true;
        _frame.IsEnabled = false;
        ToolTip.SetTip(_editor, ValueHint);
        foreach (var control in new Control[] { _frame, _fit, _curved, _map })
        {
            tools.Children.Add(control);
        }
        foreach (var control in new Control[] { _surface, _empty, _minimap, tools, _arrangeBar, _findBar, _pasteBar, _noticeBar, _palette, _editor })
        {
            Children.Add(control);
        }
    }

    void Wire()
    {
        _source.Changed += () =>
        {
            if (!_edits.Busy)
            {
                Reload();
            }
        };
        _edits.Settled += Reload;
        _edits.Refused += ShowNotice;
        _edits.Folded += Select;
        _clipboard.Refused += ShowNotice;
        _clipboard.Pasted += Select;
        _surface.Interacted += OnInteracted;
        _gestures.Pressing += () =>
        {
            _noticeBar.IsVisible = false;
            _palette.Close();
        };
        _gestures.PaletteRequested += OpenPalette;
        _palette.Closed += () => _surface.Focus();
        _surface.SizeChanged += (_, _) => FitOnce();
        _minimap.Moved += _surface.InvalidateVisual;
        _gestures.ValueEditRequested += (step, parameter) => BeginEdit(step.ValueRect(parameter), step.ValueOf(parameter), text => _edits.SetValue(step.Id, parameter, text));
        _gestures.TitleEditRequested += frame => BeginEdit(frame.TitleRect, frame.Frame.Title, text => _edits.RenameFrame(frame, text));
        _editor.KeyDown += OnEditorKey;
        _editor.LostFocus += (_, _) => EndEdit(accept: true);
        _frame.Click += (_, _) => _edits.FrameSelection();
        _fit.Click += (_, _) => FitView();
        _curved.Click += (_, _) => ChooseStyle(_curved.IsChecked == true);
        _map.Click += (_, _) => _minimap.IsVisible = _map.IsChecked == true;
    }

    void ShowNotice(string reason)
    {
        _notice.Text = reason;
        _noticeBar.IsVisible = true;
    }

    // A copy or paste reads the clipboard and so runs as a task, and its failures are reported by the clipboard.
    GraphShortcuts Shortcuts() => new(new Dictionary<(Key Key, bool Shift), Action<Point>>
    {
        [(Key.Z, false)] = _ => _edits.Undo(),
        [(Key.Z, true)] = _ => _edits.Redo(),
        [(Key.Y, false)] = _ => _edits.Redo(),
        [(Key.C, false)] = at => { _ = _clipboard.Copy(); },
        [(Key.X, false)] = at => { _ = _clipboard.Cut(); },
        [(Key.V, false)] = at => { _ = _clipboard.Paste(at); },
        [(Key.D, false)] = _ => _clipboard.Duplicate(),
        [(Key.G, false)] = _ => _edits.FrameSelection(),
        [(Key.F, false)] = _ => _findBar.Open(),
    });

    /// <summary>Opens the palette at a screen point. A step picked there is added at the point and joined to the pin when there is one.</summary>
    internal void OpenPalette(Point screen, PinRef? from)
    {
        var (side, type, sort) = DraggedKind(from);
        _palette.Open(screen, _surface.Bounds.Size, text => _source.FindSteps(text, side, type, sort), entry => _edits.AddStep(entry, _viewport.ToGraph(screen), from));
    }

    // The palette lists steps with a port that fits the pin, and every step when no wire was dragged.
    (GraphDraggedSide Side, int Type, GraphPortSort Sort) DraggedKind(PinRef? from)
    {
        if (from is not { } pin || _scene.Find(pin.Step) is not { } view)
        {
            return (GraphDraggedSide.None, 0, GraphPortSort.Flow);
        }
        var port = (pin.IsInput ? view.Step.Inputs : view.Step.Outputs)[pin.Port];
        return (pin.IsInput ? GraphDraggedSide.Input : GraphDraggedSide.Output, port.Type, port.Sort);
    }

    // What the selection switches on: framing, and the bar of arrange buttons.
    void SyncTools()
    {
        var any = _scene.SelectedSteps.Count > 0;
        _frame.IsEnabled = any;
        _arrangeBar.IsVisible = any;
    }

    void OnInteracted()
    {
        SyncTools();
        _minimap.InvalidateVisual();
        NotifySelection();
    }

    void NotifySelection()
    {
        int? one = _scene.SelectedSteps.Count == 1 ? _scene.SelectedSteps.First() : null;
        if (one != _selected)
        {
            _selected = one;
            SelectionChanged?.Invoke(one);
        }
    }

    void OnEditorKey(object? sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Escape)
        {
            EndEdit(accept: e.Key == Key.Enter);
            e.Handled = true;
        }
    }

    void ChooseStyle(bool curved)
    {
        _scene.Style = curved ? WireStyle.Curved : WireStyle.Straight;
        Redraw();
    }

    // The first time the surface has a size and the graph has steps, the view shows all of it.
    void FitOnce()
    {
        CenterOnRevealed();
        if (!_fitted && _surface.Bounds.Width > 0 && _scene.Steps.Count > 0)
        {
            _fitted = true;
            FitView();
        }
    }

    // A canvas with no size has nothing to center on, so the step waits for the first size and the view is not fitted over it.
    // shortcut: a step inside a collapsed frame is selected but not drawn. Open the frame first when frames are told apart here.
    void CenterOnRevealed()
    {
        if (_revealed is { } id && _scene.Find(id) is { } step && _surface.Bounds.Width > 0)
        {
            _fitted = true;
            _revealed = null;
            _viewport.CenterOn(step.Bounds.Center, _surface.Bounds.Size);
        }
    }

    void FitView()
    {
        _viewport.Fit(_scene.ContentBounds, _surface.Bounds.Size);
        Redraw();
    }

    void Redraw()
    {
        _surface.InvalidateVisual();
        _minimap.InvalidateVisual();
    }
}
