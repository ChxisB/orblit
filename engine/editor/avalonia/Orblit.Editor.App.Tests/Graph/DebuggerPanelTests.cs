using Avalonia.Controls;
using Avalonia.Interactivity;
using Orblit.Editor.App.Graph;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App.Tests.Graph;

public sealed class DebuggerPanelTests
{
    const string Script = "res://sum.ograph";

    static (FakeDebugChannel Channel, GraphDebugger Debugger, DebuggerPanel Panel) Open()
    {
        var channel = new FakeDebugChannel();
        var debugger = new GraphDebugger(channel, new GraphBreakpoints());
        return (channel, debugger, new DebuggerPanel(debugger));
    }

    static void Hear(FakeDebugChannel channel, GraphDebugger debugger, DebugState state)
    {
        channel.Next = state;
        debugger.Poll();
    }

    static void Click(DebuggerPanel panel, string id) => panel.Find<Button>(id)!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    static DebugState Stopped() => FakeDebugChannel.Stopped("breakpoint", FakeDebugChannel.Frame(Script, "helper", 7), FakeDebugChannel.Frame(Script, "main", 2));

    static string Status(DebuggerPanel panel) => panel.Find<TextBlock>("debugger:status")!.Text!;

    [Fact]
    public void With_no_game_the_panel_says_so_and_every_command_is_off()
    {
        UiThread.Run(() =>
        {
            var (_, _, panel) = Open();

            Assert.Equal("No game is connected.", Status(panel));
            foreach (var id in new[] { "pause", "continue", "step", "next", "out" })
            {
                Assert.False(panel.Find<Button>($"debugger:{id}")!.IsEnabled, id);
            }
        });
    }

    [Fact]
    public void A_running_game_can_be_paused_and_nothing_else()
    {
        UiThread.Run(() =>
        {
            var (channel, debugger, panel) = Open();

            Hear(channel, debugger, FakeDebugChannel.Running());

            Assert.Equal("The game is running.", Status(panel));
            Assert.True(panel.Find<Button>("debugger:pause")!.IsEnabled);
            Assert.False(panel.Find<Button>("debugger:continue")!.IsEnabled);
            Assert.False(panel.Find<Button>("debugger:step")!.IsEnabled);
        });
    }

    [Fact]
    public void A_stopped_game_says_why_and_can_go_on_but_not_pause()
    {
        UiThread.Run(() =>
        {
            var (channel, debugger, panel) = Open();

            Hear(channel, debugger, Stopped());

            Assert.Equal("Stopped: breakpoint.", Status(panel));
            Assert.False(panel.Find<Button>("debugger:pause")!.IsEnabled);
            foreach (var id in new[] { "continue", "step", "next", "out" })
            {
                Assert.True(panel.Find<Button>($"debugger:{id}")!.IsEnabled, id);
            }
        });
    }

    [Fact]
    public void Each_button_sends_its_command()
    {
        UiThread.Run(() =>
        {
            var (channel, debugger, panel) = Open();
            Hear(channel, debugger, Stopped());

            Click(panel, "debugger:step");
            Click(panel, "debugger:next");
            Click(panel, "debugger:out");
            Click(panel, "debugger:continue");
            Hear(channel, debugger, FakeDebugChannel.Running());
            Click(panel, "debugger:pause");

            Assert.Equal(["step", "next", "out", "continue", "break"], channel.Sent);
        });
    }

    [Fact]
    public void The_stack_lists_every_frame_and_a_click_asks_for_that_level()
    {
        UiThread.Run(() =>
        {
            var (channel, debugger, panel) = Open();
            Hear(channel, debugger, Stopped());

            Assert.Equal("> helper, step 7", panel.Find<Button>("debugger:frame:0")!.Content);
            Assert.Equal("main, step 2", panel.Find<Button>("debugger:frame:1")!.Content);

            Click(panel, "debugger:frame:1");
            Assert.Equal(["variables 1"], channel.Sent);

            Hear(channel, debugger, debugger.State with { Level = 1 });
            Assert.Equal("helper, step 7", panel.Find<Button>("debugger:frame:0")!.Content);
            Assert.Equal("> main, step 2", panel.Find<Button>("debugger:frame:1")!.Content);
        });
    }

    [Fact]
    public void A_frame_that_is_not_a_graph_step_shows_its_source_and_line()
    {
        UiThread.Run(() =>
        {
            var (channel, debugger, panel) = Open();

            Hear(channel, debugger, FakeDebugChannel.Stopped("step", new DebugFrame("res://tool.cs", 12, "Run")));

            Assert.Equal("> Run, res://tool.cs line 12", panel.Find<Button>("debugger:frame:0")!.Content);
        });
    }

    [Fact]
    public void Locals_and_members_show_their_values()
    {
        UiThread.Run(() =>
        {
            var (channel, debugger, panel) = Open();

            Hear(channel, debugger, Stopped() with
            {
                Locals = [new DebugValue("total", "5")],
                Members = [new DebugValue("amount", "3"), new DebugValue("name", "\"Ann\"")],
            });

            Assert.Equal("total = 5", panel.Find<TextBlock>("debugger:local:total")!.Text);
            Assert.Equal("amount = 3", panel.Find<TextBlock>("debugger:member:amount")!.Text);
            Assert.Equal("name = \"Ann\"", panel.Find<TextBlock>("debugger:member:name")!.Text);
        });
    }

    [Fact]
    public void The_panel_is_rebuilt_when_the_game_goes_on()
    {
        UiThread.Run(() =>
        {
            var (channel, debugger, panel) = Open();
            Hear(channel, debugger, Stopped());
            Assert.NotNull(panel.Find<Button>("debugger:frame:0"));

            Hear(channel, debugger, FakeDebugChannel.Running());

            Assert.Null(panel.Find<Button>("debugger:frame:0"));
            Assert.Equal("The game is running.", Status(panel));
        });
    }
}
