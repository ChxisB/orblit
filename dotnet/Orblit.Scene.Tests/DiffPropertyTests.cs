namespace Orblit.Scene.Tests;

/// <summary>
/// Documents nobody would think to write by hand.
/// <para>
/// The worked examples in <see cref="SceneDiffTests"/> check the cases somebody thought of. This
/// checks the ones nobody did: two hundred pairs of scenes that differ in every way at once, each of
/// which has to survive being turned into a diff and back. A diff is what an undo stack is made of,
/// and an undo that is right for the edits the author imagined is not worth having.
/// </para>
/// <para>
/// Seeded, so a failure is a failure somebody else can reproduce rather than a story about a build
/// that went red once.
/// </para>
/// </summary>
public class DiffPropertyTests
{
    private const int Seeds = 200;

    [Fact]
    public void AnyTwoDocumentsDiffApplyAndInvert()
    {
        for (var seed = 0; seed < Seeds; seed++)
        {
            var (a, b) = Pair(seed);

            var diff = SceneDiff.Between(a, b);
            var forward = diff.ApplyTo(a);
            Assert.True(b.Encode() == forward.Encode(), $"seed {seed}: the diff did not arrive at b");
            Assert.True(
                a.Encode() == diff.Inverse.ApplyTo(forward).Encode(),
                $"seed {seed}: the inverse did not get back to a");
        }
    }

    [Fact]
    public void ADiffStoredAsJsonDoesTheSameAsTheOneThatMadeIt()
    {
        for (var seed = 0; seed < Seeds; seed++)
        {
            var (a, b) = Pair(seed);

            var stored = SceneDiff.FromJson(SceneDiff.Between(a, b).ToJson());
            Assert.True(b.Encode() == stored.ApplyTo(a).Encode(), $"seed {seed}");
            Assert.True(
                a.Encode() == stored.Inverse.ApplyTo(stored.ApplyTo(a)).Encode(),
                $"seed {seed}");
        }
    }

    private static (SceneDocument A, SceneDocument B) Pair(int seed)
    {
        var random = new Random(seed);
        var a = RandomDocument(random, 1 + random.Next(8));
        var b = RandomDocument(random, 1 + random.Next(8));
        return (a, b);
    }

    private static bool Coin(Random random) => random.Next(2) == 0;

    private static SceneDocument RandomDocument(Random random, int size)
    {
        var entities = new List<SceneEntity>();
        var ids = new List<string>();

        for (var i = 0; i < size; i++)
        {
            var id = $"e{random.Next(size * 2)}";
            if (ids.Contains(id))
            {
                continue;
            }

            // Parented only to something already placed, so the generator cannot invent a loop. What a
            // loop does to a document is decode's problem and it has its own tests.
            var parent = ids.Count == 0 || Coin(random) ? null : ids[random.Next(ids.Count)];

            ids.Add(id);
            entities.Add(new SceneEntity(
                id,
                $"name{random.Next(3)}",
                parent,
                visible: random.Next(4) != 0,
                components: RandomComponents(random)));
        }

        return new SceneDocument(
            $"scene{random.Next(2)}",
            new SceneSettings(
                ambient: random.Next(5) * 1000,
                timeOfDay: random.Next(24),
                dayCycle: Coin(random)),
            entities);
    }

    private static OrderedDictionary<string, SceneComponent> RandomComponents(Random random)
    {
        var components = new OrderedDictionary<string, SceneComponent>();
        if (Coin(random))
        {
            components[SceneComponents.Transform] = new TransformComponent(
                position: new Vector3(random.Next(5), random.Next(5), random.Next(5)),
                scale: Vector3.All(random.Next(3) + 1));
        }

        if (Coin(random))
        {
            components[SceneComponents.Mesh] = new MeshComponent(
                asset: Coin(random) ? $"m{random.Next(3)}.glb" : null,
                castShadows: Coin(random),
                sway: random.Next(3) / 2.0);
        }

        if (Coin(random))
        {
            components[SceneComponents.Light] = new LightComponent(power: random.Next(500));
        }

        if (Coin(random))
        {
            components[SceneComponents.Sprite] = new SpriteComponent(depth: random.Next(4));
        }

        if (Coin(random))
        {
            var shapes = Enum.GetValues<BodyShape>();
            var motions = Enum.GetValues<BodyMotion>();
            components[SceneComponents.Body] = new BodyComponent(
                shape: shapes[random.Next(shapes.Length)],
                motion: motions[random.Next(motions.Length)],
                mass: random.Next(4) + 1);
        }

        if (random.Next(5) == 0)
        {
            components["unheardof"] = new UnknownComponent("unheardof", Tree.Map(("value", (long)random.Next(9))));
        }

        return components;
    }
}
