using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless;

namespace Orblit.Game.Avalonia.Tests;

public sealed class GameRunnerTests
{
    // A window made before the platform starts throws, and no headless test shows it, because a headless
    // run has the platform from its first line.
    [Fact]
    public Task TheWindowIsMadeOnlyOnceTheApplicationHasStarted() => Headless.Run(() =>
    {
        var made = new List<Window>();
        var app = new GameRunner.GameApp(() =>
        {
            var window = new Window();
            made.Add(window);
            return window;
        });
        Assert.Empty(made);

        var lifetime = new ClassicDesktopStyleApplicationLifetime();
        app.ApplicationLifetime = lifetime;
        app.OnFrameworkInitializationCompleted();

        Assert.Same(made.Single(), lifetime.MainWindow);
    });
}
