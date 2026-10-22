using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Orblit.Cells;
using static Orblit.Parity.Dump;

namespace Orblit.Scene.Tests;

/// <summary>Plays one streaming case from the fixture: a world of cell files and a list of steps.</summary>
internal static class StreamingRun
{
    public static JsonNode Run(JsonElement input)
    {
        var world = new StreamWorld
        {
            Cost = Millis(input, "cost"),
        };
        foreach (var file in input.GetProperty("files").EnumerateObject())
        {
            world.Files[CellAt(file.Name)] = FileOf(file.Value);
        }

        var streamer = world.Streamer(
            input.GetProperty("radius").GetInt32(),
            input.GetProperty("inFlight").GetInt32(),
            Millis(input, "budget"));
        var steps = new JsonArray();
        foreach (var step in input.GetProperty("steps").EnumerateArray())
        {
            world.Now += Millis(step, "advance");
            streamer.Step(
                step.GetProperty("x").GetDouble(),
                step.GetProperty("z").GetDouble(),
                step.GetProperty("fx").GetDouble(),
                step.GetProperty("fz").GetDouble(),
                step.GetProperty("pinned").EnumerateArray().Select(static p => new Cell(p[0].GetInt64(), p[1].GetInt64())).ToHashSet());
            steps.Add(Observe(streamer, world));
        }

        return new JsonObject
        {
            ["steps"] = steps,
            ["asked"] = Arr(world.Asked, static cell => Text(Key(cell))),
            ["loads"] = Arr(world.Loads, Describe),
        };
    }

    private static TimeSpan Millis(JsonElement from, string name) => TimeSpan.FromMilliseconds(from.GetProperty(name).GetInt32());

    private static Cell CellAt(string key)
    {
        var parts = key.Split(',');
        return new Cell(long.Parse(parts[0], CultureInfo.InvariantCulture), long.Parse(parts[1], CultureInfo.InvariantCulture));
    }

    private static string Key(Cell cell) => string.Create(CultureInfo.InvariantCulture, $"{cell.X},{cell.Z}");

    private static object? FileOf(JsonElement value) => value.ValueKind == JsonValueKind.Null
        ? null
        : value.GetString() == "throws"
            ? new InvalidOperationException("the network")
            : Encoding.UTF8.GetBytes(value.GetString()!);

    private static long Micros(TimeSpan time) => time.Ticks / 10;

    private static JsonObject Observe(CellStreamer streamer, StreamWorld world) => new()
    {
        ["loaded"] = streamer.LastStep.Loaded,
        ["dropped"] = streamer.LastStep.Dropped,
        ["spent"] = Micros(streamer.LastStep.Spent),
        ["pending"] = streamer.Pending,
        ["cells"] = Arr(streamer.Loaded.Select(Key).Order(StringComparer.Ordinal), static key => Text(key)),
        ["ids"] = Arr(world.Scene.Entities, static entity => Text(entity.Id)),
    };

    private static JsonNode Describe(CellLoad load) => new JsonObject
    {
        ["cell"] = Key(load.Cell),
        ["fetch"] = Micros(load.Fetch),
        ["apply"] = Micros(load.Apply),
        ["bytes"] = load.Bytes,
        ["entities"] = load.Entities,
        ["problems"] = load.Problems.Count,
        ["failed"] = load.Error is not null,
    };
}
