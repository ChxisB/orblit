namespace Orblit.Stage.Tests;

// The one cooked shader that the surface and document tests both wear.
internal static class PulseShader
{
    public static readonly object?[] Declared =
    [
        Entry("speed", "float", instance: true, @default: 1.5),
        Entry(
            "tint",
            "vec4",
            instance: true,
            hints: new OrderedDictionary<string, object?> { ["source_color"] = new List<object?>() },
            @default: new List<object?> { 1L, 0.5, 0.25, 1L }),
        Entry("glow", "bool", instance: false),
        Entry(
            "noise",
            "sampler2D",
            instance: false,
            hints: new OrderedDictionary<string, object?> { ["filter_nearest"] = new List<object?>() }),
    ];

    public static readonly CookedSurface Cooked = CookedSurface.FromParams("cook/pulse.filamat", Declared);

    public static OrderedDictionary<string, object?> Entry(
        string name,
        string type,
        bool instance,
        OrderedDictionary<string, object?>? hints = null,
        object? @default = null)
    {
        var entry = new OrderedDictionary<string, object?> { ["name"] = name, ["type"] = type, ["instance"] = instance };
        if (hints is not null)
        {
            entry["hints"] = hints;
        }

        if (@default is not null)
        {
            entry["default"] = @default;
        }

        return entry;
    }
}
