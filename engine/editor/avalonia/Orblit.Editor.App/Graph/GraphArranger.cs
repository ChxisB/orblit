using Orblit.Editor.Bridge;

namespace Orblit.Editor.App.Graph;

/// <summary>
/// Lines up, spreads, stacks and straightens the selected steps, and finds the steps a flow reaches. The
/// geometry is in <see cref="StepLayout"/>. This class reads the selection and hands the moves to the edits.
/// </summary>
internal sealed class GraphArranger(GraphScene scene, GraphEdits edits)
{
    public void Align(Edge edge) => edits.MoveEach(StepLayout.Align(Selected(), edge));

    public void Distribute(Axis axis) => edits.MoveEach(StepLayout.Distribute(Selected(), axis));

    public void Stack(Axis axis) => edits.MoveEach(StepLayout.Stack(Selected(), axis));

    public void Straighten() => edits.MoveEach(StepLayout.Straighten(Links()));

    /// <summary>The selected steps and every step joined to them by flow wires, in either direction.</summary>
    public IReadOnlyCollection<int> Flow() =>
        StepLayout.Reach(scene.SelectedSteps, scene.Wires.Where(IsFlow).Select(wire => (wire.FromStep, wire.ToStep)));

    List<Placed> Selected() => [.. scene.SelectedSteps.Select(scene.Find).OfType<StepView>().Select(view => new Placed(view.Id, view.Bounds))];

    // Only wires between selected steps move anything, so a step outside the selection is never pulled along.
    List<Link> Links() =>
    [
        .. scene.Wires
            .Where(wire => scene.SelectedSteps.Contains(wire.FromStep) && scene.SelectedSteps.Contains(wire.ToStep))
            .Select(wire => (Wire: wire, From: scene.Find(wire.FromStep), To: scene.Find(wire.ToStep)))
            .Where(pair => pair.From is not null && pair.To is not null)
            .OrderBy(pair => pair.From!.Origin.X)
            .Select(pair => new Link(pair.Wire.FromStep, pair.From!.PinCenter(false, pair.Wire.FromPort).Y, pair.Wire.ToStep, pair.To!.PinCenter(true, pair.Wire.ToPort).Y)),
    ];

    bool IsFlow(GraphWire wire) =>
        scene.Find(wire.FromStep) is { } from && wire.FromPort < from.Step.Outputs.Count && from.Step.Outputs[wire.FromPort].Sort == GraphPortSort.Flow;
}
