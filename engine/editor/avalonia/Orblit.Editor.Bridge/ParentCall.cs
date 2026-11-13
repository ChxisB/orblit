namespace Orblit.Editor.Bridge;

/// <summary>
/// The palette entry that calls the engine's own version of a function. Only a function that overrides a virtual method has
/// one, and the entries the engine lists for overrides say which functions those are.
/// </summary>
public static class ParentCall
{
    /// <summary>
    /// The entry for the function, or null when it overrides nothing, or the search or the dragged wire do not ask for it.
    /// A wire of data cannot join a step that only calls a parent, so only a flow wire is offered it.
    /// </summary>
    public static GraphStepEntry? EntryFor(string function, IReadOnlyList<GraphStepEntry> overrides, string text, GraphDraggedSide side, GraphPortSort sort)
    {
        var overridden = overrides.FirstOrDefault(entry => entry.Label == function);
        if (overridden is null || (side != GraphDraggedSide.None && sort != GraphPortSort.Flow))
        {
            return null;
        }

        var label = $"Call parent {function}";
        var haystack = $"calls {label}".ToLowerInvariant();
        var words = text.ToLowerInvariant().Replace('_', ' ').Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (!words.All(word => haystack.Contains(word)))
        {
            return null;
        }

        // The arguments come from the override, because a release engine does not list virtual methods to read them from.
        return new GraphStepEntry("call_parent", "Calls", label, new Dictionary<string, string>
        {
            ["class"] = VariantText.Quote(overridden.Group),
            ["method"] = VariantText.Quote(function),
            ["arguments"] = overridden.Parameters.GetValueOrDefault("arguments", "[]"),
        });
    }
}
