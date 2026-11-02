using System.Collections.ObjectModel;
using Orblit.Json;
using Orblit.Light;

namespace Orblit.Scene;

/// <summary>
/// Something that made a file unreadable as a whole.
/// </summary>
/// <remarks>
/// Thrown, unlike everything else in here, because there is no partial answer to give: a file that
/// is not JSON, or that is JSON but is not a scene, has nothing in it to salvage.
/// </remarks>
public sealed class SceneFormatException(string message) : Exception(message);

/// <summary>
/// A scene read back off disk, with anything that could not be read.
/// </summary>
/// <remarks>
/// Problems are returned rather than thrown. A scene with one broken object is still worth opening,
/// losing the other ninety-nine because of it is the worse outcome, and whoever opened it can be
/// told what was dropped.
/// </remarks>
public sealed class SceneLoad(SceneDocument document, IReadOnlyList<string>? problems = null)
{
    public SceneDocument Document { get; } = document;

    public IReadOnlyList<string> Problems { get; } = problems ?? [];

    public bool HasProblems => Problems.Count > 0;
}

/// <summary>
/// The scene's own settings: the things that belong to it rather than to anything in it.
/// </summary>
// F2: six settings of one scene, called by name.
public sealed class SceneSettings(
    Tint? sky = null,
    double ambient = 28000,
    double timeOfDay = 10,
    bool dayCycle = false,
    double hoursPerSecond = 0.5,
    IReadOnlyList<string>? layerNames = null)
{
    /// <summary>How many layers there are, one bit each in a body's <c>layers</c> and <c>cares</c>.</summary>
    public const int LayerCount = 32;

    private static readonly Tint NewScene = Tint.Hex(0x1A2029);

    /// <summary>The sky, and by the same setting the light it casts.</summary>
    public Tint Sky { get; } = sky ?? NewScene;

    /// <summary>How much light the sky casts, in lux.</summary>
    public double Ambient { get; } = ambient;

    /// <summary>
    /// The hour the scene is set at, from zero to twenty-four.
    /// <para>
    /// What was authored, not wherever a running cycle has carried it to. A clock left going should
    /// not rewrite somebody's scene every time it is saved.
    /// </para>
    /// </summary>
    public double TimeOfDay { get; } = timeOfDay;

    public bool DayCycle { get; } = dayCycle;

    public double HoursPerSecond { get; } = hoursPerSecond;

    /// <summary>
    /// What each layer is called, from the first. Shorter than <see cref="LayerCount"/> when the last
    /// layers are not named, and an empty name is a layer with none.
    /// </summary>
    public IReadOnlyList<string> LayerNames { get; } = layerNames ?? [];

    public static SceneSettings FromJson(JsonMap json)
    {
        var time = Values.Object(Values.At(json, "time"));
        return new SceneSettings(
            // Absent and unreadable are different values here, and always have been. A file that
            // says nothing about its sky gets the one a new scene has; a file that says something we
            // cannot read gets the lighter grey the reader has always fallen back to. Collapsing the
            // two would re-light every scene in the second case.
            Values.At(json, "sky") is null ? NewScene : Values.ReadTint(Values.At(json, "sky"), Tint.Hex(0x59616F)),
            Values.Number(json, "ambient", 28000),
            Values.Number(time, "hour", 10),
            Values.Flag(time, "cycle", false),
            Values.Number(time, "hoursPerSecond", 0.5),
            LayerNamesOf(Values.At(json, "layerNames")));
    }

    /// <summary>
    /// What layer <paramref name="index"/> is called, or null when it has no name.
    /// </summary>
    public string? NameOf(int index)
    {
        if (index < 0 || index >= LayerNames.Count)
        {
            return null;
        }

        var name = LayerNames[index];
        return name.Length == 0 ? null : name;
    }

    // F2: six settings of one scene, called by name.
    public SceneSettings CopyWith(
        Tint? sky = null,
        double? ambient = null,
        double? timeOfDay = null,
        bool? dayCycle = null,
        double? hoursPerSecond = null,
        IReadOnlyList<string>? layerNames = null) => new(
        sky ?? Sky,
        ambient ?? Ambient,
        timeOfDay ?? TimeOfDay,
        dayCycle ?? DayCycle,
        hoursPerSecond ?? HoursPerSecond,
        layerNames ?? LayerNames);

    public JsonMap ToJson()
    {
        var json = new OrderedDictionary<string, object?>
        {
            ["sky"] = Values.TintToJson(Sky),
            ["ambient"] = Ambient,
            ["time"] = new OrderedDictionary<string, object?>
            {
                ["hour"] = TimeOfDay,
                ["cycle"] = DayCycle,
                ["hoursPerSecond"] = HoursPerSecond,
            },
        };

        // Only when a layer has a name, so a scene that names none is written as it always was and
        // an older editor never meets the key.
        if (LayerNames.Any(name => name.Length > 0))
        {
            json["layerNames"] = LayerNames;
        }

        return json;
    }

    // The names a file gives its layers. Something that is not a name leaves its layer unnamed
    // rather than moving the names after it up one.
    private static List<string> LayerNamesOf(object? raw) =>
        raw is JsonList list ? [.. list.Take(LayerCount).Select(one => one as string ?? string.Empty)] : [];
}

/// <summary>
/// A whole scene, as the thing that is saved and loaded.
/// </summary>
/// <remarks>
/// JSON with indentation and a stable key order, because a scene file lives in somebody's
/// repository: a format that reorders itself between saves turns every commit into a diff nobody
/// can review.
/// <para>
/// The entities are a flat list, and their order in it is the document's order, siblings draw and
/// list in the order they appear. A tree would say the same thing, and would make every operation
/// that addresses an entity by id walk it first; this way a lookup is a map and a reorder is a move.
/// </para>
/// </remarks>
public sealed class SceneDocument
{
    /// <summary>The extension a scene file carries.</summary>
    public const string SceneExtension = ".oscene";

    /// <summary>
    /// Bumped when the shape changes in a way an older editor could misread.
    /// <para>
    /// Two was a light's power changing units. Three moved the air out of the scene and into an
    /// object. Four replaced an object's kind with the set of components it has. Five made a prefab
    /// instance a link and a diff rather than a copy, which an older reader would open as a lone
    /// entity with everything under it missing. Six added the layouts, which an older reader would
    /// open as the base scene with its responsive edits gone.
    /// </para>
    /// </summary>
    public const int FormatVersion = 6;

    /// <summary>
    /// The version a scene with no layouts is written at.
    /// <para>
    /// Six only added a key, so a scene without it reads the same under five. Writing it at five
    /// leaves every existing scene byte for byte as it was, and an older editor refuses only the
    /// files that need the newer reader.
    /// </para>
    /// </summary>
    public const int PlainVersion = 5;

    private readonly Dictionary<string, SceneEntity> _byId = [];

    public SceneDocument(
        string name = "Scene",
        SceneSettings? settings = null,
        IReadOnlyList<SceneEntity>? entities = null,
        SceneLayouts? layouts = null)
    {
        Name = name;
        Settings = settings ?? new SceneSettings();
        Layouts = layouts ?? SceneLayouts.None;
        Entities = new ReadOnlyCollection<SceneEntity>([.. entities ?? []]);
        foreach (var entity in Entities)
        {
            _byId[entity.Id] = entity;
        }
    }

    public string Name { get; }

    public SceneSettings Settings { get; }

    /// <summary>
    /// The edits that turn this scene into how it looks held upright or on its side. The scene
    /// itself is the base, and holds what both share.
    /// </summary>
    public SceneLayouts Layouts { get; }

    /// <summary>Every entity, in document order.</summary>
    public IReadOnlyList<SceneEntity> Entities { get; }

    public int Length => Entities.Count;

    public IReadOnlyList<SceneEntity> Roots => SiblingsOf(null);

    public SceneEntity? this[string id] => _byId.GetValueOrDefault(id);

    /// <summary>A document out of a file's text, with whatever could not be read.</summary>
    public static SceneLoad Decode(string text)
    {
        var parsed = Parsed(text);
        var version = VersionOf(parsed);

        var problems = new List<string>();
        var json = SceneMigrations.Run(parsed, version, problems);

        if (Values.At(json, "entities") is not JsonList raw)
        {
            throw new SceneFormatException("This scene has no list of entities in it.");
        }

        var tree = SceneTree.Rooted(EntitiesOf(raw, problems), problems, folded: true);

        var layouts = SceneLayouts.FromJson(Values.At(json, "layouts"), problems);

        return new SceneLoad(
            new SceneDocument(Values.Text(json, "name") ?? "Scene", SceneSettings.FromJson(json), tree, layouts),
            problems);
    }

    public bool Contains(string id) => _byId.ContainsKey(id);

    /// <summary>Where an entity sits in the flat list, or -1 when it is not in it.</summary>
    public int IndexOf(string id)
    {
        for (var i = 0; i < Entities.Count; i++)
        {
            if (Entities[i].Id == id)
            {
                return i;
            }
        }

        return -1;
    }

    public IReadOnlyList<SceneEntity> ChildrenOf(string id) => SiblingsOf(id);

    /// <summary>The entities under <paramref name="parent"/>, in the order they are in.</summary>
    public IReadOnlyList<SceneEntity> SiblingsOf(string? parent) => [.. Entities.Where(entity => entity.Parent == parent)];

    /// <summary>
    /// An entity and everything under it, the entity first.
    /// <para>
    /// The unit a move works on: reparenting something takes its children with it, and so does
    /// deleting it.
    /// </para>
    /// </summary>
    public IReadOnlyList<SceneEntity> SubtreeOf(string id)
    {
        var wanted = new HashSet<string> { id };

        // One pass works because nothing is ever its own ancestor, decode breaks any loop before
        // this is reachable, but a child may appear before its parent in the list, so it takes as
        // many passes as the tree is deep.
        var growing = true;
        while (growing)
        {
            growing = false;
            foreach (var entity in Entities)
            {
                if (!wanted.Contains(entity.Id) && entity.Parent is { } parent && wanted.Contains(parent))
                {
                    wanted.Add(entity.Id);
                    growing = true;
                }
            }
        }

        return [.. Entities.Where(entity => wanted.Contains(entity.Id))];
    }

    public SceneDocument CopyWith(
        string? name = null,
        SceneSettings? settings = null,
        IReadOnlyList<SceneEntity>? entities = null,
        SceneLayouts? layouts = null) =>
        new(name ?? Name, settings ?? Settings, entities ?? Entities, layouts ?? Layouts);

    /// <summary>
    /// The scene as it looks held this way: the base with that shape's edits applied, and no layouts
    /// of its own, because it is what is drawn rather than what is saved.
    /// </summary>
    public SceneDocument InLayout(LayoutShape shape) => Layouts[shape].ApplyTo(CopyWith(layouts: SceneLayouts.None));

    /// <summary>The same document with one entity replaced, or removed when null.</summary>
    public SceneDocument WithEntity(string id, SceneEntity? entity)
    {
        var next = new List<SceneEntity>();
        var replaced = false;
        foreach (var one in Entities)
        {
            if (one.Id != id)
            {
                next.Add(one);
                continue;
            }

            replaced = true;
            if (entity is not null)
            {
                next.Add(entity);
            }
        }

        if (!replaced && entity is not null)
        {
            next.Add(entity);
        }

        return CopyWith(entities: next);
    }

    public JsonMap ToJson()
    {
        var json = new OrderedDictionary<string, object?>
        {
            ["formatVersion"] = (long)(Layouts.IsEmpty ? PlainVersion : FormatVersion),
            ["name"] = Name,
        };
        foreach (var (key, value) in Settings.ToJson())
        {
            json[key] = value;
        }

        json["entities"] = Entities.Select(entity => (object?)entity.ToJson()).ToList();

        // Last, and only when there is something in it, so a scene with no layouts is written as it
        // always was.
        if (!Layouts.IsEmpty)
        {
            json["layouts"] = Layouts.ToJson();
        }

        return json;
    }

    /// <summary>The document as the bytes that go on disk.</summary>
    public string Encode() => $"{JsonText.Encode(ToJson(), "  ")}\n";

    private static JsonMap Parsed(string text)
    {
        object? parsed;
        try
        {
            parsed = JsonText.Decode(text);
        }
        catch (FormatException error)
        {
            throw new SceneFormatException($"This is not a scene file: {error.Message}");
        }

        return parsed as JsonMap
            ?? throw new SceneFormatException("A scene file has to be a JSON object, and this one is not.");
    }

    private static long VersionOf(JsonMap parsed)
    {
        if (Values.At(parsed, "formatVersion") is not long version)
        {
            throw new SceneFormatException(
                "This file does not say what format version it is, so it cannot be read safely.");
        }

        // Refused rather than half-read: a newer file may mean something different by the same
        // keys, and guessing loses somebody's work.
        return version > FormatVersion
            ? throw new SceneFormatException(
                $"This scene was written by a newer Orblit (format {version}; this one reads up to {FormatVersion}).")
            : version;
    }

    private static List<SceneEntity> EntitiesOf(JsonList raw, List<string> problems)
    {
        var entities = new List<SceneEntity>();
        var seen = new HashSet<string>();
        for (var index = 0; index < raw.Count; index++)
        {
            if (raw[index] is not JsonMap entry)
            {
                problems.Add($"Entity {index} is not an object, and was left out.");
            }
            else if (SceneEntity.FromJson(entry) is not { } entity)
            {
                problems.Add($"Entity {index} has no id, and was left out.");
            }
            else if (!seen.Add(entity.Id))
            {
                problems.Add($"Two entities share the id \"{entity.Id}\"; the second was dropped.");
            }
            else
            {
                entities.Add(entity);
            }
        }

        return entities;
    }
}
