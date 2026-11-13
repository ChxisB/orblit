using Orblit.Editor.App.Shell;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App.Graph;

/// <summary>One pin of a step as the inspector drew it. Port counts from 0 on its own side.</summary>
internal readonly record struct Pin(GraphStep Step, bool IsInput, int Index)
{
    public GraphPort Port => (IsInput ? Step.Inputs : Step.Outputs)[Index];
}

/// <summary>
/// The edits a pin offers: make a variable or a local of it, put its zero back, or split or combine a struct.
/// Each runs several engine calls in order and stops at the first one the engine refuses, returning that refusal.
/// The calls of one edit are one entry in the history.
/// </summary>
internal sealed class PinEdits
{
    const double Gap = 240;
    const double RowHeight = 40;
    const int InvalidParameter = 31;

    // Indexed by the side of the pin (input first) and then by variable or local.
    static readonly string[] s_promoted = ["get", "local", "set", "set_local"];

    readonly IGraphFile _file;

    public PinEdits(IGraphFile file) => _file = file;

    public GraphEditResult Reset(Pin pin) =>
        VariantText.ZeroOf(pin.Port.Type) is { } zero
            ? _file.SetStepParameter(pin.Step.Id, pin.Port.Parameter, zero)
            : Refuse("That type has no value to put back.");

    /// <summary>Declares a variable or a local of the pin's type, and a step that reads it or writes it, wired to the pin.</summary>
    public GraphEditResult Promote(Pin pin, bool toLocal)
    {
        var id = _file.NextStepId();
        var kind = s_promoted[(pin.IsInput ? 0 : 2) + (toLocal ? 1 : 0)];
        var signature = FunctionSignature.Of(_file.Function);
        var taken = toLocal ? LocalNames(signature) : _file.Document.Variables.Select(variable => variable.Name);
        var name = PanelFields.Unique(taken, pin.Port.Name);
        return Chain(
            () => toLocal ? AddLocal(signature, name, pin.Port.Type) : _file.AddVariable(name, pin.Port.Type),
            () => _file.AddStep(id, kind),
            () => _file.SetStepParameter(id, "name", VariantText.Quote(name)),
            () => Place(id, pin),
            () => Wire(id, pin));
    }

    /// <summary>Feeds an output into a Decompose step that has one output for each part of its type.</summary>
    public GraphEditResult Split(Pin pin) => Reshape(pin, "decompose");

    /// <summary>Feeds an input from a Compose step that takes one value for each part of its type.</summary>
    public GraphEditResult Combine(Pin pin) => Reshape(pin, "compose");

    GraphEditResult Reshape(Pin pin, string kind)
    {
        var type = pin.Port.Type;
        var parts = VariantTypes.PartsOf(type);
        var id = _file.NextStepId();
        return parts == 0
            ? Refuse("That type has no parts.")
            : Chain(
                () => _file.AddStep(id, kind),
                () => _file.SetStepParameter(id, "type", VariantText.Quote(VariantTypes.NameOf(type))),
                () => _file.SetStepPins(id, parts),
                () => Place(id, pin),
                () => Wire(id, pin));
    }

    static IEnumerable<string> LocalNames(FunctionSignature? signature) =>
        signature is null ? [] : signature.Locals.Concat(signature.Arguments).Select(typed => typed.Name);

    // A local lives on the Entry step of the function on show, in the list of locals it already has.
    GraphEditResult AddLocal(FunctionSignature? signature, string name, int type) =>
        signature is null
            ? Refuse("This function has no Entry step to hold a local.")
            : _file.SetStepParameter(signature.StepId, "locals", GraphTyped.WriteList([.. signature.Locals, new GraphTyped(name, type)]));

    // The new step sits beside the pin: before a step it feeds, after a step it reads.
    GraphEditResult Place(int id, Pin pin) =>
        _file.MoveStep(id, pin.Step.X + (pin.IsInput ? -Gap : Gap), pin.Step.Y + RowHeight * pin.Index);

    // The new step joins the pin by the first value port it has on the other side.
    GraphEditResult Wire(int id, Pin pin)
    {
        var created = _file.Function?.Steps.FirstOrDefault(step => step.Id == id);
        var ports = (pin.IsInput ? created?.Outputs : created?.Inputs) ?? [];
        var at = ports.ToList().FindIndex(port => port.Sort == GraphPortSort.Data);
        return _file.Connect(pin.IsInput ? new GraphWire(id, at, pin.Step.Id, pin.Index) : new GraphWire(pin.Step.Id, pin.Index, id, at));
    }

    GraphEditResult Chain(params Func<GraphEditResult>[] edits)
    {
        var result = new GraphEditResult(0, "");
        _file.History.Group(() =>
        {
            foreach (var edit in edits)
            {
                result = edit();
                if (!result.Ok)
                {
                    break;
                }
            }
        });
        return result;
    }

    static GraphEditResult Refuse(string reason) => new(InvalidParameter, reason);
}
