using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using Orblit.Editor.App.Shell;
using Orblit.Editor.App.Theme;
using static Orblit.Editor.App.Tests.ReferenceWindow;

namespace Orblit.Editor.App.Tests;

public sealed class ScenesPanelTests
{
    static TreeViewItem[] Items(EditorWindow window)
    {
        window.UpdateLayout();
        return [.. window.Scenes.Tree.GetVisualDescendants().OfType<TreeViewItem>()];
    }

    static string[] Names(EditorWindow window) =>
        [.. Items(window).Select(item => item.GetVisualDescendants().OfType<TextBlock>().First().Text!)];

    static void PressEnter(EditorWindow window) =>
        window.Scenes.Tree.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });

    [Fact]
    public void The_path_row_holds_back_forward_the_path_and_the_layout_button()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var panel = window.Scenes;

            AssertBox(6, 698, 24, 24, BoxOf(panel.BackButton, window));
            AssertBox(30, 698, 24, 24, BoxOf(panel.ForwardButton, window));
            AssertBox(60, 696, 187, 28, BoxOf(panel.PathBox, window));
            AssertBox(253, 698, 24, 24, BoxOf(panel.LayoutButton, window));
        });
    }

    static Color? NameColour(EditorWindow window, string name) =>
        (Items(window).Select(item => item.GetVisualDescendants().OfType<TextBlock>().First()).Single(text => text.Text == name).Foreground as ISolidColorBrush)?.Color;

    [Fact]
    public void The_open_scene_is_named_in_the_accent_colour_and_the_others_are_not()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize(new FakeScene { SceneFile = "res://field.oscn" });

            Assert.Equal(EditorColors.Accent, NameColour(window, "field.oscn"));
            Assert.NotEqual(EditorColors.Accent, NameColour(window, "res://"));
        });
    }

    [Fact]
    public void Opening_another_scene_moves_the_accent_colour_to_it()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize(new FakeScene { SceneFile = "res://field.oscn" });
            Items(window).Single(item => item.DataContext is FileNode { Name: "levels" }).IsExpanded = true;

            OpenScene(window, "res://levels/second.oscn");

            Assert.Equal(EditorColors.Accent, NameColour(window, "second.oscn"));
            Assert.NotEqual(EditorColors.Accent, NameColour(window, "field.oscn"));
        });
    }

    [Fact]
    public void The_path_box_shows_the_project_folder_and_takes_no_typing()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            Assert.Equal("res://", window.Scenes.PathBox.Text);
            Assert.True(window.Scenes.PathBox.IsReadOnly);
        });
    }

    [Fact]
    public void Back_and_forward_are_off_because_nothing_keeps_a_history()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            Assert.False(window.Scenes.BackButton.IsEnabled);
            Assert.False(window.Scenes.ForwardButton.IsEnabled);
        });
    }

    [Fact]
    public void The_filter_row_holds_the_filter_box_and_the_sort_button()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var panel = window.Scenes;

            AssertBox(8, 729, 239, 28, BoxOf(panel.FilterBox, window));
            AssertBox(253, 731, 24, 24, BoxOf(panel.SortButton, window));
            Assert.Equal("Filter Files", panel.FilterBox.PlaceholderText);
        });
    }

    [Fact]
    public void The_layout_button_swaps_the_tree_for_a_flat_list_and_back()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var layout = window.Scenes.LayoutButton;
            Assert.Equal("Show the files as a list", ToolTip.GetTip(layout));

            ClickOn(layout, window);

            Assert.Equal(["Favorites", "res://", "field.oscn", "levels/second.oscn"], Names(window));
            Assert.Equal("Show the files as a tree", ToolTip.GetTip(layout));

            ClickOn(layout, window);

            Assert.Equal(["Favorites", "res://", "levels", "field.oscn"], Names(window));
            Assert.Equal("Show the files as a list", ToolTip.GetTip(layout));
        });
    }

    [Fact]
    public void The_sort_button_puts_the_newest_file_first_and_back_to_names()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene { Times = { ["res://field.oscn"] = new DateTime(2026, 11, 1), ["res://levels/second.oscn"] = new DateTime(2026, 11, 5) } };
            var window = OpenAtReferenceSize(scene);
            ClickOn(window.Scenes.LayoutButton, window);
            var sort = window.Scenes.SortButton;
            Assert.Equal("Sort the files newest first", ToolTip.GetTip(sort));

            ClickOn(sort, window);

            Assert.Equal(["Favorites", "res://", "levels/second.oscn", "field.oscn"], Names(window));
            Assert.Equal("Sort the files by name", ToolTip.GetTip(sort));

            ClickOn(sort, window);

            Assert.Equal(["Favorites", "res://", "field.oscn", "levels/second.oscn"], Names(window));
        });
    }

    [Fact]
    public void The_sort_button_orders_the_files_inside_the_tree()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene { Times = { ["res://alpha.oscn"] = new DateTime(2026, 11, 2), ["res://field.oscn"] = new DateTime(2026, 11, 6) } };
            scene.ScenePaths.Insert(0, "res://alpha.oscn");
            var window = OpenAtReferenceSize(scene);
            Assert.Equal(["Favorites", "res://", "levels", "alpha.oscn", "field.oscn"], Names(window));

            ClickOn(window.Scenes.SortButton, window);

            Assert.Equal(["Favorites", "res://", "levels", "field.oscn", "alpha.oscn"], Names(window));
        });
    }

    [Fact]
    public void The_filter_keeps_the_newest_first_order()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene { Times = { ["res://field.oscn"] = new DateTime(2026, 11, 1), ["res://levels/second.oscn"] = new DateTime(2026, 11, 5) } };
            var window = OpenAtReferenceSize(scene);
            ClickOn(window.Scenes.SortButton, window);

            window.Scenes.FilterBox.Text = "oscn";

            Assert.Equal(["Favorites", "res://", "levels/second.oscn", "field.oscn"], Names(window));
        });
    }

    [Fact]
    public void The_rows_start_14_pixels_under_the_filter_and_are_23_high()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var items = Items(window);

            Assert.Equal(["Favorites", "res://", "levels", "field.oscn"], Names(window));
            Assert.Equal(771, BoxOf(items[0], window).Y);
            Assert.Equal(794, BoxOf(items[1], window).Y);
            Assert.Equal(817, BoxOf(items[2], window).Y);
            Assert.Equal(840, BoxOf(items[3], window).Y);
        });
    }

    [Fact]
    public void Only_the_project_folder_starts_open()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var items = Items(window);

            Assert.False(items[0].IsExpanded);
            Assert.True(items[1].IsExpanded);
            Assert.False(items[2].IsExpanded);
            Assert.DoesNotContain("second.oscn", Names(window));
        });
    }

    [Fact]
    public void A_row_puts_the_icon_and_the_name_where_the_reference_does()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var items = Items(window);

            Assert.Equal(26, BoxOf(RowIconOf(items[1]), window).X);
            Assert.Equal(49, BoxOf(items[1].GetVisualDescendants().OfType<TextBlock>().First(), window).X);
            Assert.Equal(61, BoxOf(items[2].GetVisualDescendants().OfType<TextBlock>().First(), window).X);
        });
    }

    [Fact]
    public void Each_kind_of_row_has_its_own_icon()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            window.Scenes.Tree.ExpandSubTree(Items(window)[1]);

            Assert.Equal(["Favorites", "Folder", "Folder", "Prefab", "Prefab"], Items(window).Select(item => RowIconOf(item).IconName));
        });
    }

    [Fact]
    public void The_selected_row_is_a_block_from_before_the_icon_to_near_the_dock_edge()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var items = Items(window);
            var line = items[1].GetVisualDescendants().OfType<Border>().First(border => border.Name == "PART_LayoutRoot");
            var highlight = items[1].GetVisualDescendants().OfType<RowHighlight>().First();

            var area = highlight.Area();
            var corner = highlight.TranslatePoint(area.TopLeft, window)!.Value;

            Assert.Equal(new Point(22, BoxOf(line, window).Y), corner);
            Assert.Equal(new Size(256, 23), area.Size);
            Assert.Equal(EditorColors.SelectedRow, ((ISolidColorBrush)highlight.Fill!).Color);
            Assert.Null(items[0].GetVisualDescendants().OfType<RowHighlight>().First().Fill);
        });
    }

    [Fact]
    public void A_row_under_the_pointer_takes_the_hover_colour()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var items = Items(window);
            var line = items[0].GetVisualDescendants().OfType<Border>().First(border => border.Name == "PART_LayoutRoot");
            var highlight = items[0].GetVisualDescendants().OfType<RowHighlight>().First();

            window.MouseMove(BoxOf(line, window).Center);

            Assert.Equal(EditorColors.Hover, ((ISolidColorBrush)highlight.Fill!).Color);
        });
    }

    [Fact]
    public void The_row_icons_are_pale_and_the_folders_are_blue()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            window.Scenes.Tree.ExpandSubTree(Items(window)[1]);

            var colors = Items(window).Select(item => ((ISolidColorBrush)TextElement.GetForeground(RowIconOf(item))!).Color).ToArray();

            Assert.Equal([EditorColors.TreeIcon, EditorColors.FolderIcon, EditorColors.FolderIcon, EditorColors.TreeIcon, EditorColors.TreeIcon], colors);
        });
    }

    [Fact]
    public void The_buttons_use_the_reference_icons()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var panel = window.Scenes;

            Assert.Equal(
                ["Back", "Forward", "Panels1", "Sort", "Search"],
                new Control[] { panel.BackButton, panel.ForwardButton, panel.LayoutButton, panel.SortButton, panel.FilterBox }
                    .Select(control => control.GetVisualDescendants().OfType<EditorIcon>().Single().IconName));
        });
    }

    [Fact]
    public void The_project_folder_is_selected_at_the_start()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var items = Items(window);

            Assert.True(items[1].IsSelected);
        });
    }

    [Fact]
    public void Enter_on_a_scene_raises_it_to_open()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var chosen = new List<string>();
            window.Scenes.Chosen += chosen.Add;
            window.Scenes.Tree.SelectedItem = FileNodes(window).Single(node => node.Name == "field.oscn");

            PressEnter(window);

            Assert.Equal(["res://field.oscn"], chosen);
        });
    }

    [Fact]
    public void Enter_on_a_folder_opens_nothing()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var chosen = new List<string>();
            window.Scenes.Chosen += chosen.Add;

            PressEnter(window);

            Assert.Empty(chosen);
        });
    }

    [Fact]
    public void Another_key_opens_nothing()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var chosen = new List<string>();
            window.Scenes.Chosen += chosen.Add;
            window.Scenes.Tree.SelectedItem = FileNodes(window).Single(node => node.Name == "field.oscn");

            window.Scenes.Tree.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Space });

            Assert.Empty(chosen);
        });
    }

    // Raised on the control that was pressed, so it bubbles up to the tree as a real press does.
    static void DoubleTap(Interactive source) =>
        source.RaiseEvent(new TappedEventArgs(InputElement.DoubleTappedEvent, null!));

    static TreeViewItem RowOf(EditorWindow window, Func<FileNode, bool> which) =>
        Items(window).Single(item => item.DataContext is FileNode node && which(node));

    [Fact]
    public void A_double_press_on_a_scene_row_raises_it_to_open()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var chosen = new List<string>();
            window.Scenes.Chosen += chosen.Add;
            window.Scenes.Tree.SelectedItem = FileNodes(window).Single(node => node.Name == "field.oscn");

            DoubleTap(RowOf(window, node => node.Name == "field.oscn").GetVisualDescendants().OfType<TextBlock>().First());

            Assert.Equal(["res://field.oscn"], chosen);
        });
    }

    [Fact]
    public void A_double_press_off_the_selected_scene_opens_nothing()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var chosen = new List<string>();
            window.Scenes.Chosen += chosen.Add;
            window.Scenes.Tree.SelectedItem = FileNodes(window).Single(node => node.Name == "field.oscn");

            DoubleTap(window.Scenes.Tree);
            DoubleTap(RowOf(window, node => node.Kind == FileKind.Folder && node.Path == FileTree.Root));

            Assert.Empty(chosen);
        });
    }

    [Fact]
    public void The_filter_lists_the_scenes_whose_path_matches_and_selects_the_project_folder()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            window.Scenes.Tree.SelectedItem = FileNodes(window).Single(node => node.Name == "field.oscn");

            window.Scenes.FilterBox.Text = "SECOND";

            Assert.Equal(["Favorites", "res://", "levels/second.oscn"], Names(window));
            Assert.Equal("res://", ((FileNode)window.Scenes.Tree.SelectedItem!).Path);
        });
    }

    [Fact]
    public void Clearing_the_filter_brings_the_folders_back()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            window.Scenes.FilterBox.Text = "second";

            window.Scenes.FilterBox.Text = " ";

            Assert.Equal(["Favorites", "res://", "levels", "field.oscn"], Names(window));
        });
    }

    [Fact]
    public void A_folder_the_user_opened_is_still_open_when_the_filter_clears()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            Items(window).Single(item => item.DataContext is FileNode { Name: "levels" }).IsExpanded = true;
            window.Scenes.FilterBox.Text = "field";

            window.Scenes.FilterBox.Text = "";

            Assert.Equal(["Favorites", "res://", "levels", "second.oscn", "field.oscn"], Names(window));
        });
    }

    [Fact]
    public void A_folder_the_user_closed_stays_closed_when_the_filter_clears()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var levels = Items(window).Single(item => item.DataContext is FileNode { Name: "levels" });
            levels.IsExpanded = true;
            levels.IsExpanded = false;
            window.Scenes.FilterBox.Text = "field";

            window.Scenes.FilterBox.Text = "";

            Assert.Equal(["Favorites", "res://", "levels", "field.oscn"], Names(window));
        });
    }

    [Fact]
    public void The_project_folder_stays_open_for_a_filter_after_the_user_closed_it()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            Items(window).Single(item => item.DataContext is FileNode { Path: FileTree.Root }).IsExpanded = false;

            window.Scenes.FilterBox.Text = "second";

            Assert.Equal(["Favorites", "res://", "levels/second.oscn"], Names(window));
        });
    }

    [Fact]
    public void A_scene_found_by_the_filter_opens_with_its_full_path()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            window.Scenes.FilterBox.Text = "second";

            OpenScene(window, "res://levels/second.oscn");

            Assert.Equal("Opened second.", window.Status);
        });
    }
}
