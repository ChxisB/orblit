using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input.Platform;
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
        [.. window.Center.Output.List.GetVisualDescendants().OfType<TextBlock>().Where(text => !text.Classes.Contains(OutputPanel.Stamp)).Select(text => text.Text!)];

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
            Assert.Equal(BoxOf(window.Center.BottomBar, window).Y - CenterArea.GapHeight, panel.Bottom);
            Assert.Equal(shut - PanelHeight - CenterArea.GapHeight, BoxOf(window.Viewport, window).Height);
            Assert.Equal(EditorColors.Text, ((ISolidColorBrush)bar.OutputTab.Foreground!).Color);
            Assert.Equal(EditorColors.Hover, ((ISolidColorBrush)bar.OutputTab.Background!).Color);

            ClickOn(bar.OutputTab, window);
            window.UpdateLayout();

            Assert.False(window.Center.Output.IsVisible);
            Assert.False(bar.OutputOpen);
            Assert.Equal(shut, BoxOf(window.Viewport, window).Height);
            Assert.Equal(EditorColors.TextDim, ((ISolidColorBrush)bar.OutputTab.Foreground!).Color);
            Assert.Equal(Colors.Transparent, ((ISolidColorBrush)bar.OutputTab.Background!).Color);
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
    public void The_panel_is_an_island_6_pixels_under_the_viewport_with_a_36_pixel_strip_for_the_title_copy_and_clear()
    {
        UiThread.Run(() =>
        {
            var (window, _) = Open();
            ClickOn(BarOf(window).OutputTab, window);
            window.UpdateLayout();
            var panel = window.Center.Output;
            var strip = BoxOf(panel.Strip, window);
            var title = panel.Strip.GetVisualDescendants().OfType<TextBlock>().First(text => text.Text == "Output");

            Assert.Contains(EditorTheme.Island, panel.Classes);
            Assert.Equal(BoxOf(window.Center.Stage, window).Bottom + CenterArea.GapHeight, BoxOf(panel, window).Y);
            Assert.Equal(36, strip.Height);
            Assert.Equal(EditorBrushes.Line, panel.Strip.BorderBrush);
            Assert.Equal(("Output", FontWeight.SemiBold, strip.X + 12), (title.Text, title.FontWeight, BoxOf(title, window).X));
            Assert.Equal(strip.Right - 6, BoxOf(panel.ClearButton, window).Right);
            Assert.Equal(BoxOf(panel.ClearButton, window).X - 2, BoxOf(panel.CopyButton, window).Right);
            Assert.Equal(strip.Bottom, BoxOf(panel.List, window).Y);
        });
    }

    [Fact]
    public void Copy_all_puts_every_line_as_the_engine_wrote_it_on_the_clipboard()
    {
        UiThread.Run(async () =>
        {
            var (window, scene) = Open();
            ClickOn(BarOf(window).OutputTab, window);
            Write(window, scene, "one", "ERROR: two");

            ClickOn(window.Center.Output.CopyButton, window);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal("ActionCopy", window.Center.Output.CopyButton.GetVisualDescendants().OfType<EditorIcon>().Single().IconName);
            Assert.Empty(Texts(window.Center.Output.CopyButton));
            Assert.Equal("Copy all", AutomationProperties.GetName(window.Center.Output.CopyButton));
            Assert.Equal("Copies every line, to paste in a bug report.", ((HoverCard)ToolTip.GetTip(window.Center.Output.CopyButton)!).Line);
            Assert.Equal("one\nERROR: two", await window.Clipboard!.TryGetTextAsync());
            Assert.Equal("Copied the output.", window.Status);
        });
    }

    [Fact]
    public void Errors_logged_while_the_panel_is_shut_wait_on_a_badge_until_it_opens()
    {
        UiThread.Run(() =>
        {
            var (window, scene) = Open();
            var bar = BarOf(window);

            Write(window, scene, "fine", "ERROR: broke");

            Assert.True(bar.Unread.IsVisible);
            Assert.Equal("1", ((TextBlock)bar.Unread.Child!).Text);

            Write(window, scene, "SCRIPT ERROR: broke again");
            Assert.Equal("2", ((TextBlock)bar.Unread.Child!).Text);

            ClickOn(bar.OutputTab, window);
            Assert.False(bar.Unread.IsVisible);

            Write(window, scene, "ERROR: seen at once");
            Assert.False(bar.Unread.IsVisible);
        });
    }

    [Fact]
    public void Each_chip_counts_its_level_and_hides_it_when_turned_off()
    {
        UiThread.Run(() =>
        {
            var (window, scene) = Open();
            var output = window.Center.Output;
            ClickOn(BarOf(window).OutputTab, window);
            Write(window, scene, "ERROR: broke", "WARNING: slow", "plain", "also plain");

            Assert.Equal(["1", "1", "2"], output.Chips.Select(chip => chip.Count.Text));
            Assert.Equal(["Errors", "1", "Warnings", "1", "Messages", "2"], output.Chips.SelectMany(Texts));
            Assert.Equal(["Errors", "Warnings", "Messages"], output.Chips.Select(chip => ((HoverCard)ToolTip.GetTip(chip)!).Title));
            Assert.Equal("Lines where something failed. Click to hide or show them.", ((HoverCard)ToolTip.GetTip(output.Chips[0])!).Line);

            ClickOn(output.Chips[2], window);
            window.UpdateLayout();

            Assert.Equal(["broke", "slow"], Shown(window));
            Assert.Equal(0.4, output.Chips[2].Opacity);
            Assert.Equal(1, output.Chips[0].Opacity);
        });
    }

    [Fact]
    public void The_filter_keeps_the_lines_that_hold_its_text_in_any_case()
    {
        UiThread.Run(() =>
        {
            var (window, scene) = Open();
            ClickOn(BarOf(window).OutputTab, window);
            Write(window, scene, "alpha", "beta", "Alphabet");

            window.Center.Output.FilterBox.Text = "ALPHA";
            window.UpdateLayout();

            Assert.Equal(["alpha", "Alphabet"], Shown(window));
            Assert.Equal("Find in messages", window.Center.Output.FilterBox.PlaceholderText);
        });
    }

    [Fact]
    public void A_fault_shows_its_text_in_its_colour_with_the_file_it_came_from()
    {
        UiThread.Run(() =>
        {
            var (window, scene) = Open();
            ClickOn(BarOf(window).OutputTab, window);

            Write(window, scene, "ERROR: Cannot open file", "   at: load (res://scenes/main.oscn:12)");

            var texts = window.Center.Output.List.GetVisualDescendants().OfType<TextBlock>().ToArray();
            Assert.Equal(["Cannot open file", "main.oscn:12"], Shown(window).Order());
            var text = texts.Single(block => block.Text == "Cannot open file");
            Assert.True(BoxOf(texts.Single(block => block.Text == "main.oscn:12"), window).X > BoxOf(text, window).Right);
            Assert.Equal(EditorColors.FaultText, ((ISolidColorBrush)text.Foreground!).Color);
            Assert.Equal("FaultCircle", window.Center.Output.List.GetVisualDescendants().OfType<EditorIcon>().Single().IconName);
        });
    }

    [Fact]
    public void Each_line_shows_the_time_it_came_and_an_info_mark_and_a_blank_line_shows_no_row()
    {
        UiThread.Run(() =>
        {
            var time = new ManualTime();
            var scene = new FakeScene();
            var window = OpenAtReferenceSize(scene, time: time);
            ClickOn(BarOf(window).OutputTab, window);

            Write(window, scene, "first");
            time.Advance(TimeSpan.FromSeconds(62));
            Write(window, scene, "", "second");

            var texts = window.Center.Output.List.GetVisualDescendants().OfType<TextBlock>().ToArray();
            Assert.Equal(["16:33:00", "16:34:02"], texts.Where(text => text.Classes.Contains(OutputPanel.Stamp)).Select(text => text.Text));
            Assert.Equal(["first", "second"], Shown(window));
            Assert.All(window.Center.Output.List.GetVisualDescendants().OfType<EditorIcon>(), icon => Assert.Equal("InfoCircle", icon.IconName));
            Assert.All(texts.Where(text => !text.Classes.Contains(OutputPanel.Stamp)), text => Assert.Equal(TextWrapping.Wrap, text.TextWrapping));
        });
    }

    [Fact]
    public void Rows_sit_close_like_lines_of_a_log()
    {
        UiThread.Run(() =>
        {
            var (window, scene) = Open();
            ClickOn(BarOf(window).OutputTab, window);

            Write(window, scene, "first", "second");

            var rows = window.Center.Output.List.GetVisualDescendants().OfType<ListBoxItem>().ToArray();
            Assert.Equal(2, rows.Length);
            Assert.True(rows[0].Bounds.Height < 26, $"a row is {rows[0].Bounds.Height} high");
        });
    }

    [Fact]
    public void The_clear_button_is_the_clear_icon_alone_with_its_name_on_a_card()
    {
        UiThread.Run(() =>
        {
            var (window, _) = Open();
            ClickOn(BarOf(window).OutputTab, window);
            var button = window.Center.Output.ClearButton;

            Assert.Equal("Clear", button.GetVisualDescendants().OfType<EditorIcon>().Single().IconName);
            Assert.Empty(Texts(button));
            Assert.Equal("Clear", AutomationProperties.GetName(button));
            Assert.Equal("Clear", ((HoverCard)ToolTip.GetTip(button)!).Title);
            Assert.Equal("Empties the list. New lines still come in.", ((HoverCard)ToolTip.GetTip(button)!).Line);
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
}
