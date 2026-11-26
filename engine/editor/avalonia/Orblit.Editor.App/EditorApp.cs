using System.ComponentModel;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Rendering.Composition;
using Orblit.Editor.App.Launcher;
using Orblit.Editor.App.Shell;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App;

/// <summary>
/// The editor app. With a project list it opens on the launcher and edits the project chosen there. Without one
/// it edits the project the options name.
/// </summary>
internal sealed class EditorApp(EditorOptions options, Action<EditorWindow, EditorEngine>? loaded = null, ProjectList? projects = null) : Application
{
    EditorEngine? _engine;

    public override void Initialize() => EditorTheme.Apply(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = projects is null ? Edit(desktop, options) : Launch(desktop, projects);
            if (OperatingSystem.IsMacOS())
            {
                ShowAppMenu(desktop);
            }
        }
        base.OnFrameworkInitializationCompleted();
    }

    // The app menu of macOS outlives each window, so it acts on the one in front. It takes the place of the
    // About Avalonia item the system menu shows when the app sets none.
    void ShowAppMenu(IClassicDesktopStyleApplicationLifetime desktop) => NativeMenu.SetMenu(this, EditorMenu.AppMenu(new Dictionary<string, MenuAction>
    {
        ["About Orblit"] = new(() => (desktop.MainWindow as IAppMenuTarget)?.CopyVersion()),
        ["Editor Settings…"] = new(() => (desktop.MainWindow as IAppMenuTarget)?.OpenSettings()),
    }));

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
        var window = new EditorWindow(settings: projects?.Settings());
        ShowFromDisk(window, chosen);
        // The engine start holds the UI thread for about a second, so the first frame is drawn before it.
        window.Opened += async (_, _) =>
        {
            await ElementComposition.GetElementVisual(window)!.Compositor.RequestCompositionBatchCommitAsync().Rendered;
            StartEngine(window, chosen);
        };
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

    // The engine reports a folder it cannot read too, so the window only says why and carries on.
    static void ShowFromDisk(EditorWindow window, EditorOptions chosen)
    {
        try
        {
            window.ShowProject(new DiskProject(chosen));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            window.Fail(error.Message);
        }
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
            window.FailToStart(error.Message);
        }
    }
}

/// <summary>What the app menu of macOS asks of the window in front.</summary>
internal interface IAppMenuTarget
{
    void CopyVersion();

    void OpenSettings();
}
