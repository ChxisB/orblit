using Orblit.Editor.App.Graph;
using Orblit.Editor.Bridge;
using static Orblit.Editor.App.Tests.Graph.FakeGraphSource;

namespace Orblit.Editor.App.Tests.Graph;

public sealed class GraphSentencesTests
{
    static readonly HashSet<string> s_starts = ["_frame", "_start"];

    static GraphStep Entry(int id) => MakeStep(id, "entry", 0, 0, [], [Flow("then")]);

    static GraphStep Plain(int id, string kind) => MakeStep(id, kind, 0, 0, [Flow("run")], [Flow("then")]);

    static GraphStep Branch(int id) => MakeStep(id, "branch", 0, 0, [Flow("run"), Data("condition", 1, "condition")], [Flow("then"), Flow("else")]);

    static GraphStep Each(int id) => MakeStep(id, "each", 0, 0, [Flow("run"), Data("list", 28, "list")], [Flow("body"), Flow("done"), Data("item", 0), Data("index", 2)]);

    static GraphWire Wire(int from, int port, int to) => new(from, port, to, 0);

    static GraphDocument Document(params GraphFunction[] functions) => new("Entity", "", [], [], functions, []);

    static GraphFunction Function(string name, GraphStep[] steps, params GraphWire[] wires) => new(name, "", steps, wires, []);

    static string[] Read(GraphDocument document) =>
        [.. GraphSentences.Of(document, s_starts).SelectMany(block => block.Lines.Select(line => $"{new string(' ', line.Depth * 2)}{line.Text}").Prepend($"[{block.Start}]"))];

    [Fact]
    public void One_start_reads_its_sentence_then_each_step_by_number_and_plain_name_in_run_order()
    {
        var frame = Function("_frame", [Entry(1), Plain(3, "free"), Plain(2, "print")], Wire(1, 0, 2), Wire(2, 0, 3));

        Assert.Equal(["[Every frame]", "2 Print text", "3 Remove"], Read(Document(frame)));
    }

    [Fact]
    public void A_branch_reads_as_if_then_otherwise_with_each_way_one_deeper()
    {
        var frame = Function("_frame", [Entry(1), Branch(2), Plain(3, "print"), Plain(4, "wait"), Plain(5, "free")], Wire(1, 0, 2), Wire(2, 0, 3), Wire(3, 0, 4), Wire(2, 1, 5));

        Assert.Equal(["[Every frame]", "2 If", "  then 3 Print text", "  4 Wait", "  Otherwise, 5 Remove"], Read(Document(frame)));
    }

    [Fact]
    public void A_loop_reads_its_body_one_deeper_and_goes_on_after_it()
    {
        var frame = Function("_frame", [Entry(1), Each(2), Plain(3, "print"), Plain(4, "free")], Wire(1, 0, 2), Wire(2, 0, 3), Wire(2, 1, 4));

        Assert.Equal(["[Every frame]", "2 For each item", "  3 Print text", "4 Remove"], Read(Document(frame)));
    }

    [Fact]
    public void A_step_met_twice_reads_as_back_to_its_number_and_ends_the_way()
    {
        var frame = Function("_frame", [Entry(1), Branch(2), Plain(3, "print"), Plain(4, "wait")], Wire(1, 0, 2), Wire(2, 0, 3), Wire(3, 0, 4), Wire(2, 1, 4), Wire(4, 0, 2));

        Assert.Equal(["[Every frame]", "2 If", "  then 3 Print text", "  4 Wait", "  Back to step 2", "  Otherwise, back to step 4"], Read(Document(frame)));
    }

    [Fact]
    public void No_start_reads_nothing_and_an_action_is_left_out()
    {
        var action = Function("open_door", [Entry(1), Plain(2, "print")], Wire(1, 0, 2));

        Assert.Empty(GraphSentences.Of(Document(action), s_starts));
        Assert.Empty(GraphSentences.Of(Document(), s_starts));
    }

    [Fact]
    public void A_start_with_no_step_after_it_has_no_lines_and_a_kind_with_no_plain_name_goes_by_its_kind()
    {
        var start = Function("_start", [Entry(1)]);
        var frame = Function("_frame", [Entry(1), Plain(2, "play_sound")], Wire(1, 0, 2));

        Assert.Equal(["[The game starts]", "[Every frame]", "2 Play sound"], Read(Document(start, frame)));
    }
}
