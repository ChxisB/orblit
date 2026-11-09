using Orblit.Editor.Bridge;

namespace Orblit.Editor.App;

/// <summary>Cuts an outline down to the entities whose name holds a text, and the entities above them.</summary>
internal static class OutlineFilter
{
    /// <summary>The same tree when the text is blank, a pruned copy otherwise, and null when nothing matches.</summary>
    public static OutlineNode? Apply(OutlineNode? root, string text) =>
        root is null || string.IsNullOrWhiteSpace(text) ? root : Prune(root, text.Trim());

    static OutlineNode? Prune(OutlineNode entity, string text)
    {
        var kept = entity.Children.Select(child => Prune(child, text)).OfType<OutlineNode>().ToArray();
        return kept.Length > 0 || entity.Name.Contains(text, StringComparison.OrdinalIgnoreCase) ? entity with { Children = kept } : null;
    }
}
