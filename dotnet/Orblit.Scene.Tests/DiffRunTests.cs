namespace Orblit.Scene.Tests;

/// <summary>
/// A run of adds, of removes or of edits to one entity is applied in one pass, which has to land on
/// the document the same operations give one at a time.
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

    [Fact]
    public void EditsToOneEntityApplyInOrder()
    {
        var diff = new SceneDiff([
            new SetField("b", SceneComponents.Transform, "position", Position(0), Position(1)),
            new SetEntityName("b", "b", "bee"),
            new SetField("b", SceneComponents.Transform, "position", Position(1), Position(2)),
            new SetVisible("b", true, false),
            new Reparent("b", null, "a"),
        ]);

        var result = diff.ApplyTo(Placed("a", "b", "c"));

        var edited = result["b"]!;
        Assert.Equal("bee", edited.Name);
        Assert.False(edited.Visible);
        Assert.Equal("a", edited.Parent);
        Assert.Equal(Position(2), edited[SceneComponents.Transform]!.ToJson()["position"]);
        Assert.Equal(["a", "b", "c"], Ids(result));
    }

    [Fact]
    public void AnEditOfSomethingMissingIsSkippedAndTheRestStillRun()
    {
        var diff = new SceneDiff([
            new SetEntityName("missing", "x", "y"),
            new SetField("a", SceneComponents.Camera, "fieldOfView", 50.0, 70.0),
            new SetVisible("c", true, false),
            new SetComponent("missing", SceneComponents.Transform, null, new TransformComponent().ToJson()),
        ]);

        var result = diff.ApplyTo(Placed("a", "b", "c"));

        Assert.Equal(["a", "b", "c"], Ids(result));
        Assert.Null(result["a"]![SceneComponents.Camera]);
        Assert.False(result["c"]!.Visible);
        Assert.True(result["b"]!.Visible);
    }

    [Fact]
    public void EditsDoNotMoveAnythingInTheDocumentOrder()
    {
        var diff = new SceneDiff([
            new SetVisible("d", true, false),
            new SetEntityName("a", "a", "first"),
            new SetVisible("d", false, true),
        ]);

        var result = diff.ApplyTo(Placed("d", "c", "b", "a"));

        Assert.Equal(["d", "c", "b", "a"], Ids(result));
        Assert.Equal("first", result["a"]!.Name);
        Assert.True(result["d"]!.Visible);
    }

    [Fact]
    public void RandomEditsLandWhereOneAtATimeDoes()
    {
        var random = new Random(37);
        string[] names = ["a", "b", "c", "d", "e", "f", "g", "h"];
        for (var round = 0; round < 300; round++)
        {
            var start = Placed([.. names.Where(_ => random.Next(4) != 0)]);
            var operations = new List<SceneOp>();
            var count = random.Next(18);
            for (var i = 0; i < count; i++)
            {
                operations.Add(Any(random, names));
            }

            var batched = new SceneDiff(operations).ApplyTo(start);

            Assert.True(
                batched.Encode() == OneAtATime(start, operations).Encode(),
                $"round {round}: {string.Join(", ", operations.Select(op => op.ToJson()))}");
        }
    }

    [Fact]
    public void ARunOfEditsCopiesTheDocumentOnce()
    {
        var ids = Enumerable.Range(0, 1000).Select(i => $"e{i}").ToArray();
        var document = Placed(ids);
        var diff = new SceneDiff([.. ids.Take(400).Select(id =>
            (SceneOp)new SetField(id, SceneComponents.Transform, "position", Position(0), Position(1)))]);
        diff.ApplyTo(document);

        var before = GC.GetAllocatedBytesForCurrentThread();
        diff.ApplyTo(document);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        // One copy at a time allocates tens of megabytes here. One pass allocates a few hundred kilobytes.
        Assert.True(allocated < 4_000_000, $"{allocated} bytes");
    }

    private static SceneOp Any(Random random, string[] names)
    {
        var id = names[random.Next(names.Length)];
        var type = random.Next(5) == 0 ? SceneComponents.Camera : SceneComponents.Transform;
        return random.Next(11) switch
        {
            0 => new SetEntityName(id, id, names[random.Next(names.Length)]),
            1 => new SetVisible(id, true, random.Next(2) == 0),
            2 => new SetComponent(id, SceneComponents.Transform, null, random.Next(3) == 0 ? null : Transform(random.Next(9))),
            3 => new SetComponent(id, SceneComponents.Transform, null, Transform(random.Next(9))),
            4 => new SetField(id, type, "position", null, Position(random.Next(9))),
            5 => new SetField(id, type, "position", null, random.Next(3) == 0 ? null : Position(random.Next(9))),
            6 => new Reparent(id, null, random.Next(3) == 0 ? null : names[random.Next(names.Length)]),
            7 => new Reorder(id, null, random.Next(3) == 0 ? null : names[random.Next(names.Length)]),
            8 => new AddEntity(Entity(id), random.Next(3) == 0 ? null : names[random.Next(names.Length)]),
            _ => new RemoveEntity(Entity(id)),
        };
    }

    private static JsonMap Transform(int x) => new TransformComponent(position: new Vector3(x, 0, 0)).ToJson();

    private static object? Position(int x) => Transform(x)["position"];

    private static SceneDocument Placed(params string[] names) => new(entities:
        [.. names.Select(id => new SceneEntity(id, id).WithComponent(SceneComponents.Transform, new TransformComponent()))]);

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
