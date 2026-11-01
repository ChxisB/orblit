namespace Orblit.Scene.Tests;

/// <summary>
/// A run of adds or removes is applied in one pass, which has to land on the document the same
/// operations give one at a time.
/// </summary>
public class DiffRunTests
{
    [Fact]
    public void AddsInAChainKeepTheirOrderAndSitAtTheHead()
    {
        var diff = new SceneDiff([
            new AddEntity(Entity("a")),
            new AddEntity(Entity("b"), after: "a"),
            new AddEntity(Entity("c"), after: "b"),
        ]);

        Assert.Equal(["a", "b", "c", "x", "y"], Ids(diff.ApplyTo(World("x", "y"))));
    }

    [Fact]
    public void AnAddAfterSomethingMissingGoesToTheEnd()
    {
        var diff = new SceneDiff([
            new AddEntity(Entity("a"), after: "nothing"),
            new AddEntity(Entity("b"), after: "nothing"),
        ]);

        Assert.Equal(["x", "a", "b"], Ids(diff.ApplyTo(World("x"))));
    }

    [Fact]
    public void AnAddOfAnIdThatIsThereIsLeftAlone()
    {
        var diff = new SceneDiff([
            new AddEntity(Entity("x", parent: "y")),
            new AddEntity(Entity("a")),
            new AddEntity(Entity("a")),
        ]);

        var result = diff.ApplyTo(World("x"));

        Assert.Equal(["a", "x"], Ids(result));
        Assert.Null(result["x"]!.Parent);
    }

    [Fact]
    public void RemovesTakeOutOnlyWhatTheyName()
    {
        var diff = new SceneDiff([
            new RemoveEntity(Entity("a")),
            new RemoveEntity(Entity("missing")),
            new RemoveEntity(Entity("c")),
        ]);

        Assert.Equal(["b"], Ids(diff.ApplyTo(World("a", "b", "c"))));
    }

    [Fact]
    public void ARunInTheMiddleLeavesTheOperationsAroundItInOrder()
    {
        var diff = new SceneDiff([
            new AddEntity(Entity("a")),
            new AddEntity(Entity("b"), after: "a"),
            new RemoveEntity(Entity("x")),
            new RemoveEntity(Entity("y")),
            new AddEntity(Entity("c")),
        ]);

        Assert.Equal(["c", "a", "b", "z"], Ids(diff.ApplyTo(World("x", "y", "z"))));
    }

    [Fact]
    public void RandomRunsLandWhereOneAtATimeDoes()
    {
        var random = new Random(15);
        string[] names = ["a", "b", "c", "d", "e", "f", "g", "h"];
        for (var round = 0; round < 300; round++)
        {
            var start = World([.. names.Where(_ => random.Next(2) == 0)]);
            var operations = new List<SceneOp>();
            var count = random.Next(14);
            for (var i = 0; i < count; i++)
            {
                operations.Add(random.Next(3) == 0
                    ? new RemoveEntity(Entity(names[random.Next(names.Length)]))
                    : new AddEntity(Entity(names[random.Next(names.Length)]), After(random, names)));
            }

            var batched = new SceneDiff(operations).ApplyTo(start);

            Assert.True(
                Ids(batched).SequenceEqual(Ids(OneAtATime(start, operations))),
                $"round {round}: {string.Join(", ", operations.Select(op => op.ToJson()))}");
        }
    }

    private static string? After(Random random, string[] names)
    {
        if (random.Next(4) == 0)
        {
            return null;
        }

        return random.Next(5) == 0 ? "missing" : names[random.Next(names.Length)];
    }

    private static JsonMap Entity(string id, string? parent = null) => new SceneEntity(id, id, parent).ToJson();

    private static SceneDocument OneAtATime(SceneDocument document, IEnumerable<SceneOp> operations)
    {
        var current = document;
        foreach (var op in operations)
        {
            current = op.ApplyTo(current);
        }

        return current;
    }

    private static List<string> Ids(SceneDocument document) => [.. document.Entities.Select(e => e.Id)];

    private static SceneDocument World(params string[] names) =>
        new(entities: [.. names.Select(id => new SceneEntity(id, id))]);
}
