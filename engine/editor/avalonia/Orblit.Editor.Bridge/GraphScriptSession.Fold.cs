using System.Diagnostics.CodeAnalysis;

namespace Orblit.Editor.Bridge;

/// <summary>What a collapse or an expand did. Steps holds the ids to select afterwards: the new call, or the steps that came back.</summary>
public sealed record GraphFold(GraphEditResult Result, IReadOnlyList<int> Steps);

// Collapse and expand are made of the calls the engine already has, so the rules are here and not in the engine.
// shortcut: they take only the plain case, flow across the edge and no value, and refuse the rest with a reason. Widen them when someone needs a value across the edge.
public sealed partial class GraphScriptSession
{
    const string CallKind = "call_function";
    const string EntryKind = "entry";
    const int CallRun = 0;
    const int CallThen = 0;
    const int CallPortCount = 2;
    const double EntryGap = 260;
    const double SamePlace = 0.01;

    // A step of these kinds starts or ends a function, or reads its locals, so it cannot move to another function.
    static readonly HashSet<string> s_stays = [EntryKind, "event", "return", "local", "set_local"];

    sealed record CollapsePlan(IReadOnlyList<GraphStep> Steps, IReadOnlyList<GraphWire> Entering, GraphWire? Leaving, GraphEditResult? Refusal);

    sealed record ExpandPlan(GraphStep Call, GraphFunction Callee, IReadOnlyList<GraphStep> Body, GraphWire Head, IReadOnlyList<GraphWire> Entering, GraphWire? Leaving);

    /// <summary>
    /// Moves the steps into a new function with the given name and puts one Call step in their place. The steps
    /// need one way in by flow, at most one way out, and no value across their edge. A refused collapse changes nothing.
    /// </summary>
    public GraphFold Collapse(string function, IReadOnlyList<int> ids, string name)
    {
        if (FindFunction(function) is not { } source)
        {
            return Declined(GraphEditResult.From(33));
        }

        var plan = PlanCollapse(source, ids);
        return plan.Refusal is { } refusal ? Declined(refusal) : Atomic(() => ApplyCollapse(source, plan, name));
    }

    /// <summary>
    /// Puts the steps of the function a Call step names where the call is, and takes the call away. The function
    /// stays, since other calls may use it. The call needs no target, no argument and no result in use. A refused expand changes nothing.
    /// </summary>
    public GraphFold Expand(string function, int call)
    {
        if (FindFunction(function) is not { } source)
        {
            return Declined(GraphEditResult.From(33));
        }

        return TryPlanExpand(source, call, out var plan, out var refusal) ? Atomic(() => ApplyExpand(source, plan)) : Declined(refusal);
    }

    static GraphFold Declined(GraphEditResult why) => new(why, []);

    static GraphEditResult Refusal(string reason) => new(31, reason);

    GraphFunction? FindFunction(string name) => Document.Functions.FirstOrDefault(function => function.Name == name);

    // The calls that follow a plan can fail half way. The snapshot puts the script back, so a refusal leaves it as it was.
    GraphFold Atomic(Func<GraphFold> edit)
    {
        var before = GetSnapshot();
        var done = edit();
        if (!done.Result.Ok)
        {
            Restore(before);
        }

        return done;
    }

    static bool IsFlowOut(GraphFunction function, GraphWire wire) =>
        function.Steps.First(step => step.Id == wire.FromStep).Outputs[wire.FromPort].Sort == GraphPortSort.Flow;

    static int FlowOut(GraphStep step) => step.Outputs.ToList().FindIndex(port => port.Sort == GraphPortSort.Flow);

    // The flow outputs of the steps that nothing is wired to.
    static List<(int Step, int Port)> LooseEnds(GraphFunction function, IEnumerable<GraphStep> steps) =>
        [.. steps.SelectMany(step => step.Outputs.Select((port, at) => (Step: step.Id, Port: at, port.Sort)))
            .Where(end => end.Sort == GraphPortSort.Flow && !function.Wires.Any(wire => wire.FromStep == end.Step && wire.FromPort == end.Port))
            .Select(end => (end.Step, end.Port))];

    static CollapsePlan PlanCollapse(GraphFunction function, IReadOnlyList<int> ids)
    {
        var chosen = function.Steps.Where(step => ids.Contains(step.Id)).ToList();
        if (chosen.Count == 0)
        {
            return Stopped("Select the steps to collapse.");
        }
        if (chosen.FirstOrDefault(step => s_stays.Contains(step.Kind)) is { } stays)
        {
            return Stopped($"A {stays.Kind} step stays in its function.");
        }

        var inside = chosen.Select(step => step.Id).ToHashSet();
        var crossing = function.Wires.Where(wire => inside.Contains(wire.FromStep) != inside.Contains(wire.ToStep)).ToList();
        if (crossing.Any(wire => !IsFlowOut(function, wire)))
        {
            return Stopped("A value crosses the edge of the selection. Only flow can.");
        }

        var entering = crossing.Where(wire => inside.Contains(wire.ToStep)).ToList();
        var leaving = crossing.Where(wire => inside.Contains(wire.FromStep)).ToList();
        if (entering.Select(wire => (wire.ToStep, wire.ToPort)).Distinct().Count() != 1)
        {
            return Stopped("The selection needs exactly one step that flow enters.");
        }
        if (leaving.Count > 1 || (leaving.Count == 1 && LooseEnds(function, chosen).Count > 0))
        {
            return Stopped("The selection needs one way out. Flow may leave it by one wire, and nowhere else.");
        }

        return new CollapsePlan(chosen, entering, leaving.FirstOrDefault(), null);
    }

    static CollapsePlan Stopped(string reason) => new([], [], null, Refusal(reason));

    GraphFold ApplyCollapse(GraphFunction source, CollapsePlan plan, string name)
    {
        var minX = plan.Steps.Min(step => step.X);
        var minY = plan.Steps.Min(step => step.Y);
        var head = plan.Steps.First(step => step.Id == plan.Entering[0].ToStep);
        var made = MakeFunctionOf(source, plan, name, minX, minY);
        if (!made.Ok)
        {
            return Declined(made);
        }

        var call = -1;
        var edits = new List<Func<GraphEditResult>>();
        edits.AddRange(plan.Steps.Select(step => (Func<GraphEditResult>)(() => RemoveStep(source.Name, step.Id))));
        edits.Add(() =>
        {
            call = GetNextStepId(source.Name);
            return AddStep(source.Name, call, CallKind);
        });
        edits.Add(() => MoveStep(source.Name, call, head.X, head.Y));
        edits.Add(() => SetStepParameter(source.Name, call, "function", VariantText.Quote(name)));
        // The steps may wait, so the call waits for them and what follows keeps its place in the order.
        edits.Add(() => SetStepParameter(source.Name, call, "wait", "true"));
        edits.AddRange(plan.Entering.Select(wire => (Func<GraphEditResult>)(() => Connect(source.Name, new GraphWire(wire.FromStep, wire.FromPort, call, CallRun)))));
        if (plan.Leaving is { } leaving)
        {
            edits.Add(() => Connect(source.Name, new GraphWire(call, CallThen, leaving.ToStep, leaving.ToPort)));
        }

        var result = Chain(edits);
        return new GraphFold(result, result.Ok ? [call] : []);
    }

    // The new function holds an entry step, then the copied steps in the places they had, with the entry wired to the one that flow entered.
    GraphEditResult MakeFunctionOf(GraphFunction source, CollapsePlan plan, string name, double minX, double minY)
    {
        var first = Chain([() => AddFunction(name), () => AddStep(name, 1, EntryKind), () => MoveStep(name, 1, minX - EntryGap, minY)]);
        if (!first.Ok)
        {
            return first;
        }

        var pasted = PasteSteps(name, GetPayload(source.Name, [.. plan.Steps.Select(step => step.Id)]), minX, minY, GraphPasteChoice.Reuse);
        if (!pasted.Result.Ok)
        {
            return pasted.Result;
        }

        var mapped = MapPasted(name, pasted.Steps, plan.Steps, 0, 0);
        var entry = FindFunction(name)!.Steps.First(step => step.Id == 1);
        var enters = plan.Entering[0];
        return mapped is null
            ? Refusal("The engine pasted steps that do not match the ones copied.")
            : Connect(name, new GraphWire(1, FlowOut(entry), mapped[enters.ToStep], enters.ToPort));
    }

    static GraphEditResult Chain(IEnumerable<Func<GraphEditResult>> edits)
    {
        var result = GraphEditResult.From(0);
        foreach (var edit in edits)
        {
            result = edit();
            if (!result.Ok)
            {
                break;
            }
        }

        return result;
    }

    // The engine says which ids a paste made, but not which copy is which original. A copy is the new step of the same kind
    // at the original's place moved by the shift, and null says some original had none or more than one.
    Dictionary<int, int>? MapPasted(string function, IReadOnlyList<int> made, IReadOnlyList<GraphStep> originals, double shiftX, double shiftY)
    {
        var copies = FindFunction(function)!.Steps.Where(step => made.Contains(step.Id)).ToList();
        var mapped = new Dictionary<int, int>();
        foreach (var original in originals)
        {
            var match = copies.Where(copy => copy.Kind == original.Kind
                && Math.Abs(copy.X - (original.X + shiftX)) < SamePlace
                && Math.Abs(copy.Y - (original.Y + shiftY)) < SamePlace).ToList();
            if (match.Count != 1)
            {
                return null;
            }

            mapped[original.Id] = match[0].Id;
        }

        return mapped;
    }

    bool TryPlanExpand(GraphFunction function, int callId, [NotNullWhen(true)] out ExpandPlan? plan, out GraphEditResult refusal)
    {
        plan = null;
        refusal = ExpandRefusal(function, callId, out var call, out var callee);
        if (!refusal.Ok)
        {
            return false;
        }

        var entry = callee!.Steps.FirstOrDefault(step => step.Kind == EntryKind);
        var body = callee.Steps.Where(step => step.Kind != EntryKind).ToList();
        var fromEntry = callee.Wires.Where(wire => wire.FromStep == entry?.Id).ToList();
        refusal = entry is null || body.Count == 0 ? Refusal("The function has no steps to open out.")
            : body.FirstOrDefault(step => s_stays.Contains(step.Kind)) is { } stays ? Refusal($"The function has a {stays.Kind} step, which stays in it.")
            : fromEntry.Count != 1 || fromEntry[0].FromPort != FlowOut(entry) ? Refusal("The function must start with one flow wire from its entry and read no argument.")
            : GraphEditResult.From(0);
        if (!refusal.Ok)
        {
            return false;
        }

        var entering = function.Wires.Where(wire => wire.ToStep == call!.Id).ToList();
        plan = new ExpandPlan(call!, callee, body, fromEntry[0], entering, function.Wires.FirstOrDefault(wire => wire.FromStep == call!.Id));
        return true;
    }

    // The reasons a call cannot be opened out whatever the function it names holds. A taken result is Ok.
    GraphEditResult ExpandRefusal(GraphFunction function, int callId, out GraphStep? call, out GraphFunction? callee)
    {
        callee = null;
        call = function.Steps.FirstOrDefault(step => step.Id == callId && step.Kind == CallKind);
        if (call is null)
        {
            return Refusal("Select one Call step to expand.");
        }

        var name = VariantText.Unquote(call.Parameters.GetValueOrDefault("function"));
        if (name == function.Name)
        {
            return Refusal("The function calls itself, so it cannot be opened out.");
        }

        callee = FindFunction(name);
        if (callee is null)
        {
            return Refusal($"This script has no function named {name}.");
        }

        var used = call.Inputs.Count > CallPortCount
            || function.Wires.Any(wire => (wire.ToStep == callId && wire.ToPort != CallRun) || (wire.FromStep == callId && wire.FromPort != CallThen));
        return used ? Refusal("The call has a target, an argument or a result in use.") : GraphEditResult.From(0);
    }

    GraphFold ApplyExpand(GraphFunction source, ExpandPlan plan)
    {
        var minX = plan.Body.Min(step => step.X);
        var minY = plan.Body.Min(step => step.Y);
        var pasted = PasteSteps(source.Name, GetPayload(plan.Callee.Name, [.. plan.Body.Select(step => step.Id)]), plan.Call.X, plan.Call.Y, GraphPasteChoice.Reuse);
        if (!pasted.Result.Ok)
        {
            return Declined(pasted.Result);
        }

        var mapped = MapPasted(source.Name, pasted.Steps, plan.Body, plan.Call.X - minX, plan.Call.Y - minY);
        if (mapped is null)
        {
            return Declined(Refusal("The engine pasted steps that do not match the ones copied."));
        }

        var edits = new List<Func<GraphEditResult>> { () => RemoveStep(source.Name, plan.Call.Id) };
        edits.AddRange(plan.Entering.Select(wire => (Func<GraphEditResult>)(() => Connect(source.Name, new GraphWire(wire.FromStep, wire.FromPort, mapped[plan.Head.ToStep], plan.Head.ToPort)))));
        if (plan.Leaving is { } leaving)
        {
            // A function that ends where flow stops hands control back, so every loose end carries on at what followed the call.
            edits.AddRange(LooseEnds(plan.Callee, plan.Body).Select(end => (Func<GraphEditResult>)(() => Connect(source.Name, new GraphWire(mapped[end.Step], end.Port, leaving.ToStep, leaving.ToPort)))));
        }

        var result = Chain(edits);
        return new GraphFold(result, result.Ok ? [.. mapped.Values] : []);
    }
}
