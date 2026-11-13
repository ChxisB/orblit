namespace Orblit.Editor.App.Graph;

/// <summary>Finds steps by the text they show, and walks a list of steps in reading order, wrapping at the ends.</summary>
internal static class StepSearch
{
    /// <summary>
    /// The ids of the steps that show the text in a title, a kind, a pin name or a parameter value, in reading
    /// order. Case is ignored. Empty text finds nothing.
    /// </summary>
    public static List<int> Find(IEnumerable<StepView> steps, string text)
    {
        var needle = text.Trim();
        return needle.Length == 0 ? [] : InReadingOrder(steps.Where(step => Shows(step, needle)));
    }

    /// <summary>Left to right, then top to bottom, then by id.</summary>
    public static List<int> InReadingOrder(IEnumerable<StepView> steps) =>
        [.. steps.OrderBy(step => step.Step.X).ThenBy(step => step.Step.Y).ThenBy(step => step.Id).Select(step => step.Id)];

    /// <summary>
    /// The id after the current one, or before it going back, and the first or last id when nothing is current or
    /// the current one is not in the list. Null for an empty list.
    /// </summary>
    public static int? Cycle(List<int> ids, int? current, bool forward)
    {
        if (ids.Count == 0)
        {
            return null;
        }

        var at = current is { } id ? ids.IndexOf(id) : -1;
        if (at < 0)
        {
            return forward ? ids[0] : ids[^1];
        }

        return ids[(at + (forward ? 1 : ids.Count - 1)) % ids.Count];
    }

    static bool Shows(StepView view, string needle)
    {
        var step = view.Step;
        return new[] { view.Title, step.Kind }
            .Concat(step.Inputs.Select(port => port.Name))
            .Concat(step.Outputs.Select(port => port.Name))
            .Concat(step.Parameters.Values)
            .Any(text => text.Contains(needle, StringComparison.OrdinalIgnoreCase));
    }
}
