namespace Orblit.Scene;

/// <summary>
/// Files this entity carries: scripts, tables, whatever a game needs beside what it draws.
/// </summary>
/// <remarks>
/// Paths rather than contents, and a list rather than one, because the thing people actually do is
/// hang three or four related files off one object, behaviour, a stat table, some dialogue, and
/// making that require three child entities gives the outliner a tree of things that are not in the
/// scene in any spatial sense.
/// </remarks>
public sealed class DataComponent(IReadOnlyList<string>? paths = null) : SceneComponent
{
    /// <summary>Project-relative, in the order somebody put them in.</summary>
    public IReadOnlyList<string> Paths { get; } = paths ?? [];

    public override string Type => SceneComponents.Data;

    public static DataComponent FromJson(JsonMap json) => new(Values.Texts(Values.At(json, "paths")));

    public override JsonMap ToJson() => new OrderedDictionary<string, object?> { ["paths"] = Paths };
}

/// <summary>
/// This entity is an instance of a prefab: a link to it, and what is different here.
/// </summary>
/// <remarks>
/// A component rather than a field on the entity so that an entity is nothing but an id, a name, a
/// place in the tree and a bag of components, with no exceptions to carry through every piece of
/// code that walks one. It is also honestly a fact about the entity rather than part of its
/// identity: an instance that somebody unpacks stops being one, and that should be a component going
/// away rather than a field going null.
/// <para>
/// Sits on the instance's root only. What the instance is made of is the prefab's, and is not
/// written down here at all, see <see cref="PrefabState"/>.
/// </para>
/// </remarks>
public sealed class PrefabComponent(
    string? asset = null,
    PrefabState state = PrefabState.Folded,
    SceneDiff? overrides = null) : SceneComponent
{
    /// <summary>The prefab, relative to the project.</summary>
    public string? Asset { get; } = asset;

    public PrefabState State { get; } = state;

    /// <summary>
    /// What this instance changes about the prefab, addressed by the ids the prefab's own document
    /// uses.
    /// <para>
    /// Only meaningful while <see cref="State"/> is <see cref="PrefabState.Folded"/>. Once the
    /// instance is open its parts are in the document and are the truth, and these are worked out
    /// again from them when the document is folded to be saved, so they stay as small as the edit,
    /// however many times it was made.
    /// </para>
    /// </summary>
    public SceneDiff Overrides { get; } = overrides ?? SceneDiff.None;

    public override string Type => SceneComponents.Prefab;

    public static PrefabComponent FromJson(JsonMap json) => new(
        Values.Text(json, "asset"),
        Values.Named<PrefabState>(Values.At(json, "state")) ?? PrefabState.Folded,
        SceneDiff.FromJson(Values.At(json, "overrides")));

    public PrefabComponent CopyWith(string? asset = null, PrefabState? state = null, SceneDiff? overrides = null) =>
        new(asset ?? Asset, state ?? State, overrides ?? Overrides);

    public override JsonMap ToJson() => Values.Pruned(new OrderedDictionary<string, object?>
    {
        ["asset"] = Asset,
        ["state"] = State == PrefabState.Folded ? null : EnumNames.Of(State),
        ["overrides"] = Overrides.IsEmpty ? null : Overrides.ToJson(),
    });
}

/// <summary>How much of an instance is in the document.</summary>
public enum PrefabState
{
    /// <summary>
    /// Only the link is: an entity with nothing but a name, a place and this component, and the
    /// prefab's parts left in the prefab. How every instance is saved, and how one whose prefab
    /// cannot be read stays, as a link that still says everything it said, rather than as a hole.
    /// </summary>
    Folded,

    /// <summary>
    /// Every part is in the document, under ids that are paths into the instance, and they are what
    /// the instance looks like now. How an editor or a game holds one. Written down only if
    /// something writes an open document without folding it, and then it stops the parts being
    /// opened a second time on top of themselves.
    /// </summary>
    Open,

    /// <summary>
    /// A copy of the prefab stamped into the scene before instances were links: every part is here
    /// under an id of its own, each one carrying this component. What a scene saved at format four
    /// holds, and turned into an open instance the first time the prefab can be read.
    /// </summary>
    Stamped,
}
