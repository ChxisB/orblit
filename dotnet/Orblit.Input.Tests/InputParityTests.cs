using System.Text.Json;
using System.Text.Json.Nodes;
using Orblit.Parity;
using Orblit.VectorMath;

namespace Orblit.Input.Tests;

public class InputParityTests
{
    private static readonly Fixture Input = Fixture.Load("orblit_input", "input");

    private static JsonValue Num(double value) =>
        double.IsFinite(value)
            ? JsonValue.Create(value)
            : JsonValue.Create(double.IsNaN(value) ? "NaN" : value > 0 ? "Infinity" : "-Infinity");

    private static double Read(JsonElement spec, string name) => Fixture.Number(spec.GetProperty(name));

    private static string Camel<T>(T value)
        where T : struct, Enum => char.ToLowerInvariant(value.ToString()[0]) + value.ToString()[1..];

    private static Shaping ShapingOf(JsonElement spec) =>
        new(Read(spec, "inner"), Read(spec, "outer"), Read(spec, "curve"));

    private static JsonArray Array(IEnumerable<JsonNode?> items) => new([.. items]);

    private static JsonArray Triple(Shaping s) => Array([Num(s.Inner), Num(s.Outer), Num(s.Curve)]);

    [Fact]
    public void AScalarIsShapedAsTheFixtureShapesIt() =>
        Input.Check("scalar", c => Num(ShapingOf(c.GetProperty("spec")).Scalar(Read(c.GetProperty("spec"), "value"))));

    [Fact]
    public void AStickIsShapedAsTheFixtureShapesIt() =>
        Input.Check("stick", c =>
        {
            var spec = c.GetProperty("spec");
            var shaped = ShapingOf(spec).Stick(new Vector2(Read(spec, "x"), Read(spec, "y")));
            return Array([Num(shaped.X), Num(shaped.Y)]);
        });

    [Fact]
    public void AnAxisRangeReadsAValueAsTheFixtureReadsIt() =>
        Input.Check("range", c =>
        {
            var spec = c.GetProperty("spec");
            var range = new PadAxisRange(
                spec.GetProperty("min").GetInt32(),
                spec.GetProperty("max").GetInt32(),
                spec.GetProperty("flat").GetInt32());
            var value = spec.GetProperty("value").GetInt32();
            return new JsonObject
            {
                ["signed"] = Num(range.ToSigned(value)),
                ["unsigned"] = Num(range.ToUnsigned(value)),
                ["unipolar"] = range.IsUnipolar,
            };
        });

    [Fact]
    public void ACapabilityLineIsReadAsTheFixtureReadsIt() =>
        Input.Check("bitmap", c =>
            Array(EvdevCapabilities.ParseBitmap(c.GetProperty("spec").GetProperty("line").GetString()!)
                .Order()
                .Select(bit => (JsonNode?)JsonValue.Create(bit))));

    [Fact]
    public void ARecordSplitAnywhereDecodesAsTheFixtureDecodesIt() => Input.Check("decode", c => Decode(c.GetProperty("spec")));

    private static JsonArray Decode(JsonElement spec)
    {
        var timeBytes = spec.GetProperty("timeBytes").GetInt32();
        var all = spec.GetProperty("events").EnumerateArray()
            .Select(e => e.EnumerateArray().Select(n => n.GetInt32()).ToArray())
            .SelectMany(e => Evdev.Encode(e[0], e[1], e[2], timeBytes))
            .ToArray();
        var cuts = spec.GetProperty("cuts").EnumerateArray().Select(n => n.GetInt32()).Append(all.Length);
        var decoder = new EvdevDecoder(timeBytes);
        var chunks = new JsonArray();
        var from = 0;
        foreach (var end in cuts)
        {
            var events = decoder.Decode(all.AsSpan(from, end - from));
            from = end;
            chunks.Add(new JsonObject
            {
                ["events"] = Array(events.Select(e => (JsonNode?)Array([e.Type, e.Code, e.Value]))),
                ["partial"] = decoder.HasPartial,
            });
        }

        return chunks;
    }

    [Fact]
    public void ALayoutIsChosenAsTheFixtureChoosesIt() => Input.Check("layout", c => LayoutOf(c.GetProperty("spec")));

    private static JsonObject LayoutOf(JsonElement spec)
    {
        var abs = spec.GetProperty("abs");
        var layout = PadLayout.Of(
            new PadIdentity(
                spec.GetProperty("name").GetString()!,
                vendor: spec.GetProperty("vendor").GetInt32(),
                product: spec.GetProperty("product").GetInt32()),
            abs.ValueKind == JsonValueKind.Null ? null : EvdevCapabilities.Parse(ev: "b", key: string.Empty, abs: abs.GetString()!));
        return new JsonObject
        {
            ["name"] = layout.Name,
            ["labels"] = Array([layout.Labels.South, layout.Labels.East, layout.Labels.West, layout.Labels.North]),
            ["buttons"] = Named(layout.Buttons.ToDictionary(e => e.Key, e => Camel(e.Value))),
            ["axes"] = Named(layout.Axes.ToDictionary(e => e.Key, e => Camel(e.Value))),
            ["sticks"] = Triple(layout.Sticks),
            ["triggers"] = Triple(layout.Triggers),
        };
    }

    private static JsonObject Named(Dictionary<int, string> map) =>
        new(map.OrderBy(e => e.Key).Select(e => KeyValuePair.Create(e.Key.ToString(System.Globalization.CultureInfo.InvariantCulture), (JsonNode?)e.Value)));

    [Fact]
    public void AScriptedSessionReadsAsTheFixtureReadsIt() => Input.Check("pads", c => RunPads(c.GetProperty("spec")));

    private static RecordedPad DeviceOf(JsonElement spec) =>
        new(
            new PadIdentity(
                spec.GetProperty("name").GetString()!,
                vendor: spec.GetProperty("vendor").GetInt32(),
                product: spec.GetProperty("product").GetInt32(),
                uniq: spec.GetProperty("uniq").GetString()!,
                path: spec.GetProperty("path").GetString()!),
            EvdevCapabilities.Parse(ev: "b", key: "3000000000000 0 0 0 0", abs: spec.GetProperty("abs").GetString()!),
            new Dictionary<int, PadAxisRange>
            {
                [0] = PadAxisRange.Stick,
                [1] = PadAxisRange.Stick,
                [2] = spec.GetProperty("unipolarZ").GetBoolean() ? PadAxisRange.Trigger : PadAxisRange.Stick,
                [3] = PadAxisRange.Stick,
                [4] = PadAxisRange.Stick,
                [5] = PadAxisRange.Trigger,
                [0x10] = new PadAxisRange(-1, 1),
                [0x11] = new PadAxisRange(-1, 1),
            },
            spec.GetProperty("held").EnumerateArray().Select(n => n.GetInt32()).ToList(),
            canRumble: spec.GetProperty("canRumble").GetBoolean());

    private static JsonArray RunPads(JsonElement spec)
    {
        var devices = spec.GetProperty("pads").EnumerateArray().Select(DeviceOf).ToList();
        var attached = spec.GetProperty("attached").EnumerateArray().Select(a => a.GetBoolean()).ToList();
        var backend = new RecordedPads(devices.Where((_, i) => attached[i]));
        var pads = new Pads(backend, maxPads: spec.GetProperty("max").GetInt32());
        var output = new JsonArray();
        foreach (var step in spec.GetProperty("steps").EnumerateArray())
        {
            var result = Apply(step, devices, backend, pads);
            if (result is not null)
            {
                output.Add(result);
            }
        }

        return output;
    }

    private static JsonObject? Apply(JsonElement step, List<RecordedPad> devices, RecordedPads backend, Pads pads)
    {
        var kind = step.GetProperty("step").GetString();
        if (kind == "rumble")
        {
            return Rumble(step, devices, pads);
        }

        if (kind == "poll")
        {
            return Poll(pads);
        }

        Drive(kind!, step, devices[step.GetProperty("pad").GetInt32()], backend);
        return null;
    }

    private static void Drive(string kind, JsonElement step, RecordedPad device, RecordedPads backend)
    {
        switch (kind)
        {
            case "button":
                device.Button(step.GetProperty("code").GetInt32(), step.GetProperty("down").GetBoolean());
                break;
            case "axis":
                device.Axis(step.GetProperty("code").GetInt32(), step.GetProperty("value").GetInt32());
                break;
            case "send":
                device.Send(step.GetProperty("type").GetInt32(), step.GetProperty("code").GetInt32(), step.GetProperty("value").GetInt32());
                break;
            case "drift":
                device.Drift(
                    step.GetProperty("buttons").EnumerateArray().Select(n => n.GetInt32()).ToList(),
                    step.GetProperty("axes").EnumerateObject().Select(p => KeyValuePair.Create(int.Parse(p.Name, System.Globalization.CultureInfo.InvariantCulture), p.Value.GetInt32())).ToList());
                break;
            case "detach":
                if (backend.Devices.Contains(device))
                {
                    backend.Detach(device);
                }

                break;
            default:
                if (!backend.Devices.Contains(device) && device.IsAlive)
                {
                    backend.Attach(device);
                }

                break;
        }
    }

    private static JsonObject Rumble(JsonElement step, List<RecordedPad> devices, Pads pads)
    {
        var before = devices.Select(d => d.Rumbles.Count).ToList();
        var slot = step.GetProperty("slot").GetInt32();
        pads.Rumble(slot, Fixture.Number(step.GetProperty("strong")), Fixture.Number(step.GetProperty("weak")), TimeSpan.FromMilliseconds(step.GetProperty("ms").GetInt32()));
        var calls = devices
            .Select((d, i) => d.Rumbles.Skip(before[i]).Select(r => (JsonNode?)Array([i, Num(r.Strong), Num(r.Weak), (int)r.Duration.TotalMilliseconds])))
            .SelectMany(r => r);
        return new JsonObject { ["rumble"] = Array(calls), ["can"] = pads.CanRumble(slot) };
    }

    private static JsonObject Poll(Pads pads)
    {
        var frame = pads.Poll();
        return new JsonObject
        {
            ["events"] = Array(frame.Events.Select(e => (JsonNode?)e.ToString())),
            ["pads"] = Array(frame.Pads.Select(p => (JsonNode?)StateOf(p))),
            ["count"] = frame.Count,
            ["active"] = frame.IsActive,
        };
    }

    private static JsonObject StateOf(PadState state)
    {
        var buttons = Enum.GetValues<PadButton>();
        var axes = Enum.GetValues<PadAxis>();
        return new JsonObject
        {
            ["slot"] = state.Slot,
            ["connected"] = state.Connected,
            ["name"] = state.Identity.Name,
            ["held"] = Array(state.Held.Select(b => (JsonNode?)Camel(b))),
            ["pressed"] = Array(buttons.Where(state.WasPressed).Select(b => (JsonNode?)Camel(b))),
            ["released"] = Array(buttons.Where(state.WasReleased).Select(b => (JsonNode?)Camel(b))),
            ["axes"] = Array(axes.Select(a => Num(state.Axis(a)))),
            ["raw"] = Array(axes.Select(a => Num(state.RawAxis(a)))),
        };
    }
}
