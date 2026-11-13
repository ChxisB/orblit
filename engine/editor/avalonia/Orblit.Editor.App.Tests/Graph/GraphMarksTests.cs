using Avalonia;
using Avalonia.Input;
using Orblit.Editor.App.Graph;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App.Tests.Graph;

public sealed class GraphMarksTests
{
    const string Script = "res://sum.ograph";
    const string Main = "res://sum.ograph#main";

    static (FakeGraphSource Source, GraphBreakpoints Marks, GraphCanvas Canvas) Open()
    {
        var source = FakeGraphSource.Sample();
        var marks = new GraphBreakpoints();
        var canvas = new GraphCanvas(source);
        canvas.Scene.Script = Script;
        canvas.Scene.Breakpoints = marks;
        return (source, marks, canvas);
    }

    static void Click(GraphCanvas canvas, Point at)
    {
        canvas.Gestures.Press(at, MouseButton.Left, KeyModifiers.None, 1);
        canvas.Gestures.Release(at);
    }

    static Point MarginOf(GraphCanvas canvas, int step) => canvas.Scene.Find(step)!.Origin + new Vector(-8, 10);

    [Fact]
    public void A_click_in_the_margin_of_a_step_sets_a_breakpoint_and_a_second_click_takes_it_off()
    {
        UiThread.Run(() =>
        {
            var (_, marks, canvas) = Open();

            Click(canvas, MarginOf(canvas, 2));
            Assert.Contains(2, canvas.Scene.Breakpointed);
            Assert.True(marks.Has(Main, 2));

            Click(canvas, MarginOf(canvas, 2));
            Assert.Empty(canvas.Scene.Breakpointed);
            Assert.False(marks.Has(Main, 2));
        });
    }

    [Fact]
    public void A_click_in_the_margin_selects_nothing_and_edits_nothing()
    {
        UiThread.Run(() =>
        {
            var (source, _, canvas) = Open();

            Click(canvas, MarginOf(canvas, 2));

            Assert.Empty(canvas.Scene.SelectedSteps);
            Assert.Empty(source.Calls);
        });
    }

    [Fact]
    public void A_breakpoint_tells_its_listeners_the_function_the_step_and_whether_it_is_set()
    {
        UiThread.Run(() =>
        {
            var (_, marks, canvas) = Open();
            var heard = new List<string>();
            marks.Changed += (source, step, set) => heard.Add($"{source} {step} {set}");

            Click(canvas, MarginOf(canvas, 3));
            Click(canvas, MarginOf(canvas, 3));

            Assert.Equal([$"{Main} 3 True", $"{Main} 3 False"], heard);
        });
    }

    [Fact]
    public void A_breakpoint_goes_with_its_step_and_stays_through_an_edit_to_another()
    {
        UiThread.Run(() =>
        {
            var (source, marks, canvas) = Open();
            Click(canvas, MarginOf(canvas, 2));
            Click(canvas, MarginOf(canvas, 3));
            var heard = new List<string>();
            marks.Changed += (name, step, set) => heard.Add($"{step} {set}");

            source.MoveStep(2, 300, 40);
            Assert.Equal([2, 3], canvas.Scene.Breakpointed.Order());

            source.RemoveStep(3);
            Assert.Equal([2], canvas.Scene.Breakpointed);
            Assert.Equal(["3 False"], heard);
        });
    }

    [Fact]
    public void Each_function_keeps_its_own_breakpoints()
    {
        UiThread.Run(() =>
        {
            var (_, marks, canvas) = Open();
            Click(canvas, MarginOf(canvas, 2));

            canvas.Scene.Load(new GraphFunction("other", "", [FakeGraphSource.MakeStep(2, "print", 0, 0, [], [])], [], []));
            Assert.Empty(canvas.Scene.Breakpointed);
            Click(canvas, MarginOf(canvas, 2));
            Assert.True(marks.Has("res://sum.ograph#other", 2));

            canvas.Reload();
            Assert.Equal([2], canvas.Scene.Breakpointed);
            Assert.True(marks.Has(Main, 2));
        });
    }

    [Fact]
    public void A_reroute_has_no_margin()
    {
        var reroute = new StepView(FakeGraphSource.MakeStep(9, "reroute", 100, 100, [], []), new HashSet<PinRef>());

        Assert.Null(reroute.HitAt(reroute.Origin + new Vector(-8, 8)));
    }

    [Fact]
    public void The_margin_is_the_strip_beside_the_header_and_nothing_else()
    {
        var step = new StepView(FakeGraphSource.MakeStep(1, "print", 100, 100, [], []), new HashSet<PinRef>());

        Assert.Equal(StepPart.Margin, step.HitAt(step.Origin + new Vector(-8, 10))?.Part);
        Assert.Null(step.HitAt(step.Origin + new Vector(-30, 10)));
        Assert.Null(step.HitAt(step.Origin + new Vector(-8, -4)));
    }

    [Fact]
    public void A_warning_marks_the_step_it_names_in_the_function_on_show()
    {
        UiThread.Run(() =>
        {
            var source = FakeGraphSource.Sample();
            source.Put(new GraphWarning("unreachable_step", "main", 3, "", "Nothing runs this step."));
            source.Put(new GraphWarning("unreachable_step", "other", 2, "", "Nothing runs this step."));
            source.Put(new GraphWarning("unused_variable", "", 0, "total", "Nothing reads total."));

            var canvas = new GraphCanvas(source);

            Assert.Equal([3], canvas.Scene.Warned);
        });
    }

    [Fact]
    public void A_warning_that_arrives_with_an_edit_marks_its_step()
    {
        UiThread.Run(() =>
        {
            var source = FakeGraphSource.Sample();
            var canvas = new GraphCanvas(source);
            Assert.Empty(canvas.Scene.Warned);

            source.Put(new GraphWarning("unreachable_step", "main", 2, "", "Nothing runs this step."));
            source.MoveStep(2, 300, 0);

            Assert.Equal([2], canvas.Scene.Warned);
        });
    }

    [Fact]
    public void The_step_the_game_stopped_on_is_marked_in_its_own_script_and_function_only()
    {
        UiThread.Run(() =>
        {
            var (_, _, canvas) = Open();
            Assert.False(Marks(canvas, 2).HasFlag(StepMarks.Current));

            canvas.Scene.StoppedAt = new DebugPlace(Script, "main", 2);
            Assert.True(Marks(canvas, 2).HasFlag(StepMarks.Current));
            Assert.False(Marks(canvas, 3).HasFlag(StepMarks.Current));

            canvas.Scene.StoppedAt = new DebugPlace(Script, "other", 2);
            Assert.False(Marks(canvas, 2).HasFlag(StepMarks.Current));

            canvas.Scene.StoppedAt = new DebugPlace("res://other.ograph", "main", 2);
            Assert.False(Marks(canvas, 2).HasFlag(StepMarks.Current));

            canvas.Scene.StoppedAt = null;
            Assert.Equal(StepMarks.None, Marks(canvas, 2));
        });
    }

    [Fact]
    public void The_breakpoints_of_every_script_are_listed_with_their_sources()
    {
        var marks = new GraphBreakpoints();
        marks.Toggle(Main, 2);
        marks.Toggle(Main, 3);
        marks.Toggle("res://other.ograph#_start", 1);
        marks.Toggle(Main, 3);

        Assert.Equal([("res://other.ograph#_start", 1), (Main, 2)], marks.All.OrderBy(pair => pair.Source, StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("res://sum.ograph#main", "res://sum.ograph", "main")]
    [InlineData("res://a#b.ograph#_start", "res://a#b.ograph", "_start")]
    public void A_source_splits_at_its_last_hash_into_the_script_and_the_function(string source, string script, string function)
    {
        Assert.Equal((script, function), GraphBreakpoints.Split(source));
    }

    [Fact]
    public void A_source_without_a_hash_is_not_a_graph_function()
    {
        Assert.Null(GraphBreakpoints.Split("res://tool.cs"));
    }

    static StepMarks Marks(GraphCanvas canvas, int step) => canvas.Scene.MarksOf(canvas.Scene.Find(step)!);
}
