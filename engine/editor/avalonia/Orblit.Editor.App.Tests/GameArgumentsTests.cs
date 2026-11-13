namespace Orblit.Editor.App.Tests;

public sealed class GameArgumentsTests
{
    [Fact]
    public void A_game_with_no_breakpoints_gets_its_project_its_log_and_the_link()
    {
        var arguments = EditorEngine.GameArguments("/work/game", "/tmp/game.log", 4711, []);

        Assert.Equal(["--path", "/work/game", "--log-file", "/tmp/game.log", "--remote-debug", "tcp://127.0.0.1:4711"], arguments);
    }

    [Fact]
    public void Breakpoints_go_as_one_list_of_source_and_step_pairs()
    {
        var arguments = EditorEngine.GameArguments("/work/game", "/tmp/game.log", 4711, [("res://a.ograph#main", 2), ("res://b.ograph#_start", 5)]);

        Assert.Equal(["--breakpoints", "res://a.ograph#main:2,res://b.ograph#_start:5"], arguments.TakeLast(2));
    }

    [Fact]
    public void A_project_path_with_a_space_stays_one_argument()
    {
        var arguments = EditorEngine.GameArguments("/work/My Game", "/tmp/game.log", 4711, []);

        Assert.Equal("/work/My Game", arguments[1]);
    }
}
