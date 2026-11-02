using Orblit.Json;
using Read = Orblit.Scene.Values;

namespace Orblit.Scene;

/// <summary>A prefab read back off disk, with anything that could not be read.</summary>
public sealed class PrefabLoad(PrefabDocument prefab, IReadOnlyList<string>? problems = null)
{
    public PrefabDocument Prefab { get; } = prefab;

    public IReadOnlyList<string> Problems { get; } = problems ?? [];
}

/// <summary>
/// Something built once and used many times: an entity and everything under it, saved as a file of
/// its own.
/// </summary>
/// <remarks>
/// The point of one is that the thing exists in the project rather than in a scene. A lamp post made
/// once can stand down a street a hundred times, and when the lamp changes, the street changes,
/// because each of the hundred is a link to this file and a note of what is different about that
/// one, never a copy of it.
/// <para>
/// Its entities are a scene's entities, written the way a scene writes them, so a prefab is
/// readable, diffable and mergeable, and a scene and a prefab cannot drift into two encodings of one
/// thing. One may hold instances of others, folded exactly as a scene holds them.
/// </para>
/// </remarks>
public sealed class PrefabDocument(string name, string root, SceneDocument document)
{
    /// <summary>The extension a prefab file carries.</summary>
    public const string Extension = ".oprefab";

    public const string Marker = "orblit.prefab";

    /// <summary>
    /// The shape of the file around the entities. The entities have their own version,
    /// <see cref="SceneDocument.FormatVersion"/>, written beside it.
    /// </summary>
    public const int FormatVersion = 1;

    /// <summary>What the thing is called. The file's name is what identifies it; this is what somebody reads.</summary>
    public string Name { get; } = name;

    /// <summary>The id, inside <see cref="Document"/>, of the entity everything else hangs off.</summary>
    public string Root { get; } = root;

    /// <summary>
    /// The root and everything under it. Ids are the prefab's own: an instance puts its own id in
    /// front of them, so two instances never argue over one.
    /// </summary>
    public SceneDocument Document { get; } = document;

    /// <summary>The root entity, which a prefab that has lost it cannot give.</summary>
    public SceneEntity RootEntity =>
        Document[Root] ?? throw new PrefabException($"The prefab \"{Name}\" has lost its root, \"{Root}\".");

    public string Encode() => $"{JsonText.Encode(ToJson(), "  ")}\n";

    /// <summary>
    /// A prefab out of a file's text, with whatever could not be read.
    /// <para>
    /// Throws <see cref="SceneFormatException"/> when the text is not a prefab at all, or is one from
    /// a newer Orblit, for the reasons <see cref="SceneDocument.Decode"/> does.
    /// </para>
    /// </summary>
    public static PrefabLoad Decode(string text)
    {
        var parsed = Parsed(text);

        var version = Read.At(parsed, "formatVersion");
        var sceneVersion = Read.At(parsed, "sceneFormatVersion");
        if (version is long and > FormatVersion || sceneVersion is long and > SceneDocument.FormatVersion)
        {
            throw new SceneFormatException("This prefab was written by a newer Orblit.");
        }

        if (Read.At(parsed, "objects") is not JsonList raw)
        {
            throw new SceneFormatException("This prefab has nothing in it.");
        }

        var problems = new List<string>();
        var entities = EntitiesOf(raw, sceneVersion is long known ? known : SceneDocument.FormatVersion, problems);
        if (entities.Count == 0)
        {
            throw new SceneFormatException("This prefab has nothing in it.");
        }

        var tree = SceneTree.Rooted(entities, problems, folded: true);
        var name = Read.Text(parsed, "name") ?? "Prefab";
        return new PrefabLoad(
            new PrefabDocument(name, RootOf(parsed, tree), new SceneDocument(name, entities: tree)),
            problems);
    }

    private OrderedDictionary<string, object?> ToJson() => new()
    {
        ["kind"] = Marker,
        ["formatVersion"] = (long)FormatVersion,
        ["sceneFormatVersion"] = (long)SceneDocument.PlainVersion,
        ["name"] = Name,
        ["root"] = Root,
        ["objects"] = Document.Entities.Select(entity => (object?)entity.ToJson()).ToList(),
    };

    private static JsonMap Parsed(string text)
    {
        object? parsed;
        try
        {
            parsed = JsonText.Decode(text);
        }
        catch (FormatException error)
        {
            throw new SceneFormatException($"This is not a prefab file: {error.Message}");
        }

        return parsed is JsonMap map && Read.Text(map, "kind") == Marker
            ? map
            : throw new SceneFormatException("This is not a prefab file.");
    }

    // An entry that is not an object, has no usable kind, or repeats an id is skipped without a
    // note, the same way a hand-edited prefab loses a stray line and keeps the rest.
    private static List<SceneEntity> EntitiesOf(JsonList raw, long version, List<string> problems)
    {
        var entities = new List<SceneEntity>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in raw.OfType<JsonMap>())
        {
            var migrated = SceneMigrations.Entity(entry, version, problems);
            if (migrated is not null && SceneEntity.FromJson(migrated) is { } entity && seen.Add(entity.Id))
            {
                entities.Add(entity);
            }
        }

        return entities;
    }

    private static string RootOf(JsonMap parsed, IReadOnlyList<SceneEntity> tree)
    {
        var named = Read.Text(parsed, "root");
        if (named is not null && tree.Any(entity => entity.Id == named))
        {
            return named;
        }

        // A file whose root is missing still has one: the first thing with nothing above it. Better
        // than refusing something recoverable.
        return (tree.FirstOrDefault(static entity => entity.Parent is null) ?? tree[0]).Id;
    }
}
