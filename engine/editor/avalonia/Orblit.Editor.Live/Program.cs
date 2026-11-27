using System.Diagnostics;
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
    int? game = null;
    EditorRun.WritePlanarScene(project);
    EditorRun.WriteSwatch(project);
    var options = new EditorOptions(project, "res://main.oscn", true);
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
    if (game is { } id && Running(id))
    {
        Console.WriteLine($"FAIL game {id} kept running after the editor closed");
        passed = false;
    }

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
        // A game still running when the window closes ends with the editor. Main checks once the editor is gone.
        engine.Play([], [], [], 1);
        game = engine.GameId;
        Console.WriteLine($"the run is over at {DateTime.Now:HH:mm:ss}, closing the window with game {game} running");
        _ = Task.Delay(TimeSpan.FromMinutes(1)).ContinueWith(_ => EndWith("FAIL the editor did not exit a minute after its window closed", 3));
        window.Close();
        // A close the save prompt holds back says what is unsaved, because the run saves every edit it makes.
        if (window.SaveChanges.IsVisible)
        {
            var toasts = string.Join(" | ", window.Toasts.Cards.Select(card => card.Title));
            Console.WriteLine($"the save prompt held the close: title {window.Title}, script {window.Script.IsDirty}, graph {window.Graph.IsDirty}, toasts {toasts}");
        }
    }
}

static bool Running(int id)
{
    try
    {
        using var process = Process.GetProcessById(id);
        return !process.HasExited;
    }
    catch (ArgumentException)
    {
        return false;
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
