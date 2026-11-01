using Orblit.Scene;

namespace Orblit.Test;

/// <summary>Finding one entity of a scene for a test, and saying what is there when it is not found.</summary>
public static class SceneFinders
{
    /// <summary>The one entity called <paramref name="name"/>. Fails for none and for several.</summary>
    public static SceneEntity EntityNamed(this SceneDocument scene, string name)
    {
        var found = scene.Entities.Where(entity => entity.Name == name).ToList();
        if (found.Count == 1)
        {
            return found[0];
        }

        var names = string.Join(", ", scene.Entities.Select(entity => $"\"{entity.Name}\""));
        throw new InvalidOperationException(found.Count == 0
            ? $"No entity is called \"{name}\". The names are {names}."
            : $"{found.Count} entities are called \"{name}\": {string.Join(", ", found.Select(entity => entity.Id))}.");
    }

    /// <summary>
    /// The entity whose id is <paramref name="path"/>. An entity inside a prefab instance has its
    /// path as its id, <c>street1/lamp3/bulb</c>, once the instance is opened.
    /// </summary>
    public static SceneEntity EntityAt(this SceneDocument scene, string path) =>
        scene[path] ?? throw new InvalidOperationException($"No entity has the id \"{path}\".");
}
