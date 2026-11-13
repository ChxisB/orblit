using Orblit.Editor.Bridge;

namespace Orblit.Editor.App.Tests.Graph;

public sealed class ParentCallTests
{
    static readonly GraphStepEntry[] s_overrides =
    [
        new("event", "Entity", "_process", new Dictionary<string, string> { ["arguments"] = "[{\"name\": \"delta\", \"type\": 3}]" }),
        new("event", "Object", "_get_property_list", new Dictionary<string, string> { ["arguments"] = "[]" }),
    ];

    static GraphStepEntry? Offer(string function, string text = "", GraphDraggedSide side = GraphDraggedSide.None, GraphPortSort sort = GraphPortSort.Flow) =>
        ParentCall.EntryFor(function, s_overrides, text, side, sort);

    [Fact]
    public void A_function_that_overrides_a_virtual_is_offered_a_call_to_the_class_that_declares_it()
    {
        var entry = Offer("_process");

        Assert.NotNull(entry);
        Assert.Equal(("call_parent", "Calls", "Call parent _process"), (entry.Kind, entry.Group, entry.Label));
        Assert.Equal("\"Entity\"", entry.Parameters["class"]);
        Assert.Equal("\"_process\"", entry.Parameters["method"]);
        Assert.Equal(s_overrides[0].Parameters["arguments"], entry.Parameters["arguments"]);
    }

    [Fact]
    public void A_function_that_overrides_nothing_is_offered_nothing()
    {
        Assert.Null(Offer("_ready"));
        Assert.Null(Offer("go"));
    }

    [Theory]
    [InlineData("parent", true)]
    [InlineData("call parent process", true)]
    [InlineData("_process", true)]
    [InlineData("translate", false)]
    public void The_search_text_must_match_the_group_or_the_label(string text, bool offered) => Assert.Equal(offered, Offer("_process", text) is not null);

    [Fact]
    public void A_wire_of_data_is_not_offered_the_step_and_a_wire_of_flow_is()
    {
        Assert.Null(Offer("_process", side: GraphDraggedSide.Output, sort: GraphPortSort.Data));
        Assert.NotNull(Offer("_process", side: GraphDraggedSide.Output, sort: GraphPortSort.Flow));
    }
}
