using System.Text.Json;
using Orblit.Parity;

namespace Orblit.Progress.Tests;

public class ProgressParityTests
{
    private static readonly Fixture Golden = Fixture.Load("orblit_progress", "progress");

    private static List<JsonElement> Cases(string op)
    {
        var cases = Golden.Cases.Where(c => c.GetProperty("op").GetString() == op).ToList();
        Assert.True(cases.Count > 0, $"the fixture has no {op} cases");
        return cases;
    }

    private static string Why(JsonElement c, string what) =>
        $"{what}, case {c.GetRawText()[..Math.Min(c.GetRawText().Length, 160)]}, from fixture commit {Golden.SourceCommit}";

    private static ProgressName[] Names(JsonElement names) =>
        [.. names.EnumerateArray().Select(n => n[0].GetString() == "w"
            ? (ProgressName)new Whole(n[1].GetString()!)
            : new Slice(n[1].GetString()!, n[2].GetString()!, n[3].GetInt32(), n[4].GetInt32()))];

    private static ProgressSchema? TryBuild(JsonElement c)
    {
        try
        {
            return new ProgressSchema(c.GetProperty("version").GetInt32(), Names(c.GetProperty("names")));
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static string NameAt(JsonElement c, int i) => c.GetProperty("names")[i][1].GetString()!;

    private static int NameCount(JsonElement c) => c.GetProperty("names").GetArrayLength();

    private static long[] Gets(ProgressTable table, JsonElement c) =>
        [.. Enumerable.Range(0, NameCount(c)).Select(i => table.Get(NameAt(c, i)))];

    // What a JSON reader gives: whole numbers are integers, other numbers are doubles.
    private static object? Plain(JsonElement json) => json.ValueKind switch
    {
        JsonValueKind.Null => null,
        JsonValueKind.String => json.GetString(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Number => WholeOrDouble(json),
        JsonValueKind.Array => json.EnumerateArray().Select(Plain).ToList(),
        JsonValueKind.Object => json.EnumerateObject().ToDictionary(p => p.Name, p => Plain(p.Value)),
        _ => throw new InvalidDataException($"unexpected {json.ValueKind}"),
    };

    // Not a conditional expression: its type would be double and turn every whole number into one.
    private static object WholeOrDouble(JsonElement number)
    {
        if (number.GetRawText().AsSpan().IndexOfAny('.', 'e', 'E') < 0)
        {
            return number.GetInt64();
        }

        return number.GetDouble();
    }

    private static string? Written(ProgressTable? table) => table is null ? null : Convert.ToHexStringLower(table.Encode());

    private static ProgressTable? TryDecodeTable(ProgressSchema schema, byte[] bytes)
    {
        try
        {
            return ProgressTable.Decode(schema, bytes);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    [Fact]
    public void SchemasMatchTheFixture()
    {
        foreach (var c in Cases("schema"))
        {
            var schema = TryBuild(c);
            var expect = c.GetProperty("expect");
            if (expect.ValueKind == JsonValueKind.Null)
            {
                Assert.True(schema is null, Why(c, "the port accepted a schema the fixture refused"));
                continue;
            }

            Assert.True(schema is not null, Why(c, "the port refused a schema the fixture accepted"));
            Assert.Equal(expect.GetProperty("slotCount").GetInt32(), schema.SlotCount);
            foreach (var p in expect.GetProperty("places").EnumerateArray())
            {
                Assert.Equal(
                    new Place(p[1].GetInt32(), p[2].GetInt32(), p[3].GetInt32()),
                    schema.PlaceOf(p[0].GetString()!));
            }

            var fits = expect.GetProperty("fits");
            for (var i = 0; i < NameCount(c); i++)
            {
                var fitting = fits[i].EnumerateArray().Select(f => f.GetBoolean()).ToArray();
                long[] probes = [-1, 0, 1, 2, 7, 8, 255, 256, 0xFFFF, 0xFFFFFFFF, 0x100000000];
                Assert.True(
                    probes.Select(v => schema.Fits(NameAt(c, i), v)).SequenceEqual(fitting),
                    Why(c, $"fits of {NameAt(c, i)}"));
            }
        }
    }

    [Fact]
    public void TablesMatchTheFixture()
    {
        foreach (var c in Cases("table"))
        {
            var table = new ProgressTable(TryBuild(c)!);
            var steps = c.GetProperty("steps").EnumerateArray().ToArray();
            var expects = c.GetProperty("expect").EnumerateArray().ToArray();
            for (var i = 0; i < steps.Length; i++)
            {
                var (name, value) = (steps[i][0].GetString()!, steps[i][1].GetInt64());
                if (!expects[i].GetProperty("ok").GetBoolean())
                {
                    Assert.False(table.Schema.Fits(name, value), Why(c, $"step {i}: the port fits a value the fixture refused"));
                    Assert.Throws<ArgumentOutOfRangeException>(() => table.Set(name, value));
                    continue;
                }

                var (next, changes) = table.Set(name, value);
                table = next;
                Assert.Equal(expects[i].GetProperty("changes").GetString(), Convert.ToHexStringLower(changes.Encode()));
                Assert.Equal(expects[i].GetProperty("table").GetString(), Written(table));
                Assert.Equal(expects[i].GetProperty("gets").EnumerateArray().Select(g => g.GetInt64()), Gets(table, c));
            }
        }
    }

    [Fact]
    public void DecodedTablesMatchTheFixture()
    {
        foreach (var c in Cases("decodeTable"))
        {
            var table = TryDecodeTable(TryBuild(c)!, Convert.FromHexString(c.GetProperty("bytes").GetString()!));
            var expect = c.GetProperty("expect");
            if (expect.ValueKind == JsonValueKind.Null)
            {
                Assert.True(table is null, Why(c, "the port decoded a table the fixture refused"));
                continue;
            }

            Assert.True(table is not null, Why(c, "the port refused a table the fixture decoded"));
            Assert.Equal(expect.GetProperty("table").GetString(), Written(table));
            Assert.Equal(expect.GetProperty("gets").EnumerateArray().Select(g => g.GetInt64()), Gets(table, c));
        }
    }

    [Fact]
    public void DecodedChangesMatchTheFixture()
    {
        foreach (var c in Cases("decodeChanges"))
        {
            var changes = Changes.TryDecode(TryBuild(c)!, Convert.FromHexString(c.GetProperty("bytes").GetString()!));
            var expect = c.GetProperty("expect");
            var actual = changes is null ? null : Convert.ToHexStringLower(changes.Encode());
            Assert.True(
                expect.GetString() == actual,
                Why(c, $"the fixture {expect.GetString() ?? "refused"}, the port {actual ?? "refused"}"));
        }
    }

    [Fact]
    public void ConditionsMatchTheFixture()
    {
        foreach (var c in Cases("condition"))
        {
            Condition? condition = null;
            try
            {
                condition = Condition.FromJson(Plain(c.GetProperty("json")));
            }
            catch (FormatException)
            {
                // Refused, which the fixture may say too.
            }

            var expect = c.GetProperty("expect");
            Assert.True(
                (expect.ValueKind == JsonValueKind.Null) == (condition is null),
                Why(c, "the port and the fixture disagree about whether this is a condition"));
            if (condition is not null)
            {
                var difference = JsonCompare.FirstDifference(expect, JsonSerializer.SerializeToElement(condition.ToJson()));
                Assert.True(difference is null, Why(c, difference ?? string.Empty));
            }
        }
    }

    [Fact]
    public void ConditionListsMatchTheFixture()
    {
        foreach (var c in Cases("conditionList"))
        {
            IReadOnlyList<Condition>? list = null;
            try
            {
                list = Condition.ListFromJson(Plain(c.GetProperty("json")));
            }
            catch (FormatException)
            {
                // Refused, which the fixture may say too.
            }

            var expect = c.GetProperty("expect");
            Assert.True(
                (expect.ValueKind == JsonValueKind.Null) == (list is null),
                Why(c, "the port and the fixture disagree about whether this is a list"));
            if (list is not null)
            {
                var difference = JsonCompare.FirstDifference(
                    expect,
                    JsonSerializer.SerializeToElement(list.Select(x => x.ToJson())));
                Assert.True(difference is null, Why(c, difference ?? string.Empty));
            }
        }
    }

    [Fact]
    public void ComparisonsHoldAsTheyDoInTheFixture()
    {
        var schema = new ProgressSchema(
            1,
            [new Whole("flags"), new Whole("gold"), new Slice("quest.cook.stage", "flags", 0, 4)]);
        foreach (var c in Cases("holds"))
        {
            var table = new ProgressTable(schema)
                .Set("quest.cook.stage", c.GetProperty("stage").GetInt64()).Table
                .Set("gold", c.GetProperty("gold").GetInt64()).Table;
            var condition = new Condition(
                c.GetProperty("name").GetString()!,
                Compare.BySymbol(c.GetProperty("compare").GetString()!)!,
                c.GetProperty("want").GetInt64());

            Assert.True(condition.Holds(table) == c.GetProperty("expect").GetBoolean(), Why(c, "holds"));
        }
    }
}
