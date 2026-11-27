using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App.Tests;

public sealed class CsharpDebugChannelTests : IDisposable
{
    static readonly string s_project = Path.Join(Path.GetTempPath(), "csharp-debug-game");
    static readonly string s_main = Path.Join(s_project, "Main.cs");

    readonly string _log = Path.GetTempFileName();
    readonly FakeDap _dap = new();
    readonly CsharpDebugChannel _channel = new(s_project);

    sealed class FakeDap : IDapLink
    {
        readonly Queue<JsonElement> _coming = new();

        public List<(string Command, JsonObject? Arguments)> Asked { get; } = [];

        public bool Closed { get; set; }

        public void Request(string command, JsonObject? arguments = null) => Asked.Add((command, arguments));

        public IReadOnlyList<JsonElement> Take()
        {
            var taken = _coming.ToList();
            _coming.Clear();
            return taken;
        }

        public void Event(string name, string body = "{}") => _coming.Enqueue(Parse($$"""{"type":"event","event":"{{name}}","body":{{body}}}"""));

        public void Answer(string command, string body = "{}", bool success = true, string message = "") =>
            _coming.Enqueue(Parse($$"""{"type":"response","command":"{{command}}","success":{{(success ? "true" : "false")}},"message":"{{message}}","body":{{body}}}"""));

        public IEnumerable<string> Commands => Asked.Select(asked => asked.Command);

        public JsonObject Last(string command) => Asked.Last(asked => asked.Command == command).Arguments!;

        static JsonElement Parse(string json)
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.Clone();
        }
    }

    public void Dispose() => File.Delete(_log);

    void Start(params (string, int)[] breakpoints)
    {
        var game = new ProcessStartInfo("/bin/orblit") { ArgumentList = { "--path", s_project } };
        _channel.Start(_dap, game, _log, breakpoints);
    }

    void Connect()
    {
        Start();
        _dap.Answer("initialize");
        _dap.Event("initialized");
        _dap.Event("process", """{"systemProcessId":4321}""");
        _dap.Event("thread", """{"reason":"started","threadId":7}""");
        _channel.Poll();
        _dap.Asked.Clear();
    }

    void Stop()
    {
        Connect();
        _dap.Event("stopped", """{"reason":"breakpoint","threadId":9}""");
        _channel.Poll();
        var source = new JsonObject { ["path"] = s_main }.ToJsonString();
        var frames = $$"""{"stackFrames":[{"id":100,"name":"Main.OnStart()","line":11,"source":{{source}} },{"id":101,"name":"[External Code]","line":0}]}""";
        _dap.Answer("stackTrace", frames);
        _dap.Answer("scopes", """{"scopes":[{"name":"Locals","variablesReference":5}]}""");
        _dap.Answer("variables", """{"variables":[{"name":"this","value":"{Main}"},{"name":"_frames","value":"0"}]}""");
        _channel.Poll();
    }

    [Fact]
    public void Start_launches_the_game_after_initialize_and_sends_the_breakpoints_before_configuration_is_done()
    {
        Start(("res://Main.cs", 11), ("res://Main.cs", 7), ("res://../outside.cs", 3));
        Assert.Equal(["initialize"], _dap.Commands);
        Assert.Equal("coreclr", (string?)_dap.Last("initialize")["adapterID"]);

        _dap.Answer("initialize");
        _dap.Event("initialized");
        _channel.Poll();

        Assert.Equal(["initialize", "launch", "setBreakpoints", "configurationDone"], _dap.Commands);
        var launch = _dap.Last("launch");
        Assert.Equal("/bin/orblit", (string?)launch["program"]);
        Assert.Equal(["--path", s_project], launch["args"]!.AsArray().Select(argument => (string?)argument));
        Assert.Equal(s_project, (string?)launch["cwd"]);
        var breakpoints = _dap.Last("setBreakpoints");
        Assert.Equal(s_main, (string?)breakpoints["source"]!["path"]);
        Assert.Equal([7, 11], breakpoints["breakpoints"]!.AsArray().Select(line => (int)line!["line"]!));
        Assert.True(_channel.State.Connected);
        Assert.False(_channel.State.Stopped);
    }

    [Fact]
    public void A_stop_shows_the_stack_with_project_files_as_res_paths_and_the_locals_of_the_top_level()
    {
        Stop();

        Assert.Equal(4321, _channel.ProcessId);
        Assert.Equal(["stackTrace", "scopes", "variables"], _dap.Commands);
        Assert.Equal(9, (int)_dap.Last("stackTrace")["threadId"]!);
        Assert.Equal(100, (int)_dap.Last("scopes")["frameId"]!);
        Assert.Equal(5, (int)_dap.Last("variables")["variablesReference"]!);
        var state = _channel.State;
        Assert.True(state.Stopped);
        Assert.Equal("breakpoint", state.Reason);
        Assert.Equal([new DebugFrame("res://Main.cs", 11, "Main.OnStart()"), new DebugFrame("", 0, "[External Code]")], state.Stack);
        Assert.Equal([new DebugValue("this", "{Main}"), new DebugValue("_frames", "0")], state.Locals);
    }

    [Fact]
    public void Selecting_a_level_asks_for_its_locals()
    {
        Stop();
        _dap.Asked.Clear();

        _channel.RequestVariables(1);

        Assert.Equal(1, _channel.State.Level);
        Assert.Empty(_channel.State.Locals);
        Assert.Equal(101, (int)_dap.Last("scopes")["frameId"]!);
    }

    [Theory]
    [InlineData("step", "stepIn")]
    [InlineData("next", "next")]
    [InlineData("out", "stepOut")]
    [InlineData("continue", "continue")]
    [InlineData("break", "pause")]
    public void Each_command_is_the_adapters_request_for_the_stopped_thread(string command, string request)
    {
        Stop();
        _dap.Asked.Clear();

        _channel.Send(command);

        Assert.Equal([request], _dap.Commands);
        Assert.Equal(9, (int)_dap.Last(request)["threadId"]!);
    }

    [Fact]
    public void Pause_before_any_stop_names_the_first_thread()
    {
        Connect();

        _channel.Send("break");

        Assert.Equal(7, (int)_dap.Last("pause")["threadId"]!);
    }

    [Fact]
    public void A_game_that_goes_on_runs_with_no_stack()
    {
        Stop();

        _dap.Event("continued", """{"threadId":9}""");
        _channel.Poll();

        Assert.True(_channel.State.Connected);
        Assert.False(_channel.State.Stopped);
        Assert.Empty(_channel.State.Stack);
        // An answer that comes after the game went on is late.
        _dap.Answer("variables", """{"variables":[{"name":"late","value":"1"}]}""");
        _channel.Poll();
        Assert.Empty(_channel.State.Locals);
    }

    [Fact]
    public void A_breakpoint_changed_while_connected_sends_the_files_lines_again_only_when_they_change()
    {
        Connect();

        _channel.SetBreakpoint("res://Main.cs", 4, true);
        _channel.SetBreakpoint("res://Main.cs", 4, true);
        _channel.SetBreakpoint("res://Main.cs", 9, true);
        _channel.SetBreakpoint("res://Main.cs", 4, false);
        _channel.SetBreakpoint("res://../outside.cs", 1, true);

        Assert.Equal(["setBreakpoints", "setBreakpoints", "setBreakpoints"], _dap.Commands);
        Assert.Equal([9], _dap.Last("setBreakpoints")["breakpoints"]!.AsArray().Select(line => (int)line!["line"]!));
    }

    [Fact]
    public void A_game_that_ends_tells_netcoredbg_to_go_and_the_closed_link_ends_the_session()
    {
        Stop();
        _dap.Asked.Clear();

        _dap.Event("exited", """{"exitCode":0}""");
        _dap.Event("terminated");
        _channel.Poll();

        Assert.Same(DebugState.None, _channel.State);
        Assert.Equal(["disconnect"], _dap.Commands);
        Assert.Equal(4321, _channel.ProcessId);
        _dap.Closed = true;
        _channel.Poll();
        Assert.Null(_channel.ProcessId);
        Assert.False(_channel.End());
    }

    [Fact]
    public void The_games_exit_code_outlives_the_closed_link_until_the_next_end()
    {
        Connect();
        Assert.Null(_channel.ExitCode);

        _dap.Event("exited", """{"exitCode":134}""");
        _dap.Event("terminated");
        _channel.Poll();
        _dap.Closed = true;
        _channel.Poll();

        Assert.Equal(134, _channel.ExitCode);
        _channel.End();
        Assert.Null(_channel.ExitCode);
    }

    [Fact]
    public void End_asks_netcoredbg_to_end_the_game_too()
    {
        Connect();

        Assert.True(_channel.End());

        Assert.Equal(["disconnect"], _dap.Commands);
        Assert.True((bool)_dap.Last("disconnect")["terminateDebuggee"]!);
    }

    [Fact]
    public void A_launch_that_fails_says_why_in_the_log_and_lets_netcoredbg_go()
    {
        Start();
        _dap.Answer("initialize");
        _dap.Answer("launch", success: false, message: "The program was not found.");

        _channel.Poll();

        Assert.Equal("disconnect", _dap.Commands.Last());
        Assert.Contains("netcoredbg did not start the game. The program was not found.", File.ReadAllText(_log), StringComparison.Ordinal);
    }

    [Fact]
    public void A_message_of_another_shape_reads_as_empty()
    {
        Connect();
        _dap.Event("stopped", "[]");
        _channel.Poll();

        _dap.Answer("stackTrace", """{"stackFrames":{"id":"x"}}""");
        _dap.Answer("scopes", """{"scopes":[]}""");
        _channel.Poll();

        Assert.True(_channel.State.Stopped);
        Assert.Empty(_channel.State.Stack);
    }
}
