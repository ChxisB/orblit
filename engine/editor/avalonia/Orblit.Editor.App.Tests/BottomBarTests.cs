using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Orblit.Editor.App.Shell;
using Orblit.Editor.App.Tests.Graph;
using Orblit.Editor.App.Theme;
using Orblit.Editor.Bridge;
using static Orblit.Editor.App.Tests.ReferenceWindow;

namespace Orblit.Editor.App.Tests;

public sealed class BottomBarTests
{
    static BottomBar BarOf(EditorWindow window) => Assert.IsType<BottomBar>(window.Center.BottomBar.Child);

    // The badge on the Output tab holds a count while errors wait, and hides it otherwise.
    static TextBlock[] Words(Control tab) => [.. tab.GetVisualDescendants().OfType<TextBlock>().Where(text => text.IsEffectivelyVisible)];

    static string[] Labels(Control tab) => [.. Words(tab).Select(text => text.Text!)];

    [Fact]
    public void The_bar_holds_the_five_panel_buttons_in_order()
    {
        UiThread.Run(() =>
        {
            var tabs = BarOf(OpenAtReferenceSize()).Tabs;

            Assert.Equal(["Output", "Debugger"], tabs.Select(tab => Labels(tab).Single()));
        });
    }

    [Fact]
    public void The_first_tab_starts_eight_pixels_in_with_eight_before_its_icon_and_four_after_it()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var bar = BarOf(window);
            var icon = bar.Tabs[0].GetVisualDescendants().OfType<EditorIcon>().Single();
            var text = Words(bar.Tabs[0]).Single();

            AssertBox(297 + 8, 1239, BoxOf(bar.Tabs[0], window).Width, 26, BoxOf(bar.Tabs[0], window));
            // A 13 pixel icon in a 26 pixel tab is half a pixel off center. Geist's line box rounds it up.
            AssertBox(313, 1245, 13, 13, BoxOf(icon, window));
            Assert.Equal(313 + 13 + 4, BoxOf(text, window).X);
        });
    }

    [Fact]
    public void Each_tab_shows_its_panel_icon_13_square_and_no_dot()
    {
        UiThread.Run(() =>
        {
            var bar = BarOf(OpenAtReferenceSize());
            var icons = bar.Tabs.Select(tab => tab.GetVisualDescendants().OfType<EditorIcon>().Single()).ToArray();

            Assert.Equal(["Output", "Debug"], icons.Select(icon => icon.IconName));
            Assert.All(icons, icon => Assert.Equal((13d, 13d), (icon.Bounds.Width, icon.Bounds.Height)));
            Assert.Empty(bar.Tabs.SelectMany(tab => tab.GetVisualDescendants().OfType<Ellipse>()));
        });
    }

    [Fact]
    public void Every_label_sits_on_the_centre_line_of_the_bar()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var texts = BarOf(window).Tabs.SelectMany(Words).ToArray();

            Assert.Equal(2, texts.Length);
            Assert.All(texts, text => Assert.InRange(BoxOf(text, window).Y + text.Bounds.Height / 2, 1251, 1252));
        });
    }

    [Fact]
    public void The_buttons_follow_each_other_two_pixels_apart()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var tabs = BarOf(window).Tabs;

            for (var i = 1; i < tabs.Count; i++)
            {
                Assert.Equal(2, BoxOf(tabs[i], window).X - BoxOf(tabs[i - 1], window).Right);
            }
        });
    }

    [Fact]
    public void The_tabs_are_dim_rounded_and_have_no_border()
    {
        UiThread.Run(() =>
        {
            var tabs = BarOf(OpenAtReferenceSize()).Tabs;

            Assert.All(tabs, tab => Assert.Equal(EditorColors.TextDim, ((ISolidColorBrush)tab.Foreground!).Color));
            Assert.All(tabs, tab => Assert.Equal((0d, EditorTheme.Corners), (tab.BorderThickness.Left, tab.CornerRadius)));
        });
    }

    [Fact]
    public void The_rule_is_one_by_eighteen_and_eight_pixels_clear_of_the_status_and_the_version()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            window.Fail("Saved.");
            window.UpdateLayout();
            var bar = BarOf(window);
            var status = bar.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Text == "Saved.");
            var separator = BoxOf(bar.Separator, window);

            AssertBox(BoxOf(status, window).Right + 8, 1243, 1, 18, separator);
            Assert.Equal(separator.Right + 8, BoxOf(bar.VersionButton, window).X);
            Assert.Equal(Color.Parse("#32271F"), ((ISolidColorBrush)bar.Separator.Background!).Color);
        });
    }

    [Fact]
    public void The_version_is_small_dim_text_with_no_logo()
    {
        UiThread.Run(() =>
        {
            var version = BarOf(OpenAtReferenceSize()).VersionButton;
            var text = version.GetVisualDescendants().OfType<TextBlock>().Single();

            Assert.Equal("1.5.stable", text.Text);
            Assert.Equal((11d, EditorColors.TextDim, 1d), (text.FontSize, ((ISolidColorBrush)version.Foreground!).Color, version.Opacity));
            Assert.Empty(version.GetVisualDescendants().OfType<EditorIcon>());
        });
    }

    [Fact]
    public void A_click_on_the_version_puts_it_on_the_clipboard_and_says_so()
    {
        UiThread.Run(async () =>
        {
            var window = OpenAtReferenceSize();
            var version = BarOf(window).VersionButton;

            ClickOn(version, window);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal("Copy the version", ToolTip.GetTip(version));
            Assert.Equal("1.5.stable", await window.Clipboard!.TryGetTextAsync());
            Assert.Equal(("Copied 1.5.stable.", "Tick"), (window.Status, BarOf(window).StatusIcon.IconName));
        });
    }

    [Fact]
    public void The_pin_icon_ends_thirteen_pixels_from_the_bar_edge_and_follows_the_version_by_thirteen()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var bar = BarOf(window);
            var icon = BoxOf(bar.PinButton.GetVisualDescendants().OfType<EditorIcon>().Single(), window);
            var text = BoxOf(bar.VersionButton.GetVisualDescendants().OfType<TextBlock>().Single(), window);

            Assert.Equal(297 + 2810 - 13, icon.Right);
            Assert.Equal("Pin", bar.PinButton.GetVisualDescendants().OfType<EditorIcon>().Single().IconName);
            Assert.Equal(16, icon.Width);
            Assert.InRange(icon.X - text.Right, 12.5, 13.5);
            AssertBox(3073, 1239, 26, 26, BoxOf(bar.PinButton, window));
        });
    }

    [Fact]
    public void The_status_follows_a_dim_info_icon_in_soft_12_pixel_text()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var bar = BarOf(window);
            var status = bar.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Text == window.Status);
            var icon = bar.StatusIcon;

            Assert.Equal("Info", icon.IconName);
            Assert.Equal(BoxOf(icon, window).Right + 4, BoxOf(status, window).X);
            Assert.Equal((13d, EditorColors.TextDim), (icon.Bounds.Width, ((ISolidColorBrush)TextElement.GetForeground(icon)!).Color));
            Assert.Equal((12d, EditorColors.TextSoft), (status.FontSize, ((ISolidColorBrush)status.Foreground!).Color));
            Assert.InRange(BoxOf(status, window).Y + status.Bounds.Height / 2, 1251, 1252);
        });
    }

    [Fact]
    public void The_mark_before_the_status_says_its_kind()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var bar = BarOf(window);
            string Mark() => $"{bar.StatusIcon.IconName} {((ISolidColorBrush)TextElement.GetForeground(bar.StatusIcon)!).Color}";

            bar.Say("Framed Lamp.", StatusKind.Info);
            Assert.Equal($"Info {EditorColors.TextDim}", Mark());

            bar.Say("Saved.", StatusKind.Done);
            Assert.Equal($"Tick {EditorColors.Done}", Mark());

            window.Fail("The scene could not be saved.");
            Assert.Equal($"FaultCircle {EditorColors.Fault}", Mark());
            Assert.Equal("The scene could not be saved.", window.Status);
        });
    }

    [Fact]
    public void Saving_says_saved_with_a_tick()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize(new FakeScene());

            window.SaveButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            Assert.Equal(("Saved.", "Tick"), (window.Status, BarOf(window).StatusIcon.IconName));
        });
    }

    [Fact]
    public void The_debugger_tab_lights_while_its_panel_shows_and_dims_when_it_closes()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var bar = BarOf(window);
            var before = window.Status;

            ClickOn(bar.Tabs[1], window);
            window.UpdateLayout();

            var panel = window.Center.Panels[1];
            Assert.True(panel.IsVisible);
            Assert.Equal(EditorColors.Text, ((ISolidColorBrush)bar.Tabs[1].Foreground!).Color);
            Assert.False(window.Center.Output.IsVisible);
            Assert.Equal(before, window.Status);
            Assert.Equal(OutputPanel.PanelHeight, panel.Bounds.Height);

            ClickOn(bar.Tabs[1], window);

            Assert.False(panel.IsVisible);
            Assert.Equal(EditorColors.TextDim, ((ISolidColorBrush)bar.Tabs[1].Foreground!).Color);
        });
    }

    [Fact]
    public void One_panel_shows_at_a_time()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var bar = BarOf(window);

            ClickOn(bar.OutputTab, window);
            ClickOn(bar.Tabs[1], window);

            Assert.Equal([false, true], window.Center.Panels.Select(panel => panel.IsVisible));
            Assert.False(bar.OutputOpen);
        });
    }

    [Fact]
    public void The_debugger_says_no_game_is_playing_and_plays_the_game()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            var window = OpenAtReferenceSize(scene);
            var debugger = window.Center.Debugger;
            ClickOn(BarOf(window).Tabs[1], window);
            window.UpdateLayout();

            Assert.True(window.Center.Panels[1].IsVisible);
            Assert.Equal(
                [
                    "Debugger", "No game is playing",
                    "Press Play and the debugger joins in. It shows errors, pauses and the value of everything while the game runs.",
                    "Play the game", Parts.KeyText(new KeyGesture(Key.B, window.CommandKey)),
                ],
                Labels(window.Center.Panels[1]));
            Assert.True(debugger.RunButton.IsEnabled);
            Assert.Equal("Starts the scene Play settings pick.", Assert.IsType<HoverCard>(ToolTip.GetTip(debugger.RunButton)).Line);

            debugger.RunButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            Assert.Equal(1, scene.Plays);
            Assert.Equal("The game is running", debugger.Title.Text);
            Assert.False(debugger.RunButton.IsVisible);
            Assert.True(window.Center.Panels[1].IsVisible);
            Assert.False(window.Center.Output.IsVisible);
        });
    }

    [Fact]
    public void A_running_game_shows_its_process_id_for_an_outside_ide_and_hides_it_once_stopped()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            var window = OpenAtReferenceSize(scene);
            var debugger = window.Center.Debugger;
            var copied = new List<string>();
            debugger.CopyRequested += copied.Add;
            Assert.False(debugger.CopyButton.IsVisible);

            debugger.RunButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            debugger.CopyButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            Assert.Equal(
                "Graph scripts stop at their breakpoints in the Graph workspace. C# stops at its breakpoints when netcoredbg is on PATH "
                + "or ORBLIT_NETCOREDBG names it. Get it from https://github.com/Samsung/netcoredbg/releases. Or attach VS Code or Rider to process 4242.",
                debugger.Line.Text);
            Assert.True(debugger.CopyButton.IsVisible);
            Assert.Equal(["4242"], copied);

            window.Game.StopButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            Assert.False(debugger.CopyButton.IsVisible);
            Assert.Equal("No game is playing", debugger.Title.Text);
        });
    }

    [Fact]
    public void Play_sends_the_csharp_breakpoints_and_a_csharp_stop_shows_the_script_and_the_session()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            scene.ScriptText["res://spin.cs"] = "class Spin\n{\n}\n";
            var window = OpenAtReferenceSize(scene);
            var debugger = window.Center.Debugger;
            window.Script.Breakpoints.Toggle("res://spin.cs", 2);

            window.PlayButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            scene.CsharpChannel.Next = FakeDebugChannel.Stopped("breakpoint", new DebugFrame("res://spin.cs", 2, "Spin.Run()"));
            window.Poll();

            Assert.Equal([("res://spin.cs", 2)], scene.PlayedWithCode);
            Assert.True(window.Script.IsVisible);
            Assert.True(window.Center.Panels[1].IsVisible);
            Assert.Same(debugger.Session, debugger.Child);
            Assert.Same(window.Script.DebugPanel, debugger.Session.Content);

            scene.CsharpChannel.Next = DebugState.None;
            window.Poll();
            window.UpdateLayout();

            Assert.NotSame(debugger.Session, debugger.Child);
            Assert.Null(debugger.Session.Content);
            Assert.Contains(debugger.Line, debugger.GetVisualDescendants());
        });
    }

    [Fact]
    public void The_debugger_cannot_run_before_a_scene_is_open()
    {
        UiThread.Run(() =>
        {
            var window = new EditorWindow();
            window.Show();

            Assert.Equal("Starting the engine.", window.Status);
            Assert.False(window.Center.Debugger.RunButton.IsEnabled);
            Assert.Equal("Open a scene first.", Assert.IsType<HoverCard>(ToolTip.GetTip(window.Center.Debugger.RunButton)).Line);
            Assert.Equal("No game is playing", window.Center.Debugger.Title.Text);
        });
    }

    [Fact]
    public void A_long_status_grows_leftward_and_leaves_the_pin_where_it_is()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var bar = BarOf(window);
            var before = BoxOf(bar.PinButton, window).X;

            window.Fail("A long answer that takes much more room than the short one before it.");
            window.UpdateLayout();

            Assert.Equal(before, BoxOf(bar.PinButton, window).X);
        });
    }

    [Fact]
    public void The_bar_is_the_child_of_the_bottom_strip()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            Assert.IsType<BottomBar>(window.Center.BottomBar.Child);
            AssertBox(297, 1234, 2810, 36, BoxOf(BarOf(window), window));
        });
    }
}
