using Orblit.Light;
using Orblit.Scene;
using Orblit.VectorMath;
using Orblit.Weather;
using static Orblit.Stage.Tests.Documents;

namespace Orblit.Stage.Tests;

// Documents with every kind of part in random places, so a view moved by a diff can be held to what a
// view built from scratch shows. The same seed always gives the same document.
internal static class RandomDocuments
{
    public static SceneDocument Of(Random random, int size)
    {
        var entities = new List<SceneEntity>();
        foreach (var _ in Enumerable.Range(0, size))
        {
            var id = $"e{random.Next(size * 2)}";
            if (entities.Any(one => one.Id == id))
            {
                continue;
            }

            var parent = entities.Count == 0 || random.Next(2) == 0 ? null : entities[random.Next(entities.Count)].Id;
            entities.Add(new SceneEntity(id, id, parent, random.Next(4) != 0, PartsOf(random)));
        }

        return new SceneDocument(
            settings: new SceneSettings(Tint.Hex(random.Next(0xFFFFFF)), random.Next(5) * 1000),
            entities: entities);
    }

    private static OrderedDictionary<string, SceneComponent> PartsOf(Random random)
    {
        var parts = new OrderedDictionary<string, SceneComponent>();
        if (random.Next(4) != 0)
        {
            parts[SceneComponents.Transform] = Placement(random);
        }

        if (random.Next(2) == 0)
        {
            parts[SceneComponents.Mesh] = new MeshComponent(
                random.Next(2) == 0 ? $"m{random.Next(3)}.glb" : null,
                colour: Tint.Hex(random.Next(0xFFFFFF)),
                castShadows: random.Next(2) == 0);
        }

        if (random.Next(3) == 0)
        {
            parts[SceneComponents.Light] = new LightComponent(power: random.Next(500));
        }

        AddFlatParts(random, parts);
        return parts;
    }

    private static void AddFlatParts(Random random, OrderedDictionary<string, SceneComponent> parts)
    {
        if (random.Next(4) == 0)
        {
            parts[SceneComponents.Sprite] = new SpriteComponent(depth: random.Next(4));
        }

        if (random.Next(5) == 0)
        {
            parts[SceneComponents.Camera] = new CameraComponent(30 + random.Next(40));
        }

        if (random.Next(6) == 0)
        {
            var conditions = Enum.GetValues<WeatherCondition>();
            var condition = conditions[random.Next(conditions.Length)];
            parts[SceneComponents.Weather] = new WeatherComponent(WeatherState.Of(condition), condition);
        }
    }

    private static TransformComponent Placement(Random random) => new(
        new Vector3(random.Next(5), random.Next(5), random.Next(5)),
        new Vector3(0, random.Next(4) * 90, 0),
        Vector3.All(random.Next(3) + 1));
}
