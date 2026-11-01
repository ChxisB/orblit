namespace Orblit.Scene;

/// <summary>
/// One thing in a scene.
/// </summary>
/// <remarks>
/// An id, a name, where it sits in the tree, and what it is, which is the set of components it has
/// and nothing else. There is deliberately no <c>kind</c> field: the question "is this a light" is
/// answered by asking whether it has a light component, which means a lamp can be a mesh and a light
/// at once instead of being two objects somebody has to keep in step by hand.
/// <para>
/// Identified by <see cref="Id"/> rather than by name because a rename is an ordinary edit and
/// everything that points at an entity, a parent link, the selection, an undo step, has to survive
/// one.
/// </para>
/// </remarks>
public sealed class SceneEntity(
    string id,
    string name,
    string? parent = null,
    bool visible = true,
    IReadOnlyDictionary<string, SceneComponent>? components = null)
{
    public string Id { get; } = id;

    /// <summary>What it is called, which need not be unique and is not identity.</summary>
    public string Name { get; } = name;

    /// <summary>The entity this hangs from, or null when it sits at the top level.</summary>
    public string? Parent { get; } = parent;

    /// <summary>
    /// Hidden is not deleted: it keeps its place in the tree, its children and its id, so showing it
    /// again is immediate.
    /// </summary>
    public bool Visible { get; } = visible;

    /// <summary>What this entity is, keyed by component type.</summary>
    public IReadOnlyDictionary<string, SceneComponent> Components { get; } =
        components ?? new OrderedDictionary<string, SceneComponent>();

    public SceneComponent? this[string type] => Components.GetValueOrDefault(type);

    /// <summary>
    /// One entity out of its JSON, with anything unrecognised kept whole. Returns null when there is
    /// no usable id, because an entity nothing can refer to cannot be parented to, selected or
    /// undone: it is not a recoverable entity, it is a hole.
    /// </summary>
    public static SceneEntity? FromJson(JsonMap json)
    {
        var id = Values.Text(json, "id");
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        var components = new OrderedDictionary<string, SceneComponent>();
        foreach (var (type, value) in Values.Object(Values.At(json, "components")))
        {
            if (value is JsonMap map)
            {
                components[type] = SceneComponents.Read(type, map);
            }
        }

        return new SceneEntity(
            id,
            Values.Text(json, "name") ?? id,
            Values.Text(json, "parent"),
            Values.Flag(json, "visible", true),
            components);
    }

    public bool Has(string type) => Components.ContainsKey(type);

    // F2: six fields of one entity, called by name.
    public SceneEntity CopyWith(
        string? id = null,
        string? name = null,
        string? parent = null,
        bool clearParent = false,
        bool? visible = null,
        IReadOnlyDictionary<string, SceneComponent>? components = null) => new(
        id ?? Id,
        name ?? Name,
        clearParent ? null : parent ?? Parent,
        visible ?? Visible,
        components ?? Components);

    /// <summary>The same entity with one component set, or removed when <paramref name="component"/> is null.</summary>
    public SceneEntity WithComponent(string type, SceneComponent? component)
    {
        var next = new OrderedDictionary<string, SceneComponent>();
        foreach (var (key, value) in Components)
        {
            next[key] = value;
        }

        if (component is null)
        {
            next.Remove(type);
        }
        else
        {
            next[type] = component;
        }

        return CopyWith(components: next);
    }

    /// <summary>
    /// The entity as it goes into the file. Components are written in the order
    /// <see cref="SceneComponents.Order"/> gives, with anything unrecognised after them in the order
    /// it was read. Two saves of one scene therefore produce the same bytes, which is what lets a
    /// scene file live in a repository without turning every commit into a diff nobody can review.
    /// </summary>
    public JsonMap ToJson()
    {
        var known = SceneComponents.Order.Where(Components.ContainsKey);
        var rest = Components.Keys.Where(type => !SceneComponents.Order.Contains(type));

        var written = new OrderedDictionary<string, object?>();
        foreach (var type in known.Concat(rest))
        {
            written[type] = Components[type].ToJson();
        }

        return Values.Pruned(new OrderedDictionary<string, object?>
        {
            ["id"] = Id,
            ["name"] = Name,
            ["parent"] = Parent,

            // Written only when it is false, so the ordinary case stays out of the file and out of
            // everybody's diffs.
            ["visible"] = Visible ? null : false,
            ["components"] = written,
        });
    }
}
