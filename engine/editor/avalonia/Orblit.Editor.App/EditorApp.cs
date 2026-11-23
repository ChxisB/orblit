using System.ComponentModel;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
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
            desktop.MainWindow = projects is null ? Edit(desktop, options) : Launch(desktop, projects);
        }
        base.OnFrameworkInitializationCompleted();
    }

    LauncherWindow Launch(IClassicDesktopStyleApplicationLifetime desktop, ProjectList list)
    {
        var launcher = new LauncherWindow(list, InitGit);
        launcher.Opening += folder =>
        {
            var window = Edit(desktop, options with { Project = folder, Scene = ProjectSettings.MainSceneOf(folder) });
            desktop.MainWindow = window;
            window.Show();
            launcher.Close();
        };
        return launcher;
    }

    // The engine pumps the app's events inside a frame. A quit in a frame lets the engine take the one event that
    // wakes the main loop to stop, and the app then idles for ever. So a quit closes the window, and the app stops
    // once the engine is destroyed, which is after any frame.
    EditorWindow Edit(IClassicDesktopStyleApplicationLifetime desktop, EditorOptions chosen)
    {
        var window = new EditorWindow();
        window.Opened += (_, _) => DispatcherTimer.RunOnce(() => StartEngine(window, chosen), EngineStartDelay);
        desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        desktop.ShutdownRequested += (_, e) =>
        {
            e.Cancel = true;
            window.Close();
        };
        window.Closed += (_, _) =>
        {
            if (_engine is null)
            {
                desktop.Shutdown();
                return;
            }
            _engine.Destroyed += () => desktop.Shutdown();
            _engine.Dispose();
        };
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
