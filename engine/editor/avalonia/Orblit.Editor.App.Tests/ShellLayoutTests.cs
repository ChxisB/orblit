using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
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

            AssertBox(0, 0, 3440, 46, BoxOf(window.Bar, window));
        });
    }

    [Fact]
    public void The_left_column_holds_the_scene_and_file_docks()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            AssertBox(6, 46, 280, 611, BoxOf(window.SceneDock, window));
            AssertBox(6, 663, 280, 610, BoxOf(window.FileDock, window));
        });
    }

    [Fact]
    public void The_middle_column_stacks_tabs_viewport_and_bottom_bar_with_the_tools_over_the_viewport()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            AssertBox(293, 47, 2818, 39, BoxOf(window.Center.SceneTabs, window));
            AssertBox(293, 86, 2818, 1142, BoxOf(window.Center.Toolbar, window));
            AssertBox(293, 86, 2818, 1142, BoxOf(window.Viewport, window));
            AssertBox(292, 1235, 2820, 38, BoxOf(window.Center.BottomBar, window));
        });
    }

    [Fact]
    public void The_right_column_holds_the_inspector_dock()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            AssertBox(3118, 46, 316, 1227, BoxOf(window.InspectorDock, window));
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
    public void The_header_is_46_high_with_the_workspaces_centred_in_a_pill_and_the_run_buttons_in_another()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            Assert.Equal(46, window.Bar.Bounds.Height);
            Assert.Equal(window.Bounds.Width / 2, BoxOf(window.Bar.Switcher, window).Center.X, 0.5);
            foreach (var pill in new[] { window.Bar.Switcher, window.Bar.Run })
            {
                Assert.Contains(EditorTheme.Island, pill.Classes);
                Assert.Equal(new Thickness(3), pill.Padding);
                Assert.Equal(23, BoxOf(pill, window).Center.Y, 0.5);
            }

            Assert.Equal(26, window.Bar.Tabs[0].Bounds.Height);
            Assert.Equal(176, window.Bar.SearchButton.Bounds.Width);
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
    public void Stop_is_off_while_no_game_runs()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            Assert.False(window.StopButton.IsEnabled);
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
    public void The_run_buttons_are_26_high_with_a_rule_before_the_more_menu_and_the_pill_8_from_the_edge()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var play = BoxOf(window.PlayButton, window);
            var stop = BoxOf(window.StopButton, window);
            var more = BoxOf(window.Bar.MoreButton, window);

            Assert.Equal((26d, 26d), (play.Height, stop.Height));
            Assert.Equal(new Size(26, 26), more.Size);
            Assert.Equal(play.Right + 2, stop.X);
            // The rule is 1 wide with 3 each side, inside the 2 pixel gaps.
            Assert.Equal(stop.Right + 11, more.X);
            Assert.Equal(3440 - 8, BoxOf(window.Bar.Run, window).Right);
        });
    }

    [Fact]
    public void The_lit_workspace_is_lifted_with_a_bright_name_and_an_orange_icon()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var lit = window.Bar.Tabs.Single(tab => tab.IsChecked == true);
            var dim = window.Bar.Tabs.First(tab => tab.IsChecked != true);

            static Color Backing(WorkspaceTab tab) =>
                ((ISolidColorBrush)tab.GetVisualDescendants().OfType<ContentPresenter>().Single(presenter => presenter.Name == "PART_ContentPresenter").Background!).Color;
            static Color Name(WorkspaceTab tab) => ((ISolidColorBrush)tab.GetVisualDescendants().OfType<TextBlock>().Single().Foreground!).Color;
            static Color Icon(WorkspaceTab tab) => ((ISolidColorBrush)TextElement.GetForeground(tab.GetVisualDescendants().OfType<EditorIcon>().Single())!).Color;

            Assert.Equal((EditorColors.Lifted, EditorColors.Text, EditorColors.Accent), (Backing(lit), Name(lit), Icon(lit)));
            Assert.Equal((Colors.Transparent, EditorColors.TextDim, EditorColors.TextDim), (Backing(dim), Name(dim), Icon(dim)));
        });
    }

    [Fact]
    public void Each_workspace_name_is_medium_weight_and_sits_10_pixels_inside_its_tab()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            foreach (var tab in window.Bar.Tabs)
            {
                var content = (StackPanel)tab.Content!;
                Assert.Equal(FontWeight.Medium, content.Children.OfType<TextBlock>().Single().FontWeight);
                Assert.Equal(BoxOf(tab, window).X + 10 + tab.BorderThickness.Left, BoxOf(content, window).X);
            }
        });
    }

    [Fact]
    public void An_icon_button_is_the_icon_with_5_pixels_around_it()
    {
        UiThread.Run(() =>
        {
            var button = Parts.IconButton("Add", "Add");
            button.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;
            button.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;
            var window = new Window { Width = 200, Height = 100, Content = button };
            window.Show();
            window.UpdateLayout();

            Assert.Equal(new Size(26, 26), button.Bounds.Size);
        });
    }

    [Fact]
    public void The_search_icon_of_each_filter_box_is_sixteen_square_and_centred()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            // The Files panel's empty state draws a Search mark too, outside any box.
            var icons = window.GetVisualDescendants().OfType<EditorIcon>().Where(icon => icon.IconName == "Search" && icon.FindAncestorOfType<TitleBar>() is null && icon.FindAncestorOfType<TextBox>() is not null).ToArray();

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
            Assert.Null(first.GetVisualParent());
            Assert.Same(window.FileDock, window.FileDock.Content.FindAncestorOfType<DockTabs>());
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

            Assert.Equal(EditorColors.TextDim, Foreground(window.SceneDock.Tabs[0]));
            Assert.Equal(EditorColors.Text, Foreground(window.SceneDock.Tabs[1]));
            Assert.Equal(EditorColors.Line, ((ISolidColorBrush)window.SceneDock.Tabs[1].Background!).Color);
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
            Assert.Equal(46, window.ExtendClientAreaTitleBarHeightHint);
        });
    }

    [Fact]
    public void The_heading_starts_after_the_traffic_lights_and_lets_a_press_through()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            Assert.Equal(92, BoxOf(window.Bar.Heading, window).X);
            Assert.False(window.Bar.Heading.IsHitTestVisible);
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
    public void Each_dock_the_stage_and_the_bottom_bar_is_an_island()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            foreach (var island in new Border[] { window.SceneDock, window.FileDock, window.InspectorDock, window.Center.Stage, window.Center.BottomBar })
            {
                Assert.Equal(EditorColors.Panel, ((ISolidColorBrush)island.Background!).Color);
                Assert.Equal(EditorColors.Line, ((ISolidColorBrush)island.BorderBrush!).Color);
                Assert.Equal(new Thickness(1), island.BorderThickness);
                Assert.Equal(new CornerRadius(10), island.CornerRadius);
                Assert.True(island.ClipToBounds);
            }
        });
    }

    [Fact]
    public void The_dock_menu_opens_under_its_button_at_the_right()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var menu = window.SceneDock.GetVisualDescendants().OfType<Button>().Single(button => ToolTip.GetTip(button) is "Dock options");

            Assert.Equal(PlacementMode.BottomEdgeAlignedRight, Assert.IsType<MenuFlyout>(menu.Flyout).Placement);
        });
    }

    [Fact]
    public void A_dock_tab_is_a_26_pixel_pill_in_a_39_pixel_strip()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var dock = BoxOf(window.SceneDock, window);
            var tab = BoxOf(window.SceneDock.Tabs[0], window);

            Assert.Equal(26, tab.Height);
            Assert.Equal(dock.X + 1 + 6, tab.X);
            Assert.Equal(dock.Y + 1 + 6, tab.Y);
            Assert.Equal(new CornerRadius(6), window.SceneDock.Tabs[0].CornerRadius);
        });
    }

    [Fact]
    public void Save_changes_covers_the_whole_window_and_leaves_the_docks_where_they_are()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var dock = BoxOf(window.SceneDock, window);

            window.SaveChanges.Open();
            window.UpdateLayout();

            AssertBox(0, 0, window.Bounds.Width, window.Bounds.Height, BoxOf(window.SaveChanges, window));
            Assert.Equal(dock, BoxOf(window.SceneDock, window));
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
