namespace Orblit.Scene;

/// <summary>
/// The prefab at <paramref name="asset"/>, or null when there is none to be had.
/// </summary>
/// <remarks>
/// A function rather than a folder, because what a prefab <em>is</em> at a given moment is the
/// caller's to say: an editor answers from what it has open, which may be newer than the file; a game
/// answers from what it shipped; a test answers from a map.
/// </remarks>
public delegate PrefabDocument? PrefabSource(string asset);

/// <summary>Something asked of a prefab that cannot be done.</summary>
public sealed class PrefabException(string message) : Exception(message);

/// <summary>
/// What making a prefab, or applying an instance to one, produces: the prefab to write, and the scene
/// with the instance linked to it.
/// </summary>
public sealed class PrefabEdit(
    PrefabDocument prefab,
    SceneDocument document,
    IReadOnlyDictionary<string, string>? renamed = null,
    IReadOnlyList<string>? problems = null)
{
    public PrefabDocument Prefab { get; } = prefab;

    public SceneDocument Document { get; } = document;

    /// <summary>
    /// Old id to new, for everything that became a part and so took a path. Whatever holds ids, a
    /// selection, follows these.
    /// </summary>
    public IReadOnlyDictionary<string, string> Renamed { get; } = renamed ?? new Dictionary<string, string>();

    public IReadOnlyList<string> Problems { get; } = problems ?? [];
}

/// <summary>
/// Instances: the opening of a scene's links into parts, and the folding of those parts back into
/// links.
/// </summary>
public static class Instances
{
    /// <summary>
    /// <paramref name="document"/> with its instances opened: every part of every prefab put in,
    /// under ids that are <see cref="EntityPath"/>s into the instance it belongs to.
    /// <para>
    /// What a document is read into before anything uses it. The parts are then ordinary entities,
    /// drawn, selected, edited, animated by path, and what an instance changes about its prefab is
    /// simply how its parts differ from the prefab's, worked out again by <see cref="Fold"/> when it
    /// is saved.
    /// </para>
    /// <para>
    /// Opens prefabs inside prefabs, all the way down, and copies stamped into a scene before
    /// instances were links are relinked here, keeping everything that was changed about them. An
    /// instance whose prefab cannot be read, or which contains itself, stays folded: a link that still
    /// says everything it said. Each of those is a problem in the result rather than a failure.
    /// </para>
    /// <para><paramref name="only"/> limits it to those instances, by id.</para>
    /// </summary>
    public static SceneLoad Expand(SceneDocument document, PrefabSource source, IEnumerable<string>? only = null)
    {
        var problems = new List<string>();
        var opened = new InstanceOpener(source, problems).OpenAll(document, only?.ToHashSet(StringComparer.Ordinal));
        return new SceneLoad(opened, problems);
    }

    /// <summary>
    /// <paramref name="document"/> with its open instances folded back into links, as it is saved.
    /// <para>
    /// Each one becomes the entity it was, name, place in the tree, visibility, carrying a
    /// <see cref="PrefabComponent"/> whose overrides are the difference between the prefab and the
    /// parts as they stand now, and the parts themselves are left out. Something hung on a part from
    /// outside the instance is not part of it: it belongs to this document, is written here, and
    /// keeps its link into the instance by path.
    /// </para>
    /// <para>
    /// An instance whose prefab <paramref name="source"/> cannot give is left open. Folding it
    /// against nothing would say it had removed everything, and writing it as it stands keeps it:
    /// <see cref="Expand"/> recognises an instance already open and leaves its parts alone.
    /// </para>
    /// <para>
    /// A part stays inside the instance it belongs to. One whose parent has been set to something
    /// outside it cannot be said as a change to the instance, and is folded back under the instance's
    /// root: moving parts out is for <see cref="Unpack"/> to do first.
    /// </para>
    /// <para><paramref name="only"/> limits it to those instances, by id.</para>
    /// </summary>
    public static SceneDocument Fold(SceneDocument document, PrefabSource source, IEnumerable<string>? only = null)
    {
        var stubs = StubsOf(document, new InstanceOpener(source, []), only?.ToHashSet(StringComparer.Ordinal));
        if (stubs.Count == 0)
        {
            return document;
        }

        var entities = new List<SceneEntity>();
        foreach (var entity in document.Entities)
        {
            if (stubs.TryGetValue(entity.Id, out var stub))
            {
                entities.Add(stub);
            }
            else if (EntityPath.InstanceOf(entity.Id) is not { } head || !stubs.ContainsKey(head))
            {
                entities.Add(entity);
            }
        }

        return document.CopyWith(entities: entities);
    }

    /// <summary>
    /// The entity at <paramref name="id"/> and everything under it made into a prefab to be saved at
    /// <paramref name="asset"/>, and the entity made an instance of it.
    /// <para>
    /// Everything under it becomes a part, and so takes a path: <see cref="PrefabEdit.Renamed"/> says
    /// which. It stays where it stood and keeps its name.
    /// </para>
    /// <para>
    /// Throws <see cref="PrefabException"/> for something inside an instance, which belongs to that
    /// instance's prefab, and for a prefab that would contain itself.
    /// </para>
    /// </summary>
    // F2: the asset, its source and an optional name are separate inputs a caller always has to hand.
    public static PrefabEdit MakePrefab(
        SceneDocument document,
        string id,
        string asset,
        PrefabSource source,
        string? name = null)
    {
        if (EntityPath.IsPart(id))
        {
            throw new PrefabException(
                "That belongs to the prefab it is inside. Make the prefab from the instance, or unpack the instance first.");
        }

        var made = PrefabOf(document, id, new PrefabPlan(asset, source, null, name));
        var linked = Linked(document, id, asset, After(source, asset, made.Prefab));
        return new PrefabEdit(made.Prefab, linked.Document, Renames(document, id, made), linked.Problems);
    }

    /// <summary>
    /// The open instance <paramref name="id"/> written into its prefab, and every other instance
    /// brought up to date with it.
    /// <para>
    /// This instance's changes become the prefab's. Every other instance keeps its own and picks up
    /// the rest, including instances that hold this prefab inside another. The new prefab keeps the
    /// old one's ids, its root's name and where its root stands, so every other instance's overrides
    /// still land on the parts they were written against.
    /// </para>
    /// <para>
    /// Whatever is hung on the instance from outside comes along, and becomes a part: applying is
    /// saying "the prefab should look like this", and this is what it looks like.
    /// </para>
    /// <para>
    /// Throws <see cref="PrefabException"/> for something that is not an open instance, for an
    /// instance inside another one, and when the prefab cannot be read.
    /// </para>
    /// </summary>
    public static PrefabEdit Apply(SceneDocument document, string id, PrefabSource source)
    {
        if (document[id] is not { } entity || OpenInstance(entity) is not { } link)
        {
            throw new PrefabException("That is not an instance of a prefab.");
        }

        if (EntityPath.IsPart(id))
        {
            throw new PrefabException(
                "That instance belongs to the prefab it is inside. Open that prefab to change it.");
        }

        var asset = link.Asset!;
        var was = Usable(source, asset)
            ?? throw new PrefabException($"The prefab \"{asset}\" could not be read.");

        var made = PrefabOf(document, id, new PrefabPlan(asset, source, was, null));
        var after = After(source, asset, made.Prefab);
        var linked = Linked(document, id, asset, after);
        var others = Refresh(linked.Document, asset, source, after, [id]);
        return new PrefabEdit(
            made.Prefab,
            others.Document,
            Renames(document, id, made),
            [.. linked.Problems, .. others.Problems]);
    }

    /// <summary>
    /// <paramref name="document"/> with the open instance <paramref name="id"/> reverted: its parts
    /// made the prefab's again, and where it stands kept.
    /// <para>
    /// Everything else the instance changed goes. What is hung on it from outside stays where it was,
    /// or on the nearest thing still there.
    /// </para>
    /// </summary>
    public static SceneLoad Revert(SceneDocument document, string id, PrefabSource source)
    {
        if (document[id] is not { } entity || OpenInstance(entity) is not { } link)
        {
            return new SceneLoad(document);
        }

        var folded = Fold(document, source, [id]);
        if (ReferenceEquals(folded, document))
        {
            return new SceneLoad(document);
        }

        var cleared = folded.WithEntity(
            id,
            folded[id]!.WithComponent(
                SceneComponents.Prefab,
                link.CopyWith(state: PrefabState.Folded, overrides: SceneDiff.None)));
        return Standing(Expand(cleared, source, [id]), entity);
    }

    /// <summary>
    /// <paramref name="document"/> with every instance that uses <paramref name="asset"/> brought up
    /// to date with it, keeping what each one changed.
    /// <para>
    /// <paramref name="before"/> is what the instances were opened against and
    /// <paramref name="after"/> is what they should be opened against now. That includes instances
    /// that hold the prefab inside another one: a street of lamps changes when the lamp does.
    /// <paramref name="except"/> are left as they are.
    /// </para>
    /// </summary>
    // F2: the two sources are what the refresh is between, and the skip list is optional.
    public static SceneLoad Refresh(
        SceneDocument document,
        string asset,
        PrefabSource before,
        PrefabSource after,
        IEnumerable<string>? except = null)
    {
        var skip = except?.ToHashSet(StringComparer.Ordinal) ?? [];
        var opener = new InstanceOpener(before, []);
        var affected = document.Entities
            .Where(entity => FoldableInstance(entity) is { } link
                && !skip.Contains(entity.Id)
                && (link.Asset == asset || opener.Reaches(link.Asset!, asset)))
            .Select(static entity => entity.Id)
            .ToList();
        if (affected.Count == 0)
        {
            return new SceneLoad(document);
        }

        return Expand(Fold(document, before, affected), after, affected);
    }

    /// <summary>
    /// <paramref name="document"/> with the open instance <paramref name="id"/> turned back into
    /// ordinary entities that no longer follow the prefab.
    /// <para>
    /// Parts need ids of their own once they stop being parts, so each gets one from
    /// <paramref name="fresh"/>. A prefab inside the instance stays an instance, one level up, and
    /// when the prefab's root was itself an instance of another, so is this. Returns what was renamed,
    /// so that anything holding the old ids, a selection, can follow.
    /// </para>
    /// </summary>
    public static (SceneDocument Document, IReadOnlyDictionary<string, string> Renamed) Unpack(
        SceneDocument document,
        string id,
        PrefabSource source,
        Func<string> fresh)
    {
        if (document[id] is not { } entity || OpenInstance(entity) is not { } link)
        {
            return (document, new Dictionary<string, string>());
        }

        // Parts of the prefab's root, when that root is an instance of another prefab, sit under a
        // segment no entity has: the root's id in the prefab. They become this entity's parts, and it
        // becomes that instance.
        var prefab = Usable(source, link.Asset!);
        var inner = prefab is null ? null : LinkOf(prefab.RootEntity);
        var renamed = UnpackedIds(document, id, inner is null ? null : prefab?.Root, fresh);

        var unpacked = inner?.CopyWith(state: PrefabState.Open, overrides: SceneDiff.None);
        var entities = document.Entities
            .Select(one => one.Id == id
                ? one.WithComponent(SceneComponents.Prefab, unpacked)
                : one.CopyWith(id: RenamedOf(renamed, one.Id), parent: RenamedOf(renamed, one.Parent)))
            .ToList();
        return (document.CopyWith(entities: entities), renamed);
    }

    internal static PrefabComponent? LinkOf(SceneEntity entity) =>
        entity[SceneComponents.Prefab] is PrefabComponent { Asset: not null } link ? link : null;

    /// <summary>
    /// The prefab at <paramref name="asset"/>, unless there is none or it has lost its root. A source
    /// is the caller's and may hand back anything, so everything here that reaches into a prefab's
    /// root asks through this first.
    /// </summary>
    internal static PrefabDocument? Usable(PrefabSource source, string asset) =>
        source(asset) is { } prefab && prefab.Document.Contains(prefab.Root) ? prefab : null;

    // Each open instance wanted, folded to the link it is saved as. One whose prefab cannot be given
    // is left out, and so stays open.
    private static Dictionary<string, SceneEntity> StubsOf(
        SceneDocument document,
        InstanceOpener opener,
        HashSet<string>? wanted)
    {
        var stubs = new Dictionary<string, SceneEntity>(StringComparer.Ordinal);
        foreach (var entity in document.Entities)
        {
            if (FoldableInstance(entity) is not { } link || wanted?.Contains(entity.Id) == false)
            {
                continue;
            }

            if (opener.Open(link.Asset!) is { } prefab)
            {
                stubs[entity.Id] = Folded(document, entity, link, prefab);
            }
        }

        return stubs;
    }

    private static PrefabComponent? OpenInstance(SceneEntity entity) =>
        LinkOf(entity) is { State: PrefabState.Open } link ? link : null;

    // An open instance that is not itself a part of another: the ones that fold and refresh.
    private static PrefabComponent? FoldableInstance(SceneEntity entity) =>
        EntityPath.IsPart(entity.Id) ? null : OpenInstance(entity);

    internal static string? RenamedOf(IReadOnlyDictionary<string, string> renamed, string? id) =>
        id is null ? null : renamed.GetValueOrDefault(id);

    private static PrefabSource After(PrefabSource source, string asset, PrefabDocument made) =>
        one => one == asset ? made : source(one);

    // The ids the parts of the instance take once they stop being parts, one fresh head for each
    // part directly under the instance, in the order the document meets them.
    private static OrderedDictionary<string, string> UnpackedIds(
        SceneDocument document,
        string id,
        string? innerHead,
        Func<string> fresh)
    {
        var heads = new Dictionary<string, string>(StringComparer.Ordinal);
        var renamed = new OrderedDictionary<string, string>();
        foreach (var one in document.Entities)
        {
            var rest = EntityPath.LocalTo(id, one.Id, string.Empty);
            if (string.IsNullOrEmpty(rest))
            {
                continue;
            }

            var cut = rest.IndexOf(EntityPath.Separator, StringComparison.Ordinal);
            var head = cut < 0 ? rest : rest[..cut];
            if (head == innerHead)
            {
                if (cut >= 0)
                {
                    renamed[one.Id] = EntityPath.Join(id, rest[(cut + 1)..]);
                }

                continue;
            }

            if (!heads.TryGetValue(head, out var mapped))
            {
                heads[head] = mapped = fresh();
            }

            renamed[one.Id] = cut < 0 ? mapped : mapped + rest[cut..];
        }

        return renamed;
    }

    // Where an instance stands is the one thing about its root that every instance changes, and none
    // of the operations here should move it.
    private static SceneLoad Standing(SceneLoad opened, SceneEntity was) =>
        opened.Document[was.Id] is not { } root
            ? opened
            : new SceneLoad(
                opened.Document.WithEntity(was.Id, Positioned(root, PositionOf(was))),
                opened.Problems);

    // The entity standing at a position as a file writes one.
    private static SceneEntity Positioned(SceneEntity entity, object? at)
    {
        if (entity[SceneComponents.Transform] is not { } transform || at is null)
        {
            return entity;
        }

        var json = new OrderedDictionary<string, object?>();
        foreach (var (key, value) in transform.ToJson())
        {
            json[key] = value;
        }

        json["position"] = at;
        return entity.WithComponent(SceneComponents.Transform, SceneComponents.Read(SceneComponents.Transform, json));
    }

    private static object? PositionOf(SceneEntity? entity) =>
        entity?[SceneComponents.Transform]?.ToJson().GetValueOrDefault("position");

    private static string Free(string wanted, HashSet<string> taken)
    {
        var id = wanted;
        var attempt = 2;
        while (!taken.Add(id))
        {
            id = $"{wanted}_{attempt++}";
        }

        return id;
    }

    // The entity at id and everything under it, as a prefab to be saved at the plan's asset. For an
    // apply, the plan's prefab is the one as it was: the new one keeps its ids, its root's name and
    // where its root stands. Instances inside are written folded, as a scene writes them.
    private static MadePrefab PrefabOf(SceneDocument document, string id, PrefabPlan plan)
    {
        var top = document[id] ?? throw new PrefabException("That is not in this scene.");
        var subtree = document.SubtreeOf(id);
        var applying = LinkOf(top)?.Asset == plan.Asset;
        RefuseContainingItself(subtree, id, applying, plan);

        var root = plan.Replacing?.Root ?? id;
        var local = LocalIds(subtree, id, root, applying);

        var made = RootOf(top, root, plan);
        var entities = new List<SceneEntity> { applying ? made.WithComponent(SceneComponents.Prefab, null) : made };
        entities.AddRange(subtree
            .Where(entity => entity.Id != id)
            .Select(entity => entity.CopyWith(id: local[entity.Id], parent: RenamedOf(local, entity.Parent))));

        var name = plan.NameFor(top);
        var loose = new SceneDocument(name, entities: entities);
        return new MadePrefab(new PrefabDocument(name, root, Fold(loose, plan.Source)), local);
    }

    private static void RefuseContainingItself(
        IReadOnlyList<SceneEntity> subtree,
        string id,
        bool applying,
        PrefabPlan plan)
    {
        var opener = new InstanceOpener(plan.Source, []);
        foreach (var entity in subtree)
        {
            if (entity.Id == id && applying)
            {
                continue;
            }

            if (LinkOf(entity)?.Asset is { } inner && (inner == plan.Asset || opener.Reaches(inner, plan.Asset)))
            {
                throw new PrefabException(
                    $"\"{entity.Name}\" is an instance of a prefab that would then contain itself.");
            }
        }
    }

    private static Dictionary<string, string> LocalIds(
        IReadOnlyList<SceneEntity> subtree,
        string id,
        string root,
        bool applying)
    {
        var taken = new HashSet<string>(StringComparer.Ordinal);
        var local = new Dictionary<string, string>(StringComparer.Ordinal);

        // The instance's own parts take their ids first, so something hung on it from outside is
        // what gets renamed when two want the same one.
        var ordered = subtree.Where(entity => EntityPath.Within(id, entity.Id))
            .Concat(subtree.Where(entity => !EntityPath.Within(id, entity.Id)));
        foreach (var entity in ordered)
        {
            local[entity.Id] = Free(WantedId(entity.Id, id, root, applying), taken);
        }

        return local;
    }

    private static string WantedId(string entityId, string id, string root, bool applying)
    {
        if (entityId == id)
        {
            return root;
        }

        if (EntityPath.LocalTo(id, entityId, root) is not { } inside)
        {
            return entityId;
        }

        // A part goes back to the id the prefab gave it. Otherwise the root is an instance of
        // something else and stays one, so its parts stay under it.
        return applying ? inside : EntityPath.Join(root, inside);
    }

    // A prefab is a thing, not a thing at a place: one saved from something standing forty metres
    // away should not arrive forty metres away. Its rotation and scale are kept, because those are
    // usually part of what the thing is. An existing prefab's root stays where it was, since every
    // instance that never moved stands there.
    private static SceneEntity RootOf(SceneEntity top, string root, PrefabPlan plan)
    {
        var was = plan.Replacing?.RootEntity;
        var made = new SceneEntity(root, was?.Name ?? plan.Name ?? top.Name, visible: was?.Visible ?? top.Visible, components: top.Components);
        return Positioned(made, PositionOf(was) ?? Values.NumbersToJson([0.0, 0.0, 0.0]));
    }

    // The document with the subtree at id replaced by an instance of the new prefab.
    private static SceneLoad Linked(SceneDocument document, string id, string asset, PrefabSource source)
    {
        var top = document[id]!;
        var gone = document.SubtreeOf(id).Select(static entity => entity.Id).ToHashSet(StringComparer.Ordinal);
        var stub = new SceneEntity(
            id,
            top.Name,
            top.Parent,
            top.Visible,
            new OrderedDictionary<string, SceneComponent> { [SceneComponents.Prefab] = new PrefabComponent(asset) });

        var entities = new List<SceneEntity>();
        foreach (var entity in document.Entities)
        {
            if (entity.Id == id)
            {
                entities.Add(stub);
            }
            else if (!gone.Contains(entity.Id))
            {
                entities.Add(entity);
            }
        }

        return Standing(Expand(document.CopyWith(entities: entities), source, [id]), top);
    }

    private static OrderedDictionary<string, string> Renames(SceneDocument document, string id, MadePrefab made)
    {
        var renamed = new OrderedDictionary<string, string>();
        foreach (var entity in document.SubtreeOf(id).Where(entity => entity.Id != id))
        {
            var now = EntityPath.Inside(id, made.Local[entity.Id], made.Prefab.Root);
            if (now != entity.Id)
            {
                renamed[entity.Id] = now;
            }
        }

        return renamed;
    }

    // The open instance as the link it is saved as.
    private static SceneEntity Folded(SceneDocument document, SceneEntity entity, PrefabComponent link, OpenedPrefab opened)
    {
        var baseRoot = opened.Document[opened.Root]!;

        // The root's name, place in the tree and visibility are the instance's own and are written on
        // it, so the prefab's stand in for them here and the diff says nothing about them. Its link is
        // not a difference either.
        var components = new OrderedDictionary<string, SceneComponent>();
        foreach (var (type, component) in entity.Components.Where(pair => pair.Key != SceneComponents.Prefab))
        {
            components[type] = component;
        }

        if (baseRoot[SceneComponents.Prefab] is { } inner)
        {
            components[SceneComponents.Prefab] = inner;
        }

        var rootNow = new SceneEntity(opened.Root, baseRoot.Name, baseRoot.Parent, baseRoot.Visible, components);
        var now = InPrefabTerms(document, entity.Id, rootNow);
        var overrides = SceneDiff.Between(opened.Document, opened.Document.CopyWith(entities: now));

        return new SceneEntity(
            entity.Id,
            entity.Name,
            entity.Parent,
            entity.Visible,
            new OrderedDictionary<string, SceneComponent>
            {
                [SceneComponents.Prefab] = link.CopyWith(state: PrefabState.Folded, overrides: overrides),
            });
    }

    // The instance and its parts with the ids the prefab gives them.
    private static List<SceneEntity> InPrefabTerms(SceneDocument document, string instance, SceneEntity rootNow)
    {
        var root = rootNow.Id;
        var now = new List<SceneEntity>();
        foreach (var one in document.Entities)
        {
            if (one.Id == instance)
            {
                now.Add(rootNow);
            }
            else if (EntityPath.LocalTo(instance, one.Id, root) is { } local)
            {
                // A part moved out of its instance cannot be said in the instance's terms, so it is
                // kept on the instance's root rather than lost.
                var parent = one.Parent is null ? root : EntityPath.LocalTo(instance, one.Parent, root) ?? root;
                now.Add(one.CopyWith(id: local, parent: parent));
            }
        }

        return now;
    }

    // A prefab made from a subtree, and the id each entity of the subtree has in it.
    private sealed record MadePrefab(PrefabDocument Prefab, IReadOnlyDictionary<string, string> Local);

    // What a prefab is made for: where it will be saved, what answers for the other prefabs, the
    // prefab it replaces when applying, and the name to give a new one.
    private sealed record PrefabPlan(string Asset, PrefabSource Source, PrefabDocument? Replacing, string? Name)
    {
        public string NameFor(SceneEntity top) => Replacing?.Name ?? Name ?? top.Name;
    }
}
