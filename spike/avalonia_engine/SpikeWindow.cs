using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Themes.Fluent;

namespace AvaloniaEngine;

public sealed class SpikeOptions
{
    public required string Library { get; init; }
    public required string Project { get; init; }
    public required string OutputPng { get; init; }
}

public sealed class SpikeApp : Application
{
    public static SpikeOptions? Options;
    public static Engine? Engine;
    public static ViewportReadback? Readback;

    public override void Initialize() => Styles.Add(new FluentTheme());

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new SpikeWindow(Options!, desktop);
        }
        base.OnFrameworkInitializationCompleted();
    }
}

/// <summary>
/// Path 3 inside Avalonia: each display frame runs one engine iteration, reads the viewport back
/// and copies it into a WriteableBitmap. Times every stage, resizes once, saves the last frame.
/// </summary>
public sealed unsafe class SpikeWindow : Window
{
    const int Frames = 300;
    static readonly string? s_size = Environment.GetEnvironmentVariable("ORBLIT_SPIKE_SIZE");
    // A fixed size run measures one resolution, so it skips the resize.
    static readonly int ResizeAt = s_size is null ? 150 : -1;
    const long SetSize = 1130785943;

    readonly SpikeOptions _options;
    readonly IClassicDesktopStyleApplicationLifetime _lifetime;
    readonly Image _image = new() { Stretch = Stretch.Uniform };
    readonly TextBlock _label = new() { Foreground = Brushes.White, Margin = new Thickness(8) };
    readonly List<double> _iterate = new(), _read = new(), _upload = new(), _interval = new();
    byte[] _pixels = [];
    WriteableBitmap? _bitmap;
    int _frame;
    long _lastTick;
    long _resizeRequested;
    int _resizeFrames = -1;
    double _resizeMs = -1;
    ulong _lastHash;
    int _changed;

    public SpikeWindow(SpikeOptions options, IClassicDesktopStyleApplicationLifetime lifetime)
    {
        _options = options;
        _lifetime = lifetime;
        Width = 1280;
        Height = 760;
        Title = "Orblit Avalonia engine spike";
        // Quiet runs must not take focus from whoever is typing.
        ShowActivated = Environment.GetEnvironmentVariable("ORBLIT_SPIKE_QUIET") is null;
        Content = new Grid { Children = { _image, new Border { Child = _label, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left, Background = new SolidColorBrush(Color.FromArgb(160, 0, 0, 0)) } } };
        // The engine's macOS startup takes over NSApplication's delegate. Created before the app has
        // finished launching, its delegate runs Main::setup a second time. Wait for launch to end.
        Opened += (_, _) => Avalonia.Threading.DispatcherTimer.RunOnce(Begin, TimeSpan.FromSeconds(1));
    }

    static int s_depth;

    void Begin()
    {
        Console.WriteLine($"Begin entered, depth {++s_depth}");
        if (s_depth > 1)
        {
            Console.WriteLine(string.Join('\n', Environment.StackTrace.Split('\n').Take(14)));
            return;
        }
        var timer = Stopwatch.StartNew();
        var engine = Engine.Create(_options.Library,
            ["--path", _options.Project, "--resolution", s_size ?? "1280x720", "--rendering-driver", "metal"]);
        if (engine is null || !engine.Start())
        {
            Console.Error.WriteLine("engine did not start inside Avalonia");
            _lifetime.Shutdown(1);
            return;
        }
        if (Environment.GetEnvironmentVariable("ORBLIT_SPIKE_PARK") is not null)
        {
            // Try to hide the engine's own window by moving it off every screen.
            int* away = stackalloc int[2] { -20000, -20000 };
            long window = 0;
            void** args = stackalloc void*[2] { away, &window };
            engine.Call("DisplayServer", "window_set_position", 2019273902, engine.Singleton("DisplayServer"), args, null);
            int* now = stackalloc int[2];
            void** getArgs = stackalloc void*[1] { &window };
            engine.Call("DisplayServer", "window_get_position", 763922886, engine.Singleton("DisplayServer"), getArgs, now);
            Console.WriteLine($"engine window position after parking: ({now[0]}, {now[1]})");
        }
        SpikeApp.Engine = engine;
        SpikeApp.Readback = new ViewportReadback(engine);
        Console.WriteLine($"engine started inside Avalonia in {timer.ElapsedMilliseconds} ms");
        _lastTick = Stopwatch.GetTimestamp();
        RequestAnimationFrame(OnFrame);
    }

    void OnFrame(TimeSpan _)
    {
        var engine = SpikeApp.Engine!;
        var t0 = Stopwatch.GetTimestamp();
        var quit = engine.Iteration();
        var t1 = Stopwatch.GetTimestamp();
        var got = SpikeApp.Readback!.TryRead(out var width, out var height, ref _pixels);
        var t2 = Stopwatch.GetTimestamp();
        if (got)
        {
            var hash = Sample(width * height);
            if (hash != _lastHash && _frame >= 20)
            {
                _changed++;
            }
            _lastHash = hash;
            Upload(width, height);
        }
        var t3 = Stopwatch.GetTimestamp();

        if (_frame == ResizeAt)
        {
            void* size = stackalloc int[2] { 960, 540 };
            void** args = stackalloc void*[1] { size };
            engine.Call("Window", "set_size", SetSize, RootWindow(engine), args, null);
            _resizeRequested = t3;
            _resizeFrames = 0;
        }
        else if (_resizeFrames >= 0 && _resizeMs < 0)
        {
            _resizeFrames++;
            if (width == 960)
            {
                _resizeMs = Stopwatch.GetElapsedTime(_resizeRequested).TotalMilliseconds;
            }
        }

        if (_frame >= 20)
        {
            _iterate.Add(Stopwatch.GetElapsedTime(t0, t1).TotalMilliseconds);
            _read.Add(Stopwatch.GetElapsedTime(t1, t2).TotalMilliseconds);
            _upload.Add(Stopwatch.GetElapsedTime(t2, t3).TotalMilliseconds);
            _interval.Add(Stopwatch.GetElapsedTime(_lastTick, t0).TotalMilliseconds);
        }
        _lastTick = t0;
        _label.Text = $"frame {_frame}  {width}x{height}  iterate {Last(_iterate):F1} ms  read {Last(_read):F1} ms  upload {Last(_upload):F1} ms";

        if (quit || ++_frame >= Frames)
        {
            Finish();
            return;
        }
        RequestAnimationFrame(OnFrame);
    }

    /// <summary>Cheap fingerprint of the pixels, to tell a fresh frame from a stale one.</summary>
    ulong Sample(int pixels)
    {
        ulong sum = 14695981039346656037;
        for (var i = 0; i < pixels * 4 - 8; i += 509)
        {
            sum = (sum ^ BitConverter.ToUInt64(_pixels, i)) * 1099511628211;
        }
        return sum;
    }

    static double Last(List<double> values) => values.Count == 0 ? 0 : values[^1];

    nint RootWindow(Engine engine)
    {
        nint loop = 0;
        engine.Call("Engine", "get_main_loop", 1016888095, engine.Singleton("Engine"), null, &loop);
        nint root = 0;
        engine.Call("SceneTree", "get_root", 1757182445, loop, null, &root);
        return root;
    }

    void Upload(int width, int height)
    {
        if (_bitmap is null || _bitmap.PixelSize.Width != width || _bitmap.PixelSize.Height != height)
        {
            _bitmap = new WriteableBitmap(new PixelSize(width, height), new Vector(96, 96), PixelFormat.Rgba8888, AlphaFormat.Opaque);
            _image.Source = _bitmap;
        }
        using (var frame = _bitmap.Lock())
        {
            var rowBytes = width * 4;
            fixed (byte* source = _pixels)
            {
                for (var y = 0; y < height; y++)
                {
                    Buffer.MemoryCopy(source + (long)y * rowBytes, (byte*)frame.Address + (long)y * frame.RowBytes, rowBytes, rowBytes);
                }
            }
        }
        _image.InvalidateVisual();
    }

    void Finish()
    {
        _bitmap?.Save(_options.OutputPng);
        Console.WriteLine($"readback size: {_bitmap?.PixelSize.Width}x{_bitmap?.PixelSize.Height}");
        Console.WriteLine($"frames timed: {_iterate.Count}, frames whose pixels changed: {_changed}");
        Console.WriteLine($"iterate  ms: {Summary(_iterate)}");
        Console.WriteLine($"readback ms: {Summary(_read)}");
        Console.WriteLine($"upload   ms: {Summary(_upload)}");
        Console.WriteLine($"frame interval ms: {Summary(_interval)}");
        Console.WriteLine($"resize to 960x540: {(_resizeMs < 0 ? "never arrived" : $"{_resizeMs:F1} ms, {_resizeFrames} frames")}");
        Console.WriteLine($"working set MB: {Process.GetCurrentProcess().WorkingSet64 / 1048576}");
        _lifetime.Shutdown();
    }

    static string Summary(List<double> values)
    {
        var sorted = values.Order().ToList();
        return $"avg {sorted.Average():F2}, median {sorted[sorted.Count / 2]:F2}, p95 {sorted[(int)(sorted.Count * 0.95)]:F2}, max {sorted[^1]:F2}";
    }
}
