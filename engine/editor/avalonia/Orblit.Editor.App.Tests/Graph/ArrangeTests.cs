using Avalonia.Controls;
using Orblit.Editor.App.Graph;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App.Tests.Graph;

public sealed class ArrangeTests
{
    // The sample with its steps at three different heights.
    static FakeGraphSource Uneven()
    {
        var source = FakeGraphSource.Sample();
        source.MoveStep(2, 260, 90);
        source.MoveStep(3, 520, 200);
        return source;
    }

    static GraphCanvas Open(FakeGraphSource source, params int[] selected)
    {
        _ = source.History;
        var canvas = new GraphCanvas(source);
        canvas.Select(selected);
        return canvas;
    }

    static void Click(GraphCanvas canvas, string id) => canvas.Find<Button>($"arrange:{id}")!.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

    static (double X, double Y) At(FakeGraphSource source, int id)
    {
        var step = source.Function!.Steps.Single(step => step.Id == id);
        return (step.X, step.Y);
    }

    [Fact]
    public void Align_top_puts_three_steps_on_one_row_and_undo_takes_it_back_in_one_step()
    {
        UiThread.Run(() =>
        {
            var source = Uneven();
            var canvas = Open(source, 1, 2, 3);

            Click(canvas, "align_top");

            Assert.Equal([0.0, 0.0, 0.0], new[] { At(source, 1).Y, At(source, 2).Y, At(source, 3).Y });
            Assert.Equal(260, At(source, 2).X);
            Assert.True(source.History.Undo().Ok);
            Assert.Equal(90, At(source, 2).Y);
            Assert.Equal(200, At(source, 3).Y);
            Assert.False(source.History.CanUndo);
        });
    }

    [Fact]
    public void Align_left_puts_the_steps_on_the_leftmost_edge()
    {
        UiThread.Run(() =>
        {
            var source = Uneven();
            var canvas = Open(source, 2, 3);

            Click(canvas, "align_left");

            Assert.Equal(260, At(source, 2).X);
            Assert.Equal(260, At(source, 3).X);
            Assert.Equal(200, At(source, 3).Y);
        });
    }

    [Fact]
    public void Spread_across_gives_equal_gaps_between_the_bounds_of_three_steps()
    {
        UiThread.Run(() =>
        {
            var source = FakeGraphSource.Sample();
            source.MoveStep(2, 100, 0);
            var canvas = Open(source, 1, 2, 3);

            Click(canvas, "spread_across");

            var bounds = new[] { 1, 2, 3 }.Select(id => canvas.Scene.Find(id)!.Bounds).OrderBy(box => box.Left).ToArray();
            Assert.Equal(bounds[1].Left - bounds[0].Right, bounds[2].Left - bounds[1].Right, 6);
            Assert.Equal(0, At(source, 1).X);
            Assert.Equal(520, At(source, 3).X);
        });
    }

    [Fact]
    public void A_row_has_the_gap_of_twenty_four_between_steps_on_the_highest_line()
    {
        UiThread.Run(() =>
        {
            var source = Uneven();
            var canvas = Open(source, 1, 2, 3);

            Click(canvas, "stack_row");

            var bounds = new[] { 1, 2, 3 }.Select(id => canvas.Scene.Find(id)!.Bounds).OrderBy(box => box.Left).ToArray();
            Assert.Equal(24, bounds[1].Left - bounds[0].Right, 6);
            Assert.Equal(24, bounds[2].Left - bounds[1].Right, 6);
            Assert.All(bounds, box => Assert.Equal(0, box.Top));
        });
    }

    [Fact]
    public void Straighten_puts_each_wire_level_from_the_pin_it_leaves_to_the_pin_it_enters()
    {
        UiThread.Run(() =>
        {
            var source = Uneven();
            var canvas = Open(source, 1, 2, 3);

            Click(canvas, "straighten");

            var scene = canvas.Scene;
            Assert.Equal(scene.Find(1)!.PinCenter(false, 0).Y, scene.Find(2)!.PinCenter(true, 0).Y, 6);
            Assert.Equal(scene.Find(2)!.PinCenter(false, 0).Y, scene.Find(3)!.PinCenter(true, 0).Y, 6);
            Assert.Equal(0, At(source, 1).Y);
        });
    }

    [Fact]
    public void Straighten_follows_a_chain_whichever_wire_the_graph_lists_first()
    {
        UiThread.Run(() =>
        {
            var source = new FakeGraphSource();
            source.Put(FakeGraphSource.MakeStep(1, "entry", 0, 0, [], [FakeGraphSource.Flow("then")]));
            source.Put(FakeGraphSource.MakeStep(2, "print", 260, 90, [FakeGraphSource.Flow("run")], [FakeGraphSource.Flow("then")]));
            source.Put(FakeGraphSource.MakeStep(3, "print", 520, 200, [FakeGraphSource.Flow("run")], [FakeGraphSource.Flow("then")]));
            source.Put(new GraphWire(2, 0, 3, 0));
            source.Put(new GraphWire(1, 0, 2, 0));
            var canvas = Open(source, 1, 2, 3);

            Click(canvas, "straighten");

            var scene = canvas.Scene;
            Assert.Equal(scene.Find(1)!.PinCenter(false, 0).Y, scene.Find(2)!.PinCenter(true, 0).Y, 6);
            Assert.Equal(scene.Find(2)!.PinCenter(false, 0).Y, scene.Find(3)!.PinCenter(true, 0).Y, 6);
        });
    }

    [Fact]
    public void Straighten_does_not_pull_a_step_that_is_not_selected()
    {
        UiThread.Run(() =>
        {
            var source = Uneven();
            var canvas = Open(source, 1, 2);

            Click(canvas, "straighten");

            Assert.Equal(200, At(source, 3).Y);
        });
    }

    [Fact]
    public void Flow_selects_every_step_a_flow_wire_reaches_from_the_selection()
    {
        UiThread.Run(() =>
        {
            var source = FakeGraphSource.Sample();
            source.Put(FakeGraphSource.MakeStep(9, "print", 900, 400, [FakeGraphSource.Flow("run")], [FakeGraphSource.Flow("then")]));
            var canvas = Open(source, 3);

            Click(canvas, "flow");

            Assert.Equal([1, 2, 3], canvas.Scene.SelectedSteps.Order());
        });
    }

    [Fact]
    public void One_selected_step_has_nothing_to_line_up_with_and_makes_no_entry()
    {
        UiThread.Run(() =>
        {
            var source = Uneven();
            var canvas = Open(source, 2);

            Click(canvas, "align_top");
            Click(canvas, "stack_row");

            Assert.Equal(90, At(source, 2).Y);
            Assert.False(source.History.CanUndo);
        });
    }

    [Fact]
    public void The_bar_shows_while_a_step_is_selected()
    {
        UiThread.Run(() =>
        {
            var canvas = Open(Uneven());
            Assert.False(canvas.ArrangeBar.IsVisible);

            canvas.Select([2]);
            Assert.True(canvas.ArrangeBar.IsVisible);

            canvas.Select([]);
            Assert.False(canvas.ArrangeBar.IsVisible);
        });
    }
}
