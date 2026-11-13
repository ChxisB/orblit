using Orblit.Editor.Bridge;

namespace Orblit.Editor.App.Tests.Graph;

public sealed class DebugStateTests
{
    [Fact]
    public void A_state_with_no_game_is_not_connected_and_holds_nothing()
    {
        var state = DebugState.Parse("""{"connected":false,"stopped":false,"reason":"","stack":[],"level":0,"locals":[],"members":[],"wire":{}}""");

        Assert.False(state.Connected);
        Assert.False(state.Stopped);
        Assert.Empty(state.Stack);
        Assert.Null(state.Wire);
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
             "wire":{}}
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
        var state = DebugState.Parse("""{"connected":true,"stopped":true,"reason":"step","stack":[],"level":0,"locals":[],"members":[],"wire":{"level":0,"step":4,"port":1,"value":"5"}}""");

        Assert.Equal(new DebugWire(0, 4, 1, "5"), state.Wire);
    }

    [Fact]
    public void Text_the_engine_does_not_write_is_refused()
    {
        Assert.ThrowsAny<Exception>(() => DebugState.Parse("not json"));
    }
}
