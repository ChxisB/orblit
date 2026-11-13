using Orblit.Editor.App.Graph;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App.Tests.Graph;

public sealed class GraphDebuggerTests
{
    const string Script = "res://sum.ograph";
    const string Main = "res://sum.ograph#main";

    static (FakeDebugChannel Channel, GraphBreakpoints Marks, GraphDebugger Debugger) Open()
    {
        var channel = new FakeDebugChannel();
        var marks = new GraphBreakpoints();
        return (channel, marks, new GraphDebugger(channel, marks));
    }

    static void Hear(FakeDebugChannel channel, GraphDebugger debugger, DebugState state)
    {
        channel.Next = state;
        debugger.Poll();
    }

    static DebugState StoppedAt(int step) => FakeDebugChannel.Stopped("breakpoint", FakeDebugChannel.Frame(Script, "main", step));

    [Fact]
    public void A_poll_takes_in_what_the_game_said_and_tells_the_listeners_only_when_it_changed()
    {
        var (channel, _, debugger) = Open();
        var heard = 0;
        debugger.Changed += () => heard++;

        Hear(channel, debugger, StoppedAt(2));
        debugger.Poll();
        debugger.Poll();

        Assert.Equal(3, channel.Polls);
        Assert.Equal(1, heard);
        Assert.True(debugger.State.Stopped);
    }

    [Fact]
    public void A_breakpoint_goes_to_a_game_that_is_connected_and_not_to_one_that_is_not()
    {
        var (channel, marks, debugger) = Open();

        marks.Toggle(Main, 2);
        Assert.Empty(channel.Sent);

        Hear(channel, debugger, FakeDebugChannel.Running());
        marks.Toggle(Main, 3);
        marks.Toggle(Main, 3);

        Assert.Equal([$"breakpoint {Main} 3 True", $"breakpoint {Main} 3 False"], channel.Sent);
    }

    [Fact]
    public void A_debugger_that_is_disposed_sends_nothing_more()
    {
        var (channel, marks, debugger) = Open();
        Hear(channel, debugger, FakeDebugChannel.Running());

        debugger.Dispose();
        marks.Toggle(Main, 2);

        Assert.Empty(channel.Sent);
    }

    [Fact]
    public void The_current_step_is_the_step_at_the_selected_level_of_the_stack()
    {
        var (channel, _, debugger) = Open();
        Assert.Null(debugger.Current);

        Hear(channel, debugger, FakeDebugChannel.Stopped("step", FakeDebugChannel.Frame(Script, "helper", 7), FakeDebugChannel.Frame(Script, "main", 2)));
        Assert.Equal(new DebugPlace(Script, "helper", 7), debugger.Current);

        Hear(channel, debugger, debugger.State with { Level = 1 });
        Assert.Equal(new DebugPlace(Script, "main", 2), debugger.Current);

        Hear(channel, debugger, FakeDebugChannel.Running());
        Assert.Null(debugger.Current);
    }

    [Fact]
    public void A_level_that_is_not_a_graph_step_is_not_a_place_and_a_level_off_the_stack_is_none()
    {
        var (channel, _, debugger) = Open();

        Hear(channel, debugger, FakeDebugChannel.Stopped("step", new DebugFrame("res://tool.cs", 12, "Run")));
        Assert.Null(debugger.Current);

        Hear(channel, debugger, StoppedAt(2) with { Level = 5 });
        Assert.Null(debugger.Current);

        Hear(channel, debugger, StoppedAt(2) with { Level = -1 });
        Assert.Null(debugger.Current);
    }

    [Theory]
    [InlineData(DebugCommand.Step, "step")]
    [InlineData(DebugCommand.Next, "next")]
    [InlineData(DebugCommand.Out, "out")]
    [InlineData(DebugCommand.Continue, "continue")]
    public void A_stopped_game_takes_the_commands_that_go_on(DebugCommand command, string name)
    {
        var (channel, _, debugger) = Open();
        Hear(channel, debugger, StoppedAt(2));

        debugger.Send(command);

        Assert.Equal([name], channel.Sent);
    }

    [Fact]
    public void A_running_game_takes_a_pause_and_a_stopped_game_does_not()
    {
        var (channel, _, debugger) = Open();
        Hear(channel, debugger, FakeDebugChannel.Running());
        Assert.True(debugger.CanPause);
        Assert.False(debugger.CanResume);

        debugger.Send(DebugCommand.Break);
        Assert.Equal(["break"], channel.Sent);

        Hear(channel, debugger, StoppedAt(2));
        Assert.False(debugger.CanPause);
        Assert.True(debugger.CanResume);

        debugger.Send(DebugCommand.Break);
        Assert.Equal(["break"], channel.Sent);
    }

    [Fact]
    public void A_command_that_goes_on_is_not_sent_to_a_game_that_runs_or_is_not_there()
    {
        var (channel, _, debugger) = Open();

        debugger.Send(DebugCommand.Continue);
        Hear(channel, debugger, FakeDebugChannel.Running());
        debugger.Send(DebugCommand.Step);

        Assert.Empty(channel.Sent);
    }

    [Fact]
    public void A_level_is_asked_for_only_when_the_stack_has_it()
    {
        var (channel, _, debugger) = Open();
        Hear(channel, debugger, FakeDebugChannel.Stopped("step", FakeDebugChannel.Frame(Script, "helper", 7), FakeDebugChannel.Frame(Script, "main", 2)));

        debugger.SelectLevel(1);
        debugger.SelectLevel(2);
        debugger.SelectLevel(-1);

        Assert.Equal(["variables 1"], channel.Sent);
    }

    [Fact]
    public void A_level_is_not_asked_for_while_the_game_runs()
    {
        var (channel, _, debugger) = Open();
        Hear(channel, debugger, FakeDebugChannel.Running());

        debugger.SelectLevel(0);

        Assert.Empty(channel.Sent);
    }

    [Fact]
    public void A_wire_is_asked_for_at_the_selected_level_and_its_value_shows_only_for_the_step_and_port_asked()
    {
        var (channel, _, debugger) = Open();
        Hear(channel, debugger, StoppedAt(2) with { Level = 0 });
        Assert.Null(debugger.WireValue(4, 1));

        debugger.AskWire(4, 1);
        Assert.Equal(["wire 0 4 1"], channel.Sent);

        Hear(channel, debugger, debugger.State with { Wire = new DebugWire(0, 4, 1, "5") });
        Assert.Equal("5", debugger.WireValue(4, 1));
        Assert.Null(debugger.WireValue(4, 2));
        Assert.Null(debugger.WireValue(5, 1));
    }

    [Fact]
    public void A_wire_value_is_the_one_of_the_selected_level_only()
    {
        var (channel, _, debugger) = Open();
        Hear(channel, debugger, StoppedAt(2) with { Level = 1, Wire = new DebugWire(0, 4, 1, "5") });

        Assert.Null(debugger.WireValue(4, 1));
    }

    [Fact]
    public void A_wire_is_not_asked_for_while_the_game_runs()
    {
        var (channel, _, debugger) = Open();
        Hear(channel, debugger, FakeDebugChannel.Running());

        debugger.AskWire(4, 1);

        Assert.Empty(channel.Sent);
    }
}
