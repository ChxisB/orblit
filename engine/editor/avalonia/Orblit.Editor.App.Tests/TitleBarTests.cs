using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.VisualTree;
using Orblit.Editor.App.Shell;
using Orblit.Editor.App.Theme;
using static Orblit.Editor.App.Tests.ReferenceWindow;

namespace Orblit.Editor.App.Tests;

public sealed class TitleBarTests
{
    static string Heading(EditorWindow window) =>
        string.Join(" ", window.Bar.Heading.Children.OfType<TextBlock>().Where(text => text.IsVisible).Select(text => text.Text));

    static MenuItem[] RunItems(EditorWindow window) => [.. ((MenuFlyout)window.Bar.MoreButton.Flyout!).Items.OfType<MenuItem>()];

    static MenuItem RunItem(EditorWindow window, string header) => RunItems(window).Single(item => AutomationProperties.GetName(item) == header);

    static string? Note(MenuItem item) => ((Panel)item.Header!).Children.OfType<TextBlock>().Last().Text;

    static string[] Words(Control control) => [.. control.GetLogicalDescendants().OfType<TextBlock>().Select(text => text.Text ?? "")];

    static HoverCard CardOf(Control control) => Assert.IsType<HoverCard>(ToolTip.GetTip(control));

    static string Keys(EditorWindow window, Key key) => Parts.KeyText(new KeyGesture(key, window.CommandKey));

    static void Press(EditorWindow window, Key key) =>
        window.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key, KeyModifiers = window.CommandKey });

    [Fact]
    public void The_bar_shows_project_slash_scene_and_an_orange_dot_while_the_scene_has_edits()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize(new FakeScene { SceneFile = "res://main.oscn", ProjectName = "view_demo" });
            Assert.Equal("view_demo / main.oscn", Heading(window));
            Assert.Equal("main.oscn - view_demo", window.Title);
            Assert.False(window.Bar.EditedDot.IsVisible);

            MakeAnEdit(window);
            Assert.True(window.Bar.EditedDot.IsVisible);
            Assert.Equal(EditorColors.Accent, ((ISolidColorBrush)window.Bar.EditedDot.Fill!).Color);
            Assert.Equal("(*) main.oscn - view_demo", window.Title);

            Click(window.SaveButton);
            Assert.False(window.Bar.EditedDot.IsVisible);
        });
    }

    [Fact]
    public void A_scene_with_no_file_shows_the_project_alone()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize(new FakeScene { SceneFile = "", ProjectName = "" });

            Assert.Equal("Unnamed Project", Heading(window));
        });
    }

    [Fact]
    public void The_project_name_is_bold_the_slash_dim_and_the_scene_soft()
    {
        UiThread.Run(() =>
        {
            var parts = OpenAtReferenceSize().Bar.Heading.Children.OfType<TextBlock>().ToArray();

            Assert.Equal(FontWeight.SemiBold, parts[0].FontWeight);
            Assert.Equal(EditorColors.Disabled, ((ISolidColorBrush)parts[1].Foreground!).Color);
            Assert.Equal(EditorColors.TextSoft, ((ISolidColorBrush)parts[2].Foreground!).Color);
        });
    }

    [Fact]
    public void The_logo_is_an_amber_to_orange_disc_15_wide_after_the_traffic_lights()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var fill = (LinearGradientBrush)window.Bar.Logo.Fill!;

            Assert.Equal([Color.Parse("#FFC21A"), Color.Parse("#FF6A00")], fill.GradientStops.Select(stop => stop.Color));
            var box = BoxOf(window.Bar.Logo, window);
            Assert.Equal(92, box.X);
            Assert.Equal(23, box.Center.Y, 0.5);
            Assert.Equal(15, box.Width);
        });
    }

    [Fact]
    public void The_window_bar_lists_the_four_menus_in_soft_26_high_tabs_where_the_system_bar_does_not()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var menus = window.Bar.Menus.GetVisualDescendants().OfType<MenuItem>().ToArray();

            Assert.True(window.Bar.Menus.IsEffectivelyVisible);
            Assert.Equal(["Scene", "Project", "Debug", "Editor"], menus.Select(item => item.Header as string));
            Assert.All(menus, item =>
            {
                Assert.Equal(26, item.Bounds.Height);
                Assert.Equal(EditorColors.TextSoft, ((ISolidColorBrush)item.Foreground!).Color);
            });
            Assert.True(BoxOf(window.Bar.Menus, window).X > BoxOf(window.Bar.EditedDot, window).Right);
        });
    }

    [Fact]
    public void The_more_menu_lists_the_ways_to_run_with_their_keys()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var command = window.CommandKey;

            Assert.Equal(
                [
                    ("Play the game", "Starts from the main scene", new KeyGesture(Key.B, command)),
                    ("Play this scene", "Starts from the scene you are in", new KeyGesture(Key.R, command)),
                ],
                RunItems(window).Select(item => (AutomationProperties.GetName(item), Note(item), item.InputGesture)));
        });
    }

    [Fact]
    public void Run_the_project_in_the_more_menu_plays_like_the_button()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            var window = OpenAtReferenceSize(scene);

            RunItem(window, "Play the game").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

            Assert.Equal(1, scene.Plays);
        });
    }

    [Fact]
    public void Command_B_runs_the_project()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            var window = OpenAtReferenceSize(scene);

            var press = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.B, KeyModifiers = window.CommandKey };
            window.RaiseEvent(press);

            Assert.True(press.Handled);
            Assert.Equal(1, scene.Plays);
        });
    }

    [Fact]
    public void Run_the_current_scene_gives_the_game_the_open_scene_file()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene { SceneFile = "res://levels/one.oscn" };
            var window = OpenAtReferenceSize(scene);
            scene.PlayedFlags.Add("--stale");

            Click(window.PlaySceneButton);

            Assert.Equal(1, scene.Plays);
            Assert.Equal(["--scene", "res://levels/one.oscn"], scene.PlayedFlags.TakeLast(2));
            Assert.DoesNotContain("--stale", scene.PlayedFlags);
        });
    }

    [Fact]
    public void Command_R_and_the_more_menu_run_the_current_scene()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene { SceneFile = "res://levels/one.oscn" };
            var window = OpenAtReferenceSize(scene);

            var press = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.R, KeyModifiers = window.CommandKey };
            window.RaiseEvent(press);
            RunItem(window, "Play this scene").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

            Assert.True(press.Handled);
            Assert.Equal(2, scene.Plays);
            Assert.Equal("res://levels/one.oscn", scene.PlayedFlags[^1]);
        });
    }

    [Fact]
    public void Run_the_current_scene_is_off_while_the_scene_has_no_file()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize(new FakeScene());

            Assert.True(window.PlayButton.IsEnabled);
            Assert.False(window.PlaySceneButton.IsEnabled);
            Assert.False(RunItem(window, "Play this scene").IsEnabled);
        });
    }

    [Fact]
    public void Play_and_Stop_show_their_icon_word_and_key()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            Assert.Equal(["Play", Keys(window, Key.B)], Words(window.PlayButton));
            Assert.Equal(["Stop", Keys(window, Key.OemPeriod)], Words(window.StopButton));
            Assert.Equal("MainPlay", window.PlayButton.GetLogicalDescendants().OfType<EditorIcon>().Single().IconName);
        });
    }

    [Fact]
    public void Every_control_of_the_bar_has_a_card()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            Control[] controls = [.. window.Bar.Tabs, window.PlayButton, window.StopButton, window.Bar.MoreButton, window.Bar.SearchButton];

            Assert.All(controls, control => Assert.False(string.IsNullOrEmpty(CardOf(control).Line)));
            Assert.Equal(Keys(window, Key.K), CardOf(window.Bar.SearchButton).Key);
        });
    }

    [Fact]
    public void Each_workspace_card_gives_its_name_and_its_number_key()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            Assert.Equal(
                [("2D", Keys(window, Key.D1)), ("3D", Keys(window, Key.D2)), ("Script", Keys(window, Key.D3)), ("Graph", Keys(window, Key.D4)), ("Game", Keys(window, Key.D5))],
                window.Bar.Tabs.Select(tab => (CardOf(tab).Title, CardOf(tab).Key)));
            Assert.Equal("Visual script editor", CardOf(window.Bar.Tabs[3]).Engine);
        });
    }

    [Theory]
    [InlineData(Key.D1, "2D")]
    [InlineData(Key.D2, "3D")]
    [InlineData(Key.D3, "Script")]
    [InlineData(Key.D4, "Graph")]
    [InlineData(Key.D5, "Game")]
    public void Command_and_a_number_switch_the_workspace(Key key, string workspace)
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            Press(window, key);

            Assert.Equal(workspace, window.Bar.Tabs.Single(tab => tab.IsChecked == true).Label);
        });
    }

    [Fact]
    public void Stop_says_why_it_is_off_until_the_game_plays()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize(new FakeScene());
            var card = CardOf(window.StopButton);

            Assert.Equal($"Nothing is playing. Press {Keys(window, Key.B)} first.", card.Line);
            Click(window.PlayButton);

            Assert.Equal("Closes the game window.", card.Line);
        });
    }

    [Fact]
    public void Command_and_full_stop_stops_the_game()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            var window = OpenAtReferenceSize(scene);
            Click(window.PlayButton);

            Press(window, Key.OemPeriod);

            Assert.Equal(1, scene.Stops);
        });
    }
}
