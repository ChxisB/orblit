using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;

namespace SurfaceSpike;

// The harness: shows the viewports, runs the frame loop, writes the numbers.
internal sealed class MainWindow : Window
{
    readonly Options _options;
    readonly List<IViewport> _viewports = new();
    readonly Border _overlay;
    readonly List<double> _callback = new();
    readonly List<double> _interval = new();
    readonly Stopwatch _clock = Stopwatch.StartNew();
    readonly Stopwatch _startup = Stopwatch.StartNew();
    TimeSpan _cpuFrom;
    double _wallFrom;
    double _last;
    int _tick;
    double _firstFrameMs = -1;
    int _resizeAt = -1;
    double _resizeMs;
    PixelSize _before;
    int _settleTicks = -1;
    double _settleMs = -1;
    bool _done;

    public MainWindow(Options options)
    {
        _options = options;
        Title = $"SurfaceSpike path {options.Path}";
        var grid = new Grid { Background = Brushes.Black };
        for (var i = 0; i < options.Views; i++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            Control viewport = options.Path switch
            {
                'A' => new ReadbackViewport(),
                'B' => new NativeChildViewport(),
                _ => new SharedTextureViewport(),
            };
            Grid.SetColumn(viewport, i);
            grid.Children.Add(viewport);
            _viewports.Add((IViewport)viewport);
        }
        _overlay = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(220, 20, 20, 24)),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 6),
            Margin = new Thickness(16),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Child = new TextBlock { Text = "Avalonia overlay", Foreground = Brushes.White },
        };
        grid.Children.Add(_overlay);
        Grid.SetColumnSpan(_overlay, options.Views);
        // A popup is a window of its own on macOS, so a native child should
        // not hide it. The overlay above is the in-tree case.
        if (options.Popup)
        {
            var popup = new Popup
            {
                PlacementTarget = grid,
                Placement = PlacementMode.Center,
                Child = new Border
                {
                    Background = Brushes.DarkSlateBlue,
                    Padding = new Thickness(24, 16),
                    Child = new TextBlock { Text = "Avalonia popup", Foreground = Brushes.White },
                },
            };
            grid.Children.Add(popup);
            Opened += (_, _) => popup.IsOpen = true;
        }
        Content = grid;
        Opened += (_, _) =>
        {
            var scale = RenderScaling;
            ClientSize = new Size(options.Width * options.Views / scale, options.Height / scale);
            this.RequestAnimationFrame(Tick);
        };
        Closed += (_, _) => _viewports.ForEach(v => v.Close());
    }

    void Tick(TimeSpan _)
    {
        if (_done) return;
        var begin = _clock.Elapsed.TotalMilliseconds;
        var drawn = false;
        // SPIKE_IDLE measures the tick rate alone, with no drawing.
        if (Environment.GetEnvironmentVariable("SPIKE_IDLE") != null) drawn = true;
        else foreach (var viewport in _viewports) drawn |= viewport.Frame(begin / 1000.0);
        var end = _clock.Elapsed.TotalMilliseconds;
        if (drawn && _firstFrameMs < 0) _firstFrameMs = _startup.Elapsed.TotalMilliseconds;
        if (drawn) _tick++;
        // SPIKE_DIRTY redraws a small Avalonia element each tick, as a live
        // interface would, so Avalonia renders at the display rate.
        if (Environment.GetEnvironmentVariable("SPIKE_DIRTY") != null) _overlay.InvalidateVisual();

        if (drawn && _tick == _options.Warmup)
        {
            _cpuFrom = Process.GetCurrentProcess().TotalProcessorTime;
            _wallFrom = end;
        }
        else if (drawn && _tick > _options.Warmup)
        {
            _callback.Add(end - begin);
            _interval.Add(begin - _last);
            ResizeStep(end);
        }
        _last = begin;

        if (_callback.Count >= _options.Frames) Finish(end);
        else this.RequestAnimationFrame(Tick);
    }

    // Halfway through, shrinks the window and times how long the first viewport
    // takes to draw at the new size.
    void ResizeStep(double now)
    {
        if (!_options.Resize) return;
        if (_resizeAt < 0 && _callback.Count == _options.Frames / 2)
        {
            _resizeAt = _callback.Count;
            _resizeMs = now;
            _before = _viewports[0].Drawn;
            Width = ClientSize.Width * 0.75;
            Height = ClientSize.Height * 0.75;
        }
        else if (_resizeAt >= 0 && _settleTicks < 0 && _viewports[0].Drawn != _before)
        {
            _settleTicks = _callback.Count - _resizeAt;
            _settleMs = now - _resizeMs;
        }
    }

    void Finish(double now)
    {
        _done = true;
        if (_options.Shot != null) Shoot(_options.Shot);
        var cpu = (Process.GetCurrentProcess().TotalProcessorTime - _cpuFrom).TotalMilliseconds;
        var wall = now - _wallFrom;
        var reported = _viewports[0].Reported;
        var failure = (_viewports[0] as SharedTextureViewport)?.Failure;
        var result = new
        {
            path = _options.Path.ToString(),
            asked_pixels = $"{_options.Width}x{_options.Height}",
            drawn_pixels = $"{_viewports[0].Drawn.Width}x{_viewports[0].Drawn.Height}",
            views = _options.Views,
            frames = _callback.Count,
            dropped = _viewports.Sum(v => v.Dropped),
            first_frame_ms = Math.Round(_firstFrameMs, 1),
            callback_p50_ms = Math.Round(Percentile(_callback, 50), 3),
            callback_p95_ms = Math.Round(Percentile(_callback, 95), 3),
            callback_p99_ms = Math.Round(Percentile(_callback, 99), 3),
            interval_p50_ms = Math.Round(Percentile(_interval, 50), 3),
            interval_p95_ms = Math.Round(Percentile(_interval, 95), 3),
            achieved_fps = Math.Round(1000.0 * _callback.Count / wall, 1),
            process_cpu_percent = Math.Round(100.0 * cpu / wall, 1),
            renderer_cpu_ms = reported?.CpuMilliseconds,
            renderer_gpu_ms = reported?.GpuMilliseconds,
            resize_settle_ticks = _settleTicks,
            resize_settle_ms = Math.Round(_settleMs, 1),
            failure,
        };
        var json = JsonSerializer.Serialize(result);
        Console.WriteLine(json);
        if (_options.Out != null) File.AppendAllText(_options.Out, json + "\n");
        (Application.Current!.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();
    }

    static double Percentile(List<double> values, int percent)
    {
        var sorted = values.OrderBy(v => v).ToList();
        return sorted.Count == 0 ? 0 : sorted[(sorted.Count * percent + 99) / 100 - 1];
    }

    // A real screenshot is the only proof of what the user sees, native child
    // and compositor texture included.
    void Shoot(string path)
    {
        var scale = RenderScaling;
        var region = $"{Position.X / scale:0},{Position.Y / scale - 28:0},{Bounds.Width:0},{Bounds.Height + 28:0}";
        Process.Start("screencapture", new[] { "-x", "-R", region, path })?.WaitForExit(5000);
    }
}
