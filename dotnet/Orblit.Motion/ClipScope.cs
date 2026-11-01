using Orblit.Scene;

namespace Orblit.Motion;

/// <summary>
/// Where a clip's targets are, for one entity playing it.
/// </summary>
/// <remarks>
/// A clip names what it moves in the ids of the document it was made in, with the entity playing it as the
/// empty target. Played on an instance of a prefab, that document is the prefab, and its ids are found in the
/// scene under the instance's path: <c>bulb</c> in a lamp's clip is <c>street1/lamp3/bulb</c> for the lamp
/// placed as <c>street1/lamp3</c>. Played on something that is not in any instance, the ids are the scene's
/// own.
/// </remarks>
public sealed class ClipScope(string owner, string? instance = null, string? root = null)
{
    /// <summary>
    /// Where a cutscene's targets are: the scene's own ids, since a cutscene is played by the scene rather
    /// than by anything in it.
    /// </summary>
    public static ClipScope WholeScene { get; } = new(string.Empty);

    /// <summary>The entity playing the clip.</summary>
    public string Owner { get; } = owner;

    /// <summary>The instance whose prefab the clip's ids are in, or null when they are the scene's own.</summary>
    public string? Instance { get; } = instance;

    /// <summary>
    /// The id the prefab gives its own root, when it is known. Only needed for a clip that names the prefab's
    /// root by id rather than as the owner, which one made by a part of the prefab can.
    /// </summary>
    public string? Root { get; } = root;

    /// <summary>The scope of <paramref name="owner"/> as <paramref name="document"/> holds it.</summary>
    /// <remarks>
    /// An instance's root plays in its prefab's ids, a part of an instance in the ids of the prefab it is a
    /// part of, and anything else in the document's.
    /// </remarks>
    public static ClipScope InScene(SceneDocument document, string owner)
    {
        if (document[owner]?[SceneComponents.Prefab] is not null)
        {
            return new ClipScope(owner, instance: owner);
        }

        var enclosing = EntityPath.Enclosing(owner);
        return new ClipScope(owner, instance: enclosing.Count == 0 ? null : enclosing[0]);
    }

    /// <summary>The id in the scene of <paramref name="target"/>.</summary>
    public string Resolve(string target)
    {
        if (target.Length == 0)
        {
            return Owner;
        }

        if (Instance is null)
        {
            return target;
        }

        return target == Root ? Instance : EntityPath.Join(Instance, target);
    }

    /// <summary>
    /// <paramref name="id"/> as a target of this scope, or null when a clip played here has no way to name
    /// it: something outside the instance it plays in.
    /// </summary>
    public string? TargetOf(string id)
    {
        if (id == Owner)
        {
            return string.Empty;
        }

        if (Instance is null)
        {
            return id;
        }

        return id == Instance ? Root : EntityPath.LocalTo(Instance, id, root: string.Empty);
    }
}

/// <summary>
/// A frame of a clip, as changes to a scene.
/// </summary>
public static class ClipScene
{
    /// <summary>
    /// The changes that make <paramref name="document"/> look the way <paramref name="frame"/> says.
    /// </summary>
    /// <remarks>
    /// One <see cref="SetField"/> a property, from what the scene has to what the frame has, so the list is
    /// its own undo and a preview can be put back exactly. Nothing for a property already at its value, nor
    /// for an entity or a component the scene does not have: a clip made for a lamp with a flame, played on
    /// one without, moves what is there. Bones are left out. A skeleton is the model's, and is posed by
    /// whoever draws it, not written into the scene.
    /// </remarks>
    public static IReadOnlyList<SetField> OpsFor(ClipFrame frame, SceneDocument document, ClipScope scope)
    {
        var ops = new List<SetField>();
        foreach (var (target, properties) in frame.Values)
        {
            var id = scope.Resolve(target);
            if (document[id] is { } entity)
            {
                ops.AddRange(OpsOn(id, entity, properties));
            }
        }

        return ops;
    }

    private static IEnumerable<SetField> OpsOn(string id, SceneEntity entity, IReadOnlyDictionary<string, object> properties)
    {
        foreach (var (property, value) in properties)
        {
            var dot = property.IndexOf('.', StringComparison.Ordinal);
            if (dot <= 0 || entity[property[..dot]] is not { } component)
            {
                continue;
            }

            var field = property[(dot + 1)..];
            var from = component.ToJson().GetValueOrDefault(field);
            var to = Json(property, value);
            if (!Values.Same(from, to))
            {
                yield return new SetField(id, property[..dot], field, from, to);
            }
        }
    }

    /// <summary>
    /// <paramref name="value"/> as the scene writes it.
    /// </summary>
    /// <remarks>
    /// A rotation onto a transform becomes the transform's degrees, because a transform is written in
    /// degrees; anywhere else it is four numbers, the way a clip writes one.
    /// </remarks>
    private static object Json(string property, object value) => value switch
    {
        Quaternion rotation when property == "transform.rotation" =>
            Values.VectorToJson(TransformComponent.AnglesOf(rotation.AsRotationMatrix())),
        Quaternion rotation => new List<object?> { rotation.X, rotation.Y, rotation.Z, rotation.W },
        Vector3 vector => Values.VectorToJson(vector),
        _ => value,
    };
}
