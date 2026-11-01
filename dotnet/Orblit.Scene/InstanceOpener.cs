namespace Orblit.Scene;

/// <summary>A prefab opened in its own ids, ready to be put in under an instance's.</summary>
/// <param name="Root">The prefab's root id.</param>
/// <param name="Document">The prefab, open all the way down.</param>
internal sealed record OpenedPrefab(string Root, SceneDocument Document);

/// <summary>Opens prefabs, each once, and notices one that contains itself.</summary>
internal sealed class InstanceOpener(PrefabSource source, List<string> problems)
{
    private readonly Dictionary<string, OpenedPrefab?> _opened = [];
    private readonly HashSet<string> _opening = [];

    /// <summary>The entity an operation changes, for the ones that change one that has to be there already.</summary>
    internal static string? Target(SceneOp op) => op switch
    {
        SetEntityName named => named.Id,
        SetVisible shown => shown.Id,
        SetComponent component => component.Id,
        SetField field => field.Id,
        Reparent moved => moved.Id,
        Reorder ordered => ordered.Id,
        _ => null,
    };

    internal OpenedPrefab? Open(string asset)
    {
        if (_opened.TryGetValue(asset, out var done))
        {
            return done;
        }

        if (!_opening.Add(asset))
        {
            problems.Add($"\"{asset}\" contains an instance of itself, which was left closed.");
            return null;
        }

        try
        {
            return _opened[asset] = Read(asset);
        }
        finally
        {
            _opening.Remove(asset);
        }
    }

    /// <summary>Whether the prefab at <paramref name="from"/> contains <paramref name="target"/>, at any depth.</summary>
    internal bool Reaches(string from, string target, HashSet<string>? walked = null)
    {
        var seen = walked ?? [];
        if (!seen.Add(from) || source(from) is not { } prefab)
        {
            return false;
        }

        foreach (var entity in prefab.Document.Entities)
        {
            if (Instances.LinkOf(entity)?.Asset is { } inner && (inner == target || Reaches(inner, target, seen)))
            {
                return true;
            }
        }

        return false;
    }

    internal SceneDocument OpenAll(SceneDocument document, IReadOnlySet<string>? only)
    {
        var relinked = Relink(document, only);
        var output = new List<SceneEntity>();
        var changed = !ReferenceEquals(relinked, document);

        foreach (var entity in relinked.Entities)
        {
            if (Opening(relinked, entity, only) is { } replaced)
            {
                output.AddRange(replaced);
                changed = true;
            }
            else
            {
                output.Add(entity);
            }
        }

        var closed = output
            .Where(static entity => Instances.LinkOf(entity)?.State == PrefabState.Folded)
            .Select(static entity => entity.Id)
            .ToHashSet(StringComparer.Ordinal);
        var tree = SceneTree.Rooted(output, problems, closed: closed);
        return !changed && ReferenceEquals(tree, output) ? document : relinked.CopyWith(entities: tree);
    }

    private static bool StampedFrom(SceneEntity? entity, string asset) =>
        entity is not null && Instances.LinkOf(entity) is { State: PrefabState.Stamped } link && link.Asset == asset;

    private static PrefabComponent? FoldedLink(SceneEntity entity, IReadOnlySet<string>? only) =>
        Instances.LinkOf(entity) is { State: PrefabState.Folded } link
            && !EntityPath.IsPart(entity.Id)
            && (only is null || only.Contains(entity.Id))
                ? link
                : null;

    private static SceneEntity RootPart(SceneEntity stub, SceneEntity root, PrefabComponent link)
    {
        var components = new OrderedDictionary<string, SceneComponent>();
        foreach (var (type, component) in root.Components)
        {
            components[type] = component;
        }

        components[SceneComponents.Prefab] = link.CopyWith(state: PrefabState.Open, overrides: SceneDiff.None);
        return new SceneEntity(stub.Id, stub.Name, stub.Parent, stub.Visible, components);
    }

    private static SceneEntity Part(SceneEntity stub, SceneEntity entity, string root) => entity.CopyWith(
        id: EntityPath.Join(stub.Id, entity.Id),
        parent: entity.Parent is null ? stub.Id : EntityPath.Inside(stub.Id, entity.Parent, root));

    // The root's name, place and visibility belong to the stub, and it cannot be taken out of its own
    // instance. Nothing that folds writes otherwise, but a hand edited file might.
    private static bool TakesRootOut(SceneOp op, string root) => op switch
    {
        RemoveEntity removed => removed.Id == root,
        Reparent moved => moved.Id == root,
        _ => false,
    };

    private OpenedPrefab? Read(string asset)
    {
        if (Instances.Usable(source, asset) is not { } prefab)
        {
            problems.Add(
                $"The prefab \"{asset}\" could not be read, so its instances are links with nothing in them until it can.");
            return null;
        }

        return new OpenedPrefab(prefab.Root, OpenAll(prefab.Document, null));
    }

    // What a folded instance, or one already open with its parts beside it, becomes. Null leaves the
    // entity as it is.
    private List<SceneEntity>? Opening(SceneDocument document, SceneEntity entity, IReadOnlySet<string>? only)
    {
        if (FoldedLink(entity, only) is not { } link)
        {
            return null;
        }

        // Parts already here mean it was written open and never folded. They are what it looked like,
        // so they are kept rather than doubled.
        if (document.Entities.Any(one => one.Id != entity.Id && EntityPath.Within(entity.Id, one.Id)))
        {
            return [entity.WithComponent(
                SceneComponents.Prefab,
                link.CopyWith(state: PrefabState.Open, overrides: SceneDiff.None))];
        }

        return Open(link.Asset!) is { } prefab ? Parts(entity, link, prefab) : null;
    }

    private List<SceneEntity> Parts(SceneEntity stub, PrefabComponent link, OpenedPrefab prefab)
    {
        var root = prefab.Root;
        var overrides = new SceneDiff([.. link.Overrides.Operations.Where(op => !TakesRootOut(op, root))]);
        ReportLost(stub, overrides, prefab.Document);

        var parts = new List<SceneEntity>();
        foreach (var entity in overrides.ApplyTo(prefab.Document).Entities)
        {
            parts.Add(entity.Id == root ? RootPart(stub, entity, link) : Part(stub, entity, root));
        }

        return parts;
    }

    private void ReportLost(SceneEntity stub, SceneDiff overrides, SceneDocument prefab)
    {
        var added = overrides.Operations.OfType<AddEntity>().Select(static op => op.Id).ToHashSet(StringComparer.Ordinal);
        var lost = overrides.Operations.Count(
            op => Target(op) is { } target && !prefab.Contains(target) && !added.Contains(target));
        if (lost == 0)
        {
            return;
        }

        problems.Add(lost == 1
            ? $"One change to \"{stub.Name}\" was to a part its prefab no longer has, and was let go."
            : $"{lost} changes to \"{stub.Name}\" were to parts its prefab no longer has, and were let go.");
    }

    /// <summary>
    /// <paramref name="document"/> with its stamped copies turned into open instances.
    /// <para>
    /// A copy's parts are paired with the prefab's by walking both trees in the same order, among the
    /// children that carry the copy's mark, which is how they were made, and so where the shape has
    /// not changed since, every part finds itself. A part the prefab no longer has, or a thing
    /// somebody added under the copy, becomes this document's own. A part of the prefab the copy no
    /// longer has is one it removed.
    /// </para>
    /// </summary>
    private SceneDocument Relink(SceneDocument document, IReadOnlySet<string>? only)
    {
        var plan = new RelinkPlan();
        foreach (var entity in document.Entities)
        {
            if (StampedRoot(document, entity, only) is { } link && Open(link.Asset!) is { } prefab)
            {
                plan.Add(document, entity, link.Asset!, prefab);
            }
        }

        if (plan.Opened.Count == 0)
        {
            return document;
        }

        foreach (var (asset, count) in plan.Counts)
        {
            problems.Add(count == 1
                ? $"An instance of \"{asset}\" was a copy of it, and is now linked to it. What was changed about it is kept."
                : $"{count} instances of \"{asset}\" were copies of it, and are now linked to it. What was changed about each is kept.");
        }

        return document.CopyWith(entities: [.. document.Entities.Select(plan.Applied)]);
    }

    // A copy that starts a stamped instance: not itself inside one stamped from the same prefab.
    private static PrefabComponent? StampedRoot(SceneDocument document, SceneEntity entity, IReadOnlySet<string>? only)
    {
        if (Instances.LinkOf(entity) is not { State: PrefabState.Stamped } link)
        {
            return null;
        }

        if (entity.Parent is { } parent && StampedFrom(document[parent], link.Asset!))
        {
            return null;
        }

        return only is null || only.Contains(entity.Id) ? link : null;
    }

    // The copy's parts and the prefab's, walked together, so each copy finds the part it was made
    // from.
    private sealed record Pairing(SceneDocument Scene, SceneDocument Prefab, string Asset)
    {
        public IEnumerable<(SceneEntity Copy, SceneEntity Part)> Walk(string mine, string theirs)
        {
            var ours = Scene.ChildrenOf(mine).Where(one => StampedFrom(one, Asset)).ToList();
            var parts = Prefab.ChildrenOf(theirs);
            for (var i = 0; i < ours.Count && i < parts.Count; i++)
            {
                yield return (ours[i], parts[i]);
                foreach (var deeper in Walk(ours[i].Id, parts[i].Id))
                {
                    yield return deeper;
                }
            }
        }
    }

    private sealed class RelinkPlan
    {
        private readonly Dictionary<string, string> _renamed = new(StringComparer.Ordinal);
        private readonly HashSet<string> _released = new(StringComparer.Ordinal);

        public HashSet<string> Opened { get; } = new(StringComparer.Ordinal);

        public OrderedDictionary<string, int> Counts { get; } = [];

        public void Add(SceneDocument document, SceneEntity entity, string asset, OpenedPrefab prefab)
        {
            var pairing = new Pairing(document, prefab.Document, asset);
            foreach (var (copy, part) in pairing.Walk(entity.Id, prefab.Root))
            {
                _renamed[copy.Id] = EntityPath.Join(entity.Id, part.Id);
            }

            Opened.Add(entity.Id);
            Counts[asset] = Counts.GetValueOrDefault(asset) + 1;
            foreach (var one in document.SubtreeOf(entity.Id))
            {
                if (one.Id != entity.Id && StampedFrom(one, asset))
                {
                    _released.Add(one.Id);
                }
            }
        }

        public SceneEntity Applied(SceneEntity entity)
        {
            if (Opened.Contains(entity.Id))
            {
                // Opened only holds ids of entities that carry a link.
                return entity.WithComponent(
                    SceneComponents.Prefab,
                    Instances.LinkOf(entity)!.CopyWith(state: PrefabState.Open));
            }

            var parent = Instances.RenamedOf(_renamed, entity.Parent);
            return _released.Contains(entity.Id)
                ? entity.WithComponent(SceneComponents.Prefab, null)
                    .CopyWith(id: Instances.RenamedOf(_renamed, entity.Id), parent: parent)
                : entity.CopyWith(parent: parent);
        }
    }
}
