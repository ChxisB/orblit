using Avalonia;
using Orblit.Editor.App.Graph;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App.Tests.Graph;

public sealed class WireHoverTests
{
    const string Script = "res://sum.ograph";

    /// <summary>The sample graph with a constant wired into the text of the print step, and a debugger on a fake game.</summary>
    sealed class Rig
    {
        public Rig()
        {
            var source = FakeGraphSource.Sample();
            source.Put(FakeGraphSource.MakeStep(4, "constant", 0, 120, [], [FakeGraphSource.Data("value", 4)]));
            source.Put(new GraphWire(4, 0, 3, 1));
            Canvas = new GraphCanvas(source);
            Canvas.Scene.Script = Script;
            Debugger = new GraphDebugger(Channel, new GraphBreakpoints());
            Canvas.Probe(Debugger);
            OnData = Middle(Canvas.Scene.VisibleWires().Single(shape => shape.Wire.FromStep == 4));
            OnFlow = Middle(Canvas.Scene.VisibleWires().Single(shape => shape.Wire.FromStep == 2));
        }

        public FakeDebugChannel Channel { get; } = new();

        public GraphDebugger Debugger { get; }

        public GraphCanvas Canvas { get; }

        public WireHover Hover => Canvas.Hover!;

        public Point OnData { get; }

        public Point OnFlow { get; }

        public Point Away => new(260, 400);

        public void Hear(DebugState state)
        {
            Channel.Next = state;
            Debugger.Poll();
        }

        static Point Middle(WireShape shape) => new((shape.From.X + shape.To.X) / 2, (shape.From.Y + shape.To.Y) / 2);
    }

    static DebugState StoppedAt(int step) => FakeDebugChannel.Stopped("breakpoint", FakeDebugChannel.Frame(Script, "main", step));

    static DebugState WithValue(DebugState state, string value) => state with { Wire = new DebugWire(0, 3, 1, value) };

    [Fact]
    public void A_data_wire_under_the_pointer_is_asked_for_once_while_the_pointer_stays_on_it()
    {
        UiThread.Run(() =>
        {
            var rig = new Rig();
            rig.Hear(StoppedAt(3));

            rig.Hover.Move(rig.OnData);
            rig.Hover.Move(rig.OnData + new Vector(2, 0));

            Assert.Equal(["wire 0 3 1"], rig.Channel.Sent);
        });
    }

    [Fact]
    public void A_wire_is_asked_for_again_when_the_pointer_comes_back_to_it()
    {
        UiThread.Run(() =>
        {
            var rig = new Rig();
            rig.Hear(StoppedAt(3));

            rig.Hover.Move(rig.OnData);
            rig.Hover.Move(rig.Away);
            rig.Hover.Move(rig.OnData);

            Assert.Equal(["wire 0 3 1", "wire 0 3 1"], rig.Channel.Sent);
        });
    }

    [Fact]
    public void The_label_shows_the_value_at_the_input_once_the_game_has_said_it()
    {
        UiThread.Run(() =>
        {
            var rig = new Rig();
            rig.Hear(StoppedAt(3));
            rig.Hover.Move(rig.OnData);
            Assert.Null(rig.Hover.Label);

            rig.Hear(WithValue(StoppedAt(3), "7"));

            var pin = rig.Canvas.Scene.Find(3)!.PinCenter(true, 1);
            Assert.Equal(new HoverLabel("7", pin), rig.Hover.Label);
        });
    }

    [Fact]
    public void A_flow_wire_asks_nothing_and_shows_nothing()
    {
        UiThread.Run(() =>
        {
            var rig = new Rig();
            rig.Hear(StoppedAt(3));

            rig.Hover.Move(rig.OnFlow);

            Assert.Empty(rig.Channel.Sent);
            Assert.Null(rig.Hover.Label);
        });
    }

    [Fact]
    public void A_wire_into_a_pin_the_step_does_not_have_asks_nothing()
    {
        UiThread.Run(() =>
        {
            var source = FakeGraphSource.Sample();
            source.Put(FakeGraphSource.MakeStep(4, "constant", 0, 120, [], [FakeGraphSource.Data("value", 4)]));
            source.Put(new GraphWire(4, 0, 3, 9));
            var canvas = new GraphCanvas(source);
            var channel = new FakeDebugChannel();
            var debugger = new GraphDebugger(channel, new GraphBreakpoints());
            canvas.Probe(debugger);
            channel.Next = StoppedAt(3);
            debugger.Poll();
            var shape = canvas.Scene.VisibleWires().Single(found => found.Wire.FromStep == 4);

            canvas.Hover!.Move(new Point((shape.From.X + shape.To.X) / 2, (shape.From.Y + shape.To.Y) / 2));

            Assert.Empty(channel.Sent);
        });
    }

    [Fact]
    public void Leaving_the_wire_takes_the_label_away()
    {
        UiThread.Run(() =>
        {
            var rig = new Rig();
            rig.Hear(StoppedAt(3));
            rig.Hover.Move(rig.OnData);
            rig.Hear(WithValue(StoppedAt(3), "7"));
            Assert.NotNull(rig.Hover.Label);

            rig.Hover.Move(rig.Away);

            Assert.Null(rig.Hover.Label);
        });
    }

    [Fact]
    public void A_value_for_another_wire_is_not_shown()
    {
        UiThread.Run(() =>
        {
            var rig = new Rig();
            rig.Hear(StoppedAt(3));
            rig.Hover.Move(rig.OnData);

            rig.Hear(StoppedAt(3) with { Wire = new DebugWire(0, 3, 0, "7") });

            Assert.Null(rig.Hover.Label);
        });
    }

    [Fact]
    public void A_game_that_is_running_is_not_asked_and_shows_nothing()
    {
        UiThread.Run(() =>
        {
            var rig = new Rig();
            rig.Hear(FakeDebugChannel.Running());

            rig.Hover.Move(rig.OnData);

            Assert.Empty(rig.Channel.Sent);
            Assert.Null(rig.Hover.Label);
        });
    }

    [Fact]
    public void A_stop_with_the_pointer_already_on_a_wire_asks_for_it_once_per_place()
    {
        UiThread.Run(() =>
        {
            var rig = new Rig();
            rig.Hover.Move(rig.OnData);
            rig.Hear(StoppedAt(3));

            rig.Canvas.ShowStop(rig.Debugger.Current);
            rig.Canvas.ShowStop(rig.Debugger.Current);

            Assert.Equal(["wire 0 3 1"], rig.Channel.Sent);
        });
    }

    [Fact]
    public void Going_on_and_stopping_again_on_the_same_step_asks_again()
    {
        UiThread.Run(() =>
        {
            var rig = new Rig();
            rig.Hover.Move(rig.OnData);
            rig.Hear(StoppedAt(3));
            rig.Canvas.ShowStop(rig.Debugger.Current);

            rig.Hear(FakeDebugChannel.Running());
            rig.Canvas.ShowStop(rig.Debugger.Current);
            rig.Hear(StoppedAt(3));
            rig.Canvas.ShowStop(rig.Debugger.Current);

            Assert.Equal(["wire 0 3 1", "wire 0 3 1"], rig.Channel.Sent);
        });
    }

    [Fact]
    public void A_stop_in_code_that_is_not_a_graph_step_asks_for_no_wire()
    {
        UiThread.Run(() =>
        {
            var rig = new Rig();
            rig.Hover.Move(rig.OnData);
            rig.Hear(StoppedAt(3));
            rig.Canvas.ShowStop(rig.Debugger.Current);
            rig.Channel.Sent.Clear();

            rig.Hear(FakeDebugChannel.Stopped("breakpoint", new DebugFrame("res://spin.cs", 12, "_Process")));
            rig.Canvas.ShowStop(rig.Debugger.Current);

            Assert.Null(rig.Debugger.Current);
            Assert.Empty(rig.Channel.Sent);
        });
    }

    [Fact]
    public void A_canvas_with_no_probe_has_no_hover()
    {
        UiThread.Run(() =>
        {
            var rig = new Rig();

            rig.Canvas.Probe(null);

            Assert.Null(rig.Canvas.Hover);
        });
    }
}
