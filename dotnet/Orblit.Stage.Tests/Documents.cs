using Orblit.Scene;
using Orblit.VectorMath;

namespace Orblit.Stage.Tests;

// The documents the view tests build, kept in one place because four files read them.
internal static class Documents
{
    public static OrderedDictionary<string, SceneComponent> Parts(params (string Type, SceneComponent Component)[] parts)
    {
        var made = new OrderedDictionary<string, SceneComponent>();
        foreach (var (type, component) in parts)
        {
            made[type] = component;
        }

        return made;
    }

    public static SceneEntity Thing(
        string id,
        string? parent = null,
        bool visible = true,
        IReadOnlyDictionary<string, SceneComponent>? components = null) => new(id, id, parent, visible, components);

    public static SceneEntity Crate(string id, string? parent = null, Vector3? at = null) => Thing(
        id,
        parent,
        components: Parts(
            (SceneComponents.Transform, new TransformComponent(position: at)),
            (SceneComponents.Mesh, new MeshComponent())));

    public static SceneDocument Of(params SceneEntity[] entities) => new(entities: entities);
}
