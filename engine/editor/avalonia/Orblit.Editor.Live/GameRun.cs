using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Themes.Fluent;
using Avalonia.VisualTree;
using Orblit.Editor.Bridge;
using Orblit.Ui;
using Orblit.Ui.Sample;

namespace Orblit.Editor.Live;

public sealed class GameApp : Application
{
    public override void Initialize() => Styles.Add(new FluentTheme());

    public static AppBuilder BuildAvaloniaApp() => UiPlatform.Configure<GameApp>();
}

/// <summary>
/// Runs the sample game UI in a real engine window. The layer's frames go to the engine and the engine draws them over
/// the scene, so each picture is the engine's own readback. Input is injected at the <see cref="UiInputRouter"/>, the
/// call the engine's input events make. The operating system sees no mouse, and the scene side is a recorder.
/// </summary>
internal sealed class GameRun : IDisposable
{
    const int FramesToDraw = 20;
    const int FramesToSettle = 5;
    const int Tolerance = 4;
    const long Left = 1;

    readonly string _output;
    readonly EngineHost _host;
    readonly HeadlessUnitTestSession _session;
    readonly GameUi _ui;
    readonly UiFrame _frame = new();
    readonly SceneRecorder _scene = new();
    readonly EngineUiSurface _surface;
    readonly UiLayer _layer;
    readonly UiInputRouter _router;
    Picture _scenePicture;
    int _resumed;
    int _quit;
    int _number;
    bool _failed;

    public GameRun(string project, string output)
    {
        _output = output;
        _host = EngineHost.Create(EngineLibrary.Find(), ["--path", project, "--resolution", "640x480"]);
        _host.Start();
        _host.Pump(FramesToDraw);
        _scenePicture = Read();
        _surface = new EngineUiSurface(_host);
        _session = HeadlessUnitTestSession.StartNew(typeof(GameApp));
        // Controls belong to the thread that makes them, so the tree is built on the UI thread.
        _ui = OnUi(() => new GameUi());
        _layer = OnUi(() => new UiLayer(_ui.Root, new PixelSize(_scenePicture.Width, _scenePicture.Height), _frame));
        _router = new UiInputRouter(_layer, _scene);
        OnUi(() => _ui.Resume.Click += (_, _) => _resumed++);
        OnUi(() => _ui.Quit.Click += (_, _) => _quit++);
    }

    /// <summary>Runs every step and answers whether all passed.</summary>
    public bool Run()
    {
        Step("the engine window draws the project's scene", DrawsTheScene);
        Step("the game UI's menu draws over the scene and the rest of the frame stays the scene's", MenuDrawsOverTheScene);
        Step("a pointer move onto Resume reaches the button and the scene sees nothing", MoveReachesTheButton);
        Step("a click on Resume presses the button and the scene sees nothing", ClickPressesTheButton);
        Step("a click on empty space reaches the scene and the UI sees nothing", ClickOnEmptySpaceReachesTheScene);
        Step("a press on Quit dragged off the button ends on the UI: no click and no scene event", DragOffAButtonStaysWithTheUi);
        Step("a click on Quit then presses it", ClickOnQuit);
        Step("the operating system shows the game window", ShowsInTheDesktop);
        return !_failed;
    }

    void DrawsTheScene()
    {
        Expect(_scenePicture.Width > 0 && _scenePicture.Height > 0, "the engine gave no frame");
        var (r, g, b, _) = _scenePicture.At(0, 0);
        Expect(r >= 255 - Tolerance && Math.Abs(g - 128) <= Tolerance && b <= Tolerance, $"the corner is {r},{g},{b} and not the project's orange");
    }

    void MenuDrawsOverTheScene()
    {
        Draw();
        var covered = Read();
        var inMenu = (_scenePicture.Width / 2 - 110 + 12, _scenePicture.Height / 2);
        Expect(_scenePicture.At(inMenu.Item1, inMenu.Item2) != covered.At(inMenu.Item1, inMenu.Item2), "the menu left its pixel as the scene had it");
        var corner = (_scenePicture.Width - 1, _scenePicture.Height - 1);
        Expect(_scenePicture.At(corner.Item1, corner.Item2) == covered.At(corner.Item1, corner.Item2), "the UI changed a pixel it does not cover");
        // The 24 px title "Paused" spans about 84 pixels. Wrong glyphs draw a narrower mark.
        var inked = Enumerable.Range(225, 120).Where(x => Enumerable.Range(185, 30).Any(y => _frame.Picture.At(x, y).R > 150)).ToList();
        Expect(inked.Count > 0 && inked.Max() - inked.Min() is >= 75 and <= 95, "the title is not drawn at its width");
    }

    void MoveReachesTheButton()
    {
        var at = Center(_ui.Resume);
        OnUi(() => _router.MouseMove(at, Vector2.Zero));
        Expect(OnUi(() => _ui.Resume.IsPointerOver), "the button is not under the pointer");
        Expect(_scene.Moves == 0, $"the scene saw {_scene.Moves} moves");
    }

    void ClickPressesTheButton()
    {
        var at = Center(_ui.Resume);
        Click(at);
        Expect(_resumed == 1, $"the button was clicked {_resumed} times");
        Expect(_scene.Events == 0, $"the scene saw {_scene.Events} events");
    }

    void ClickOnEmptySpaceReachesTheScene()
    {
        var empty = new Vector2(10, _scenePicture.Height / 2);
        Click(empty);
        Expect(_scene.Buttons.SequenceEqual([(Left, empty, true), (Left, empty, false)]), $"the scene saw {_scene.Buttons.Count} button events");
        Expect(_resumed == 1 && _quit == 0, $"a control took the click: resume {_resumed}, quit {_quit}");
    }

    void DragOffAButtonStaysWithTheUi()
    {
        var seen = _scene.Events;
        var on = Center(_ui.Quit);
        var off = new Vector2(10, _scenePicture.Height / 2);
        OnUi(() => _router.MouseMove(on, Vector2.Zero));
        OnUi(() => _router.MouseButton(Left, on, true));
        OnUi(() => _router.MouseMove(off, off - on));
        OnUi(() => _router.MouseButton(Left, off, false));
        Expect(_quit == 0, $"the button clicked {_quit} times after the pointer left it");
        Expect(_scene.Events == seen, $"the scene saw {_scene.Events - seen} events of a drag that began on a control");
    }

    void ClickOnQuit()
    {
        Click(Center(_ui.Quit));
        Expect(_quit == 1, $"the button was clicked {_quit} times");
    }

    void ShowsInTheDesktop()
    {
        var shots = Shots.OfProcess(_output, "desktop").GetAwaiter().GetResult();
        if (shots.Count == 0)
        {
            throw new SkipException("no window picture: the window id tool is not set, or Screen Recording is not granted");
        }
    }

    void Click(Vector2 at)
    {
        OnUi(() => _router.MouseMove(at, Vector2.Zero));
        OnUi(() => _router.MouseButton(Left, at, true));
        OnUi(() => _router.MouseButton(Left, at, false));
    }

    // The layer renders on its own thread and the engine takes pixels on this one, as in a game.
    void Draw()
    {
        OnUi(_layer.Render);
        _surface.Upload(_frame.Picture.Pixels, _frame.Picture.Width, _frame.Picture.Height);
        _host.Pump(FramesToSettle);
    }

    Picture Read()
    {
        using var readback = new ViewportReadback(_host);
        var rgba = Array.Empty<byte>();
        Expect(readback.TryRead(out var width, out var height, ref rgba), "the engine gave no frame");
        return new Picture(width, height, rgba);
    }

    Vector2 Center(Control control) => OnUi(() =>
    {
        var center = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), _ui.Root)!.Value;
        return new Vector2((float)center.X, (float)center.Y);
    });

    T OnUi<T>(Func<T> work) => _session.Dispatch(work, CancellationToken.None).GetAwaiter().GetResult();

    void OnUi(Action work) => OnUi(() =>
    {
        work();
        return 0;
    });

    static void Expect(bool ok, string failure)
    {
        if (!ok)
        {
            throw new InvalidOperationException(failure);
        }
    }

    void Step(string name, Action body)
    {
        string? failure = null;
        var skipped = false;
        try
        {
            body();
        }
        catch (SkipException skip)
        {
            failure = skip.Message;
            skipped = true;
        }
        // A failed step is reported and the run goes on, so one report shows every step that broke.
        catch (Exception error)
        {
            failure = error.Message;
            _failed = true;
        }
        var shot = Path.Combine(_output, $"game-{++_number:00}.png");
        Draw();
        var seen = Read();
        OnUi(() => Shots.SavePixels(shot, seen.Pixels, seen.Width, seen.Height));
        Console.WriteLine(failure is null ? $"PASS {name}" : skipped ? $"SKIP {name}: {failure}" : $"FAIL {name}: {failure} (shot {shot})");
    }

    public void Dispose()
    {
        OnUi(_layer.Dispose);
        _surface.Dispose();
        _session.Dispose();
        _host.Dispose();
    }

    sealed class SkipException(string reason) : Exception(reason);

    readonly record struct Picture(int Width, int Height, byte[] Pixels)
    {
        public (byte R, byte G, byte B, byte A) At(int x, int y)
        {
            var i = (y * Width + x) * 4;
            return (Pixels[i], Pixels[i + 1], Pixels[i + 2], Pixels[i + 3]);
        }
    }

    sealed class UiFrame : IUiSurface
    {
        public Picture Picture { get; private set; }

        public void Upload(ReadOnlySpan<byte> pixels, int width, int height) => Picture = new Picture(width, height, pixels.ToArray());

        public void Dispose()
        {
        }
    }

    sealed class SceneRecorder : ISceneInput
    {
        public List<(long Button, Vector2 Position, bool Pressed)> Buttons { get; } = [];

        public int Moves { get; private set; }

        public int Keys { get; private set; }

        public int Events => Buttons.Count + Moves + Keys;

        public void Key(long keycode, bool pressed) => Keys++;

        public void MouseButton(long button, Vector2 position, bool pressed) => Buttons.Add((button, position, pressed));

        public void MouseMove(Vector2 position, Vector2 relative) => Moves++;
    }
}
