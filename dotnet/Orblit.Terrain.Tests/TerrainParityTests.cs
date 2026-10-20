using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Orblit.Json;
using Orblit.Parity;
using Orblit.VectorMath;
using static Orblit.Parity.Dump;
using static Orblit.Terrain.Tests.TerrainDump;

namespace Orblit.Terrain.Tests;

public class TerrainParityTests
{
    private static void Check(string op, Func<JsonElement, JsonNode?> run) =>
        Fixture.Load("orblit_terrain", "terrain").Check(op, run);

    private static long Long(JsonElement value) =>
        value.ValueKind == JsonValueKind.String ? long.Parse(value.GetString()!, CultureInfo.InvariantCulture) : value.GetInt64();

    private static double Double(JsonElement value) => Fixture.Number(value);

    private static int Int(JsonElement value) => value.GetInt32();

    private static Terrain World(JsonElement world)
    {
        var terrain = Terrain.Decode(world.GetProperty("terrain").GetString()!).Terrain;
        foreach (var file in world.GetProperty("regions").EnumerateArray())
        {
            terrain.PutRegion(TerrainRegion.Decode(Convert.FromBase64String(file.GetProperty("file").GetString()!)).Region);
        }

        return terrain;
    }

    // A NaN height has a bit pattern the CPU chooses, so what a stroke left is compared as numbers.
    private static JsonArray RegionDumps(Terrain terrain) => Arr(
        terrain.Regions.OrderBy(region => region.Key.Z).ThenBy(region => region.Key.X),
        region => new JsonObject
        {
            ["x"] = Big(region.Key.X),
            ["z"] = Big(region.Key.Z),
            ["heights"] = Arr(region.Heights, height => Num(height)),
            ["cover"] = Base64(region.Cover),
            ["colour"] = Base64(region.Colour),
        });

    // cover

    [Fact]
    public void CoverMatchesTheDartTwin() => Check("cover", c => Attempt(() =>
    {
        var a = c.GetProperty("args");
        var cover = Cover.Of(
            @base: Int(a.GetProperty("base")),
            overlay: Int(a.GetProperty("overlay")),
            blend: Double(a.GetProperty("blend")),
            angle: Int(a.GetProperty("angle")),
            scale: Int(a.GetProperty("scale")),
            hole: a.GetProperty("hole").GetBoolean(),
            navigation: a.GetProperty("navigation").GetBoolean(),
            automatic: a.GetProperty("automatic").GetBoolean());
        var states = new List<JsonNode?> { CoverState(cover) };
        foreach (var change in c.GetProperty("withs").EnumerateArray())
        {
            cover = CoverWith(cover, change[0].GetString()!, change[1]);
            states.Add(CoverState(cover));
        }

        return new JsonArray([.. states]);
    }));

    private static Cover CoverWith(Cover cover, string field, JsonElement value) => field switch
    {
        "base" => cover.WithBase(Int(value)),
        "overlay" => cover.WithOverlay(Int(value)),
        "blend" => cover.WithBlend(Double(value)),
        "angle" => cover.WithAngle(Int(value)),
        "scale" => cover.WithScale(Int(value)),
        "hole" => cover.WithHole(value.GetBoolean()),
        "navigation" => cover.WithNavigation(value.GetBoolean()),
        _ => cover.WithAutomatic(value.GetBoolean()),
    };

    private static JsonObject CoverState(Cover c) => new()
    {
        ["word"] = Big(c.Word),
        ["base"] = Big(c.Base),
        ["overlay"] = Big(c.Overlay),
        ["blendStep"] = Big(c.BlendStep),
        ["blend"] = Num(c.Blend),
        ["angle"] = Big(c.Angle),
        ["radians"] = Num(c.Radians),
        ["scaleCode"] = Big(c.ScaleCode),
        ["scale"] = Big(c.Scale),
        ["uv"] = Num(c.UvMultiplier),
        ["hole"] = c.Hole,
        ["navigation"] = c.Navigation,
        ["automatic"] = c.Automatic,
    };

    // ground colour

    [Fact]
    public void ColourMatchesTheDartTwin() => Check("colour", c => Attempt(() =>
    {
        var a = c.GetProperty("args");
        var colour = c.GetProperty("kind").GetString() == "of"
            ? GroundColour.Of(Int(a[0]), Int(a[1]), Int(a[2]), Double(a[3]))
            : GroundColour.Bytes(Int(a[0]), Int(a[1]), Int(a[2]), Int(a[3]));
        var states = new List<JsonNode?> { ColourState(colour) };
        foreach (var change in c.GetProperty("changes").EnumerateArray())
        {
            colour = change[0].GetString() == "colour"
                ? colour.WithColour(Int(change[1]), Int(change[2]), Int(change[3]))
                : colour.WithRoughness(Double(change[1]));
            states.Add(ColourState(colour));
        }

        return new JsonArray([.. states]);
    }));

    private static JsonObject ColourState(GroundColour c) => new()
    {
        ["rgba"] = Big(c.Rgba),
        ["red"] = Big(c.Red),
        ["green"] = Big(c.Green),
        ["blue"] = Big(c.Blue),
        ["roughnessByte"] = Big(c.RoughnessByte),
        ["roughness"] = Num(c.Roughness),
    };

    // the distance policy

    [Fact]
    public void PolicyMatchesTheDartTwin() => Check("policy", c => Attempt(() =>
    {
        var policy = new DistancePolicy(
            ceiling: Int(c.GetProperty("ceiling")),
            high: Double(c.GetProperty("high")),
            low: Double(c.GetProperty("low")),
            floor: Int(c.GetProperty("floor")),
            settle: Double(c.GetProperty("settle")));
        var steps = new List<int> { policy.Steps };
        foreach (var update in c.GetProperty("updates").EnumerateArray())
        {
            policy.Update(Double(update[0]), Double(update[1]));
            steps.Add(policy.Steps);
        }

        return new JsonObject { ["steps"] = Arr(steps, step => Big(step)), ["fraction"] = Num(policy.Fraction) };
    }));

    // region keys

    [Fact]
    public void KeyFromAFileNameMatchesTheDartTwin() => Check("keyName", c => Attempt(() =>
        RegionKey.FromFileName(c.GetProperty("name").GetString()!) is { } key
            ? new JsonObject { ["key"] = KeyJson(key), ["name"] = key.FileName }
            : null));

    [Fact]
    public void KeyContainingMatchesTheDartTwin() => Check("keyContaining", c => Attempt(() =>
        KeyJson(RegionKey.Containing(Long(c.GetProperty("i")), Long(c.GetProperty("j")), Long(c.GetProperty("size"))))));

    // region files

    [Fact]
    public void RegionEncodeMatchesTheDartTwin() => Check("regionEncode", c => Attempt(() =>
        Convert.ToBase64String(new TerrainRegion(
            new RegionKey(Long(c.GetProperty("x")), Long(c.GetProperty("z"))),
            Int(c.GetProperty("size")),
            Floats(c.GetProperty("heights")),
            Words(c.GetProperty("cover")),
            Convert.FromBase64String(c.GetProperty("colour").GetString()!)).Encode())));

    private static float[] Floats(JsonElement base64)
    {
        var bytes = Convert.FromBase64String(base64.GetString()!);
        var floats = new float[bytes.Length / sizeof(float)];
        Buffer.BlockCopy(bytes, 0, floats, 0, bytes.Length);
        return floats;
    }

    private static uint[] Words(JsonElement base64)
    {
        var bytes = Convert.FromBase64String(base64.GetString()!);
        var words = new uint[bytes.Length / sizeof(uint)];
        Buffer.BlockCopy(bytes, 0, words, 0, bytes.Length);
        return words;
    }

    [Fact]
    public void RegionNewMatchesTheDartTwin() => Check("regionNew", c => Attempt(() =>
    {
        var key = new RegionKey(0, 0);
        var size = checked((int)Long(c.GetProperty("size")));
        var lengths = c.GetProperty("lengths");
        _ = lengths.ValueKind == JsonValueKind.Null
            ? new TerrainRegion(key, size)
            : new TerrainRegion(key, size, new float[Int(lengths[0])], new uint[Int(lengths[1])], new byte[Int(lengths[2])]);
        return true;
    }));

    [Fact]
    public void ValidSizeMatchesTheDartTwin() => Check("validSize", c =>
        JsonValue.Create(TerrainRegion.ValidSize(Long(c.GetProperty("size")))));

    [Fact]
    public void RegionDecodeMatchesTheDartTwin() => Check("regionDecode", c => Attempt(() =>
    {
        var load = TerrainRegion.Decode(Convert.FromBase64String(c.GetProperty("bytes").GetString()!));
        var region = load.Region;
        return new JsonObject
        {
            ["x"] = Big(region.Key.X),
            ["z"] = Big(region.Key.Z),
            ["size"] = Big(region.Size),
            ["heights"] = Base64(region.Heights),
            ["cover"] = Base64(region.Cover),
            ["colour"] = Base64(region.Colour),
            ["problems"] = Arr(load.Problems, Text),
        };
    }));

    // terrain files

    [Fact]
    public void TerrainDecodeMatchesTheDartTwin() => Check("terrainDecode", c => Attempt(() =>
    {
        var load = Terrain.Decode(c.GetProperty("text").GetString()!);
        return new JsonObject
        {
            ["terrain"] = Tree(load.Terrain.ToJson()),
            ["regions"] = Arr(load.Regions, KeyJson),
            ["problems"] = Arr(load.Problems, Text),
            ["encoded"] = load.Terrain.Encode(),
        };
    }));

    [Fact]
    public void ScatterLayerFromJsonMatchesTheDartTwin() => Check("layer", c => Attempt(() =>
    {
        var problems = new List<string>();
        var layer = ScatterLayer.FromJson(DartJson.Decode(c.GetProperty("text").GetString()!), problems);
        return new JsonObject { ["layer"] = Tree(layer?.ToJson()), ["problems"] = Arr(problems, Text) };
    }));

    // reading the ground

    [Fact]
    public void QueriesMatchTheDartTwin() => Check("query", c =>
    {
        var terrain = World(c.GetProperty("world"));
        var spots = c.GetProperty("heights").EnumerateArray().Select(p => (X: Double(p[0]), Z: Double(p[1]))).ToList();
        var texels = c.GetProperty("texels").EnumerateArray().Select(t => (I: Long(t[0]), J: Long(t[1]))).ToList();
        var rays = c.GetProperty("rays").EnumerateArray().ToList();
        return new JsonObject
        {
            ["height"] = Arr(spots, p => Num(terrain.HeightAt(p.X, p.Z))),
            ["normal"] = Arr(spots, p => Vec(terrain.NormalAt(p.X, p.Z))),
            ["cover"] = Arr(spots, p => terrain.CoverAt(p.X, p.Z) is { } cover ? Big(cover.Word) : null),
            ["texelHeight"] = Arr(texels, t => Num(terrain.TexelHeight(t.I, t.J))),
            ["texelCover"] = Arr(texels, t => terrain.TexelCover(t.I, t.J) is { } cover ? Big(cover.Word) : null),
            ["texelColour"] = Arr(texels, t => terrain.TexelColour(t.I, t.J) is { } colour ? Big(colour.Rgba) : null),
            ["ray"] = Arr(rays, ray => Vec(terrain.Raycast(
                VectorOf(ray.GetProperty("origin")),
                VectorOf(ray.GetProperty("direction")),
                Double(ray.GetProperty("maxDistance"))))),
            ["encoded"] = terrain.Encode(),
        };
    });

    private static Vector3 VectorOf(JsonElement v) => new(Double(v[0]), Double(v[1]), Double(v[2]));

    // strokes

    [Fact]
    public void StrokesMatchTheDartTwin() => Check("stroke", c => Attempt(() =>
    {
        var terrain = World(c.GetProperty("world"));
        var stroke = StrokeOf(terrain, c);
        var patches = c.GetProperty("moves").EnumerateArray()
            .Select(move => PatchJson(stroke.MoveTo(Double(move[0]), Double(move[1]))))
            .ToList();
        return new JsonObject
        {
            ["dabs"] = Big(stroke.Dabs),
            ["start"] = stroke.Start is { } start ? new JsonArray(Num(start.X), Num(start.Z)) : null,
            ["patches"] = new JsonArray([.. patches]),
            ["regions"] = RegionDumps(terrain),
        };
    }));

    private static TerrainStroke StrokeOf(Terrain terrain, JsonElement c)
    {
        var brush = c.GetProperty("brush");
        var height = c.GetProperty("height");
        return new TerrainStroke(
            terrain,
            Enum.Parse<BrushTool>(c.GetProperty("tool").GetString()!, ignoreCase: true),
            brush: new Brush(
                size: Double(brush.GetProperty("size")),
                strength: Double(brush.GetProperty("strength")),
                falloff: Double(brush.GetProperty("falloff")),
                jitter: Double(brush.GetProperty("jitter")),
                spacing: Double(brush.GetProperty("spacing"))),
            invert: c.GetProperty("invert").GetBoolean(),
            set: Int(c.GetProperty("set")),
            colour: new GroundColour((uint)Long(c.GetProperty("colour"))),
            roughness: Double(c.GetProperty("roughness")),
            height: height.ValueKind == JsonValueKind.Null ? null : Double(height),
            tileSize: Int(c.GetProperty("tileSize")));
    }

    private static JsonObject PatchJson(TerrainPatch patch) => new()
    {
        ["empty"] = patch.IsEmpty,
        ["tiles"] = Big(patch.TileCount),
        ["bytes"] = Big(patch.ByteCount),
        ["regions"] = Arr(patch.Regions.OrderBy(key => key.Z).ThenBy(key => key.X), KeyJson),
    };

    // scatter

    [Fact]
    public void ScatterMatchesTheDartTwin() => Check("scatter", c =>
    {
        var terrain = World(c.GetProperty("world"));
        var placer = new ScatterPlacer();
        var first = PlacerJson(placer, placer.Update(terrain));
        var again = placer.Update(terrain);
        foreach (var edit in c.GetProperty("edits").EnumerateArray())
        {
            Apply(terrain, edit);
        }

        return new JsonObject { ["first"] = first, ["again"] = again, ["after"] = PlacerJson(placer, placer.Update(terrain)) };
    });

    private static void Apply(Terrain terrain, JsonElement edit)
    {
        var key = new RegionKey(Long(edit.GetProperty("x")), Long(edit.GetProperty("z")));
        switch (edit.GetProperty("op").GetString())
        {
            case "height":
                var region = terrain.RegionAt(key) ?? throw new InvalidOperationException($"No region at {key}.");
                region.SetHeight(Int(edit.GetProperty("i")), Int(edit.GetProperty("j")), Double(edit.GetProperty("value")));
                break;
            case "remove":
                terrain.RemoveRegion(key);
                break;
            default:
                terrain.AddRegion(key);
                break;
        }
    }

    private static JsonObject PlacerJson(ScatterPlacer placer, bool changed) => new()
    {
        ["changed"] = changed,
        ["count"] = Big(placer.Count),
        ["groups"] = Arr(placer.Groups, GroupJson),
    };

    private static JsonObject GroupJson(ScatterGroup g) => new()
    {
        ["key"] = KeyJson(g.Key),
        ["layer"] = Big(g.Layer),
        ["id"] = Big(g.Id),
        ["count"] = Big(g.Count),
        ["transforms"] = Base64(g.Transforms),
        ["colours"] = Base64(g.Colours),
        ["minimum"] = Vec(g.Minimum),
        ["maximum"] = Vec(g.Maximum),
    };
}
