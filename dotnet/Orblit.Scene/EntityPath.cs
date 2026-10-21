namespace Orblit.Scene;

/// <summary>
/// Where an entity is, counted through the prefabs it sits inside.
/// </summary>
/// <remarks>
/// An entity that belongs to a prefab instance has an id made of the id it has in each document it
/// is inside, outermost first, joined by <see cref="Separator"/>: <c>street1/lamp3/bulb</c> is the
/// entity <c>bulb</c> in <c>lamp.oprefab</c>, inside the instance <c>lamp3</c> in
/// <c>street.oprefab</c>, inside the instance <c>street1</c> in this scene. The id is the path.
/// Anything that names an entity names one inside an instance the same way it names one outside,
/// and resolving a path is looking the id up.
/// <para>
/// One id per document crossed, and nothing for the entities in between, because the path has to
/// survive what people do inside a document. Only crossing into a different document changes a
/// path, and that is a different entity.
/// </para>
/// <para>
/// The root of an instance takes the instance's own id, so there is exactly one name for it:
/// <c>street1</c>, never <c>street1/street</c>.
/// </para>
/// </remarks>
public static class EntityPath
{
    /// <summary>
    /// Reserved in ids for this. An entity with a plain id belongs to the document it is written
    /// in. One with a separator in its id belongs to an instance.
    /// </summary>
    public const string Separator = "/";

    /// <summary>The id of <paramref name="local"/> inside <paramref name="instance"/>.</summary>
    public static string Join(string instance, string local) => $"{instance}{Separator}{local}";

    /// <summary>Whether <paramref name="id"/> names something inside an instance.</summary>
    public static bool IsPart(string id) => id.Contains(Separator, StringComparison.Ordinal);

    /// <summary>Whether <paramref name="id"/> is <paramref name="instance"/> or anything inside it.</summary>
    public static bool Within(string instance, string id) =>
        id == instance || id.StartsWith($"{instance}{Separator}", StringComparison.Ordinal);

    /// <summary>
    /// The instance, in the outermost document, that <paramref name="id"/> belongs to, or null when
    /// it belongs to that document itself.
    /// </summary>
    public static string? InstanceOf(string id)
    {
        var at = id.IndexOf(Separator, StringComparison.Ordinal);
        return at < 0 ? null : id[..at];
    }

    /// <summary>
    /// <paramref name="id"/> as the prefab of <paramref name="instance"/> knows it, where
    /// <paramref name="root"/> is the id the prefab gives its own root. Null when
    /// <paramref name="id"/> is not inside <paramref name="instance"/> at all.
    /// </summary>
    public static string? LocalTo(string instance, string id, string root)
    {
        if (id == instance)
        {
            return root;
        }

        var prefix = $"{instance}{Separator}";
        return id.StartsWith(prefix, StringComparison.Ordinal) ? id[prefix.Length..] : null;
    }

    /// <summary>
    /// <paramref name="local"/>, an id in the prefab of <paramref name="instance"/>, as the document
    /// holding the instance knows it. The inverse of <see cref="LocalTo"/>.
    /// </summary>
    public static string Inside(string instance, string local, string root) =>
        local == root ? instance : Join(instance, local);

    /// <summary>The documents <paramref name="id"/> crosses, outermost first.</summary>
    public static IReadOnlyList<string> Segments(string id) => id.Split(Separator);

    /// <summary>
    /// Every id <paramref name="id"/> is inside, nearest first and not including itself.
    /// <c>street1/lamp3/bulb</c> is inside <c>street1/lamp3</c> and then <c>street1</c>. What a link
    /// to a part that has gone away falls back to, so something hung off a part a prefab no longer
    /// has lands on the nearest thing that is still there instead of at the top of the scene.
    /// </summary>
    public static IReadOnlyList<string> Enclosing(string id)
    {
        var found = new List<string>();
        var at = id.LastIndexOf(Separator, StringComparison.Ordinal);
        while (at > 0)
        {
            found.Add(id[..at]);
            at = id.LastIndexOf(Separator, at - 1, StringComparison.Ordinal);
        }

        return found;
    }
}
