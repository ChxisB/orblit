using Avalonia;
using Orblit.Editor.App;

// Options: --project <folder> (default: the current folder), --scene <res:// path> and --headless,
// which runs the engine with no window and so shows no viewport.
var options = new EditorOptions(
    Path.GetFullPath(Argument("--project") ?? "."),
    Argument("--scene") ?? "res://main.oscn",
    !args.Contains("--headless"));
return AppBuilder.Configure(() => new EditorApp(options)).UsePlatformDetect().StartWithClassicDesktopLifetime(args);

string? Argument(string name) => args.SkipWhile(a => a != name).Skip(1).FirstOrDefault();
