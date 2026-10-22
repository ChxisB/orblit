using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
using Orblit.Json;
using Orblit.Parity;
using static Orblit.Parity.Dump;

namespace Orblit.Scene.Tests;

public class SceneParityTests
{
    private static readonly Fixture Expected = Fixture.Load("orblit_scene", "scene");

    [Fact]
    public void ComponentsReadAndWriteAsTheFixtureSays() => Expected.Check("read", Read);

    [Fact]
    public void RotationsMatchTheFixture() => Expected.Check("rotation", Rotation);

    [Fact]
    public void DocumentsReadAndWriteAsTheFixtureSays() => Expected.Check("document", Document);

    [Fact]
    public void DiffsMatchTheFixture() => Expected.Check("diff", Diff);

    [Fact]
    public void OperationsReadAsTheFixtureSays() => Expected.Check("operations", Operations);

    [Fact]
    public void SingleEntitiesMigrateAsTheFixtureSays() => Expected.Check("entity", Entity);

    [Fact]
    public void MaterialsReadAndResolveAsTheFixtureSays() => Expected.Check("materials", Materials);

    [Fact]
    public void InstancesOpenFoldAndEditAsTheFixtureSays() => Expected.Check("instances", InstanceSteps);

    private static JsonNode? Read(JsonElement c)
    {
        var input = c.GetProperty("in");
        var type = input[0].GetString()!;
        var json = (JsonMap)JsonText.Decode(input[1].GetRawText())!;
        return Attempt(() => Tree(SceneComponents.Read(type, json).ToJson()));
    }

    private static JsonNode? Rotation(JsonElement c)
    {
        var input = c.GetProperty("in");
        var degrees = new Vector3(Fixture.Number(input[0]), Fixture.Number(input[1]), Fixture.Number(input[2]));
        var turn = TransformComponent.RotationOf(degrees);
        var back = TransformComponent.AnglesOf(turn);
        return new JsonObject
        {
            ["matrix"] = Arr(Enumerable.Range(0, 9).Select(i => turn[i]), v => Num(v)),
            ["angles"] = new JsonArray(Num(back.X), Num(back.Y), Num(back.Z)),
        };
    }

    private static JsonNode? Document(JsonElement c) => Attempt(() =>
    {
        var load = SceneDocument.Decode(c.GetProperty("in")[0].GetString()!);
        return new JsonObject
        {
            ["problems"] = Arr(load.Problems, Text),
            ["text"] = Text(load.Document.Encode()),
        };
    });

    private static JsonNode? Diff(JsonElement c)
    {
        var input = c.GetProperty("in");
        var a = SceneDocument.Decode(input[0].GetString()!).Document;
        var b = SceneDocument.Decode(input[1].GetString()!).Document;
        var diff = SceneDiff.Between(a, b);
        var ops = diff.ToJson();
        return new JsonObject
        {
            ["ops"] = Tree(ops),
            ["forward"] = Text(diff.ApplyTo(a).Encode()),
            ["back"] = Text(diff.Inverse.ApplyTo(b).Encode()),
            ["reread"] = Text(SceneDiff.FromJson(ops).ApplyTo(a).Encode()),
        };
    }

    private static JsonNode? Operations(JsonElement c) => Attempt(() =>
    {
        var input = c.GetProperty("in");
        var document = SceneDocument.Decode(input[0].GetString()!).Document;
        var diff = SceneDiff.FromJson(JsonText.Decode(input[1].GetRawText()));
        return new JsonObject
        {
            ["ops"] = Tree(diff.ToJson()),
            ["applied"] = Text(diff.ApplyTo(document).Encode()),
        };
    });

    private static JsonNode? Entity(JsonElement c) => Attempt(() =>
    {
        var input = c.GetProperty("in");
        var notes = new List<string>();
        var moved = SceneMigrations.Entity((JsonMap)JsonText.Decode(input[0].GetRawText())!, input[1].GetInt64(), notes);
        return new JsonObject { ["entity"] = Tree(moved), ["notes"] = Arr(notes, Text) };
    });

    private static JsonNode? Materials(JsonElement c) => Attempt(() =>
    {
        var input = c.GetProperty("in");
        var read = MaterialsOf(input[0]);
        var groups = MaterialsOf(input[1]);
        var library = new MaterialLibrary(
            read.ToDictionary(static one => one.Key, static one => one.Value.Document),
            groups.ToDictionary(static one => one.Key, static one => one.Value.Document));
        return new JsonObject
        {
            ["docs"] = LoadedAll(read),
            ["groups"] = LoadedAll(groups),
            ["resolved"] = new JsonObject(input[2].EnumerateArray()
                .Select(static path => path.GetString()!)
                .Select(path => KeyValuePair.Create(path, (JsonNode?)Resolved(library.Resolve(path))))),
        };
    });

    private static Dictionary<string, MaterialLoad> MaterialsOf(JsonElement files) =>
        files.EnumerateObject().ToDictionary(
            static file => file.Name,
            static file => MaterialDocument.FromJson((JsonMap)JsonText.Decode(file.Value.GetRawText())!));

    private static JsonObject LoadedAll(Dictionary<string, MaterialLoad> loads) => new(loads.Select(
        static one => KeyValuePair.Create(
            one.Key,
            (JsonNode?)new JsonObject
            {
                ["problems"] = Arr(one.Value.Problems, Text),
                ["json"] = Tree(one.Value.Document.ToJson()),
            })));

    private static JsonObject Resolved(ResolvedMaterial one) => new()
    {
        ["values"] = new JsonObject(one.Values.Select(static pair => KeyValuePair.Create(pair.Key, ValueNode(pair.Value)))),
        ["maps"] = new JsonObject(one.Maps.Select(static pair => KeyValuePair.Create(pair.Key, (JsonNode?)Text(pair.Value)))),
        ["problems"] = Arr(one.Problems, Text),
    };

    private static JsonNode? ValueNode(object value) => value switch
    {
        double number => Num(number),
        IReadOnlyList<double> numbers => Arr(numbers, static n => Num(n)),
        bool flag => flag,
        _ => Text((string)value),
    };

    // The fixture's steps, run one after the other on a scene and the prefabs it can reach.
    private static JsonNode? InstanceSteps(JsonElement c) => Attempt(() =>
    {
        var input = c.GetProperty("in");
        var prefabs = new SortedDictionary<string, PrefabDocument>(StringComparer.Ordinal);
        foreach (var file in input[1].EnumerateObject())
        {
            prefabs[file.Name] = PrefabDocument.Decode(file.Value.GetString()!).Prefab;
        }

        PrefabDocument? Source(string asset) => prefabs.GetValueOrDefault(asset);
        var run = new InstanceRun(SceneDocument.Decode(input[0].GetString()!).Document);
        foreach (var step in input[2].EnumerateArray())
        {
            run.Do(step, prefabs, Source);
        }

        return new JsonObject
        {
            ["text"] = Text(run.Document.Encode()),
            ["problems"] = Arr(run.Problems, Text),
            ["renamed"] = new JsonObject(run.Renamed.Select(static pair => KeyValuePair.Create(pair.Key, (JsonNode?)Text(pair.Value)))),
            ["prefabs"] = new JsonObject(prefabs.Select(static pair => KeyValuePair.Create(pair.Key, (JsonNode?)Text(pair.Value.Encode())))),
        };
    });

    private sealed class InstanceRun(SceneDocument start)
    {
        private int _next;

        public SceneDocument Document { get; private set; } = start;

        public List<string> Problems { get; } = [];

        public Dictionary<string, string> Renamed { get; } = [];

        [SuppressMessage("Maintainability", "CA1502", Justification = "F2: a list of cases is data.")]
        public void Do(JsonElement step, SortedDictionary<string, PrefabDocument> prefabs, PrefabSource source)
        {
            var id = Text(step, "id");
            var asset = Text(step, "asset");
            switch (step.GetProperty("op").GetString())
            {
                case "expand":
                    Take(Instances.Expand(Document, source));
                    break;
                case "fold":
                    Document = Instances.Fold(Document, source);
                    break;
                case "move":
                    Document = Document.WithEntity(id!, Document[id!]!.WithComponent(
                        SceneComponents.Transform,
                        new TransformComponent(new Vector3(Fixture.Number(step.GetProperty("x")), Fixture.Number(step.GetProperty("y")), 0))));
                    break;
                case "rename":
                    Document = Document.WithEntity(id!, Document[id!]!.CopyWith(name: step.GetProperty("name").GetString()));
                    break;
                case "add":
                    Document = Document.WithEntity(id!, Thing(id!, Text(step, "parent")));
                    break;
                case "remove":
                    Document = Document.WithEntity(id!, null);
                    break;
                case "apply":
                    prefabs[asset!] = Take(Instances.Apply(Document, id!, source));
                    break;
                case "revert":
                    Take(Instances.Revert(Document, id!, source));
                    break;
                case "unpack":
                    var unpacked = Instances.Unpack(Document, id!, source, () => $"n{++_next}");
                    Document = unpacked.Document;
                    AddRenamed(unpacked.Renamed);
                    break;
                case "make":
                    prefabs[asset!] = Take(Instances.MakePrefab(Document, id!, asset!, source, Text(step, "name")));
                    break;
                case "swap":
                    prefabs[asset!] = PrefabDocument.Decode(step.GetProperty("prefab").GetString()!).Prefab;
                    break;
                case "refresh":
                    var before = new Dictionary<string, PrefabDocument>(prefabs);
                    prefabs[asset!] = PrefabDocument.Decode(step.GetProperty("prefab").GetString()!).Prefab;
                    Take(Instances.Refresh(Document, asset!, one => before.GetValueOrDefault(one), source));
                    break;
                default:
                    throw new InvalidOperationException($"no step {step.GetProperty("op")}");
            }
        }

        private void Take(SceneLoad load)
        {
            Document = load.Document;
            Problems.AddRange(load.Problems);
        }

        private PrefabDocument Take(PrefabEdit edit)
        {
            Document = edit.Document;
            AddRenamed(edit.Renamed);
            Problems.AddRange(edit.Problems);
            return edit.Prefab;
        }

        private void AddRenamed(IReadOnlyDictionary<string, string> renamed)
        {
            foreach (var (from, to) in renamed)
            {
                Renamed[from] = to;
            }
        }

        private static string? Text(JsonElement step, string name) =>
            step.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

        private static SceneEntity Thing(string id, string? parent) => new(
            id,
            id,
            parent,
            components: new OrderedDictionary<string, SceneComponent>
            {
                [SceneComponents.Transform] = new TransformComponent(),
            });
    }
}
