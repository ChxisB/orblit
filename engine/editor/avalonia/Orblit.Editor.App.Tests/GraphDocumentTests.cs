using Orblit.Editor.Bridge;

namespace Orblit.Editor.App.Tests;

public sealed class GraphDocumentTests
{
    const string Document = """
        {
          "base_type": "Entity",
          "error": "",
          "variables": [
            {"name": "speed", "type": 3, "default": "2.5", "exported": true, "hint": 0, "hint_string": "", "category": "Movement", "description": "How fast it goes"}
          ],
          "signals": [
            {"name": "hit", "arguments": []},
            {"name": "scored", "arguments": [{"name": "amount", "type": 2}]}
          ],
          "functions": [
            {
              "name": "main",
              "error": "",
              "steps": [
                {"id": 1, "kind": "entry", "known": true, "category": "Flow", "x": 10.5, "y": -4, "parameters": {}, "problem": "",
                 "inputs": [], "outputs": [{"name": "then", "sort": "flow", "type": 0, "parameter": "", "optional": false}],
                 "growth": {"side": "output", "minimum": 2, "count": 3, "maximum": 64}},
                {"id": 2, "kind": "set", "known": true, "category": "Variables", "x": 200, "y": 0, "parameters": {"name": "\"speed\""}, "problem": "",
                 "inputs": [{"name": "run", "sort": "flow", "type": 0, "parameter": "", "optional": false}, {"name": "value", "sort": "data", "type": 3, "parameter": "", "optional": true}],
                 "outputs": [], "growth": {"side": "none", "minimum": 0, "count": 0, "maximum": 64}}
              ],
              "wires": [{"from_step": 1, "from_port": 0, "to_step": 2, "to_port": 0}],
              "frames": [{"id": 1, "title": "Setup", "x": 0, "y": -20, "width": 300.5, "height": 150, "collapsed": true}]
            }
          ],
          "warnings": [{"kind": "unused_variable", "function": "", "step": 0, "name": "speed", "text": "Nothing reads speed."}]
        }
        """;

    [Fact]
    public void ReadsStepsPortsWiresVariablesAndSignals()
    {
        var document = GraphDocument.Parse(Document);

        Assert.Equal("Entity", document.BaseType);
        var function = Assert.Single(document.Functions);
        Assert.Equal("main", function.Name);
        Assert.Equal(2, function.Steps.Count);

        var set = function.Steps[1];
        Assert.Equal("set", set.Kind);
        Assert.Equal("\"speed\"", set.Parameters["name"]);
        Assert.Equal(GraphPortSort.Flow, set.Inputs[0].Sort);
        Assert.Equal(GraphPortSort.Data, set.Inputs[1].Sort);
        Assert.True(set.Inputs[1].Optional);
        Assert.Equal(-4, function.Steps[0].Y);
        Assert.Equal(new GraphGrowth(GraphGrowthSide.Output, 2, 3, 64), function.Steps[0].Growth);
        Assert.Equal(GraphGrowthSide.None, set.Growth.Side);
        Assert.Equal(new GraphWire(1, 0, 2, 0), Assert.Single(function.Wires));
        Assert.Equal(new GraphFrame(1, "Setup", 0, -20, 300.5, 150, true), Assert.Single(function.Frames));

        var speed = Assert.Single(document.Variables);
        Assert.Equal("2.5", speed.Default);
        Assert.True(speed.Exported);
        Assert.Empty(document.Signals[0].Arguments);
        Assert.Equal(new GraphTyped("amount", 2), Assert.Single(document.Signals[1].Arguments));
        Assert.Equal("unused_variable", Assert.Single(document.Warnings).Kind);
    }

    [Fact]
    public void ReadsTheListThePaletteGets()
    {
        var entries = GraphStepEntry.ParseList("""[{"kind": "constant", "group": "Values", "label": "Constant", "parameters": {"value": "0"}}]""");

        var entry = Assert.Single(entries);
        Assert.Equal("constant", entry.Kind);
        Assert.Equal("0", entry.Parameters["value"]);
    }

    [Theory]
    [InlineData(1, "false")]
    [InlineData(2, "0")]
    [InlineData(3, "0.0")]
    [InlineData(4, "\"\"")]
    [InlineData(9, "Vector3(0, 0, 0)")]
    [InlineData(27, "{}")]
    [InlineData(28, "[]")]
    [InlineData(29, "PackedByteArray()")]
    public void TheZeroOfATypeIsTheTextTheEngineStartsItAs(int type, string text)
    {
        Assert.Equal(text, VariantText.ZeroOf(type));
    }

    [Theory]
    [InlineData(23)]
    [InlineData(25)]
    [InlineData(26)]
    [InlineData(-1)]
    [InlineData(39)]
    public void ATypeWithNoTextHasNoZero(int type)
    {
        Assert.Null(VariantText.ZeroOf(type));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("null")]
    public void RefusesTextTheEngineDoesNotWrite(string text)
    {
        Assert.Throws<InvalidDataException>(() => GraphDocument.Parse(text));
        Assert.Throws<InvalidDataException>(() => GraphStepEntry.ParseList(text));
    }
}
