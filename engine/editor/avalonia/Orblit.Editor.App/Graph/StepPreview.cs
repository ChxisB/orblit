using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Orblit.Editor.App.Theme;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App.Graph;

/// <summary>A palette entry drawn as the canvas draws the step it adds, with its pins and starting values.</summary>
internal sealed class StepPreview : Control
{
    // Room for the pins, which sit across the step's left and right edges.
    internal const double Pad = Gaps.Item;

    static readonly GraphGrowth s_fixed = new(GraphGrowthSide.None, 0, 0, 0);
    static readonly HashSet<PinRef> s_unwired = [];

    StepView? _view;

    /// <summary>The step on show, or null when no entry is chosen.</summary>
    public GraphStep? Step => _view?.Step;

    public void Show(GraphStepEntry? entry)
    {
        _view = entry is null ? null : new StepView(new GraphStep(0, entry.Kind, true, entry.Group, Pad, Pad, entry.Parameters, "", entry.Inputs, entry.Outputs, s_fixed), s_unwired);
        InvalidateMeasure();
        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize) => _view is null ? default : new Size(StepView.Width + (2 * Pad), _view.Height + (2 * Pad));

    public override void Render(DrawingContext context) => _view?.Draw(context, StepMarks.None);
}
