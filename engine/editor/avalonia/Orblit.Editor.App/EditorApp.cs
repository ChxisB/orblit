using System.ComponentModel;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Orblit.Editor.App.Launcher;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App;

/// <summary>
/// The editor app. With a project list it opens on the launcher and edits the project chosen there. Without one
/// it edits the project the options name.
/// </summary>
internal sealed class EditorApp(EditorOptions options, Action<EditorWindow, EditorEngine>? loaded = null, ProjectList? projects = null) : Application
{
    // The engine takes over NSApplication when it starts. Started before Avalonia finishes launching,
    // it runs the app setup twice, so the first window must be open for a second first.
    static readonly TimeSpan EngineStartDelay = TimeSpan.FromSeconds(1);

    EditorEngine? _engine;

    public override void Initialize() => EditorTheme.Apply(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = projects is null ? Edit(options) : Launch(desktop, projects);
        }
        base.OnFrameworkInitializationCompleted();
    }

    LauncherWindow Launch(IClassicDesktopStyleApplicationLifetime desktop, ProjectList list)
    {
        var launcher = new LauncherWindow(list, InitGit);
        launcher.Opening += folder =>
        {
            var window = Edit(options with { Project = folder });
            desktop.MainWindow = window;
            window.Show();
            launcher.Close();
        };
        return launcher;
    }

    EditorWindow Edit(EditorOptions chosen)
    {
        var window = new EditorWindow(readStore: chosen.StoreReader);
        window.Opened += (_, _) => DispatcherTimer.RunOnce(() => StartEngine(window, chosen), EngineStartDelay);
        window.Closed += (_, _) => _engine?.Dispose();
        return window;
    }

    // Git is the user's own install. Its message says why it failed, so it is passed on as it is.
    static void InitGit(string folder)
    {
        var start = new ProcessStartInfo("git", "init --quiet") { WorkingDirectory = folder, RedirectStandardError = true };
        try
        {
            using var git = Process.Start(start) ?? throw new IOException("Git did not start.");
            var error = git.StandardError.ReadToEnd();
            git.WaitForExit();
            if (git.ExitCode != 0)
            {
                throw new IOException(error.Trim());
            }
        }
        catch (Win32Exception error)
        {
            throw new IOException("Git is not installed. Untick Start a Git repository, or install Git.", error);
        }
    }

    void StartEngine(EditorWindow window, EditorOptions chosen)
    {
        try
        {
            _engine = chosen.Viewport ? EditorEngine.StartViewport(chosen) : EditorEngine.Start(chosen);
            window.Load(_engine);
            if (_engine.CreateViewport() is { } viewport)
            {
                window.ShowViewport(viewport);
            }
            loaded?.Invoke(window, _engine);
        }
        // The window is the only place an error can be shown, so it reports every start failure.
        catch (Exception error) when (error is IOException or InvalidOperationException or TimeoutException or DllNotFoundException)
        {
            window.Fail(error.Message);
        }
    }
}
