using System.Text.Json;
using System.Text.Json.Nodes;
using Orblit.Json;
using Orblit.Parity;
using Orblit.VectorMath;
using static Orblit.Input.Tests.ActionFixture;

namespace Orblit.Input.Tests;

public class ActionParityTests
{
    private static readonly Fixture Source = Fixture.Load("orblit_input", "actions");

    private static readonly string[] ContextNames = ["play", "vehicle", "menu"];

    [Fact]
    public void AControlIsReadAsTheFixtureReadsIt() =>
        Source.Check("control", c => Run(() => Node(Control.FromJson(Plain(c.GetProperty("json"))).ToJson())));

    [Fact]
    public void AFrameIsReadAsTheFixtureReadsIt() =>
        Source.Check("frame", c =>
        {
            var frame = FrameOf(c.GetProperty("frame"));
            return new JsonObject
            {
                ["reads"] = Array(Pool().Select(control => (JsonNode?)Num(frame.Read(control)))),
                ["sticks"] = Array(Enum.GetValues<PadStick>().Select(stick => (JsonNode?)Pair(frame.Stick(stick)))),
                ["active"] = Array(frame.Active.Select(control => Node(control.ToJson()))),
            };
        });

    [Fact]
    public void ASessionRunsAsTheFixtureRunsIt() => Source.Check("session", RunSession);

    [Fact]
    public void AProfileLoadsAsTheFixtureLoadsIt() => Source.Check("load", RunLoad);

    [Fact]
    public void TheNextPressIsCapturedAsTheFixtureCapturesIt() => Source.Check("next", RunNext);

    [Fact]
    public void TouchPayloadsAreReadAsTheFixtureReadsThem() => Source.Check("touch", RunTouch);

    private static JsonValue Num(double value) =>
        double.IsFinite(value)
            ? JsonValue.Create(value)
            : JsonValue.Create(double.IsNaN(value) ? "NaN" : value > 0 ? "Infinity" : "-Infinity");

    private static JsonArray Array(IEnumerable<JsonNode?> items) => new([.. items]);

    private static JsonArray Pair(Vector2 vector) => new(Num(vector.X), Num(vector.Y));

    private static JsonNode? Node(object? value) => JsonNode.Parse(JsonText.Encode(value));

    private static string Camel<T>(T value)
        where T : struct, Enum => char.ToLowerInvariant(value.ToString()[0]) + value.ToString()[1..];

    private static T Find<T>(string name)
        where T : struct, Enum => Enum.GetValues<T>().First(member => Camel(member) == name);

    private static List<string> Names(JsonElement spec, string name) =>
        [.. spec.GetProperty(name).EnumerateArray().Select(item => item.GetString()!)];

    /// <summary>A value of a case as the port takes it. A double JSON cannot hold travels as <c>{"n": "NaN"}</c>.</summary>
    private static object? Plain(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Array => value.EnumerateArray().Select(Plain).ToList(),
        JsonValueKind.Object when value.TryGetProperty("n", out var n) => Fixture.Number(n),
        _ => JsonText.Decode(value.GetRawText()),
    };

    private static double NumberOf(JsonElement value) =>
        value.ValueKind == JsonValueKind.Object ? Fixture.Number(value.GetProperty("n")) : value.GetDouble();

    private static string? KindOf(Exception error) => error switch
    {
        BindingClashException => "clash",
        ArgumentOutOfRangeException => "range",
        ArgumentException => "argument",
        FormatException => "format",
        InvalidOperationException => "state",
        _ => null,
    };

    private static JsonObject Run(Func<JsonNode?> body)
    {
        try
        {
            return new JsonObject { ["value"] = body() };
        }
        catch (Exception error) when (KindOf(error) is { } kind)
        {
            return new JsonObject { ["error"] = kind };
        }
    }

    private static string? Failure(Action body)
    {
        try
        {
            body();
            return null;
        }
        catch (Exception error) when (KindOf(error) is { } kind)
        {
            return kind;
        }
    }

    private static List<Control> Pool() =>
    [
        .. new[] { "Space", "KeyA", "KeyD", "KeyS", "KeyW", "KeyF", "KeyE", "Enter", "KeyJ", "KeyX" }.Select(Key),
        .. Enum.GetValues<PadButton>().Select(button => (Control)new PadButtonControl(button)),
        .. from axis in Enum.GetValues<PadAxis>()
           from side in Enum.GetValues<AxisSide>()
           select (Control)new PadAxisControl(axis, side),
        .. Enum.GetValues<PadStick>().Select(stick => (Control)new PadStickControl(stick)),
        .. new[] { "jump", "fire", "pad" }.Select(region => (Control)new TouchControl(region)),
    ];

    private static FrameControls FrameOf(JsonElement spec)
    {
        var axes = spec.GetProperty("axes").EnumerateObject()
            .ToDictionary(p => Find<PadAxis>(p.Name), p => Fixture.Number(p.Value));
        var sticks = spec.GetProperty("sticks").EnumerateObject()
            .ToDictionary(p => Find<PadStick>(p.Name), p => new Vector2(p.Value[0].GetDouble(), p.Value[1].GetDouble()));
        var down = Names(spec, "down").Select(Find<PadButton>).ToList();
        return new FrameControls(
            Names(spec, "keys"),
            Names(spec, "touch"),
            sticks,
            new PadState(0, new PadIdentity("test"), down, [], [], axes, axes));
    }

    private static JsonObject RunSession(JsonElement c)
    {
        var map = Map(Names(c, "active"), c.GetProperty("jumpKey").GetString()!);
        var results = new JsonArray();
        foreach (var step in c.GetProperty("steps").EnumerateArray())
        {
            results.Add(Step(map, step));
        }

        return new JsonObject
        {
            ["steps"] = results,
            ["saved"] = Node(map.Bindings.ToJson()),
            ["bindings"] = BindingsOf(map.Bindings),
        };
    }

    private static JsonObject Step(ActionMap map, JsonElement step)
    {
        var action = step.TryGetProperty("action", out var named) ? named.GetString()! : "";
        var context = step.TryGetProperty("context", out var contextName) ? contextName.GetString()! : "";
        return step.GetProperty("op").GetString() switch
        {
            "update" => Update(map, step),
            "activate" => Run(() =>
            {
                map.Activate(context);
                return null;
            }),
            "deactivate" => Run(() =>
            {
                map.Deactivate(context);
                return null;
            }),
            "clash" => Run(() =>
                JsonValue.Create(map.Bindings.Clash(action, Control.FromJson(Plain(step.GetProperty("control"))), context, step.GetProperty("slot").GetInt32()))),
            "ask" => Run(() => Ask(map, action, step.GetProperty("ask").GetString()!)),
            _ => Rebind(map, step),
        };
    }

    private static JsonObject Update(ActionMap map, JsonElement step)
    {
        map.Update(FrameOf(step.GetProperty("frame")), TimeSpan.FromMilliseconds(step.GetProperty("ms").GetInt32()));
        var snapshot = new JsonObject();
        foreach (var name in new[] { "jump", "fire", "confirm" })
        {
            snapshot[name] = new JsonArray(
                map.IsDown(name),
                map.WasPressed(name),
                map.WasReleased(name),
                map.WasHeld(name),
                map.WasTapped(name),
                map.WasDoubleTapped(name));
        }

        snapshot["steer"] = Num(map.Value("steer"));
        snapshot["move"] = Pair(map.Vector("move"));
        return new JsonObject { ["value"] = snapshot };
    }

    private static JsonNode Ask(ActionMap map, string action, string ask) => ask switch
    {
        "isDown" => JsonValue.Create(map.IsDown(action)),
        "value" => Num(map.Value(action)),
        _ => Pair(map.Vector(action)),
    };

    private static JsonObject Rebind(ActionMap map, JsonElement step) =>
        Run(() =>
        {
            var scale = step.GetProperty("scale");
            var part = step.GetProperty("part").GetString();
            map.Bindings.Rebind(
                step.GetProperty("action").GetString()!,
                Control.FromJson(Plain(step.GetProperty("control"))),
                step.GetProperty("context").GetString()!,
                step.GetProperty("slot").GetInt32(),
                step.GetProperty("swap").GetBoolean(),
                scale.ValueKind == JsonValueKind.Null ? null : NumberOf(scale),
                part is null ? null : Find<AxisPart>(part));
            return null;
        });

    private static JsonObject BindingsOf(BindingTable table)
    {
        var json = new JsonObject();
        foreach (var context in ContextNames)
        {
            var actions = new JsonObject();
            foreach (var (action, list) in table.BindingsIn(context))
            {
                actions[action] = Array(list.Select(binding => (JsonNode?)new JsonObject
                {
                    ["control"] = Node(binding.Control.ToJson()),
                    ["scale"] = Num(binding.Scale),
                    ["part"] = Camel(binding.Part),
                }));
            }

            json[context] = actions;
        }

        return json;
    }

    private static JsonObject RunLoad(JsonElement c)
    {
        var table = Map([], "Space").Bindings;
        table.Rebind("jump", Key("KeyZ"), "play");
        var error = Failure(() => table.Load(Plain(c.GetProperty("profile"))));
        return new JsonObject
        {
            ["error"] = error,
            ["saved"] = Node(table.ToJson()),
            ["bindings"] = BindingsOf(table),
        };
    }

    private static JsonArray RunNext(JsonElement c)
    {
        var next = new NextInput(Find<ActionKind>(c.GetProperty("kind").GetString()!));
        var captured = new JsonArray();
        foreach (var frame in c.GetProperty("frames").EnumerateArray())
        {
            next.Update(new Pressed([.. frame.EnumerateArray().Select(json => Control.FromJson(Plain(json)))]));
            captured.Add(next.Captured is { } control ? Node(control.ToJson()) : null);
        }

        return captured;
    }

    private static JsonArray RunTouch(JsonElement c)
    {
        var touch = new TouchControls();
        var results = new JsonArray();
        foreach (var step in c.GetProperty("steps").EnumerateArray())
        {
            Touched(touch, step);
            results.Add(TouchState(touch));
        }

        return results;
    }

    private static void Touched(TouchControls touch, JsonElement step)
    {
        switch (step.GetProperty("op").GetString())
        {
            case "hold":
                // The fixture also holds payloads of another shape, which the typed method cannot be given.
                if (Plain(step.GetProperty("payload")) is bool held)
                {
                    touch.Hold(step.GetProperty("region").GetString()!, held);
                }

                break;
            case "moveStick":
                if (Pair(Plain(step.GetProperty("payload"))) is { } push)
                {
                    touch.MoveStick(Find<PadStick>(step.GetProperty("stick").GetString()!), push);
                }

                break;
            case "swipe":
                if (Pair(Plain(step.GetProperty("payload"))) is { } by)
                {
                    touch.Swipe(by);
                }

                break;
            default:
                touch.EndFrame();
                break;
        }
    }

    private static Vector2? Pair(object? payload) =>
        payload is IReadOnlyList<object?> { Count: 2 } list && Number(list[0]) is { } x && Number(list[1]) is { } y
            ? new Vector2(x, y)
            : null;

    private static double? Number(object? value) => value switch
    {
        long whole => whole,
        int whole => whole,
        double number => number,
        _ => null,
    };

    private static JsonObject TouchState(TouchControls touch)
    {
        var sticks = new JsonObject();
        foreach (var (stick, vector) in touch.Sticks)
        {
            sticks[Camel(stick)] = Pair(vector);
        }

        return new JsonObject
        {
            ["held"] = Array(touch.Held.Select(region => (JsonNode?)region)),
            ["sticks"] = sticks,
            ["swiped"] = Pair(touch.Swiped),
        };
    }

    private sealed class Pressed(List<Control> active) : IPressedControls
    {
        public IEnumerable<Control> Active => active;
    }
}
