using Avalonia;
using Orblit.Editor.App;
using Orblit.Editor.Live;

// Options: --project <folder>, a scene project that the run edits and saves, so pass a copy. --out <folder> takes
// the shots. --game runs the sample game UI in an engine window and not the editor.
var project = Path.GetFullPath(Argument("--project") ?? throw new ArgumentException("--project <folder> is required."));
var output = Path.GetFullPath(Argument("--out") ?? Path.Combine(project, "shots"));
Directory.CreateDirectory(output);
return args.Contains("--game") ? RunGame() : RunEditor();

string? Argument(string name) => args.SkipWhile(a => a != name).Skip(1).FirstOrDefault();

int RunEditor()
{
    var passed = false;
    // The run must not reach the network, so the store reads these two invented kits.
    const string storeIndex = """{"formatVersion":1,"kits":[{"name":"Platformer Starter","publisher":"example","slug":"platformer","version":"1.2.0","summary":"A side scroller to start from."},{"name":"Racing Track","publisher":"example","slug":"racing","version":"0.1.0"}]}""";
    EditorRun.WritePlanarScene(project);
    var options = new EditorOptions(project, "res://main.oscn", true, _ => Task.FromResult(storeIndex));
    // The engine start is the only way in. If it fails the window shows the error and no run starts, so a clock ends it.
    using var starting = new CancellationTokenSource();
    _ = Task.Delay(TimeSpan.FromMinutes(3), starting.Token).ContinueWith(_ => EndWith("FAIL the editor did not start in three minutes", 2), TaskContinuationOptions.OnlyOnRanToCompletion);
    AppBuilder.Configure(() => new EditorApp(options, (window, engine) =>
        {
            starting.Cancel();
            _ = Drive(window, engine);
        }))
        .UsePlatformDetect()
        .StartWithClassicDesktopLifetime([]);
    Console.WriteLine($"the editor exited at {DateTime.Now:HH:mm:ss}");
    return passed ? 0 : 1;

    async Task Drive(EditorWindow window, EditorEngine engine)
    {
        try
        {
            passed = await new EditorRun(window, engine, project, output).Run();
        }
        // The run reports each step itself. This catches a fault outside every step, such as a missing control.
        catch (Exception error)
        {
            Console.WriteLine($"FAIL the run stopped: {error}");
        }
        Console.WriteLine($"the run is over at {DateTime.Now:HH:mm:ss}, closing the window");
        _ = Task.Delay(TimeSpan.FromMinutes(1)).ContinueWith(_ => EndWith("FAIL the editor did not exit a minute after its window closed, which an unsaved-edits prompt would cause", 3));
        window.Close();
    }
}

static void EndWith(string message, int code)
{
    Console.WriteLine(message);
    Environment.Exit(code);
}

int RunGame()
{
    try
    {
        using var run = new GameRun(project, output);
        return run.Run() ? 0 : 1;
    }
    catch (Exception error)
    {
        Console.WriteLine($"FAIL the game window did not run: {error}");
        return 1;
    }
}
