using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using Orblit.Editor.App.Theme;
using static Orblit.Editor.App.Tests.ReferenceWindow;

namespace Orblit.Editor.App.Tests;

public sealed class TitleBarTests
{
    static string Heading(EditorWindow window) =>
        string.Join(" ", window.Bar.Heading.Children.OfType<TextBlock>().Where(text => text.IsVisible).Select(text => text.Text));

    static MenuItem[] RunItems(EditorWindow window) => [.. ((MenuFlyout)window.Bar.MoreButton.Flyout!).Items.OfType<MenuItem>()];

    static MenuItem RunItem(EditorWindow window, string header) => RunItems(window).Single(item => (string?)item.Header == header);

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
    public void The_window_bar_lists_the_five_menus_in_soft_26_high_tabs_where_the_system_bar_does_not()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var menus = window.Bar.Menus.GetVisualDescendants().OfType<MenuItem>().ToArray();

            Assert.True(window.Bar.Menus.IsEffectivelyVisible);
            Assert.Equal(["Scene", "Project", "Debug", "Editor", "Help"], menus.Select(item => item.Header as string));
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
                    ("Run the project", new KeyGesture(Key.B, command)),
                    ("Run the current scene", new KeyGesture(Key.R, command)),
                    ("Run a chosen scene...", new KeyGesture(Key.R, command | KeyModifiers.Shift)),
                    ("Movie maker mode", null),
                    ("Customize Run Instances...", null),
                ],
                RunItems(window).Select(item => ((string)item.Header!, item.InputGesture)));
        });
    }

    [Fact]
    public void Lines_split_the_more_menu_into_three_groups()
    {
        UiThread.Run(() =>
        {
            var items = ((MenuFlyout)OpenAtReferenceSize().Bar.MoreButton.Flyout!).Items;

            Assert.Equal(["Item", "Item", "Item", "Line", "Item", "Line", "Item"], items.Select(item => item is Separator ? "Line" : "Item"));
        });
    }

    [Fact]
    public void Only_Run_the_project_lacks_the_soon_mark()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            Assert.Equal(["Run the project"], RunItems(window).Where(item => !EditorTheme.IsSoon(item)).Select(item => (string)item.Header!));
        });
    }

    [Fact]
    public void Run_the_project_in_the_more_menu_plays_like_the_button()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            var window = OpenAtReferenceSize(scene);

            RunItem(window, "Run the project").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

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

    [Theory]
    [InlineData("Run the current scene", "Run the current scene is not built yet.")]
    [InlineData("Run a chosen scene...", "Run a chosen scene is not built yet.")]
    [InlineData("Movie maker mode", "Movie maker mode is not built yet.")]
    [InlineData("Customize Run Instances...", "Customize Run Instances is not built yet.")]
    public void A_way_to_run_the_editor_lacks_answers_that_it_is_not_built(string header, string answer)
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            RunItem(window, header).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

            Assert.Equal(answer, window.Status);
        });
    }
}
