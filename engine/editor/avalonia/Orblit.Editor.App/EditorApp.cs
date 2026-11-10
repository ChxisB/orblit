using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App;

internal sealed class EditorApp(EditorOptions options, Action<EditorWindow, EditorEngine>? loaded = null) : Application
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
            var window = new EditorWindow(readStore: options.StoreReader);
            window.Opened += (_, _) => DispatcherTimer.RunOnce(() => StartEngine(window), EngineStartDelay);
            window.Closed += (_, _) => _engine?.Dispose();
            desktop.MainWindow = window;
        }
        base.OnFrameworkInitializationCompleted();
    }

    void StartEngine(EditorWindow window)
    {
        try
        {
            _engine = options.Viewport ? EditorEngine.StartViewport(options) : EditorEngine.Start(options);
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
