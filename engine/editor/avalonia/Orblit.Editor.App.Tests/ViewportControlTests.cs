using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Vector2 = System.Numerics.Vector2;

namespace Orblit.Editor.App.Tests;

public sealed class ViewportControlTests
{
    // A frame reader that hands out the same image every time, or none.
    static FrameReader Image(int width, int height, Func<int, int, byte[]> pixel) =>
        (out int w, out int h, ref byte[] rgba) =>
        {
            w = width;
            h = height;
            rgba = new byte[width * height * 4];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    pixel(x, y).CopyTo(rgba, (y * width + x) * 4);
                }
            }
            return true;
        };

    static bool NoFrame(out int width, out int height, ref byte[] rgba)
    {
        width = 0;
        height = 0;
        return false;
    }

    [Fact]
    public void A_tick_runs_one_engine_frame()
    {
        UiThread.Run(() =>
        {
            var frames = 0;
            var viewport = new ViewportControl(() => { frames++; return false; }, NoFrame);

            viewport.Tick();

            Assert.Equal(1, frames);
        });
    }

    [Fact]
    public void A_tick_with_no_frame_keeps_nothing()
    {
        UiThread.Run(() =>
        {
            var viewport = new ViewportControl(() => false, NoFrame);

            viewport.Tick();

            Assert.Null(viewport.Frame);
        });
    }

    [Fact]
    public void A_frame_makes_an_rgba_bitmap_of_its_size()
    {
        UiThread.Run(() =>
        {
            var viewport = new ViewportControl(() => false, Image(3, 2, (x, y) => [1, 2, 3, 255]));

            viewport.Tick();

            Assert.Equal(new PixelSize(3, 2), viewport.Frame!.PixelSize);
            Assert.Equal(PixelFormat.Rgba8888, viewport.Frame.Format);
        });
    }

    [Fact]
    public void Rows_are_copied_to_the_destination_stride_and_the_padding_is_left_alone()
    {
        const int Width = 3;
        const int Height = 2;
        const int RowBytes = 16;
        var rgba = Enumerable.Range(1, Width * Height * 4).Select(i => (byte)i).ToArray();
        var destination = Marshal.AllocHGlobal(RowBytes * Height);
        try
        {
            var padding = Enumerable.Repeat((byte)0xEE, RowBytes * Height).ToArray();
            Marshal.Copy(padding, 0, destination, padding.Length);

            ViewportControl.CopyRows(rgba, Width, Height, destination, RowBytes);

            var copied = new byte[RowBytes * Height];
            Marshal.Copy(destination, copied, 0, copied.Length);
            Assert.Equal(rgba[..12], copied[..12]);
            Assert.Equal(padding[12..16], copied[12..16]);
            Assert.Equal(rgba[12..], copied[16..28]);
            Assert.Equal(padding[28..], copied[28..]);
        }
        finally
        {
            Marshal.FreeHGlobal(destination);
        }
    }

    [Fact]
    public void A_frame_of_a_new_size_gets_a_new_bitmap()
    {
        UiThread.Run(() =>
        {
            var width = 2;
            var viewport = new ViewportControl(() => false, (out int w, out int h, ref byte[] rgba) =>
            {
                w = width;
                h = 2;
                rgba = new byte[w * h * 4];
                return true;
            });
            viewport.Tick();
            var first = viewport.Frame;

            width = 4;
            viewport.Tick();

            Assert.NotSame(first, viewport.Frame);
            Assert.Equal(new PixelSize(4, 2), viewport.Frame!.PixelSize);
        });
    }

    [Fact]
    public void The_engine_quitting_ends_the_viewport_once()
    {
        UiThread.Run(() =>
        {
            var ended = 0;
            var reads = 0;
            var viewport = new ViewportControl(() => true, (out int w, out int h, ref byte[] rgba) =>
            {
                reads++;
                w = 0;
                h = 0;
                return false;
            });
            viewport.Ended += () => ended++;

            viewport.Tick();

            Assert.Equal(1, ended);
            Assert.Equal(0, reads);
        });
    }

    [Theory]
    [InlineData(200, 100, 100, 100, 0, 25, 100, 50)]
    [InlineData(100, 200, 100, 100, 25, 0, 50, 100)]
    [InlineData(100, 100, 300, 150, 75, 0, 150, 150)]
    public void A_frame_is_centred_and_keeps_its_shape(double frameW, double frameH, double boundsW, double boundsH, double x, double y, double w, double h)
    {
        var rect = ViewportControl.FitRect(new Size(frameW, frameH), new Size(boundsW, boundsH));

        Assert.Equal(new Rect(x, y, w, h), rect);
    }

    [Fact]
    public void An_empty_frame_has_no_place()
    {
        Assert.Equal(default, ViewportControl.FitRect(new Size(0, 0), new Size(100, 100)));
    }

    // The control fills a window, and one render makes it take the pointer.
    static (Window Window, ViewportControl Viewport, List<(float Yaw, float Pitch)> Orbits, List<float> Zooms) Shown()
    {
        var viewport = new ViewportControl(() => false, NoFrame);
        var orbits = new List<(float, float)>();
        var zooms = new List<float>();
        viewport.Orbited += (yaw, pitch) => orbits.Add((yaw, pitch));
        viewport.Zoomed += zooms.Add;
        var window = new Window { Width = 200, Height = 100, Content = viewport };
        window.Show();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        return (window, viewport, orbits, zooms);
    }

    [Fact]
    public void A_viewport_in_a_window_runs_until_the_engine_quits()
    {
        UiThread.Run(() =>
        {
            var quit = false;
            var viewport = new ViewportControl(() => quit, NoFrame);
            var window = new Window { Content = viewport };
            window.Show();
            Assert.True(viewport.IsRunning);

            quit = true;
            viewport.Tick();

            Assert.False(viewport.IsRunning);
        });
    }

    [Fact]
    public void Leaving_the_window_stops_the_viewport()
    {
        UiThread.Run(() =>
        {
            var viewport = new ViewportControl(() => false, NoFrame);
            var window = new Window { Content = viewport };
            window.Show();
            Assert.True(viewport.IsRunning);

            window.Content = null;

            Assert.False(viewport.IsRunning);
        });
    }

    [Fact]
    public void A_left_drag_orbits_by_the_distance_moved()
    {
        UiThread.Run(() =>
        {
            var (window, _, orbits, _) = Shown();

            window.MouseDown(new Point(100, 50), MouseButton.Left);
            window.MouseMove(new Point(150, 60));
            window.MouseMove(new Point(150, 40));

            // Right turns the camera left, down lifts it.
            Assert.Equal(2, orbits.Count);
            Assert.Equal(-0.5f, orbits[0].Yaw, 1e-4f);
            Assert.Equal(0.1f, orbits[0].Pitch, 1e-4f);
            Assert.Equal(0f, orbits[1].Yaw, 1e-4f);
            Assert.Equal(-0.2f, orbits[1].Pitch, 1e-4f);
        });
    }

    [Fact]
    public void Moving_without_a_button_down_orbits_nothing()
    {
        UiThread.Run(() =>
        {
            var (window, _, orbits, _) = Shown();

            window.MouseMove(new Point(10, 10));
            window.MouseMove(new Point(90, 40));

            Assert.Empty(orbits);
        });
    }

    [Fact]
    public void Releasing_the_button_ends_the_drag()
    {
        UiThread.Run(() =>
        {
            var (window, _, orbits, _) = Shown();
            window.MouseDown(new Point(100, 50), MouseButton.Left);
            window.MouseMove(new Point(110, 50));

            window.MouseUp(new Point(110, 50), MouseButton.Left);
            window.MouseMove(new Point(190, 90));

            Assert.Single(orbits);
        });
    }

    [Fact]
    public void Another_button_does_not_orbit()
    {
        UiThread.Run(() =>
        {
            var (window, _, orbits, _) = Shown();

            window.MouseDown(new Point(100, 50), MouseButton.Right);
            window.MouseMove(new Point(150, 50));

            Assert.Empty(orbits);
        });
    }

    // A 200 by 100 window around the control. Returns the sizes the control asked the engine for, and the control.
    static (List<(int Width, int Height)> Asks, ViewportControl Viewport, Window Window) Asking(int frameWidth, int frameHeight)
    {
        var viewport = new ViewportControl(() => false, Image(frameWidth, frameHeight, (_, _) => [1, 2, 3, 255]));
        var asks = new List<(int, int)>();
        viewport.SizeWanted += (width, height) => asks.Add((width, height));
        var window = new Window { Width = 200, Height = 100, Content = viewport };
        window.Show();
        return (asks, viewport, window);
    }

    [Fact]
    public void A_frame_of_another_shape_makes_the_control_ask_for_its_own_size()
    {
        UiThread.Run(() =>
        {
            var (asks, viewport, _) = Asking(100, 50);

            viewport.Tick();

            Assert.Equal([(200, 100)], asks);
        });
    }

    [Fact]
    public void A_frame_of_the_control_size_asks_for_nothing()
    {
        UiThread.Run(() =>
        {
            var (asks, viewport, _) = Asking(200, 100);

            viewport.Tick();

            Assert.Empty(asks);
        });
    }

    [Fact]
    public void The_same_size_is_asked_for_once()
    {
        UiThread.Run(() =>
        {
            var (asks, viewport, _) = Asking(100, 50);

            for (var tick = 0; tick < 30; tick++)
            {
                viewport.Tick();
            }

            Assert.Single(asks);
        });
    }

    [Fact]
    public void A_new_size_waits_a_few_ticks_for_the_engine_to_catch_up()
    {
        UiThread.Run(() =>
        {
            var (asks, viewport, window) = Asking(100, 50);
            viewport.Tick();
            viewport.Width = 300;
            window.UpdateLayout();

            for (var tick = 0; tick < 7; tick++)
            {
                viewport.Tick();
            }
            Assert.Single(asks);

            viewport.Tick();
            Assert.Equal([(200, 100), (300, 100)], asks);
        });
    }

    [Fact]
    public void A_control_under_64_pixels_a_side_asks_for_nothing()
    {
        UiThread.Run(() =>
        {
            var (asks, viewport, window) = Asking(100, 50);
            viewport.Width = 50;
            window.UpdateLayout();

            viewport.Tick();

            Assert.Empty(asks);
        });
    }

    [Fact]
    public void A_control_with_no_size_asks_for_nothing()
    {
        UiThread.Run(() =>
        {
            var viewport = new ViewportControl(() => false, Image(100, 50, (_, _) => [1, 2, 3, 255]));
            var asks = 0;
            viewport.SizeWanted += (_, _) => asks++;

            viewport.Tick();

            Assert.Equal(0, asks);
        });
    }

    // A 200 by 100 window, with a frame of this size drawn as large as fit. No reader means no frame.
    static (Window Window, List<Vector2> Clicks, List<(float Yaw, float Pitch)> Orbits) ShownWithClicks(int width, int height, bool hasFrame = true)
    {
        var viewport = new ViewportControl(() => false, hasFrame ? Image(width, height, (_, _) => [1, 2, 3, 255]) : NoFrame);
        var clicks = new List<Vector2>();
        var orbits = new List<(float, float)>();
        viewport.Clicked += clicks.Add;
        viewport.Orbited += (yaw, pitch) => orbits.Add((yaw, pitch));
        var window = new Window { Width = 200, Height = 100, Content = viewport };
        window.Show();
        viewport.Tick();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        return (window, clicks, orbits);
    }

    [Fact]
    public void A_click_says_which_pixel_of_the_frame_it_hit()
    {
        UiThread.Run(() =>
        {
            var (window, clicks, orbits) = ShownWithClicks(100, 50);

            window.MouseDown(new Point(100, 50), MouseButton.Left);
            window.MouseUp(new Point(100, 50), MouseButton.Left);

            Assert.Equal([new Vector2(50, 25)], clicks);
            Assert.Empty(orbits);
        });
    }

    [Fact]
    public void A_press_that_travels_past_the_slop_is_a_drag_and_not_a_click()
    {
        UiThread.Run(() =>
        {
            var (window, clicks, orbits) = ShownWithClicks(100, 50);

            window.MouseDown(new Point(100, 50), MouseButton.Left);
            window.MouseMove(new Point(104, 50));
            window.MouseUp(new Point(104, 50), MouseButton.Left);

            Assert.Empty(clicks);
            Assert.Single(orbits);
        });
    }

    [Fact]
    public void A_wobble_under_the_slop_is_a_click_that_does_not_move_the_camera()
    {
        UiThread.Run(() =>
        {
            var (window, clicks, orbits) = ShownWithClicks(100, 50);

            window.MouseDown(new Point(100, 50), MouseButton.Left);
            window.MouseMove(new Point(102, 51));
            window.MouseUp(new Point(102, 51), MouseButton.Left);

            Assert.Equal([new Vector2(51, 25.5f)], clicks);
            Assert.Empty(orbits);
        });
    }

    [Fact]
    public void A_drag_that_comes_back_to_where_it_began_is_not_a_click()
    {
        UiThread.Run(() =>
        {
            var (window, clicks, orbits) = ShownWithClicks(100, 50);

            window.MouseDown(new Point(100, 50), MouseButton.Left);
            window.MouseMove(new Point(140, 50));
            window.MouseMove(new Point(100, 50));
            window.MouseUp(new Point(100, 50), MouseButton.Left);

            Assert.Empty(clicks);
            Assert.Equal(2, orbits.Count);
        });
    }

    [Fact]
    public void Another_button_does_not_click()
    {
        UiThread.Run(() =>
        {
            var (window, clicks, _) = ShownWithClicks(100, 50);

            window.MouseDown(new Point(100, 50), MouseButton.Right);
            window.MouseUp(new Point(100, 50), MouseButton.Right);

            Assert.Empty(clicks);
        });
    }

    [Fact]
    public void Letting_go_of_another_button_while_the_left_is_held_does_not_click()
    {
        UiThread.Run(() =>
        {
            var (window, clicks, _) = ShownWithClicks(100, 50);

            window.MouseDown(new Point(100, 50), MouseButton.Left);
            window.MouseDown(new Point(100, 50), MouseButton.Right);
            window.MouseUp(new Point(100, 50), MouseButton.Right);

            Assert.Empty(clicks);
        });
    }

    [Fact]
    public void A_click_before_the_first_frame_picks_nothing()
    {
        UiThread.Run(() =>
        {
            var (window, clicks, _) = ShownWithClicks(100, 50, hasFrame: false);

            window.MouseDown(new Point(100, 50), MouseButton.Left);
            window.MouseUp(new Point(100, 50), MouseButton.Left);

            Assert.Empty(clicks);
        });
    }

    [Fact]
    public void A_click_in_the_bars_beside_the_frame_picks_nothing()
    {
        UiThread.Run(() =>
        {
            // A square frame in a wide window leaves bars 50 wide at each side.
            var (window, clicks, _) = ShownWithClicks(100, 100);

            window.MouseDown(new Point(20, 50), MouseButton.Left);
            window.MouseUp(new Point(20, 50), MouseButton.Left);
            window.MouseDown(new Point(100, 50), MouseButton.Left);
            window.MouseUp(new Point(100, 50), MouseButton.Left);

            Assert.Equal([new Vector2(50, 50)], clicks);
        });
    }

    [Theory]
    [InlineData(0, 0, 100, 50, 200, 100, 0, 0)]
    [InlineData(100, 50, 100, 50, 200, 100, 50, 25)]
    [InlineData(50, 0, 100, 100, 200, 100, 0, 0)]
    [InlineData(100, 50, 100, 100, 200, 100, 50, 50)]
    [InlineData(150, 75, 400, 200, 200, 100, 300, 150)]
    public void A_point_of_the_control_is_a_pixel_of_the_frame(double px, double py, double fw, double fh, double bw, double bh, float x, float y)
    {
        var at = ViewportControl.FrameAt(new Point(px, py), new Size(fw, fh), new Size(bw, bh));

        Assert.Equal(new Vector2(x, y), at);
    }

    [Theory]
    [InlineData(49, 50)]
    [InlineData(100, -1)]
    [InlineData(151, 50)]
    public void A_point_outside_the_drawn_frame_is_no_pixel(double px, double py)
    {
        Assert.Null(ViewportControl.FrameAt(new Point(px, py), new Size(100, 100), new Size(200, 100)));
    }

    [Fact]
    public void An_empty_frame_has_no_pixels()
    {
        Assert.Null(ViewportControl.FrameAt(new Point(0, 0), new Size(0, 0), new Size(200, 100)));
    }

    [Fact]
    public void A_wheel_turn_zooms_by_its_notches()
    {
        UiThread.Run(() =>
        {
            var (window, _, _, zooms) = Shown();

            window.MouseWheel(new Point(100, 50), new Vector(0, 1));
            window.MouseWheel(new Point(100, 50), new Vector(0, -2));

            Assert.Equal([1f, -2f], zooms);
        });
    }
}
