using Avalonia;
using Orblit.Editor.App;
using Orblit.Editor.App.Launcher;

// Options: --project <folder>, --scene <res:// path> and --headless, which runs the engine with no window and
// so shows no viewport. With no --project the launcher opens on the user's list of projects.
var project = Argument("--project");
var options = new EditorOptions(
    project is null ? "" : Path.GetFullPath(project),
    Argument("--scene") ?? "res://main.oscn",
    !args.Contains("--headless"));
return AppBuilder.Configure(() => new EditorApp(options, projects: project is null ? ProjectList.ForUser() : null))
    .UsePlatformDetect()
    .StartWithClassicDesktopLifetime(args);

string? Argument(string name) => args.SkipWhile(a => a != name).Skip(1).FirstOrDefault();
