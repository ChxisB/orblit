using System.Diagnostics;

namespace Orblit.Scene;

/// <summary>
/// One change to a document, addressed by id.
/// </summary>
/// <remarks>
/// By id and not by position, because a position is only true of the document it was measured in.
/// An undo stack full of "the fourth object" is an undo stack that corrupts a scene the moment
/// somebody deletes the third, and that is not a theoretical failure, it is the ordinary
/// consequence of two people editing one scene and merging.
/// <para>
/// Every operation knows its own <see cref="Inverse"/>. That is what makes a diff something an
/// editor can hold on an undo stack rather than a report: the same machinery that applies a change
/// undoes it, so there is no second implementation to disagree with the first.
/// </para>
/// </remarks>
public abstract class SceneOp
{
    private protected SceneOp()
    {
    }

    /// <summary>The operation that puts back what this one changed.</summary>
    public abstract SceneOp Inverse { get; }

    /// <summary>
    /// The document with this change made.
    /// <para>
    /// Returns a new document rather than mutating one. A diff is applied speculatively, to preview
    /// a merge, to check that an inverse really inverts, and a half-applied mutation of somebody's
    /// open scene is not a state worth being able to reach.
    /// </para>
    /// </summary>
    public abstract SceneDocument ApplyTo(SceneDocument document);

    public abstract JsonMap ToJson();

    /// <summary>One operation out of its JSON, or null when it is not one.</summary>
    public static SceneOp? FromJson(object? raw)
    {
        if (raw is not JsonMap map)
        {
            return null;
        }

        return Values.At(map, "op") switch
        {
            "add" => AddEntity.FromJson(map),
            "remove" => RemoveEntity.FromJson(map),
            "name" => SetEntityName.FromJson(map),
            "visible" => SetVisible.FromJson(map),
            "component" => SetComponent.FromJson(map),
            "field" => SetField.FromJson(map),
            "reparent" => Reparent.FromJson(map),
            "reorder" => Reorder.FromJson(map),
            "setting" => SetSetting.FromJson(map),
            _ => null,
        };
    }

    /// <summary>
    /// <paramref name="entities"/> with <paramref name="entity"/> put in immediately after
    /// <paramref name="after"/>.
    /// <para>
    /// At the head when <paramref name="after"/> is null, and at the end when it names something that
    /// is not there, which can only happen to a diff that has been edited by hand or merged, and
    /// losing the entity would be the worse answer.
    /// </para>
    /// </summary>
    private protected static List<SceneEntity> Inserted(IReadOnlyList<SceneEntity> entities, SceneEntity entity, string? after)
    {
        if (after is null)
        {
            return [entity, .. entities];
        }

        var next = new List<SceneEntity>();
        var placed = false;
        foreach (var one in entities)
        {
            next.Add(one);
            if (one.Id == after)
            {
                next.Add(entity);
                placed = true;
            }
        }

        if (!placed)
        {
            next.Add(entity);
        }

        return next;
    }
}

/// <summary>
/// Puts an entity in, immediately after <see cref="After"/> or at the head when it is null.
/// </summary>
/// <remarks>
/// Placed by neighbour rather than by index. An index is invalidated by every other operation in the
/// same diff; a neighbour is not, which is what lets the operations in a diff be applied in order
/// without each one having to know what the ones before it did to the numbering.
/// </remarks>
public sealed class AddEntity(JsonMap entity, string? after = null) : SceneOp
{
    /// <summary>The entity as JSON, so an operation is a value and can be stored.</summary>
    public JsonMap Entity { get; } = entity;

    public string? After { get; } = after;

    public string Id => Values.Text(Entity, "id") ?? string.Empty;

    public override SceneOp Inverse => new RemoveEntity(Entity, After);

    public static AddEntity FromJson(JsonMap json) =>
        new(Values.Object(Values.At(json, "entity")), Values.Text(json, "after"));

    public override SceneDocument ApplyTo(SceneDocument document)
    {
        var added = SceneEntity.FromJson(Entity);
        return added is null || document.Contains(added.Id)
            ? document
            : document.CopyWith(entities: Inserted(document.Entities, added, After));
    }

    public override JsonMap ToJson() => Values.Pruned(new OrderedDictionary<string, object?>
    {
        ["op"] = "add",
        ["entity"] = Entity,
        ["after"] = After,
    });
}

/// <summary>
/// Takes an entity out, remembering where it was so it can be put back.
/// </summary>
/// <remarks>
/// Children are not touched. A parent link that now names nothing is repaired when the document is
/// next read, and repairing it here would mean a remove and its inverse were not symmetrical,
/// undoing a delete would give back a differently shaped tree from the one that was deleted.
/// </remarks>
public sealed class RemoveEntity(JsonMap entity, string? after = null) : SceneOp
{
    public JsonMap Entity { get; } = entity;

    /// <summary>What it sat after, at the moment it was removed.</summary>
    public string? After { get; } = after;

    public string Id => Values.Text(Entity, "id") ?? string.Empty;

    public override SceneOp Inverse => new AddEntity(Entity, After);

    public static RemoveEntity FromJson(JsonMap json) =>
        new(Values.Object(Values.At(json, "entity")), Values.Text(json, "after"));

    public override SceneDocument ApplyTo(SceneDocument document) => document.WithEntity(Id, null);

    public override JsonMap ToJson() => Values.Pruned(new OrderedDictionary<string, object?>
    {
        ["op"] = "remove",
        ["entity"] = Entity,
        ["after"] = After,
    });
}

/// <summary>Renames an entity.</summary>
public sealed class SetEntityName(string id, string from, string to) : SceneOp, IEntityEdit
{
    public string Id { get; } = id;

    public string From { get; } = from;

    public string To { get; } = to;

    public override SceneOp Inverse => new SetEntityName(Id, To, From);

    public static SetEntityName FromJson(JsonMap json) => new(
        Values.Text(json, "id") ?? string.Empty,
        Values.Text(json, "from") ?? string.Empty,
        Values.Text(json, "to") ?? string.Empty);

    public override SceneDocument ApplyTo(SceneDocument document) =>
        document[Id] is { } entity ? document.WithEntity(Id, entity.CopyWith(name: To)) : document;

    public override JsonMap ToJson() => new OrderedDictionary<string, object?>
    {
        ["op"] = "name",
        ["id"] = Id,
        ["from"] = From,
        ["to"] = To,
    };
}

/// <summary>Shows or hides an entity.</summary>
public sealed class SetVisible(string id, bool from, bool to) : SceneOp, IEntityEdit
{
    public string Id { get; } = id;

    public bool From { get; } = from;

    public bool To { get; } = to;

    public override SceneOp Inverse => new SetVisible(Id, To, From);

    public static SetVisible FromJson(JsonMap json) => new(
        Values.Text(json, "id") ?? string.Empty,
        Values.Flag(json, "from", true),
        Values.Flag(json, "to", true));

    public override SceneDocument ApplyTo(SceneDocument document) =>
        document[Id] is { } entity ? document.WithEntity(Id, entity.CopyWith(visible: To)) : document;

    public override JsonMap ToJson() => new OrderedDictionary<string, object?>
    {
        ["op"] = "visible",
        ["id"] = Id,
        ["from"] = From,
        ["to"] = To,
    };
}

/// <summary>
/// Gives an entity a component, takes one away, or replaces one wholesale.
/// </summary>
/// <remarks>
/// A null on either side is the absence of the component, which is why this and
/// <see cref="SetField"/> are separate operations: "the light went away" and "the light got dimmer"
/// are different edits, and a diff that could not tell them apart would undo the first by inventing
/// a light with default values.
/// </remarks>
public sealed class SetComponent(string id, string type, JsonMap? from, JsonMap? to) : SceneOp, IEntityEdit
{
    public string Id { get; } = id;

    public string Type { get; } = type;

    public JsonMap? From { get; } = from;

    public JsonMap? To { get; } = to;

    public override SceneOp Inverse => new SetComponent(Id, Type, To, From);

    public static SetComponent FromJson(JsonMap json) => new(
        Values.Text(json, "id") ?? string.Empty,
        Values.Text(json, "type") ?? string.Empty,
        Values.At(json, "from") as JsonMap,
        Values.At(json, "to") as JsonMap);

    public override SceneDocument ApplyTo(SceneDocument document)
    {
        if (document[Id] is not { } entity)
        {
            return document;
        }

        var next = To is null ? null : SceneComponents.Read(Type, To);
        return document.WithEntity(Id, entity.WithComponent(Type, next));
    }

    public override JsonMap ToJson() => Values.Pruned(new OrderedDictionary<string, object?>
    {
        ["op"] = "component",
        ["id"] = Id,
        ["type"] = Type,
        ["from"] = From,
        ["to"] = To,
    });
}

/// <summary>
/// Changes one field of one component.
/// </summary>
/// <remarks>
/// The ordinary edit, and the reason a diff of a moved object is four lines rather than the whole
/// object. A null on either side means the field was not written, which for a component that prunes
/// its unset fields is the same thing as not having one.
/// </remarks>
// F2: the five things that name one edit, called by name.
public sealed class SetField(string id, string type, string field, object? from, object? to) : SceneOp, IEntityEdit
{
    public string Id { get; } = id;

    public string Type { get; } = type;

    public string Field { get; } = field;

    public object? From { get; } = from;

    public object? To { get; } = to;

    public override SceneOp Inverse => new SetField(Id, Type, Field, To, From);

    public static SetField FromJson(JsonMap json) => new(
        Values.Text(json, "id") ?? string.Empty,
        Values.Text(json, "type") ?? string.Empty,
        Values.Text(json, "field") ?? string.Empty,
        Values.At(json, "from"),
        Values.At(json, "to"));

    public override SceneDocument ApplyTo(SceneDocument document)
    {
        var entity = document[Id];
        var component = entity?[Type];
        if (entity is null || component is null)
        {
            return document;
        }

        var json = new OrderedDictionary<string, object?>(component.ToJson());
        if (To is null)
        {
            json.Remove(Field);
        }
        else
        {
            json[Field] = To;
        }

        return document.WithEntity(Id, entity.WithComponent(Type, SceneComponents.Read(Type, json)));
    }

    public override JsonMap ToJson() => Values.Pruned(new OrderedDictionary<string, object?>
    {
        ["op"] = "field",
        ["id"] = Id,
        ["type"] = Type,
        ["field"] = Field,
        ["from"] = From,
        ["to"] = To,
    });
}

/// <summary>Hangs an entity from a different parent, or from none.</summary>
public sealed class Reparent(string id, string? from, string? to) : SceneOp, IEntityEdit
{
    public string Id { get; } = id;

    public string? From { get; } = from;

    public string? To { get; } = to;

    public override SceneOp Inverse => new Reparent(Id, To, From);

    public static Reparent FromJson(JsonMap json) =>
        new(Values.Text(json, "id") ?? string.Empty, Values.Text(json, "from"), Values.Text(json, "to"));

    public override SceneDocument ApplyTo(SceneDocument document)
    {
        if (document[Id] is not { } entity)
        {
            return document;
        }

        return document.WithEntity(Id, To is null ? entity.CopyWith(clearParent: true) : entity.CopyWith(parent: To));
    }

    public override JsonMap ToJson() => Values.Pruned(new OrderedDictionary<string, object?>
    {
        ["op"] = "reparent",
        ["id"] = Id,
        ["from"] = From,
        ["to"] = To,
    });
}

/// <summary>
/// Moves an entity along the document's order.
/// </summary>
/// <remarks>
/// Which is what changes the order it draws and lists in, among its siblings and everywhere else.
/// Stated as the neighbour it now sits after and the one it sat after before, so the operation is
/// exactly invertible.
/// </remarks>
public sealed class Reorder(string id, string? from, string? to) : SceneOp
{
    public string Id { get; } = id;

    /// <summary>What it sat after before. Null is the head of the document.</summary>
    public string? From { get; } = from;

    /// <summary>What it sits after now. Null is the head of the document.</summary>
    public string? To { get; } = to;

    public override SceneOp Inverse => new Reorder(Id, To, From);

    public static Reorder FromJson(JsonMap json) =>
        new(Values.Text(json, "id") ?? string.Empty, Values.Text(json, "from"), Values.Text(json, "to"));

    public override SceneDocument ApplyTo(SceneDocument document)
    {
        if (document[Id] is not { } entity)
        {
            return document;
        }

        var without = document.Entities.Where(one => one.Id != Id).ToList();
        return document.CopyWith(entities: Inserted(without, entity, To));
    }

    public override JsonMap ToJson() => Values.Pruned(new OrderedDictionary<string, object?>
    {
        ["op"] = "reorder",
        ["id"] = Id,
        ["from"] = From,
        ["to"] = To,
    });
}

/// <summary>Changes one of the scene's own settings, or its name.</summary>
public sealed class SetSetting(string field, object? from, object? to) : SceneOp
{
    public const string Name = "name";
    public const string Sky = "sky";
    public const string Ambient = "ambient";
    public const string Hour = "hour";
    public const string Cycle = "cycle";
    public const string HoursPerSecond = "hoursPerSecond";
    public const string LayerNames = "layerNames";

    public string Field { get; } = field;

    public object? From { get; } = from;

    public object? To { get; } = to;

    public override SceneOp Inverse => new SetSetting(Field, To, From);

    public static SetSetting FromJson(JsonMap json) =>
        new(Values.Text(json, "field") ?? string.Empty, Values.At(json, "from"), Values.At(json, "to"));

    public override SceneDocument ApplyTo(SceneDocument document)
    {
        if (Field == Name)
        {
            return document.CopyWith(name: To is string wanted ? wanted : document.Name);
        }

        var settings = new OrderedDictionary<string, object?>(document.Settings.ToJson());
        var time = new OrderedDictionary<string, object?>(Values.Object(settings["time"]));
        switch (Field)
        {
            case Sky or Ambient or LayerNames:
                settings[Field] = To;
                break;
            case Hour or Cycle or HoursPerSecond:
                time[Field] = To;
                settings["time"] = time;
                break;
            default:
                return document;
        }

        return document.CopyWith(settings: SceneSettings.FromJson(settings));
    }

    public override JsonMap ToJson() => Values.Pruned(new OrderedDictionary<string, object?>
    {
        ["op"] = "setting",
        ["field"] = Field,
        ["from"] = From,
        ["to"] = To,
    });
}

/// <summary>
/// An operation that changes the one entity it names, and nothing else of the document: not the
/// order, not another entity, not the settings.
/// </summary>
internal interface IEntityEdit
{
    string Id { get; }
}

/// <summary>
/// What changed between two documents.
/// </summary>
/// <remarks>
/// The unit an editor's undo stack stores and the unit a renderer updates from: a diff says what
/// moved, so a scene with four thousand things in it can be brought up to date by touching the one
/// that changed rather than being rebuilt.
/// </remarks>
public sealed class SceneDiff(IReadOnlyList<SceneOp> operations)
{
    public static readonly SceneDiff None = new([]);

    public IReadOnlyList<SceneOp> Operations { get; } = operations;

    public bool IsEmpty => Operations.Count == 0;

    public bool IsNotEmpty => Operations.Count > 0;

    /// <summary>
    /// The diff that undoes this one.
    /// <para>
    /// Reversed as well as inverted, and both halves are needed. Inverting each operation alone would
    /// undo a move before undoing the deletion that happened after it, and arrive somewhere neither
    /// document has ever been.
    /// </para>
    /// </summary>
    public SceneDiff Inverse => new([.. Operations.Reverse().Select(op => op.Inverse)]);

    public static SceneDiff FromJson(object? raw) =>
        new(raw is JsonList list ? [.. list.Select(SceneOp.FromJson).OfType<SceneOp>()] : []);

    /// <summary>
    /// Every change that turns <paramref name="a"/> into <paramref name="b"/>.
    /// <para>
    /// Built by applying each operation to a running copy as it is worked out, rather than by
    /// reasoning about what the operations will do to each other's positions. That is slower and it
    /// is the reason the result is right: an operation that places something by neighbour has to be
    /// measured against the document as it will actually be when that operation runs, and the
    /// cheapest way to know that is to have it in front of you.
    /// </para>
    /// </summary>
    public static SceneDiff Between(SceneDocument a, SceneDocument b)
    {
        var replay = new Replay(a);

        Settings(a, b, replay);

        foreach (var entity in a.Entities.Where(entity => !b.Contains(entity.Id)))
        {
            replay.Run(new RemoveEntity(entity.ToJson(), After(replay.Running, entity.Id)));
        }

        foreach (var entity in b.Entities.Where(entity => !a.Contains(entity.Id)))
        {
            var at = b.IndexOf(entity.Id);
            var before = at > 0 ? b.Entities[at - 1].Id : null;
            replay.Run(new AddEntity(entity.ToJson(), before is not null && replay.Running.Contains(before) ? before : null));
        }

        foreach (var was in a.Entities)
        {
            if (b[was.Id] is { } now)
            {
                Changes(was, now, replay);
            }
        }

        Reordered(b, replay);

        return new SceneDiff(replay.Operations);
    }

    /// <summary>
    /// <paramref name="document"/> with every operation applied, in order.
    /// <para>
    /// A run of adds, of removes, or of edits to entities is applied in one pass over the document.
    /// One operation at a time copies every entity for each, which is how a hundred entities
    /// streamed into a scene of three thousand cost a third of a million copies, and how two hundred
    /// moves in a frame of a game cost twenty milliseconds.
    /// </para>
    /// </summary>
    public SceneDocument ApplyTo(SceneDocument document)
    {
        var current = document;
        var at = 0;
        while (at < Operations.Count)
        {
            var first = Operations[at];
            var end = RunEnd(at);
            var run = Operations.Skip(at).Take(end - at);
            current = end - at < 2 ? first.ApplyTo(current) : AppliedAll(current, first, run);
            at = end;
        }

        return current;
    }

    public IReadOnlyList<object?> ToJson() => [.. Operations.Select(op => (object?)op.ToJson())];

    // Order last, once every entity that belongs in the result is in it. Each step puts one entity
    // where it goes, so at worst there is one operation per entity and usually there are none.
    private static void Reordered(SceneDocument b, Replay replay)
    {
        for (var i = 0; i < b.Entities.Count; i++)
        {
            var running = replay.Running.Entities;
            if (i < running.Count && running[i].Id == b.Entities[i].Id)
            {
                continue;
            }

            var id = b.Entities[i].Id;
            replay.Run(new Reorder(id, After(replay.Running, id), i > 0 ? b.Entities[i - 1].Id : null));
        }
    }

    private static void Settings(SceneDocument a, SceneDocument b, Replay replay)
    {
        if (a.Name != b.Name)
        {
            replay.Run(new SetSetting(SetSetting.Name, a.Name, b.Name));
        }

        var was = a.Settings.ToJson();
        var now = b.Settings.ToJson();
        foreach (var field in new[] { SetSetting.Sky, SetSetting.Ambient, SetSetting.LayerNames })
        {
            Differs(field, was, now, replay);
        }

        var wasTime = Values.Object(was["time"]);
        var nowTime = Values.Object(now["time"]);
        foreach (var field in new[] { SetSetting.Hour, SetSetting.Cycle, SetSetting.HoursPerSecond })
        {
            Differs(field, wasTime, nowTime, replay);
        }
    }

    private static void Differs(string field, JsonMap was, JsonMap now, Replay replay)
    {
        var before = was.GetValueOrDefault(field);
        var after = now.GetValueOrDefault(field);
        if (!Values.Same(before, after))
        {
            replay.Run(new SetSetting(field, before, after));
        }
    }

    private static void Changes(SceneEntity was, SceneEntity now, Replay replay)
    {
        if (was.Name != now.Name)
        {
            replay.Run(new SetEntityName(was.Id, was.Name, now.Name));
        }

        if (was.Visible != now.Visible)
        {
            replay.Run(new SetVisible(was.Id, was.Visible, now.Visible));
        }

        foreach (var type in was.Components.Keys.Union(now.Components.Keys))
        {
            ComponentChanges(was.Id, type, was[type]?.ToJson(), now[type]?.ToJson(), replay);
        }

        if (was.Parent != now.Parent)
        {
            replay.Run(new Reparent(was.Id, was.Parent, now.Parent));
        }
    }

    private static void ComponentChanges(string id, string type, JsonMap? before, JsonMap? after, Replay replay)
    {
        if (before is null || after is null)
        {
            if (!Values.Same(before, after))
            {
                replay.Run(new SetComponent(id, type, before, after));
            }

            return;
        }

        foreach (var field in before.Keys.Union(after.Keys))
        {
            var from = before.GetValueOrDefault(field);
            var to = after.GetValueOrDefault(field);
            if (!Values.Same(from, to))
            {
                replay.Run(new SetField(id, type, field, from, to));
            }
        }
    }

    /// <summary>What <paramref name="id"/> currently sits after, or null when it is at the head.</summary>
    private static string? After(SceneDocument document, string id)
    {
        var at = document.IndexOf(id);
        return at > 0 ? document.Entities[at - 1].Id : null;
    }

    /// <summary>
    /// What <paramref name="adds"/> do to <paramref name="document"/> one after another, in one pass.
    /// <para>
    /// The entities are linked so that putting one after another is a lookup. The rules are those of
    /// <see cref="AddEntity.ApplyTo"/>: an entity already there, or one that does not read, is
    /// skipped, and one placed after something missing goes to the end.
    /// </para>
    /// </summary>
    private static SceneDocument AddedAll(SceneDocument document, IEnumerable<SceneOp> adds)
    {
        var head = new Link(null);
        var byId = new Dictionary<string, Link>();
        var tail = head;
        foreach (var entity in document.Entities)
        {
            var link = new Link(entity);
            byId[entity.Id] = link;
            tail.Next = link;
            tail = link;
        }

        foreach (var add in adds.Cast<AddEntity>())
        {
            if (SceneEntity.FromJson(add.Entity) is not { } entity || byId.ContainsKey(entity.Id))
            {
                continue;
            }

            var before = add.After is null ? head : byId.GetValueOrDefault(add.After) ?? tail;
            var link = new Link(entity) { Next = before.Next };
            before.Next = link;
            byId[entity.Id] = link;
            if (before == tail)
            {
                tail = link;
            }
        }

        var entities = new List<SceneEntity>();
        for (var link = head.Next; link is not null; link = link.Next)
        {
            if (link.Entity is { } entity)
            {
                entities.Add(entity);
            }
        }

        return document.CopyWith(entities: entities);
    }

    private static SceneDocument AppliedAll(SceneDocument document, SceneOp first, IEnumerable<SceneOp> run) => first switch
    {
        AddEntity => AddedAll(document, run),
        RemoveEntity => RemovedAll(document, run),
        _ => EditedAll(document, run),
    };

    /// <summary>
    /// What <paramref name="edits"/> do to <paramref name="document"/> one after another, in one pass.
    /// <para>
    /// Each runs on a document of the one entity it names, as the edits before it left that entity,
    /// so the operations are the ones that apply a single edit and there is no second copy of what
    /// they do. None of them touches another entity or the order, which is what lets every entity go
    /// back in a single copy.
    /// </para>
    /// </summary>
    private static SceneDocument EditedAll(SceneDocument document, IEnumerable<SceneOp> edits)
    {
        var edited = new Dictionary<string, SceneEntity>();
        foreach (var edit in edits)
        {
            var id = ((IEntityEdit)edit).Id;
            if ((edited.GetValueOrDefault(id) ?? document[id]) is { } entity)
            {
                edited[id] = edit.ApplyTo(new SceneDocument(entities: [entity])).Entities[0];
            }
        }

        return document.CopyWith(entities: [.. document.Entities.Select(entity => edited.GetValueOrDefault(entity.Id) ?? entity)]);
    }

    /// <summary>What <paramref name="removes"/> do to <paramref name="document"/> one after another, in one pass.</summary>
    private static SceneDocument RemovedAll(SceneDocument document, IEnumerable<SceneOp> removes)
    {
        var gone = removes.Cast<RemoveEntity>().Select(remove => remove.Id).ToHashSet();
        return document.CopyWith(entities: [.. document.Entities.Where(entity => !gone.Contains(entity.Id))]);
    }

    /// <summary>Where the run that starts at <paramref name="from"/> stops, or the next index for an operation that is not in one.</summary>
    private int RunEnd(int from)
    {
        var end = from + 1;
        while (end < Operations.Count && Runs(Operations[from], Operations[end]))
        {
            end++;
        }

        return end;
    }

    // Adds run with adds and removes with removes. Edits run with each other, whichever they are.
    private static bool Runs(SceneOp first, SceneOp next) => first switch
    {
        AddEntity or RemoveEntity => first.GetType() == next.GetType(),
        IEntityEdit => next is IEntityEdit,
        _ => false,
    };

    private sealed class Link(SceneEntity? entity)
    {
        public SceneEntity? Entity { get; } = entity;

        public Link? Next { get; set; }
    }

    private sealed class Replay(SceneDocument start)
    {
        public List<SceneOp> Operations { get; } = [];

        public SceneDocument Running { get; private set; } = start;

        public void Run(SceneOp op)
        {
            Operations.Add(op);
            Running = op.ApplyTo(Running);
        }
    }
}
