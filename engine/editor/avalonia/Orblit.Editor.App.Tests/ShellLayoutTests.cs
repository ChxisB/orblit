using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using Orblit.Editor.App.Shell;
using Orblit.Editor.App.Theme;
using static Orblit.Editor.App.Tests.ReferenceWindow;

namespace Orblit.Editor.App.Tests;

public sealed class ShellLayoutTests
{
    [Fact]
    public void The_title_bar_runs_across_the_top()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            AssertBox(0, 0, 3440, 41, BoxOf(window.Bar, window));
        });
    }

    [Fact]
    public void The_left_column_holds_the_scene_and_file_docks()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            AssertBox(4, 41, 280, 615, BoxOf(window.SceneDock, window));
            AssertBox(4, 660, 280, 615, BoxOf(window.FileDock, window));
        });
    }

    [Fact]
    public void The_middle_column_stacks_tabs_toolbar_viewport_and_bottom_bar()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            AssertBox(287, 41, 2866, 33, BoxOf(window.Center.SceneTabs, window));
            AssertBox(287, 74, 2866, 42, BoxOf(window.Center.Toolbar, window));
            AssertBox(287, 116, 2866, 1115, BoxOf(window.Viewport, window));
            AssertBox(287, 1234, 2866, 41, BoxOf(window.Center.BottomBar, window));
        });
    }

    [Fact]
    public void The_right_column_holds_the_inspector_dock()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            AssertBox(3156, 41, 280, 1234, BoxOf(window.InspectorDock, window));
        });
    }

    [Fact]
    public void The_gaps_between_areas_show_the_window_colour()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            Assert.Equal(EditorColors.Window, Assert.IsAssignableFrom<ISolidColorBrush>(window.Background).Color);
        });
    }

    [Fact]
    public void Each_dock_starts_on_its_first_tab()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            Assert.Equal(0, window.SceneDock.SelectedIndex);
            Assert.Equal(0, window.FileDock.SelectedIndex);
            Assert.Equal(0, window.InspectorDock.SelectedIndex);
        });
    }

    [Fact]
    public void The_title_follows_the_window_title()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            window.Title = "(*) main.oscn - Demo";

            Assert.Equal("(*) main.oscn - Demo", window.Bar.Title.Text);
        });
    }

    [Fact]
    public void The_3D_workspace_is_lit_at_the_start_and_a_click_on_2D_lights_that_one_instead()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var tabs = window.Bar.Tabs.ToArray();
            Assert.Equal(["3D"], tabs.Where(tab => tab.IsChecked == true).Select(tab => tab.Label));

            ClickOn(tabs.Single(tab => tab.Label == "2D"), window);

            Assert.Equal(["2D"], tabs.Where(tab => tab.IsChecked == true).Select(tab => tab.Label));
        });
    }

    [Fact]
    public void The_3D_workspace_is_built_so_a_click_says_nothing()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var before = window.Status;

            ClickOn(window.Bar.Tabs.Single(tab => tab.Label == "3D"), window);

            Assert.Equal(before, window.Status);
            Assert.True(window.Bar.Tabs.Single(tab => tab.Label == "3D").IsChecked);
        });
    }

    [Fact]
    public void Pause_and_stop_are_off_while_no_game_runs()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var buttons = window.Bar.GetVisualDescendants().OfType<Button>().Where(button => ToolTip.GetTip(button) is "Pause the game" or "Stop the game");

            Assert.Equal(2, buttons.Count());
            Assert.All(buttons, button => Assert.False(button.IsEnabled));
        });
    }

    [Fact]
    public void A_run_button_the_editor_lacks_answers_in_the_status()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var button = window.Bar.GetVisualDescendants().OfType<Button>().Single(b => ToolTip.GetTip(b) is "Movie maker mode");

            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            Assert.Equal("Movie maker mode is not built yet.", window.Status);
        });
    }

    [Fact]
    public void The_play_button_sits_in_the_title_bar()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            Assert.Contains(window.PlayButton, window.Bar.GetVisualDescendants());
        });
    }

    [Fact]
    public void The_play_button_draws_the_run_triangle_not_a_word()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            Assert.Equal("MainPlay", ((EditorIcon)window.PlayButton.Content!).IconName);
        });
    }

    [Fact]
    public void The_run_buttons_are_32_wide_with_4_clear_before_the_scene_buttons_and_14_from_the_edge()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var tips = new[] { "Run the project", "Pause the game", "Stop the game", "Run the current scene", "Run a chosen scene", "Movie maker mode" };
            var boxes = tips
                .Select(tip => BoxOf(window.Bar.GetVisualDescendants().OfType<Button>().Single(button => ToolTip.GetTip(button) as string == tip), window))
                .ToArray();

            Assert.All(boxes, box => Assert.Equal(new Size(32, 32), box.Size));
            Assert.Equal(boxes[0].X + 32, boxes[1].X);
            Assert.Equal(boxes[1].X + 32, boxes[2].X);
            Assert.Equal(boxes[2].Right + 4, boxes[3].X);
            Assert.Equal(boxes[3].X + 32, boxes[4].X);
            Assert.Equal(boxes[4].X + 32, boxes[5].X);
            Assert.Equal(3440 - 14, boxes[5].Right);
        });
    }

    [Fact]
    public void The_lit_workspace_has_no_box_behind_it()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var lit = window.Bar.Tabs.Single(tab => tab.IsChecked == true);
            var backing = lit.GetVisualDescendants().OfType<ContentPresenter>().Single(presenter => presenter.Name == "PART_ContentPresenter");

            Assert.Equal(Colors.Transparent, ((ISolidColorBrush)backing.Background!).Color);
        });
    }

    [Fact]
    public void Each_workspace_name_is_bold_and_sits_10_pixels_inside_its_tab()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            foreach (var tab in window.Bar.Tabs)
            {
                var content = (StackPanel)tab.Content!;
                Assert.Equal(FontWeight.Bold, content.Children.OfType<TextBlock>().Single().FontWeight);
                Assert.Equal(BoxOf(tab, window).X + 10 + tab.BorderThickness.Left, BoxOf(content, window).X);
            }
        });
    }

    [Fact]
    public void An_icon_button_is_the_icon_with_6_pixels_around_it()
    {
        UiThread.Run(() =>
        {
            var button = Parts.IconButton("Add", "Add");
            button.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;
            button.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;
            var window = new Window { Width = 200, Height = 100, Content = button };
            window.Show();
            window.UpdateLayout();

            // The theme of the editor window gives the button no border. This bare window may.
            var border = button.BorderThickness.Left + button.BorderThickness.Right;
            Assert.Equal(new Size(28 + border, 28 + border), button.Bounds.Size);
        });
    }

    [Fact]
    public void The_search_icon_of_each_filter_box_is_sixteen_square_and_centred()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var icons = window.GetVisualDescendants().OfType<EditorIcon>().Where(icon => icon.IconName == "Search").ToArray();

            Assert.Equal(3, icons.Length);
            foreach (var icon in icons)
            {
                var box = icon.FindAncestorOfType<TextBox>()!;
                Assert.Equal(new Size(16, 16), icon.Bounds.Size);
                Assert.Equal(BoxOf(box, window).Center.Y, BoxOf(icon, window).Center.Y);
            }
        });
    }

    [Fact]
    public void Selecting_a_dock_tab_swaps_the_body()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var first = window.FileDock.Content;

            window.FileDock.Select(1);

            Assert.NotSame(first, window.FileDock.Content);
            Assert.Same(window.FileDock.Content, ((Border)window.FileDock.Children[1]).Child);
        });
    }

    [Fact]
    public void A_left_press_on_a_dock_tab_selects_it()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var tab = window.InspectorDock.Tabs[1];

            tab.RaiseEvent(Press(tab, MouseButton.Left));

            Assert.Equal(1, window.InspectorDock.SelectedIndex);
        });
    }

    [Fact]
    public void A_right_press_on_a_dock_tab_leaves_the_choice()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var tab = window.InspectorDock.Tabs[1];

            tab.RaiseEvent(Press(tab, MouseButton.Right));

            Assert.Equal(0, window.InspectorDock.SelectedIndex);
        });
    }

    [Fact]
    public void The_active_tab_is_lit_and_the_others_are_dim()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            window.SceneDock.Select(1);

            Assert.Equal(EditorColors.TabInactive, Foreground(window.SceneDock.Tabs[0]));
            Assert.Equal(EditorColors.Text, Foreground(window.SceneDock.Tabs[1]));
            Assert.Equal(EditorColors.Panel, ((ISolidColorBrush)window.SceneDock.Tabs[1].Background!).Color);
            Assert.Equal(Colors.Transparent, ((ISolidColorBrush)window.SceneDock.Tabs[0].Background!).Color);
        });
    }

    [Fact]
    public void The_window_draws_under_the_system_title_bar()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            Assert.True(window.ExtendClientAreaToDecorationsHint);
            Assert.Equal(41, window.ExtendClientAreaTitleBarHeightHint);
        });
    }

    [Fact]
    public void The_title_starts_after_the_traffic_lights_and_lets_a_press_through()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            Assert.Equal(92, BoxOf(window.Bar.Title, window).X);
            Assert.False(window.Bar.Title.IsHitTestVisible);
        });
    }

    [Fact]
    public void The_two_side_gaps_resize_columns_and_the_left_gap_resizes_rows()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var splitters = window.GetVisualDescendants().OfType<GridSplitter>().ToArray();

            Assert.Equal(3, splitters.Length);
            Assert.Equal(2, splitters.Count(splitter => splitter.ResizeDirection == GridResizeDirection.Columns));
            Assert.Equal(1, splitters.Count(splitter => splitter.ResizeDirection == GridResizeDirection.Rows));
            Assert.All(splitters, splitter => Assert.Equal(Colors.Transparent, ((ISolidColorBrush)splitter.Background!).Color));
        });
    }

    [Fact]
    public void The_inspector_scrolls_inside_its_dock()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            Assert.Same(window.InspectorPane, window.InspectorDock.Content);
            Assert.Same(window.Inspector, window.InspectorPane.Viewer.Content);
        });
    }

    [Fact]
    public void The_middle_strips_use_the_reference_colours()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            Assert.Equal(EditorColors.Window, ((ISolidColorBrush)window.Center.SceneTabs.Background!).Color);
            Assert.Equal(EditorColors.Panel, ((ISolidColorBrush)window.Center.Toolbar.Background!).Color);
            Assert.Equal(EditorColors.Panel, ((ISolidColorBrush)window.Center.BottomBar.Background!).Color);
        });
    }

    [Fact]
    public void The_close_bar_sits_under_the_title_bar_and_pushes_the_columns_down()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            window.CloseBar.IsVisible = true;
            window.UpdateLayout();

            var bar = BoxOf(window.CloseBar, window);
            Assert.Equal(41, bar.Y);
            Assert.True(bar.Height > 0);
            Assert.Equal(41 + bar.Height + window.CloseBar.Margin.Bottom, BoxOf(window.SceneDock, window).Y);
        });
    }

    static Color Foreground(Border tab) => ((ISolidColorBrush)((TextBlock)tab.Child!).Foreground!).Color;

    static PointerPressedEventArgs Press(Border tab, MouseButton button)
    {
        var left = button == MouseButton.Left;
        var properties = new PointerPointProperties(
            left ? RawInputModifiers.LeftMouseButton : RawInputModifiers.RightMouseButton,
            left ? PointerUpdateKind.LeftButtonPressed : PointerUpdateKind.RightButtonPressed);
        return new PointerPressedEventArgs(tab, new Pointer(0, PointerType.Mouse, true), tab, new Point(1, 1), 0, properties, KeyModifiers.None);
    }
}
