using Orblit.Scene;
using Orblit.VectorMath;

namespace Orblit.Test.Tests;

internal static class Things
{
    /// <summary>An entity with a transform when <paramref name="at"/> is given, and the other components after it.</summary>
    internal static SceneEntity Make(
        string id,
        string? name = null,
        Vector3? at = null,
        IReadOnlyDictionary<string, SceneComponent>? components = null)
    {
        var held = new OrderedDictionary<string, SceneComponent>();
        if (at is not null)
        {
            held[SceneComponents.Transform] = new TransformComponent(position: at);
        }

        foreach (var (type, component) in components ?? new Dictionary<string, SceneComponent>())
        {
            held[type] = component;
        }

        return new SceneEntity(id, name ?? id, components: held);
    }
}
