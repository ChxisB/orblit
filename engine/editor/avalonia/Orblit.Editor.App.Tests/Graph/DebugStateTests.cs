using Orblit.Editor.Bridge;

namespace Orblit.Editor.App.Tests.Graph;

public sealed class DebugStateTests
{
    [Fact]
    public void A_state_with_no_game_is_not_connected_and_holds_nothing()
    {
        var state = DebugState.Parse("""{"connected":false,"stopped":false,"reason":"","stack":[],"level":0,"locals":[],"members":[],"wire":{},"scene":[]}""");

        Assert.False(state.Connected);
        Assert.False(state.Stopped);
        Assert.Empty(state.Stack);
        Assert.Null(state.Wire);
        Assert.Null(state.Monitors);
        Assert.Empty(state.Scene);
    }

    [Fact]
    public void A_stopped_game_gives_its_stack_its_locals_and_its_members()
    {
        var state = DebugState.Parse("""
            {"connected":true,"stopped":true,"reason":"breakpoint",
             "stack":[{"source":"res://sum.ograph#main","line":2,"function":"main"},{"source":"res://sum.ograph#_start","line":5,"function":"_start"}],
             "level":1,
             "locals":[{"name":"total","value":"5"}],
             "members":[{"name":"amount","value":"3"}],
             "wire":{},"scene":[]}
            """);

        Assert.True(state.Stopped);
        Assert.Equal("breakpoint", state.Reason);
        Assert.Equal([new DebugFrame("res://sum.ograph#main", 2, "main"), new DebugFrame("res://sum.ograph#_start", 5, "_start")], state.Stack);
        Assert.Equal(1, state.Level);
        Assert.Equal([new DebugValue("total", "5")], state.Locals);
        Assert.Equal([new DebugValue("amount", "3")], state.Members);
    }

    [Fact]
    public void A_wire_the_game_answered_for_is_kept()
    {
        var state = DebugState.Parse("""{"connected":true,"stopped":true,"reason":"step","stack":[],"level":0,"locals":[],"members":[],"wire":{"level":0,"step":4,"port":1,"value":"5"},"scene":[]}""");

        Assert.Equal(new DebugWire(0, 4, 1, "5"), state.Wire);
    }

    [Fact]
    public void A_running_game_gives_its_monitors_and_its_tree()
    {
        var state = DebugState.Parse("""
            {"connected":true,"stopped":false,"reason":"","stack":[],"level":0,"locals":[],"members":[],"wire":{},
             "monitors":{"fps":60.0,"process":0.004,"physics":0.001,"draw_calls":12.0,"objects":340.0,"video_memory":52428800.0},
             "scene":[{"name":"Level","type":"Entity3D","depth":0},{"name":"Crate","type":"MeshInstance3D","depth":1}]}
            """);

        Assert.Equal(new DebugMonitors(60, 0.004, 0.001, 12, 340, 52428800), state.Monitors);
        Assert.Equal([new LiveEntity("Level", "Entity3D", 0), new LiveEntity("Crate", "MeshInstance3D", 1)], state.Scene);
    }

    [Fact]
    public void Text_the_engine_does_not_write_is_refused()
    {
        Assert.ThrowsAny<Exception>(() => DebugState.Parse("not json"));
    }
}
