using Avalonia;
using Orblit.Editor.App;
using Orblit.Editor.App.Launcher;

StartupProfile.Start(ProjectList.DataFolder());

// Options: --project <folder>, --scene <res:// path>, which is the project's main scene when left out, and --headless, which runs the engine with no window and
// so shows no viewport. With no --project the launcher opens on the user's list of projects.
var project = Argument("--project");
var folder = project is null ? "" : Path.GetFullPath(project);
var options = new EditorOptions(
    folder,
    Argument("--scene") ?? ProjectSettings.MainSceneOf(folder),
    !args.Contains("--headless"));
return AppBuilder.Configure(() => new EditorApp(options, projects: project is null ? ProjectList.ForUser() : null, settings: ProjectList.ForUser().Settings()))
    .UsePlatformDetect()
    .StartWithClassicDesktopLifetime(args);

string? Argument(string name) => args.SkipWhile(a => a != name).Skip(1).FirstOrDefault();
