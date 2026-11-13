using Avalonia.Controls;
using Avalonia.Interactivity;
using Orblit.Editor.App.Graph;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App.Tests.Graph;

public sealed class ProblemsListTests
{
    static GraphStep Step(int id, string problem = "", bool known = true) =>
        FakeGraphSource.MakeStep(id, "print", 0, 0, [], []) with { Problem = problem, Known = known };

    static GraphFunction Function(string name, params GraphStep[] steps) => new(name, "", steps, [], []);

    static GraphDocument Document(string error, IReadOnlyList<GraphWarning> warnings, params GraphFunction[] functions) =>
        new("Entity", error, [], [], functions, warnings);

    static GraphWarning Warning(string function, int step, string text) => new("unreachable_step", function, step, "", text);

    [Fact]
    public void Find_lists_the_script_error_then_step_errors_then_warnings()
    {
        var document = Document(
            "Parse error.",
            [Warning("main", 3, "Nothing runs this step."), new GraphWarning("unused_variable", "", 0, "total", "Nothing reads total.")],
            Function("main", Step(1), Step(2, "The step needs a name.")),
            Function("helper", Step(7, "Wire a value to the input.")));

        Assert.Equal(
            [
                new Problem(true, "", 0, "Parse error."),
                new Problem(true, "helper", 7, "Wire a value to the input."),
                new Problem(true, "main", 2, "The step needs a name."),
                new Problem(false, "main", 3, "Nothing runs this step."),
                new Problem(false, "", 0, "Nothing reads total."),
            ],
            ProblemsList.Find(document));
    }

    [Fact]
    public void A_step_the_engine_does_not_know_is_an_error()
    {
        var found = ProblemsList.Find(Document("", [], Function("main", Step(4, known: false))));

        Assert.Equal([new Problem(true, "main", 4, "Unknown step")], found);
    }

    [Fact]
    public void A_clean_document_says_there_are_no_problems()
    {
        UiThread.Run(() =>
        {
            var list = new ProblemsList(Document("", [], Function("main", Step(1))), (_, _) => { });

            Assert.NotNull(list.Find<TextBlock>("problems:empty"));
        });
    }

    [Fact]
    public void A_row_for_a_step_jumps_to_its_function_and_step()
    {
        UiThread.Run(() =>
        {
            var jumps = new List<string>();
            var list = new ProblemsList(Document("", [Warning("main", 3, "Nothing runs this step.")], Function("helper", Step(7, "Wire a value."))), (function, step) => jumps.Add($"{function} {step}"));

            list.Find<Button>("problem:helper:7")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            list.Find<Button>("problem:main:3")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            Assert.Equal(["helper 7", "main 3"], jumps);
            Assert.Equal("Error in helper, step 7", list.Find<Button>("problem:helper:7")!.Content);
            Assert.Equal("Wire a value.", list.Find<TextBlock>("problem:helper:7:text")!.Text);
        });
    }

    [Fact]
    public void A_problem_with_no_step_is_a_line_and_not_a_jump()
    {
        UiThread.Run(() =>
        {
            var list = new ProblemsList(Document("Parse error.", [], Function("main", Step(1))), (_, _) => Assert.Fail("No step to jump to."));

            Assert.Equal("Parse error.", list.Find<TextBlock>("problem:0")!.Text);
            Assert.Null(list.Find<Button>("problem::0"));
        });
    }
}
