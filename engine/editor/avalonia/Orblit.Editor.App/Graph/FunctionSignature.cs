using Orblit.Editor.Bridge;

namespace Orblit.Editor.App.Graph;

/// <summary>
/// What a function declares, read from its Entry step: the step that holds it, its arguments and locals, the type
/// it returns and its description. A function with no Entry step declares nothing, so it has no signature.
/// </summary>
internal sealed record FunctionSignature(int StepId, IReadOnlyList<GraphTyped> Arguments, IReadOnlyList<GraphTyped> Locals, int Returns, string Description)
{
    const string EntryKind = "entry";

    public static FunctionSignature? Of(GraphFunction? function)
    {
        var entry = function?.Steps.FirstOrDefault(step => step.Kind == EntryKind);
        if (entry is null)
        {
            return null;
        }

        var parameters = entry.Parameters;
        return new FunctionSignature(
            entry.Id,
            GraphTyped.ReadList(parameters.GetValueOrDefault("arguments")),
            GraphTyped.ReadList(parameters.GetValueOrDefault("locals")),
            int.TryParse(parameters.GetValueOrDefault("returns"), out var returns) ? returns : 0,
            VariantText.Unquote(parameters.GetValueOrDefault("description")));
    }
}
