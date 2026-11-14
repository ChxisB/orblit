using Avalonia.Controls.Shapes;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Orblit.Editor.App.Shell;
using Orblit.Editor.App.Theme;
using static Orblit.Editor.App.Tests.ReferenceWindow;

namespace Orblit.Editor.App.Tests;

public sealed class ToastTests
{
    // The posted hide runs on the next pass of the UI thread, as it would after a real timer.
    static void Pass(ManualTime time, double seconds)
    {
        time.Advance(TimeSpan.FromSeconds(seconds));
        Dispatcher.UIThread.RunJobs();
    }

    [Fact]
    public void A_click_on_an_unbuilt_control_shows_its_message_in_the_toast_and_the_status()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize(new FakeScene(), time: new ManualTime());
            Assert.False(window.Toast.IsVisible);

            Click(window.SceneTabs.MenuButton);

            Assert.True(window.Toast.IsVisible);
            Assert.Equal("The scene tab menu is not built yet.", window.Toast.Message);
            Assert.Equal("The scene tab menu is not built yet.", window.Status);
        });
    }

    [Fact]
    public void Closing_the_window_takes_the_toast_down_and_drops_its_timer()
    {
        UiThread.Run(() =>
        {
            var time = new ManualTime();
            var window = OpenAtReferenceSize(new FakeScene(), time: time);
            Click(window.SceneTabs.MenuButton);
            Assert.Equal(1, time.Armed);

            window.Close();

            Assert.False(window.Toast.IsVisible);
            Assert.Equal(0, time.Armed);
        });
    }

    [Fact]
    public void The_toast_goes_after_four_seconds_and_the_status_keeps_the_message()
    {
        UiThread.Run(() =>
        {
            var time = new ManualTime();
            var window = OpenAtReferenceSize(new FakeScene(), time: time);
            Click(window.SceneTabs.MenuButton);

            Pass(time, 3.9);
            Assert.True(window.Toast.IsVisible);
            Pass(time, 0.1);

            Assert.False(window.Toast.IsVisible);
            Assert.Equal("The scene tab menu is not built yet.", window.Status);
        });
    }

    [Fact]
    public void A_second_click_shows_the_new_message_for_four_seconds_of_its_own()
    {
        UiThread.Run(() =>
        {
            var time = new ManualTime();
            var window = OpenAtReferenceSize(new FakeScene(), time: time);
            Click(window.SceneTabs.MenuButton);
            Pass(time, 3);

            Click(window.SceneTabs.DistractionButton);
            Pass(time, 3);

            Assert.True(window.Toast.IsVisible);
            Assert.Equal("Distraction free mode is not built yet.", window.Toast.Message);
            Pass(time, 1);
            Assert.False(window.Toast.IsVisible);
        });
    }

    [Fact]
    public void The_toast_is_a_raised_card_16_pixels_in_from_the_right_and_10_clear_of_the_bottom_bar()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize(new FakeScene(), time: new ManualTime());
            Click(window.SceneTabs.MenuButton);
            window.UpdateLayout();

            var toast = BoxOf(window.Toast, window);
            var bar = BoxOf(window.Center.BottomBar, window);
            Assert.Equal(3440 - 16, toast.Right);
            Assert.Equal(360, toast.Width);
            Assert.Equal(bar.Top - 10, toast.Bottom);
            Assert.Same(EditorBrushes.Raised, window.Toast.Background);
            Assert.Same(EditorBrushes.LineStrong, window.Toast.BorderBrush);
            Assert.Equal(EditorTheme.IslandCorners, window.Toast.CornerRadius);
            // A card that took clicks would block the inspector under it for four seconds.
            Assert.False(window.Toast.IsHitTestVisible);
        });
    }

    [Fact]
    public void An_unbuilt_icon_button_is_dim_with_a_4_pixel_warn_dot_4_in_from_its_top_right()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var button = window.SceneTabs.MenuButton;

            var dot = button.GetVisualDescendants().OfType<Ellipse>().Single();
            var box = BoxOf(button, window);
            AssertBox(box.Right - 8, box.Top + 4, 4, 4, BoxOf(dot, window));
            Assert.Same(EditorBrushes.Warn, dot.Fill);
            Assert.Same(EditorBrushes.Unbuilt, button.Foreground);
        });
    }

    [Fact]
    public void A_lit_unbuilt_toggle_keeps_the_accent_and_takes_the_dot()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            // The overlay has no template without a viewport, so the logical tree holds what it shows.
            var environment = window.Center.Overlay.View.GetLogicalDescendants().OfType<FixedToggle>().Single(toggle => toggle.IsChecked == true);

            Assert.Single(environment.GetLogicalDescendants().OfType<Ellipse>());
            Assert.NotSame(EditorBrushes.Unbuilt, environment.Foreground);
        });
    }
}
