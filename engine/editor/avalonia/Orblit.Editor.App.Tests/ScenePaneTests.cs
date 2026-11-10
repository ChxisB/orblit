using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Orblit.Editor.App.Shell;
using Orblit.Editor.App.Theme;
using Orblit.Editor.Bridge;
using static Orblit.Editor.App.Tests.ReferenceWindow;

namespace Orblit.Editor.App.Tests;

public sealed class ScenePaneTests
{
    static OutlineRow[] Rows(EditorWindow window)
    {
        window.UpdateLayout();
        return window.Outline.GetVisualDescendants().OfType<OutlineRow>().ToArray();
    }

    static MenuItem[] MenuItems(EditorWindow window) =>
        window.ScenePane.Outline.ContextMenu!.Items.OfType<MenuItem>().ToArray();

    static void ClickItem(EditorWindow window, string header) =>
        MenuItems(window).Single(item => (string)item.Header! == header).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

    static string[] Names(EditorWindow window) =>
        Rows(window).Select(row => row.GetVisualDescendants().OfType<TextBlock>().Single().Text!).ToArray();

    [Fact]
    public void Each_row_has_a_highlight_that_starts_before_its_icon_and_covers_the_line()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            foreach (var row in Rows(window))
            {
                var highlight = row.GetVisualDescendants().OfType<RowHighlight>().Single();

                Assert.Equal(-4, highlight.Area().X);
                Assert.Equal(EditorTheme.RowHeight, highlight.Area().Height);
            }
        });
    }

    [Fact]
    public void The_row_icons_are_drawn_in_the_tree_colour()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            var icon = RowIconOf(Rows(window).First());

            Assert.Equal(EditorColors.TreeIcon, ((ISolidColorBrush)TextElement.GetForeground(icon)!).Color);
        });
    }

    [Fact]
    public void The_button_row_holds_add_instance_filter_and_menu()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var pane = window.ScenePane;

            AssertBox(72, 78, 175, 28, BoxOf(pane.FilterBox, window));
            Assert.Equal(6, BoxOf(pane.AddButton, window).X);
            Assert.Equal(38, BoxOf(pane.InstanceButton, window).X);
            Assert.Equal(249, BoxOf(pane.MenuButton, window).X);
            Assert.Equal("Filter Nodes", pane.FilterBox.PlaceholderText);
        });
    }

    [Fact]
    public void The_tree_starts_under_the_button_row_with_rows_23_high()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            window.UpdateLayout();
            var items = window.Outline.GetVisualDescendants().OfType<TreeViewItem>().ToArray();

            Assert.Equal(3, items.Length);
            Assert.Equal(120, BoxOf(items[0], window).Y);
            Assert.Equal(23, BoxOf(items[1], window).Y - BoxOf(items[0], window).Y);
            Assert.Equal(23, BoxOf(items[2], window).Y - BoxOf(items[1], window).Y);
        });
    }

    [Fact]
    public void A_row_puts_the_arrow_the_icon_and_the_name_where_the_reference_does()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var rows = Rows(window);
            var arrow = window.Outline.GetVisualDescendants().OfType<ToggleButton>().First();

            Assert.Equal(10, BoxOf(arrow, window).X);
            Assert.Equal(26, BoxOf(RowIconOf(rows[0]), window).X);
            Assert.Equal(49, BoxOf(rows[0].GetVisualDescendants().OfType<TextBlock>().Single(), window).X);
            Assert.Equal(38, BoxOf(RowIconOf(rows[1]), window).X);
            Assert.Equal(61, BoxOf(rows[1].GetVisualDescendants().OfType<TextBlock>().Single(), window).X);
        });
    }

    [Fact]
    public void The_arrow_is_a_12_pixel_icon_that_points_down_while_the_row_is_open()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var arrow = window.Outline.GetVisualDescendants().OfType<ToggleButton>().First();
            var icons = arrow.GetVisualDescendants().OfType<EditorIcon>().ToArray();

            Assert.Equal(["GuiTreeArrowRight", "GuiTreeArrowDown"], icons.Select(icon => icon.IconName));
            Assert.Equal([false, true], icons.Select(icon => icon.IsVisible));
            AssertBox(10, BoxOf(arrow, window).Y, 12, 12, BoxOf(icons[1], window));
        });
    }

    [Fact]
    public void Closing_a_row_turns_the_arrow_to_point_right()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var arrow = window.Outline.GetVisualDescendants().OfType<ToggleButton>().First();
            var icons = arrow.GetVisualDescendants().OfType<EditorIcon>().ToArray();

            arrow.IsChecked = false;
            window.UpdateLayout();

            Assert.Equal([true, false], icons.Select(icon => icon.IsVisible));
        });
    }

    [Fact]
    public void A_row_shows_the_class_icon_the_name_and_an_eye_for_a_placed_entity()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var rows = Rows(window);

            Assert.Equal(["Field", "Lamp", "Sound"], Names(window));
            Assert.Equal(2, rows.Count(row => row.GetVisualDescendants().OfType<Button>().Any()));
            Assert.Empty(rows[2].GetVisualDescendants().OfType<Button>());
        });
    }

    [Fact]
    public void The_eye_ends_nine_pixels_from_the_right_edge_of_the_dock()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var eye = Rows(window)[1].GetVisualDescendants().OfType<Button>().Single();

            Assert.Equal(284 - 9, BoxOf(eye.GetVisualDescendants().OfType<EditorIcon>().Single(), window).Right);
        });
    }

    [Theory]
    [InlineData("Entity3D", true)]
    [InlineData("Camera2D", true)]
    [InlineData("Entity", false)]
    [InlineData("Audio", false)]
    [InlineData("Control3Dx", false)]
    public void Only_a_planar_or_spatial_class_has_an_eye(string className, bool expected)
    {
        Assert.Equal(expected, OutlineNode.IsPlaced(className));
    }

    [Fact]
    public void The_tool_rows_are_in_one_flyout_that_the_add_and_menu_buttons_open()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var pane = window.ScenePane;

            var content = Assert.IsType<StackPanel>(pane.Flyout.Content);
            Assert.Equal([window.Tools, window.Move], content.Children);
            Assert.Same(pane.Flyout, pane.AddButton.Flyout);
            Assert.Same(pane.Flyout, pane.MenuButton.Flyout);
            Assert.DoesNotContain(window.Tools, window.SceneDock.GetVisualDescendants());
        });
    }

    [Fact]
    public void A_click_on_the_add_button_opens_the_flyout()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            ClickOn(window.ScenePane.AddButton, window);

            Assert.True(window.ScenePane.Flyout.IsOpen);
        });
    }

    [Fact]
    public void An_edit_from_the_flyout_closes_it()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            window.ScenePane.Flyout.ShowAt(window.ScenePane.AddButton);
            window.Outline.SelectedItem = FakeScene.Lamp;

            window.Tools.DuplicateButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            Assert.False(window.ScenePane.Flyout.IsOpen);
        });
    }

    [Fact]
    public void A_move_from_the_flyout_closes_it()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            window.Outline.SelectedItem = FakeScene.Lamp;
            window.ScenePane.Flyout.ShowAt(window.ScenePane.MenuButton);

            window.Move.DownButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            Assert.False(window.ScenePane.Flyout.IsOpen);
        });
    }

    [Fact]
    public void The_filter_keeps_the_entities_whose_name_matches_and_the_ones_above_them()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            window.ScenePane.FilterBox.Text = "lam";

            Assert.Equal(["Field", "Lamp"], Names(window));
        });
    }

    [Fact]
    public void A_filter_with_no_match_shows_no_rows_and_clearing_it_brings_them_back()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            window.ScenePane.FilterBox.Text = "zzz";
            Assert.Empty(Rows(window));

            window.ScenePane.FilterBox.Text = "";
            Assert.Equal(["Field", "Lamp", "Sound"], Names(window));
        });
    }

    [Fact]
    public void The_selection_survives_a_filter_that_still_shows_it()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            window.Outline.SelectedItem = FakeScene.Lamp;

            window.ScenePane.FilterBox.Text = "lam";

            Assert.Equal("Lamp", window.ScenePane.Selected!.Name);
            Assert.True(window.Tools.RenameButton.IsEnabled);
        });
    }

    [Fact]
    public void A_filter_that_hides_the_selection_keeps_it()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            window.Outline.SelectedItem = FakeScene.Lamp;

            window.ScenePane.FilterBox.Text = "sou";

            Assert.Equal("Lamp", window.ScenePane.Selected!.Name);
            Assert.True(window.Tools.RenameButton.IsEnabled);
            Assert.Null(window.Outline.SelectedItem);
        });
    }

    [Fact]
    public void A_kept_selection_shows_in_the_tree_again_when_the_filter_clears()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            window.Outline.SelectedItem = FakeScene.Lamp;
            window.ScenePane.FilterBox.Text = "sou";

            window.ScenePane.FilterBox.Text = "";

            Assert.Equal("Lamp", ((OutlineNode)window.Outline.SelectedItem!).Name);
        });
    }

    [Fact]
    public void A_click_in_the_viewport_on_empty_space_clears_a_selection_the_filter_hides()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            window.Outline.SelectedItem = FakeScene.Lamp;
            window.ScenePane.FilterBox.Text = "sou";

            window.ScenePane.Select(0);

            Assert.Null(window.ScenePane.Selected);
            Assert.False(window.Tools.RenameButton.IsEnabled);
        });
    }

    [Fact]
    public void A_tree_that_lacks_the_selected_entity_selects_nothing()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            window.Outline.SelectedItem = FakeScene.Lamp;

            window.ScenePane.Show(new OutlineNode(9, "Other", "Entity3D", []), FakeScene.Lamp.Handle);

            Assert.Null(window.ScenePane.Selected);
        });
    }

    [Fact]
    public void The_pane_raises_a_selection_change_only_when_the_selected_entity_changes()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var raised = 0;
            window.ScenePane.SelectionChanged += () => raised++;

            window.ScenePane.Select(FakeScene.Lamp.Handle);
            window.ScenePane.Select(FakeScene.Lamp.Handle);
            window.ScenePane.FilterBox.Text = "sou";
            Assert.Equal(1, raised);

            window.ScenePane.Select(0);
            Assert.Equal(2, raised);
        });
    }

    [Fact]
    public void A_user_selection_while_a_selection_is_hidden_replaces_it()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            window.Outline.SelectedItem = FakeScene.Lamp;
            window.ScenePane.FilterBox.Text = "sou";

            window.Outline.SelectedItem = ((OutlineNode)window.Outline.ItemsSource!.Cast<object>().Single()).Children.Single();

            Assert.Equal("Sound", window.ScenePane.Selected!.Name);
        });
    }

    [Fact]
    public void A_tool_acts_on_the_real_entity_while_the_filter_shows_a_pruned_copy()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            window.ScenePane.FilterBox.Text = "lam";
            window.Outline.SelectedItem = ((OutlineNode)window.Outline.ItemsSource!.Cast<object>().Single()).Children.Single();

            window.Tools.NameBox.Text = "Bulb";
            window.Tools.RenameButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            window.ScenePane.FilterBox.Text = "";

            Assert.Equal(["Field", "Bulb", "Sound"], Names(window));
        });
    }

    [Fact]
    public void The_selection_is_the_whole_entity_while_the_filter_shows_a_pruned_copy()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            window.ScenePane.FilterBox.Text = "lam";
            window.Outline.SelectedItem = ((OutlineNode)window.Outline.ItemsSource!.Cast<object>().Single());

            Assert.Equal(2, window.ScenePane.Selected!.Children.Count);
        });
    }

    [Fact]
    public void A_click_in_the_viewport_selects_the_picked_entity_also_when_the_filter_hides_it()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            window.ScenePane.FilterBox.Text = "lam";

            window.ScenePane.Select(FakeScene.Sound.Handle);
            Assert.Equal("Sound", window.ScenePane.Selected!.Name);
            Assert.Null(window.Outline.SelectedItem);

            window.ScenePane.Select(FakeScene.Lamp.Handle);
            Assert.Equal("Lamp", window.ScenePane.Selected!.Name);
            Assert.Equal("Lamp", ((OutlineNode)window.Outline.SelectedItem!).Name);
        });
    }

    [Fact]
    public void A_tool_edit_while_filtering_keeps_the_filter()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            window.ScenePane.FilterBox.Text = "lam";
            window.Outline.SelectedItem = ((OutlineNode)window.Outline.ItemsSource!.Cast<object>().Single()).Children.Single();

            window.Tools.DuplicateButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            Assert.Equal("lam", window.ScenePane.FilterBox.Text);
            Assert.Equal("Lamp2", window.ScenePane.Selected!.Name);
        });
    }

    [Fact]
    public void The_tree_menu_lists_the_commands_the_tool_row_has()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            Assert.Equal(
                ["Add Child Entity...", "Duplicate", "Rename...", "Move Up", "Move Down", "Remove"],
                MenuItems(window).Select(item => (string)item.Header!));
        });
    }

    [Fact]
    public void Menu_items_are_on_only_when_their_command_can_run()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            // In menu order: add, duplicate, rename, up, down, remove.
            bool[] On() => MenuItems(window).Select(item => item.IsEnabled).ToArray();

            window.ScenePane.Select(0);
            Assert.Equal([true, false, false, false, false, false], On());
            window.ScenePane.Select(FakeScene.Field.Handle);
            Assert.Equal([true, false, true, false, false, false], On());
            window.ScenePane.Select(FakeScene.Lamp.Handle);
            Assert.Equal([true, true, true, false, true, true], On());
            window.ScenePane.Select(FakeScene.Sound.Handle);
            Assert.Equal([true, true, true, true, false, true], On());
        });
    }

    [Fact]
    public void A_menu_item_runs_its_command_on_the_selected_entity()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            window.ScenePane.Select(FakeScene.Lamp.Handle);

            ClickItem(window, "Move Down");
            Assert.Equal("Moved Lamp down.", window.Status);

            ClickItem(window, "Move Up");
            Assert.Equal("Moved Lamp up.", window.Status);

            ClickItem(window, "Duplicate");
            Assert.Equal("Duplicated Lamp.", window.Status);

            ClickItem(window, "Remove");
            Assert.Equal("Removed Lamp2.", window.Status);
        });
    }

    [Theory]
    [InlineData("Add Child Entity...")]
    [InlineData("Rename...")]
    public void A_menu_item_that_needs_a_name_opens_the_tool_row_on_the_name_box(string header)
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            window.ScenePane.Select(FakeScene.Lamp.Handle);

            ClickItem(window, header);
            Dispatcher.UIThread.RunJobs();

            Assert.True(window.ScenePane.Flyout.IsOpen);
            Assert.True(window.Tools.NameBox.IsFocused);
        });
    }

    [Fact]
    public void A_right_click_on_a_row_selects_it_and_opens_the_menu()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var lamp = Rows(window).Single(row => row.GetVisualDescendants().OfType<TextBlock>().Single().Text == "Lamp");
            var point = lamp.TranslatePoint(new Point(40, 8), window)!.Value;

            window.MouseDown(point, MouseButton.Right);
            window.MouseUp(point, MouseButton.Right);

            Assert.Equal("Lamp", window.ScenePane.Selected!.Name);
            Assert.True(window.ScenePane.Outline.ContextMenu!.IsOpen);
        });
    }
}
