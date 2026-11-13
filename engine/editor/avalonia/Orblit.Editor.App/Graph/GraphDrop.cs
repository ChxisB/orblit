using Avalonia.Input;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App.Graph;

/// <summary>What a panel can drag onto the canvas. Each one becomes a step that names it.</summary>
internal enum GraphDropKind
{
    File,
    Entity,
    Variable,
    Local,
    Function,
    Signal,
}

/// <summary>
/// Something dropped on the canvas: a kind and the name or path it carries. It crosses a drag as one line of
/// text, so a drag source needs nothing from this assembly but the format and <see cref="ToText"/>.
/// </summary>
internal sealed record GraphDrop(GraphDropKind Kind, string Value)
{
    /// <summary>The format a drag source puts the text under.</summary>
    public static readonly DataFormat<string> Format = DataFormat.CreateStringApplicationFormat("orblit.graph.drop");

    // The step each kind makes and the parameter that holds the name. A path goes in the parameter of the
    // input it fills, so the step shows it as the value of that input.
    static readonly Dictionary<GraphDropKind, (string Step, string Parameter)> s_steps = new()
    {
        [GraphDropKind.File] = ("preload", "path"),
        [GraphDropKind.Entity] = ("entity_at_path", "path"),
        [GraphDropKind.Variable] = ("get", "name"),
        [GraphDropKind.Local] = ("local", "name"),
        [GraphDropKind.Function] = ("call_function", "function"),
        [GraphDropKind.Signal] = ("emit", "signal"),
    };

    /// <summary>The palette entry this drop stands for, so a drop and a pick add a step the same way.</summary>
    public GraphStepEntry ToEntry()
    {
        var (step, parameter) = s_steps[Kind];
        // A string parameter is Variant text, so the value is quoted.
        return new GraphStepEntry(step, "", Value, new Dictionary<string, string> { [parameter] = Quote(Value) });
    }

    public string ToText() => $"{Kind}:{Value}";

    /// <summary>What a drag source hands to the drag: the text, under <see cref="Format"/>.</summary>
    public DataTransfer ToTransfer()
    {
        var transfer = new DataTransfer();
        transfer.Add(DataTransferItem.Create(Format, ToText()));
        return transfer;
    }

    /// <summary>Reads what <see cref="ToText"/> wrote. Text that is not from a drag source gives false.</summary>
    public static bool TryParse(string? text, out GraphDrop drop)
    {
        drop = new GraphDrop(GraphDropKind.File, "");
        var colon = text?.IndexOf(':') ?? -1;
        if (colon <= 0 || !Enum.TryParse(text![..colon], out GraphDropKind kind) || !Enum.IsDefined(kind) || colon == text.Length - 1)
        {
            return false;
        }
        drop = new GraphDrop(kind, text[(colon + 1)..]);
        return true;
    }

    static string Quote(string text) => "\"" + text.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
}
