using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Orblit.Editor.App.Shell;
using Orblit.Editor.App.Theme;
using static Orblit.Editor.App.Tests.ReferenceWindow;

namespace Orblit.Editor.App.Tests;

public sealed class OutputPanelTests
{
    // The gap under the panel and the bar that follows it. A panel 200 high ends 3 pixels above the bar.
    const double PanelHeight = 200;

    static BottomBar BarOf(EditorWindow window) => Assert.IsType<BottomBar>(window.Center.BottomBar.Child);

    static string[] Shown(EditorWindow window) =>
        [.. window.Center.Output.List.GetVisualDescendants().OfType<TextBlock>().Select(text => text.Text!)];

    static (EditorWindow Window, FakeScene Scene) Open()
    {
        var scene = new FakeScene();
        return (OpenAtReferenceSize(scene), scene);
    }

    static void Write(EditorWindow window, FakeScene scene, params string[] lines)
    {
        foreach (var line in lines)
        {
            scene.Unread.Enqueue(line);
        }

        window.Poll();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
    }

    [Fact]
    public void The_panel_starts_shut_and_takes_no_room_from_the_viewport()
    {
        UiThread.Run(() =>
        {
            var (window, _) = Open();

            Assert.False(window.Center.Output.IsVisible);
            Assert.False(BarOf(window).OutputOpen);
            Assert.Equal(0, window.Center.Output.Bounds.Height);
        });
    }

    [Fact]
    public void A_click_on_the_output_button_opens_the_panel_over_the_bar_and_a_second_click_shuts_it()
    {
        UiThread.Run(() =>
        {
            var (window, _) = Open();
            var bar = BarOf(window);
            var shut = BoxOf(window.Viewport, window).Height;

            ClickOn(bar.OutputTab, window);
            window.UpdateLayout();
            var panel = BoxOf(window.Center.Output, window);

            Assert.True(window.Center.Output.IsVisible);
            Assert.True(bar.OutputOpen);
            Assert.Equal(PanelHeight, panel.Height);
            Assert.Equal(BoxOf(bar, window).Y - 3, panel.Bottom);
            Assert.Equal(shut - PanelHeight, BoxOf(window.Viewport, window).Height);
            Assert.Equal(EditorColors.Text, ((ISolidColorBrush)bar.OutputTab.Foreground!).Color);

            ClickOn(bar.OutputTab, window);
            window.UpdateLayout();

            Assert.False(window.Center.Output.IsVisible);
            Assert.False(bar.OutputOpen);
            Assert.Equal(shut, BoxOf(window.Viewport, window).Height);
            Assert.Equal(EditorColors.TabInactive, ((ISolidColorBrush)bar.OutputTab.Foreground!).Color);
        });
    }

    [Fact]
    public void Opening_the_output_does_not_say_it_is_not_built()
    {
        UiThread.Run(() =>
        {
            var (window, _) = Open();
            var before = window.Status;

            ClickOn(BarOf(window).OutputTab, window);

            Assert.Equal(before, window.Status);
        });
    }

    [Fact]
    public void The_panel_shows_the_lines_the_scene_logs_after_a_poll_and_none_before()
    {
        UiThread.Run(() =>
        {
            var (window, scene) = Open();
            ClickOn(BarOf(window).OutputTab, window);
            scene.Unread.Enqueue("first");
            window.UpdateLayout();
            Assert.Empty(Shown(window));

            Write(window, scene, "second");

            Assert.Equal(["first", "second"], Shown(window));
            Assert.Equal(1, scene.Polls);
        });
    }

    [Fact]
    public void Lines_logged_while_the_panel_was_shut_are_there_when_it_opens()
    {
        UiThread.Run(() =>
        {
            var (window, scene) = Open();
            Write(window, scene, "while shut", "still shut");

            ClickOn(BarOf(window).OutputTab, window);
            window.UpdateLayout();

            Assert.Equal(["while shut", "still shut"], Shown(window));
        });
    }

    [Fact]
    public void The_clear_button_empties_the_panel_and_asks_the_scene_to_forget_the_lines()
    {
        UiThread.Run(() =>
        {
            var (window, scene) = Open();
            ClickOn(BarOf(window).OutputTab, window);
            Write(window, scene, "one", "two");
            Assert.Equal(2, window.Center.Output.List.ItemCount);

            ClickOn(window.Center.Output.ClearButton, window);
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();

            Assert.Equal(1, scene.OutputClears);
            Assert.Empty(scene.Output);
            Assert.Equal(0, window.Center.Output.List.ItemCount);
            Assert.Empty(Shown(window));
        });
    }

    [Fact]
    public void The_list_follows_the_newest_line()
    {
        UiThread.Run(() =>
        {
            var (window, scene) = Open();
            ClickOn(BarOf(window).OutputTab, window);

            Write(window, scene, [.. Enumerable.Range(0, 200).Select(i => $"line {i}")]);
            window.UpdateLayout();

            var scroll = window.Center.Output.List.GetVisualDescendants().OfType<ScrollViewer>().First();
            Assert.True(scroll.Extent.Height > scroll.Viewport.Height);
            Assert.InRange(scroll.Offset.Y + scroll.Viewport.Height, scroll.Extent.Height - 1, scroll.Extent.Height);
            Assert.Contains("line 199", Shown(window));

            Write(window, scene, [.. Enumerable.Range(200, 50).Select(i => $"line {i}")]);

            Assert.InRange(scroll.Offset.Y + scroll.Viewport.Height, scroll.Extent.Height - 1, scroll.Extent.Height);
            Assert.Contains("line 249", Shown(window));
        });
    }

    [Fact]
    public void The_window_reads_the_scene_log_on_its_own_until_it_closes()
    {
        UiThread.Run(() =>
        {
            var (window, scene) = Open();

            Thread.Sleep(600);
            Dispatcher.UIThread.RunJobs();
            Assert.True(scene.Polls > 0);

            window.Close();
            var after = scene.Polls;
            Thread.Sleep(600);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(after, scene.Polls);
        });
    }

    [Fact]
    public void The_clear_button_has_the_clear_icon_and_a_tip()
    {
        UiThread.Run(() =>
        {
            var (window, _) = Open();
            ClickOn(BarOf(window).OutputTab, window);
            var button = window.Center.Output.ClearButton;

            Assert.Equal("Clear", button.GetVisualDescendants().OfType<EditorIcon>().Single().IconName);
            Assert.Equal("Clear Output", ToolTip.GetTip(button));
        });
    }

    [Fact]
    public void Play_opens_the_panel_and_says_the_game_is_running()
    {
        UiThread.Run(() =>
        {
            var (window, _) = Open();

            ClickOn(window.PlayButton, window);

            Assert.True(window.Center.Output.IsVisible);
            Assert.True(BarOf(window).OutputOpen);
            Assert.Equal("Playing in a second window.", window.Status);
        });
    }

    [Fact]
    public void Play_leaves_a_shut_panel_shut_while_the_pin_is_on()
    {
        UiThread.Run(() =>
        {
            var (window, _) = Open();
            ClickOn(BarOf(window).PinButton, window);

            ClickOn(window.PlayButton, window);

            Assert.False(window.Center.Output.IsVisible);
            Assert.Equal("Playing in a second window.", window.Status);
        });
    }

    [Fact]
    public void A_click_on_the_output_button_works_while_the_pin_is_on()
    {
        UiThread.Run(() =>
        {
            var (window, _) = Open();
            ClickOn(BarOf(window).PinButton, window);

            ClickOn(BarOf(window).OutputTab, window);
            Assert.True(window.Center.Output.IsVisible);

            ClickOn(BarOf(window).OutputTab, window);
            Assert.False(window.Center.Output.IsVisible);
        });
    }

    [Fact]
    public void Play_leaves_an_open_panel_open_with_or_without_the_pin()
    {
        UiThread.Run(() =>
        {
            var (window, _) = Open();
            ClickOn(BarOf(window).OutputTab, window);

            ClickOn(window.PlayButton, window);
            Assert.True(window.Center.Output.IsVisible);

            ClickOn(BarOf(window).PinButton, window);
            ClickOn(window.PlayButton, window);
            Assert.True(window.Center.Output.IsVisible);
        });
    }

    [Fact]
    public void A_play_that_cannot_save_runs_nothing_and_opens_nothing()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene { SaveFails = true };
            var window = OpenAtReferenceSize(scene);
            window.Outline.SelectedItem = FakeScene.Lamp;
            window.Tools.DuplicateButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            ClickOn(window.PlayButton, window);

            Assert.False(window.Center.Output.IsVisible);
            Assert.NotEqual("Playing in a second window.", window.Status);
        });
    }

    [Fact]
    public void The_pin_lights_on_a_click_and_goes_out_on_the_next_and_never_says_it_is_not_built()
    {
        UiThread.Run(() =>
        {
            var (window, _) = Open();
            var pin = BarOf(window).PinButton;
            var before = window.Status;
            Assert.False(pin.IsChecked);

            ClickOn(pin, window);
            Assert.True(pin.IsChecked);
            Assert.Equal(before, window.Status);

            ClickOn(pin, window);
            Assert.False(pin.IsChecked);
        });
    }

    [Theory]
    [InlineData(1, "The Debugger panel is not built yet.")]
    [InlineData(2, "The Audio panel is not built yet.")]
    [InlineData(3, "The Animation panel is not built yet.")]
    [InlineData(4, "The Shader Editor panel is not built yet.")]
    public void The_other_buttons_still_say_their_panel_is_not_built_and_open_nothing(int index, string message)
    {
        UiThread.Run(() =>
        {
            var (window, _) = Open();

            ClickOn(BarOf(window).Tabs[index], window);

            Assert.Equal(message, window.Status);
            Assert.False(window.Center.Output.IsVisible);
            Assert.False(BarOf(window).OutputOpen);
        });
    }
}
