using System.Text.Json;
using Orblit.Editor.App.Graph;

namespace Orblit.Editor.App.Tests.Graph;

public sealed class GraphBreakpointsTests
{
    const string Main = "res://a.ograph#main";
    const string Start = "res://b.ograph#_start";

    static GraphBreakpoints With(params (string Source, int Step)[] marks)
    {
        var breakpoints = new GraphBreakpoints();
        foreach (var (source, step) in marks)
        {
            breakpoints.Toggle(source, step);
        }

        return breakpoints;
    }

    [Fact]
    public void The_text_of_breakpoints_loads_back_as_the_same_breakpoints()
    {
        var text = With((Main, 5), (Main, 2), (Start, 7)).ToJson();

        var loaded = new GraphBreakpoints();
        loaded.Load(text);

        Assert.Equal([(Main, 2), (Main, 5), (Start, 7)], loaded.All.OrderBy(mark => mark.Source).ThenBy(mark => mark.Step));
    }

    [Fact]
    public void The_text_does_not_depend_on_the_order_the_breakpoints_were_set()
    {
        Assert.Equal(With((Main, 5), (Start, 7), (Main, 2)).ToJson(), With((Start, 7), (Main, 2), (Main, 5)).ToJson());
    }

    [Fact]
    public void A_breakpoint_that_was_taken_off_is_not_in_the_text()
    {
        var breakpoints = With((Main, 2), (Main, 5));
        breakpoints.Toggle(Main, 2);

        var loaded = new GraphBreakpoints();
        loaded.Load(breakpoints.ToJson());

        Assert.Equal([(Main, 5)], loaded.All);
    }

    [Fact]
    public void Loading_replaces_the_breakpoints_that_were_there()
    {
        var breakpoints = With((Main, 2));

        breakpoints.Load(With((Start, 7)).ToJson());

        Assert.Equal([(Start, 7)], breakpoints.All);
    }

    [Fact]
    public void Loading_raises_no_change_because_nothing_was_set_by_a_person()
    {
        var breakpoints = new GraphBreakpoints();
        var raised = 0;
        breakpoints.Changed += (_, _, _) => raised++;

        breakpoints.Load(With((Main, 2)).ToJson());

        Assert.Equal(0, raised);
        Assert.True(breakpoints.Has(Main, 2));
    }

    [Fact]
    public void No_text_loads_as_no_breakpoints()
    {
        var breakpoints = With((Main, 2));

        breakpoints.Load(null);

        Assert.Empty(breakpoints.All);
    }

    [Fact]
    public void A_source_with_no_steps_is_skipped()
    {
        var breakpoints = new GraphBreakpoints();

        breakpoints.Load("{\"res://a.ograph#main\": null, \"res://b.ograph#_start\": [7]}");

        Assert.Equal([(Start, 7)], breakpoints.All);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[1, 2]")]
    [InlineData("null")]
    [InlineData("{\"res://a.ograph#main\": \"two\"}")]
    [InlineData("{\"res://a.ograph#main\": [\"two\"]}")]
    public void Text_that_is_not_breakpoints_is_refused_and_leaves_none(string text)
    {
        var breakpoints = With((Main, 2));

        Assert.Throws<JsonException>(() => breakpoints.Load(text));

        Assert.Empty(breakpoints.All);
    }
}
