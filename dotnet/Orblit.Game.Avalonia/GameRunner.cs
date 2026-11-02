using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Orblit.Scene;
using Orblit.Stage;

namespace Orblit.Game.Avalonia;

/// <summary>
/// Plays a game in a window of its own, from a program's <c>Main</c>. The editor has its own way to run
/// the same game, so a game that runs here runs there.
/// </summary>
public static class GameRunner
{
    /// <summary>
    /// Opens a window, plays <paramref name="game"/> in <paramref name="scene"/> and returns when the
    /// window closes. <paramref name="projectFolder"/> is what paths in the scene are relative to. A game
    /// that is an <see cref="IInterfaceGame"/> gets its control over the view.
    /// </summary>
    /// <returns>The exit code: 0 for a window closed on a game that ran well, 1 when the game threw.</returns>
    public static int Run(IGame game, SceneDocument scene, string projectFolder)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(scene);

        var view = new DocumentView(scene, projectFolder);
        var host = new GameHost(game, view.Document, projectFolder, new ConsoleGameLog());
        var interfaceOf = game is IInterfaceGame withInterface ? withInterface.CreateInterface : (Func<Control>?)null;
        var window = new GameWindow(host, view, new RendererScreen(), interfaceOf) { Title = scene.Name, Width = 1100, Height = 760 };

        var code = AppBuilder.Configure(() => new GameApp(window))
            .UsePlatformDetect()
            .StartWithClassicDesktopLifetime([]);
        return code != 0 || host.Fault is not null ? 1 : 0;
    }

    private sealed class GameApp(Window main) : Application
    {
        public override void Initialize()
        {
            RequestedThemeVariant = ThemeVariant.Dark;
            Styles.Add(new FluentTheme());
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = main;
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}
