using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Orblit.Editor.App.Graph;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App;

/// <summary>What a C# debug session needs from its Debug Adapter Protocol link.</summary>
internal interface IDapLink
{
    /// <summary>True once the adapter has gone and every message it sent has been taken.</summary>
    bool Closed { get; }

    void Request(string command, JsonObject? arguments = null);

    /// <summary>The messages that came since the last call, oldest first.</summary>
    IReadOnlyList<JsonElement> Take();
}

/// <summary>
/// A C# game that netcoredbg runs, as a debug channel, so the graph debugger's commands and panel serve it too.
/// A breakpoint's source is a script's res:// path and its step is a line. The state is built from what netcoredbg
/// says: a stop asks for the stack, and then for the locals of the selected level.
/// </summary>
internal sealed class CsharpDebugChannel(string project) : IDebugChannel
{
    // The stack a stop shows. A deeper stack is engine code a person did not write.
    const int Levels = 20;

    readonly Dictionary<string, HashSet<int>> _lines = [];
    readonly List<int> _frames = [];
    IDapLink? _link;
    JsonObject? _launch;
    string _logFile = "";
    int _thread;

    public DebugState State { get; private set; } = DebugState.None;

    /// <summary>The game's process id, once netcoredbg has started it. Null when no session runs.</summary>
    public int? ProcessId { get; private set; }

    /// <summary>
    /// Asks netcoredbg to run the game's program and arguments and stop on these lines, each a res:// path and a
    /// line. A failure to start is written to the log file, where the Output panel shows it.
    /// </summary>
    public void Start(IDapLink link, ProcessStartInfo game, string logFile, IEnumerable<(string Source, int Line)> breakpoints)
    {
        _link = link;
        _logFile = logFile;
        State = DebugState.None;
        ProcessId = null;
        _thread = 0;
        _frames.Clear();
        _lines.Clear();
        foreach (var (source, line) in breakpoints)
        {
            LinesOf(source).Add(line);
        }

        _launch = new JsonObject
        {
            ["program"] = game.FileName,
            ["args"] = new JsonArray([.. game.ArgumentList.Select(argument => JsonValue.Create(argument))]),
            ["cwd"] = project,
            ["stopAtEntry"] = false,
        };
        link.Request("initialize", new JsonObject
        {
            ["clientID"] = "orblit",
            ["adapterID"] = "coreclr",
            ["linesStartAt1"] = true,
            ["columnsStartAt1"] = true,
            ["pathFormat"] = "path",
        });
    }

    /// <summary>Asks netcoredbg to end the game and then itself. False when no session runs.</summary>
    public bool End()
    {
        _link?.Request("disconnect", new JsonObject { ["terminateDebuggee"] = true });
        return _link is not null;
    }

    public void Poll()
    {
        foreach (var message in _link?.Take() ?? [])
        {
            if (Text(message, "type") == "event")
            {
                Happened(Text(message, "event"), Field(message, "body"));
            }
            else if (Text(message, "type") == "response")
            {
                Answered(message);
            }
        }

        if (_link?.Closed == true)
        {
            _link = null;
            ProcessId = null;
            State = DebugState.None;
        }
    }

    public void Send(string command)
    {
        var request = command switch
        {
            "step" => "stepIn",
            "out" => "stepOut",
            "break" => "pause",
            _ => command,
        };
        _link?.Request(request, new JsonObject { ["threadId"] = _thread });
    }

    public void SetBreakpoint(string source, int step, bool set)
    {
        var lines = LinesOf(source);
        if ((set ? lines.Add(step) : lines.Remove(step)) && State.Connected)
        {
            SendLines(source);
        }
    }

    public void RequestVariables(int level)
    {
        if (State.Stopped && level >= 0 && level < _frames.Count)
        {
            State = State with { Level = level, Locals = [] };
            _link?.Request("scopes", new JsonObject { ["frameId"] = _frames[level] });
        }
    }

    // A C# method has no wires to ask about.
    public void RequestWire(int level, int step, int port)
    {
    }

    void Happened(string name, JsonElement body)
    {
        switch (name)
        {
            case "initialized":
                State = State with { Connected = true };
                foreach (var source in _lines.Keys)
                {
                    SendLines(source);
                }

                _link?.Request("configurationDone");
                break;
            case "process":
                ProcessId = Number(body, "systemProcessId");
                break;
            case "thread" when _thread == 0:
                // Pause names a thread, and before the first stop the first thread is the one there is.
                _thread = Number(body, "threadId");
                break;
            case "stopped":
                _thread = Number(body, "threadId");
                _frames.Clear();
                State = State with { Stopped = true, Reason = Text(body, "reason"), Stack = [], Level = 0, Locals = [] };
                _link?.Request("stackTrace", new JsonObject { ["threadId"] = _thread, ["startFrame"] = 0, ["levels"] = Levels });
                break;
            case "continued":
                _frames.Clear();
                State = State with { Stopped = false, Reason = "", Stack = [], Level = 0, Locals = [] };
                break;
            case "terminated":
                // netcoredbg outlives its game until it is told to go.
                State = DebugState.None;
                _link?.Request("disconnect");
                break;
        }
    }

    void Answered(JsonElement response)
    {
        var body = Field(response, "body");
        switch (Text(response, "command"))
        {
            case "initialize":
                _link?.Request("launch", _launch);
                break;
            case "launch" when Field(response, "success").ValueKind == JsonValueKind.False:
                File.AppendAllText(_logFile, $"netcoredbg did not start the game. {Text(response, "message")}{Environment.NewLine}");
                _link?.Request("disconnect");
                break;
            case "stackTrace" when State.Stopped:
                ShowStack(Items(body, "stackFrames"));
                break;
            case "scopes" when State.Stopped && Items(body, "scopes").FirstOrDefault() is var scope && Number(scope, "variablesReference") > 0:
                _link?.Request("variables", new JsonObject { ["variablesReference"] = Number(scope, "variablesReference") });
                break;
            case "variables" when State.Stopped:
                State = State with { Locals = [.. Items(body, "variables").Select(item => new DebugValue(Text(item, "name"), Text(item, "value")))] };
                break;
        }
    }

    void ShowStack(IEnumerable<JsonElement> frames)
    {
        List<DebugFrame> stack = [];
        _frames.Clear();
        foreach (var frame in frames)
        {
            _frames.Add(Number(frame, "id"));
            stack.Add(new DebugFrame(ResourceOf(Text(Field(frame, "source"), "path")), Number(frame, "line"), Text(frame, "name")));
        }

        State = State with { Stack = stack };
        RequestVariables(0);
    }

    void SendLines(string source)
    {
        string file;
        try
        {
            file = ProjectScripts.FileOf(project, source);
        }
        catch (ArgumentException)
        {
            // Only the project's own scripts take breakpoints.
            return;
        }

        var breakpoints = new JsonArray([.. LinesOf(source).Order().Select(line => new JsonObject { ["line"] = line })]);
        _link?.Request("setBreakpoints", new JsonObject { ["source"] = new JsonObject { ["path"] = file }, ["breakpoints"] = breakpoints });
    }

    HashSet<int> LinesOf(string source)
    {
        if (!_lines.TryGetValue(source, out var lines))
        {
            _lines[source] = lines = [];
        }

        return lines;
    }

    // A project file shows as its res:// path, so the Script workspace can open it. Other files keep their path.
    string ResourceOf(string file)
    {
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(project)) + Path.DirectorySeparatorChar;
        return file.StartsWith(root, StringComparison.Ordinal) ? FileTree.Root + file[root.Length..].Replace('\\', '/') : file;
    }

    // netcoredbg is the person's own program, but a message of a shape this does not expect reads as empty, not as a crash.
    static JsonElement Field(JsonElement message, string name) =>
        message.ValueKind == JsonValueKind.Object && message.TryGetProperty(name, out var value) ? value : default;

    static string Text(JsonElement message, string name) => Field(message, name) is { ValueKind: JsonValueKind.String } text ? text.GetString()! : "";

    static int Number(JsonElement message, string name) => Field(message, name) is { ValueKind: JsonValueKind.Number } number && number.TryGetInt32(out var value) ? value : 0;

    static IEnumerable<JsonElement> Items(JsonElement message, string name) =>
        Field(message, name) is { ValueKind: JsonValueKind.Array } items ? items.EnumerateArray() : [];
}
