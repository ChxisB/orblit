using System.Diagnostics;
using Avalonia;
using Avalonia.Input;
using Orblit.Editor.App.Graph;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App.Tests.Graph;

public sealed class GraphCanvasTests
{
    static (FakeGraphSource Source, GraphCanvas Canvas) Open(FakeGraphSource? source = null)
    {
        source ??= FakeGraphSource.Sample();
        return (source, new GraphCanvas(source));
    }

    // Step 4 has an input nothing is wired to, so a drag to it is a wire the engine takes.
    static FakeGraphSource WithFreeStep()
    {
        var source = FakeGraphSource.Sample();
        source.Put(FakeGraphSource.MakeStep(4, "print", 520, 200, [FakeGraphSource.Flow("run")], [FakeGraphSource.Flow("then")]));
        return source;
    }

    static void Drag(GraphCanvas canvas, Point from, Point to, int clicks = 1)
    {
        canvas.Gestures.Press(from, MouseButton.Left, KeyModifiers.None, clicks);
        canvas.Gestures.Move(new Point((from.X + to.X) / 2, (from.Y + to.Y) / 2));
        canvas.Gestures.Move(to);
        canvas.Gestures.Release(to);
    }

    static void Click(GraphCanvas canvas, Point at, int clicks = 1, KeyModifiers keys = KeyModifiers.None)
    {
        canvas.Gestures.Press(at, MouseButton.Left, keys, clicks);
        canvas.Gestures.Release(at);
    }

    static Point Pin(GraphCanvas canvas, int step, bool isInput, int port) => canvas.Scene.Find(step)!.PinCenter(isInput, port);

    static Point HeaderOf(GraphCanvas canvas, int step) => canvas.Scene.Find(step)!.Origin + new Vector(40, 10);

    [Fact]
    public void It_shows_a_view_for_each_step_and_a_wire_for_each_connection()
    {
        UiThread.Run(() =>
        {
            var (_, canvas) = Open();

            Assert.Equal(3, canvas.Scene.Steps.Count);
            Assert.Equal(2, canvas.Scene.Wires.Count);
            Assert.Equal(2, canvas.Scene.VisibleWires().Count());
            Assert.False(canvas.NoticeBar.IsVisible);
        });
    }

    [Fact]
    public void It_says_so_when_the_script_has_no_function()
    {
        UiThread.Run(() =>
        {
            var (_, canvas) = Open(new FakeGraphSource { Empty = true });

            Assert.Empty(canvas.Scene.Steps);
            Assert.Contains(canvas.Children, child => child is Avalonia.Controls.TextBlock { IsVisible: true } text && text.Text!.Contains("no function"));
        });
    }

    [Fact]
    public void A_drag_on_a_step_header_makes_one_move_call_when_it_is_let_go()
    {
        UiThread.Run(() =>
        {
            var (source, canvas) = Open();
            var start = HeaderOf(canvas, 2);

            canvas.Gestures.Press(start, MouseButton.Left, KeyModifiers.None, 1);
            canvas.Gestures.Move(start + new Vector(30, 20));
            Assert.Equal(new Vector(30, 20), canvas.Scene.Find(2)!.Offset);
            Assert.Empty(source.Calls);

            canvas.Gestures.Release(start + new Vector(50, 40));

            Assert.Equal(["MoveStep 2 310 40"], source.Calls);
            Assert.Equal(new Vector(0, 0), canvas.Scene.Find(2)!.Offset);
            Assert.Equal(310, canvas.Scene.Find(2)!.Step.X);
        });
    }

    [Fact]
    public void A_drag_moves_every_selected_step_and_asks_the_source_once_for_each()
    {
        UiThread.Run(() =>
        {
            var (source, canvas) = Open();
            Click(canvas, HeaderOf(canvas, 1));
            Click(canvas, HeaderOf(canvas, 2), keys: KeyModifiers.Shift);

            Drag(canvas, HeaderOf(canvas, 2), HeaderOf(canvas, 2) + new Vector(0, 100));

            Assert.Equal(["MoveStep 1 0 100", "MoveStep 2 260 100"], source.Calls);
        });
    }

    [Fact]
    public void A_press_and_release_that_does_not_move_edits_nothing()
    {
        UiThread.Run(() =>
        {
            var (source, canvas) = Open();

            Click(canvas, HeaderOf(canvas, 2));

            Assert.Empty(source.Calls);
            Assert.Contains(2, canvas.Scene.SelectedSteps);
        });
    }

    [Fact]
    public void Escape_during_a_drag_puts_the_step_back_and_edits_nothing()
    {
        UiThread.Run(() =>
        {
            var (source, canvas) = Open();
            var start = HeaderOf(canvas, 2);
            canvas.Gestures.Press(start, MouseButton.Left, KeyModifiers.None, 1);
            canvas.Gestures.Move(start + new Vector(80, 80));

            canvas.Gestures.Cancel();

            Assert.Equal(new Vector(0, 0), canvas.Scene.Find(2)!.Offset);
            Assert.False(canvas.Gestures.IsActive);
            Assert.Empty(source.Calls);
        });
    }

    [Fact]
    public void Dragging_from_an_output_to_an_input_of_the_same_sort_makes_the_wire()
    {
        UiThread.Run(() =>
        {
            var (source, canvas) = Open(WithFreeStep());

            Drag(canvas, Pin(canvas, 3, false, 0), Pin(canvas, 4, true, 0));

            Assert.Equal(["Connect 3.0 4.0"], source.Calls);
            Assert.Equal(3, canvas.Scene.Wires.Count);
        });
    }

    [Fact]
    public void Dragging_from_an_input_works_the_same_as_from_an_output()
    {
        UiThread.Run(() =>
        {
            var (source, canvas) = Open(WithFreeStep());

            Drag(canvas, Pin(canvas, 4, true, 0), Pin(canvas, 3, false, 0));

            Assert.Equal(["Connect 3.0 4.0"], source.Calls);
        });
    }

    [Fact]
    public void A_flow_pin_dragged_to_a_data_pin_is_refused_and_the_bar_gives_the_reason()
    {
        UiThread.Run(() =>
        {
            var (source, canvas) = Open();

            Drag(canvas, Pin(canvas, 2, false, 1), Pin(canvas, 3, true, 1));

            Assert.Equal(["Connect 2.1 3.1"], source.Calls);
            Assert.Equal(2, canvas.Scene.Wires.Count);
            Assert.True(canvas.NoticeBar.IsVisible);
            Assert.Contains("does not fit", canvas.NoticeText);
        });
    }

    [Fact]
    public void Two_pins_on_the_same_side_are_refused_before_the_source_is_asked()
    {
        UiThread.Run(() =>
        {
            var (source, canvas) = Open();

            Drag(canvas, Pin(canvas, 2, false, 0), Pin(canvas, 3, false, 0));

            Assert.Empty(source.Calls);
            Assert.True(canvas.NoticeBar.IsVisible);
            Assert.Contains("output to an input", canvas.NoticeText);
        });
    }

    [Fact]
    public void An_input_that_has_a_wire_refuses_a_second_and_the_bar_says_why()
    {
        UiThread.Run(() =>
        {
            var (_, canvas) = Open();

            Drag(canvas, Pin(canvas, 3, false, 0), Pin(canvas, 3, true, 0));

            Assert.Contains("already has a wire", canvas.NoticeText);
        });
    }

    [Fact]
    public void The_next_press_clears_the_bar()
    {
        UiThread.Run(() =>
        {
            var (_, canvas) = Open();
            Drag(canvas, Pin(canvas, 2, false, 0), Pin(canvas, 3, false, 0));
            Assert.True(canvas.NoticeBar.IsVisible);

            Click(canvas, new Point(900, 600));

            Assert.False(canvas.NoticeBar.IsVisible);
        });
    }

    [Fact]
    public void A_wire_dropped_on_nothing_makes_no_call()
    {
        UiThread.Run(() =>
        {
            var (source, canvas) = Open();

            Drag(canvas, Pin(canvas, 3, false, 0), new Point(900, 600));

            Assert.Empty(source.Calls);
        });
    }

    [Fact]
    public void Deleting_a_selected_step_takes_its_wires_with_it()
    {
        UiThread.Run(() =>
        {
            var (source, canvas) = Open();
            Click(canvas, HeaderOf(canvas, 2));

            canvas.Edits.DeleteSelection();

            Assert.Equal(["RemoveStep 2"], source.Calls);
            Assert.Equal(2, canvas.Scene.Steps.Count);
            Assert.Empty(canvas.Scene.Wires);
        });
    }

    [Fact]
    public void A_click_on_a_wire_selects_it_and_delete_disconnects_it()
    {
        UiThread.Run(() =>
        {
            var (source, canvas) = Open();
            var shape = canvas.Scene.VisibleWires().First();
            var middle = new Point((shape.From.X + shape.To.X) / 2, (shape.From.Y + shape.To.Y) / 2);

            Click(canvas, middle);
            Assert.Equal(shape.Wire, canvas.Scene.SelectedWire);
            canvas.Edits.DeleteSelection();

            Assert.Equal(["Disconnect 1.0 2.0"], source.Calls);
            Assert.Single(canvas.Scene.Wires);
        });
    }

    [Fact]
    public void A_straight_wire_is_hit_where_it_is_drawn_and_not_off_it()
    {
        UiThread.Run(() =>
        {
            var (_, canvas) = Open();
            canvas.Scene.Style = WireStyle.Straight;
            var shape = canvas.Scene.VisibleWires().First();

            Assert.NotNull(canvas.Scene.WireAt(new Point((shape.From.X + shape.To.X) / 2, shape.From.Y + 2)));
            Assert.Null(canvas.Scene.WireAt(new Point((shape.From.X + shape.To.X) / 2, shape.From.Y + 40)));
        });
    }

    [Fact]
    public void A_box_drag_on_empty_space_selects_the_steps_it_touches()
    {
        UiThread.Run(() =>
        {
            var (_, canvas) = Open();

            Drag(canvas, new Point(-50, -50), new Point(300, 80));

            Assert.Equal(new[] { 1, 2 }, canvas.Scene.SelectedSteps.Order());
        });
    }

    [Fact]
    public void A_click_on_empty_space_clears_the_selection()
    {
        UiThread.Run(() =>
        {
            var (_, canvas) = Open();
            Click(canvas, HeaderOf(canvas, 1));

            Click(canvas, new Point(700, 400));

            Assert.Empty(canvas.Scene.SelectedSteps);
        });
    }

    [Fact]
    public void A_right_button_drag_pans_the_view_and_edits_nothing()
    {
        UiThread.Run(() =>
        {
            var (source, canvas) = Open();

            canvas.Gestures.Press(new Point(10, 10), MouseButton.Right, KeyModifiers.None, 1);
            canvas.Gestures.Move(new Point(60, 40));
            canvas.Gestures.Release(new Point(60, 40));

            Assert.Equal(new Vector(50, 30), canvas.Viewport.Pan);
            Assert.Empty(source.Calls);
        });
    }

    [Fact]
    public void A_drag_while_zoomed_moves_the_step_by_graph_distance()
    {
        UiThread.Run(() =>
        {
            var (source, canvas) = Open();
            canvas.Viewport.ZoomAt(new Point(0, 0), 2);
            var start = canvas.Viewport.ToScreen(HeaderOf(canvas, 2));

            Drag(canvas, start, start + new Vector(60, 0));

            Assert.Equal(["MoveStep 2 290 0"], source.Calls);
        });
    }

    [Fact]
    public void A_pin_is_found_through_the_zoom_and_the_pan()
    {
        UiThread.Run(() =>
        {
            var (source, canvas) = Open();
            canvas.Viewport.ZoomAt(new Point(0, 0), 0.5);
            canvas.Viewport.PanBy(new Vector(100, 40));
            var from = canvas.Viewport.ToScreen(Pin(canvas, 3, false, 0));
            var to = canvas.Viewport.ToScreen(Pin(canvas, 2, true, 0));

            Drag(canvas, from, to);

            Assert.Equal(["Connect 3.0 2.0"], source.Calls);
        });
    }

    [Fact]
    public void Dragging_a_frame_title_moves_the_frame_and_the_steps_inside_it()
    {
        UiThread.Run(() =>
        {
            var source = FakeGraphSource.Sample();
            source.Put(new GraphFrame(1, "Setup", 240, -40, 240, 120, false));
            var (_, canvas) = Open(source);
            var title = canvas.Scene.Frames[0].TitleRect.Center - new Vector(40, 0);

            Drag(canvas, title, title + new Vector(0, 30));

            Assert.Equal(["SetFrame 1 Setup 240 -10 240 120 False", "MoveStep 2 260 30"], source.Calls);
        });
    }

    [Fact]
    public void Folding_a_frame_hides_the_steps_inside_it_and_the_wires_that_touch_them()
    {
        UiThread.Run(() =>
        {
            var source = FakeGraphSource.Sample();
            source.Put(new GraphFrame(1, "Setup", 240, -40, 240, 120, false));
            var (_, canvas) = Open(source);

            Click(canvas, canvas.Scene.Frames[0].ToggleRect.Center);

            Assert.EndsWith("True", source.Calls[0]);
            Assert.True(canvas.Scene.IsHidden(canvas.Scene.Find(2)!));
            Assert.False(canvas.Scene.IsHidden(canvas.Scene.Find(1)!));
            Assert.Empty(canvas.Scene.VisibleWires());
            Assert.Equal(FrameView.TitleHeight, canvas.Scene.Frames[0].Bounds.Height);
            Assert.Null(canvas.Scene.HitStep(canvas.Scene.Find(2)!.Bounds.Center));
        });
    }

    [Fact]
    public void Framing_the_selection_puts_a_frame_around_the_steps_with_room_for_a_title()
    {
        UiThread.Run(() =>
        {
            var (source, canvas) = Open();
            Click(canvas, HeaderOf(canvas, 2));

            canvas.Edits.FrameSelection();

            var frame = Assert.Single(canvas.Scene.Frames).Frame;
            Assert.Equal(1, frame.Id);
            Assert.True(frame.X < 260 && frame.Y < -FrameView.TitleHeight);
            Assert.True(canvas.Scene.Frames[0].FullBounds.Contains(canvas.Scene.Find(2)!.Bounds));
            Assert.StartsWith("SetFrame 1", source.Calls[0]);
        });
    }

    [Fact]
    public void Framing_with_nothing_selected_does_nothing()
    {
        UiThread.Run(() =>
        {
            var (source, canvas) = Open();

            Assert.False(canvas.Edits.FrameSelection());
            Assert.Empty(source.Calls);
        });
    }

    [Fact]
    public void Dragging_the_grip_of_a_frame_resizes_it_and_never_below_the_least_size()
    {
        UiThread.Run(() =>
        {
            var source = FakeGraphSource.Sample();
            source.Put(new GraphFrame(1, "Setup", 240, -40, 240, 120, false));
            var (_, canvas) = Open(source);
            var grip = new Point(240 + 240 - 5, -40 + 120 - 5);

            Drag(canvas, grip, grip + new Vector(100, 50));
            Drag(canvas, new Point(240 + 340 - 5, -40 + 170 - 5), new Point(0, 0));

            Assert.Equal("SetFrame 1 Setup 240 -40 340 170 False", source.Calls[0]);
            Assert.Equal($"SetFrame 1 Setup 240 -40 {FrameView.MinWidth} {FrameView.MinHeight} False", source.Calls[1]);
        });
    }

    [Fact]
    public void Pressing_a_frame_title_twice_opens_the_title_box_and_accepting_it_renames_the_frame()
    {
        UiThread.Run(() =>
        {
            var source = FakeGraphSource.Sample();
            source.Put(new GraphFrame(1, "Setup", 240, -40, 240, 120, false));
            var (_, canvas) = Open(source);

            Click(canvas, canvas.Scene.Frames[0].TitleRect.Center - new Vector(40, 0), clicks: 2);
            Assert.True(canvas.IsEditing);
            Assert.Equal("Setup", canvas.Editor.Text);
            canvas.Editor.Text = "Start";
            canvas.EndEdit(accept: true);

            Assert.Equal("SetFrame 1 Start 240 -40 240 120 False", source.Calls[0]);
            Assert.False(canvas.Editor.IsVisible);
        });
    }

    [Fact]
    public void Deleting_a_selected_frame_removes_the_frame_and_keeps_its_steps()
    {
        UiThread.Run(() =>
        {
            var source = FakeGraphSource.Sample();
            source.Put(new GraphFrame(1, "Setup", 240, -40, 240, 120, false));
            var (_, canvas) = Open(source);
            Click(canvas, canvas.Scene.Frames[0].TitleRect.Center - new Vector(40, 0));

            canvas.Edits.DeleteSelection();

            Assert.Equal(["RemoveFrame 1"], source.Calls);
            Assert.Equal(3, canvas.Scene.Steps.Count);
        });
    }

    [Fact]
    public void Pressing_a_wire_twice_puts_a_reroute_on_it()
    {
        UiThread.Run(() =>
        {
            var (source, canvas) = Open();
            var shape = canvas.Scene.VisibleWires().First();
            var middle = new Point((shape.From.X + shape.To.X) / 2, (shape.From.Y + shape.To.Y) / 2);

            Click(canvas, middle, clicks: 2);

            Assert.Equal(4, source.Calls.Count - 1);
            Assert.StartsWith("AddStep 4 reroute_flow", source.Calls[0]);
            Assert.Equal(4, canvas.Scene.Steps.Count);
            Assert.Contains(new GraphWire(1, 0, 4, 0), canvas.Scene.Wires);
            Assert.Contains(new GraphWire(4, 0, 2, 0), canvas.Scene.Wires);
            Assert.DoesNotContain(new GraphWire(1, 0, 2, 0), canvas.Scene.Wires);
            Assert.True(canvas.Scene.Find(4)!.IsReroute);
        });
    }

    [Fact]
    public void A_reroute_on_a_data_wire_is_a_data_reroute()
    {
        UiThread.Run(() =>
        {
            var source = FakeGraphSource.Sample();
            source.Put(FakeGraphSource.MakeStep(4, "constant", 0, 200, [], [FakeGraphSource.Data("value", 4)]));
            source.Put(new GraphWire(4, 0, 3, 1));
            var (_, canvas) = Open(source);

            canvas.Edits.InsertReroute(new GraphWire(4, 0, 3, 1), new Point(300, 220));

            Assert.Equal("AddStep 5 reroute", source.Calls[0]);
        });
    }

    [Fact]
    public void A_press_on_a_value_box_opens_the_value_box_and_accepting_a_new_text_sets_the_parameter()
    {
        UiThread.Run(() =>
        {
            var (source, canvas) = Open();
            var step = canvas.Scene.Find(3)!;

            Click(canvas, step.ValueRect("text").Center);
            Assert.True(canvas.IsEditing);
            Assert.Equal("\"hi\"", canvas.Editor.Text);
            canvas.Editor.Text = "\"bye\"";
            canvas.EndEdit(accept: true);

            Assert.Equal(["SetStepParameter 3 text \"bye\""], source.Calls);
            Assert.Equal("\"bye\"", canvas.Scene.Find(3)!.ValueOf("text"));
        });
    }

    [Fact]
    public void Dropping_a_value_edit_or_leaving_the_text_alone_sets_nothing()
    {
        UiThread.Run(() =>
        {
            var (source, canvas) = Open();
            var step = canvas.Scene.Find(3)!;

            Click(canvas, step.ValueRect("text").Center);
            canvas.Editor.Text = "7";
            canvas.EndEdit(accept: false);
            Click(canvas, step.ValueRect("text").Center);
            canvas.EndEdit(accept: true);

            Assert.Empty(source.Calls);
        });
    }

    [Fact]
    public void A_value_the_engine_refuses_shows_its_reason()
    {
        UiThread.Run(() =>
        {
            var (_, canvas) = Open();
            Assert.False(canvas.Edits.SetValue(99, "text", "1"));
            Assert.True(canvas.NoticeBar.IsVisible);
        });
    }

    [Fact]
    public void An_input_that_has_a_wire_shows_no_value_box()
    {
        UiThread.Run(() =>
        {
            var (_, canvas) = Open();
            Assert.Equal(default, canvas.Scene.Find(3)!.ValueRect("run"));
            Assert.Equal(default, canvas.Scene.Find(3)!.ValueRect("nothing"));
            Assert.NotEqual(default, canvas.Scene.Find(3)!.ValueRect("text"));
        });
    }

    [Fact]
    public void A_step_that_grows_pins_has_a_plus_and_a_minus_that_each_make_one_call()
    {
        UiThread.Run(() =>
        {
            var source = FakeGraphSource.Sample();
            source.Put(FakeGraphSource.MakeStep(4, "sequence", 0, 200, [FakeGraphSource.Flow("run")], [FakeGraphSource.Flow("then 0"), FakeGraphSource.Flow("then 1"), FakeGraphSource.Flow("then 2")], new GraphGrowth(GraphGrowthSide.Output, 2, 3, 4)));
            var (_, canvas) = Open(source);
            var step = canvas.Scene.Find(4)!;
            var row = step.Origin.Y + step.Height - (StepView.RowHeight / 2);
            var centre = step.Origin.X + (StepView.Width / 2);

            Click(canvas, new Point(centre - 18, row));
            Click(canvas, new Point(centre + 18, row));

            Assert.Equal(["SetStepPins 4 4", "SetStepPins 4 3"], source.Calls);
        });
    }

    [Fact]
    public void The_plus_is_dead_at_the_most_pins_and_the_minus_at_the_least()
    {
        UiThread.Run(() =>
        {
            var source = FakeGraphSource.Sample();
            source.Put(FakeGraphSource.MakeStep(4, "sequence", 0, 200, [FakeGraphSource.Flow("run")], [FakeGraphSource.Flow("then 0"), FakeGraphSource.Flow("then 1")], new GraphGrowth(GraphGrowthSide.Output, 2, 2, 2)));
            var (_, canvas) = Open(source);
            var step = canvas.Scene.Find(4)!;
            var row = step.Origin.Y + step.Height - (StepView.RowHeight / 2);
            var centre = step.Origin.X + (StepView.Width / 2);

            Click(canvas, new Point(centre - 18, row));
            Click(canvas, new Point(centre + 18, row));

            Assert.Empty(source.Calls);
        });
    }

    [Fact]
    public void A_step_the_engine_does_not_know_is_marked_and_has_no_pins()
    {
        UiThread.Run(() =>
        {
            var source = FakeGraphSource.Sample();
            source.Put(new GraphStep(9, "future_kind", false, "", 0, 300, new Dictionary<string, string>(), "", [], [], new GraphGrowth(GraphGrowthSide.None, 0, 0, 64)));
            var (_, canvas) = Open(source);
            var step = canvas.Scene.Find(9)!;

            Assert.Null(step.PinAt(step.Origin + new Vector(2, 30)));
            Assert.True(step.Height > StepView.HeaderHeight);
        });
    }

    [Fact]
    public void The_curved_toggle_chooses_the_wire_style_and_the_minimap_toggle_shows_the_minimap()
    {
        UiThread.Run(() =>
        {
            var (_, canvas) = Open();

            canvas.CurvedToggle.IsChecked = false;
            canvas.CurvedToggle.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            Assert.Equal(WireStyle.Straight, canvas.Scene.Style);

            canvas.MapToggle.IsChecked = false;
            canvas.MapToggle.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            Assert.False(canvas.Minimap.IsVisible);
        });
    }

    [Fact]
    public void Fit_shows_every_step_and_a_map_point_is_a_graph_point_the_view_can_center_on()
    {
        UiThread.Run(() =>
        {
            var (_, canvas) = Open();
            var size = new Size(800, 600);

            canvas.Viewport.Fit(canvas.Scene.ContentBounds, size);
            Assert.True(canvas.Viewport.VisibleRect(size).Contains(canvas.Scene.ContentBounds));

            var graph = canvas.Minimap.ToGraph(new Point(GraphMinimap.MapWidth / 2, GraphMinimap.MapHeight / 2));
            canvas.Viewport.CenterOn(graph, size);
            Assert.Equal(graph.X, canvas.Viewport.VisibleRect(size).Center.X, 3);
            Assert.Equal(graph.Y, canvas.Viewport.VisibleRect(size).Center.Y, 3);
        });
    }

    // A move during a drag only offsets the held steps and the pending wire, so its cost must not grow with the step
    // count. The median is far under a millisecond headless, so the bound of 1 ms has wide headroom.
    // shortcut: the bound only catches a large regression, tighten it once a machine runs it steadily.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_pointer_move_in_a_graph_of_500_steps_costs_under_the_bound(bool dragWire)
    {
        UiThread.Run(() =>
        {
            var source = new FakeGraphSource();
            for (var i = 0; i < 500; i++)
            {
                source.Put(FakeGraphSource.MakeStep(i + 1, "print", i % 25 * 220, i / 25 * 120, [FakeGraphSource.Flow("run")], [FakeGraphSource.Flow("then")]));
            }
            var (_, canvas) = Open(source);
            var first = canvas.Scene.Find(1)!;
            var start = dragWire ? first.PinCenter(false, 0) : first.Origin + new Vector(40, 10);
            canvas.Gestures.Press(start, MouseButton.Left, KeyModifiers.None, 1);

            var ms = new double[50];
            for (var i = 0; i < ms.Length; i++)
            {
                var at = start + new Vector(10 + i, 5 + i);
                var begun = Stopwatch.GetTimestamp();
                canvas.Gestures.Move(at);
                ms[i] = Stopwatch.GetElapsedTime(begun).TotalMilliseconds;
            }
            canvas.Gestures.Cancel();

            Array.Sort(ms);
            Assert.True(ms[25] < 1, $"median {ms[25]:F3} ms, p95 {ms[47]:F3} ms");
        });
    }

    [Fact]
    public void A_move_the_engine_refuses_shows_its_reason_and_leaves_the_step_where_it_was()
    {
        UiThread.Run(() =>
        {
            var (source, canvas) = Open();
            var start = HeaderOf(canvas, 2);
            source.Forget(2);

            Drag(canvas, start, start + new Vector(40, 40));

            Assert.Equal(["MoveStep 2"], source.Calls);
            Assert.True(canvas.NoticeBar.IsVisible);
            Assert.Contains("not there", canvas.NoticeText);
        });
    }
}
