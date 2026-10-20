using System.Text.Json;
using Orblit.Parity;

namespace Orblit.Cells.Tests;

public class CellsParityTests
{
    private static Cell ToCell(JsonElement pair) => new(pair[0].GetInt64(), pair[1].GetInt64());

    private static List<JsonElement> Cases(Fixture fixture, string op)
    {
        var cases = fixture.Cases.Where(c => c.GetProperty("op").GetString() == op).ToList();
        Assert.True(cases.Count > 0, $"the fixture has no {op} cases");
        return cases;
    }

    [Fact]
    public void CellOfMatchesTheDartTwin()
    {
        var fixture = Fixture.Load("orblit_cells", "cells");

        foreach (var c in Cases(fixture, "cellOf"))
        {
            var size = c.GetProperty("size").GetDouble();
            var at = c.GetProperty("at");
            var expect = c.GetProperty("expect");
            var actual = new Grid(size).CellOf(at[0].GetDouble(), at[1].GetDouble());

            Assert.True(
                expect.ValueKind == JsonValueKind.Null
                    ? actual is null
                    : actual == ToCell(expect),
                $"size {size} at {at}: Dart {expect}, C# {actual}, from Dart commit {fixture.DartCommit}");
        }
    }

    [Fact]
    public void PlanMatchesTheDartTwin()
    {
        var fixture = Fixture.Load("orblit_cells", "cells");

        foreach (var c in Cases(fixture, "plan"))
        {
            var window = new CellWindow(
                new Grid(c.GetProperty("size").GetDouble()),
                loadRadius: c.GetProperty("loadRadius").GetInt64(),
                dropRadius: c.GetProperty("dropRadius").ValueKind == JsonValueKind.Null
                    ? null
                    : c.GetProperty("dropRadius").GetInt64(),
                behindWeight: c.GetProperty("behindWeight").GetDouble());
            var at = c.GetProperty("at");
            var facing = c.GetProperty("facing");
            var expect = c.GetProperty("expect");

            var plan = window.Plan(
                at[0].GetDouble(),
                at[1].GetDouble(),
                facingX: facing[0].GetDouble(),
                facingZ: facing[1].GetDouble(),
                loaded: c.GetProperty("loaded").EnumerateArray().Select(ToCell).ToHashSet(),
                pinned: c.GetProperty("pinned").EnumerateArray().Select(ToCell).ToHashSet());

            var where = $"{c.GetRawText()}, from Dart commit {fixture.DartCommit}";
            Assert.True(expect.GetProperty("load").EnumerateArray().Select(ToCell).SequenceEqual(plan.Load), "load " + where);
            Assert.True(expect.GetProperty("drop").EnumerateArray().Select(ToCell).SequenceEqual(plan.Drop), "drop " + where);
        }
    }
}
