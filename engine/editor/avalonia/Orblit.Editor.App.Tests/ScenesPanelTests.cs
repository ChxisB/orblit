using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using Orblit.Editor.App.Graph;
using Orblit.Editor.App.Shell;
using Orblit.Editor.App.Tests.Graph;
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

    static FakeScene WithGraph()
    {
        var scene = new FakeScene();
        scene.Graphs["res://grow.ograph"] = FakeGraphSource.Sample();
        return scene;
    }

    static void PressEnter(EditorWindow window) =>
        window.Scenes.Tree.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });

    [Fact]
    public void The_path_row_holds_the_path_and_the_tree_or_list_switch()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var panel = window.Scenes;

            AssertBox(17, 707, 164, 28, BoxOf(panel.PathBox, window));
            AssertBox(189, 705, 90, 30, BoxOf(panel.LayoutChoice, window));
            Assert.Equal(["Tree", "List"], panel.LayoutChoice.Choices.Select(choice => choice.Content as string));
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
    public void The_filter_row_holds_the_find_box_and_the_worded_sort_menu()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var panel = window.Scenes;

            AssertBox(17, 739, 183, 28, BoxOf(panel.FilterBox, window));
            AssertBox(208, 740, 71, 26, BoxOf(panel.SortButton, window));
            Assert.Equal("Find a file", panel.FilterBox.PlaceholderText);
            Assert.Equal(["Name"], Texts(panel.SortButton));
            Assert.Equal("Picks the order of the files in each folder. Folders stay on top.", ((HoverCard)ToolTip.GetTip(panel.SortButton)!).Line);
        });
    }

    [Fact]
    public void List_swaps_the_tree_for_a_flat_list_and_Tree_brings_it_back()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var choices = window.Scenes.LayoutChoice.Choices;

            ClickOn(choices[1], window);

            Assert.Equal(["enemies/slime.cs", "field.oscn", "levels/second.oscn", "spin.cs"], Names(window));

            ClickOn(choices[0], window);

            Assert.Equal(["res://", "enemies", "levels", "field.oscn", "spin.cs"], Names(window));
        });
    }

    static void SortBy(EditorWindow window, string item) =>
        window.Scenes.SortItems.Single(sort => AutomationProperties.GetName(sort) == item).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

    [Fact]
    public void The_sort_menu_names_each_order_and_checks_the_one_in_use()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var items = window.Scenes.SortItems;
            Assert.Equal(["A to Z", "The last file you changed goes on top", "Scenes, then scripts, then the rest"], items.Select(Note));

            SortBy(window, "Newest first");

            Assert.Equal([false, true, false], items.Select(item => item.IsChecked));
            Assert.Equal(["Newest"], Texts(window.Scenes.SortButton));
        });
    }

    [Fact]
    public void Newest_first_puts_the_newest_file_first_and_Name_puts_names_back()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene { Times = { ["res://field.oscn"] = new DateTime(2026, 11, 1), ["res://levels/second.oscn"] = new DateTime(2026, 11, 5) } };
            var window = OpenAtReferenceSize(scene);
            ClickOn(window.Scenes.LayoutChoice.Choices[1], window);

            SortBy(window, "Newest first");

            Assert.Equal(["levels/second.oscn", "field.oscn", "enemies/slime.cs", "spin.cs"], Names(window));

            SortBy(window, "Name");

            Assert.Equal(["enemies/slime.cs", "field.oscn", "levels/second.oscn", "spin.cs"], Names(window));
        });
    }

    [Fact]
    public void Newest_first_orders_the_files_inside_the_tree()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene { Times = { ["res://alpha.oscn"] = new DateTime(2026, 11, 2), ["res://field.oscn"] = new DateTime(2026, 11, 6) } };
            scene.ScenePaths.Insert(0, "res://alpha.oscn");
            var window = OpenAtReferenceSize(scene);
            Assert.Equal(["res://", "enemies", "levels", "alpha.oscn", "field.oscn", "spin.cs"], Names(window));

            SortBy(window, "Newest first");

            Assert.Equal(["res://", "enemies", "levels", "field.oscn", "alpha.oscn", "spin.cs"], Names(window));
        });
    }

    [Fact]
    public void Kind_puts_scenes_before_scripts_and_imported_files_last()
    {
        UiThread.Run(() =>
        {
            var scene = WithGraph();
            scene.ImportText["res://alpha.png"] = "";
            var window = OpenAtReferenceSize(scene);

            SortBy(window, "Kind");

            Assert.Equal(["res://", "enemies", "levels", "field.oscn", "grow.ograph", "spin.cs", "alpha.png"], Names(window));
        });
    }

    [Fact]
    public void The_filter_keeps_the_newest_first_order()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene { Times = { ["res://field.oscn"] = new DateTime(2026, 11, 1), ["res://levels/second.oscn"] = new DateTime(2026, 11, 5) } };
            var window = OpenAtReferenceSize(scene);
            SortBy(window, "Newest first");

            window.Scenes.FilterBox.Text = "oscn";

            Assert.Equal(["levels/second.oscn", "field.oscn"], Names(window));
        });
    }

    [Fact]
    public void A_file_row_names_its_kind_beside_its_name()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene { Kinds = { ["res://field.oscn"] = "3D" } };
            scene.ImportText["res://alpha.png"] = "";
            var window = OpenAtReferenceSize(scene);

            Assert.Equal(["field.oscn", "3D scene"], RowTexts(window, "field.oscn"));
            Assert.Equal(["spin.cs", "C# script"], RowTexts(window, "spin.cs"));
            Assert.Equal(["alpha.png", "Picture"], RowTexts(window, "alpha.png"));
        });
    }

    static IEnumerable<string?> RowTexts(EditorWindow window, string name) =>
        RowOf(window, node => node.Name == name).GetVisualDescendants().OfType<TextBlock>().Select(text => text.Text);

    [Fact]
    public void A_filter_with_no_match_says_so_and_offers_to_clear_it()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var panel = window.Scenes;

            panel.FilterBox.Text = "zzz";
            window.UpdateLayout();

            Assert.True(panel.NoMatch.IsVisible);
            Assert.Equal(["No file matches \"zzz\"", "Search looks at file names in every folder.", "Clear the search", "esc"], Texts(panel.NoMatch));

            ClickOn(panel.ClearSearchButton, window);

            Assert.Equal("", panel.FilterBox.Text);
            Assert.False(panel.NoMatch.IsVisible);
            Assert.Equal(["res://", "enemies", "levels", "field.oscn", "spin.cs"], Names(window));
        });
    }

    [Fact]
    public void Escape_in_the_filter_empties_it()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            window.Scenes.FilterBox.Text = "second";
            var escape = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape };

            window.Scenes.FilterBox.RaiseEvent(escape);

            Assert.Equal("", window.Scenes.FilterBox.Text);
            Assert.True(escape.Handled);
        });
    }

    [Fact]
    public void The_rows_start_12_pixels_under_the_filter_and_are_26_high()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var items = Items(window);

            Assert.Equal(["res://", "enemies", "levels", "field.oscn", "spin.cs"], Names(window));
            Assert.Equal([779.0, 805.0, 831.0, 857.0], items.Take(4).Select(item => BoxOf(item, window).Y));
        });
    }

    [Fact]
    public void Only_the_project_folder_starts_open()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var items = Items(window);

            Assert.True(items[0].IsExpanded);
            Assert.False(items[1].IsExpanded);
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

            Assert.Equal(31, BoxOf(RowIconOf(items[0]), window).X);
            Assert.Equal(51, BoxOf(items[0].GetVisualDescendants().OfType<TextBlock>().First(), window).X);
            Assert.Equal(67, BoxOf(items[1].GetVisualDescendants().OfType<TextBlock>().First(), window).X);
        });
    }

    [Fact]
    public void Each_kind_of_row_has_its_own_icon()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize(WithGraph());
            window.Scenes.Tree.ExpandSubTree(Items(window)[0]);

            Assert.Equal(["Folder", "Folder", "CodeFile", "Folder", "SceneFile", "SceneFile", "GraphFile", "CodeFile"], Items(window).Select(item => RowIconOf(item).IconName));
        });
    }

    [Fact]
    public void The_selected_row_is_a_soft_accent_block_4_pixels_in_from_each_side()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var items = Items(window);
            var line = items[0].GetVisualDescendants().OfType<Border>().First(border => border.Name == "PART_LayoutRoot");
            var highlight = items[0].GetVisualDescendants().OfType<RowHighlight>().First();

            var area = highlight.Area();
            var corner = highlight.TranslatePoint(area.TopLeft, window)!.Value;

            Assert.Equal(new Point(BoxOf(line, window).X + 4, BoxOf(line, window).Y), corner);
            Assert.Equal(new Size(BoxOf(line, window).Width - 8, 26), area.Size);
            Assert.Equal(EditorColors.AccentSoft, ((ISolidColorBrush)highlight.Fill!).Color);
            Assert.Null(items[1].GetVisualDescendants().OfType<RowHighlight>().First().Fill);
        });
    }

    [Fact]
    public void A_row_under_the_pointer_takes_the_hover_colour()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var items = Items(window);
            var line = items[1].GetVisualDescendants().OfType<Border>().First(border => border.Name == "PART_LayoutRoot");
            var highlight = items[1].GetVisualDescendants().OfType<RowHighlight>().First();

            window.MouseMove(BoxOf(line, window).Center);

            Assert.Equal(EditorColors.Hover, ((ISolidColorBrush)highlight.Fill!).Color);
        });
    }

    [Fact]
    public void Each_kind_of_row_tints_its_icon()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize(WithGraph());
            window.Scenes.Tree.ExpandSubTree(Items(window)[0]);

            var colors = Items(window).Select(item => ((ISolidColorBrush)TextElement.GetForeground(RowIconOf(item))!).Color).ToArray();

            Assert.Equal([EditorColors.FolderIcon, EditorColors.FolderIcon, EditorColors.ScriptIcon, EditorColors.FolderIcon, EditorColors.SceneIcon, EditorColors.SceneIcon, EditorColors.GraphIcon, EditorColors.ScriptIcon], colors);
        });
    }

    [Fact]
    public void The_find_box_shows_a_search_mark_and_the_sort_menu_an_arrow()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var panel = window.Scenes;

            Assert.Equal(
                ["Search", "GuiDropdown"],
                new Control[] { panel.FilterBox, panel.SortButton }.Select(control => control.GetVisualDescendants().OfType<EditorIcon>().Single().IconName));
        });
    }

    [Fact]
    public void The_project_folder_is_selected_at_the_start()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var items = Items(window);

            Assert.True(items[0].IsSelected);
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
    public void The_filter_lists_the_files_whose_path_matches_and_selects_the_first()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            window.Scenes.Tree.SelectedItem = FileNodes(window).Single(node => node.Name == "field.oscn");

            window.Scenes.FilterBox.Text = "SECOND";

            Assert.Equal(["levels/second.oscn"], Names(window));
            Assert.Equal("res://levels/second.oscn", ((FileNode)window.Scenes.Tree.SelectedItem!).Path);
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

            Assert.Equal(["res://", "enemies", "levels", "field.oscn", "spin.cs"], Names(window));
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

            Assert.Equal(["res://", "enemies", "levels", "second.oscn", "field.oscn", "spin.cs"], Names(window));
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

            Assert.Equal(["res://", "enemies", "levels", "field.oscn", "spin.cs"], Names(window));
        });
    }

    [Fact]
    public void A_filter_lists_its_matches_after_the_user_closed_the_project_folder()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            Items(window).Single(item => item.DataContext is FileNode { Path: FileTree.Root }).IsExpanded = false;

            window.Scenes.FilterBox.Text = "second";

            Assert.Equal(["levels/second.oscn"], Names(window));
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

    static IEnumerable<Control> MenuItems(ScenesPanel panel) => (IEnumerable<Control>)panel.Tree.ContextMenu!.ItemsSource!;

    static MenuItem Item(ScenesPanel panel, string name) =>
        MenuItems(panel).OfType<MenuItem>().Single(item => AutomationProperties.GetName(item) == name);

    static void Press(EditorWindow window, string name) => Item(window.Scenes, name).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

    static string? Note(MenuItem item) => item.Header is StackPanel { Children: [_, TextBlock note] } ? note.Text : null;

    // A separator and a heading have no name.
    static IEnumerable<string?> MenuNames(EditorWindow window) =>
        MenuItems(window.Scenes).Select(item => item is MenuItem menu ? AutomationProperties.GetName(menu) : null);

    static void Pick(EditorWindow window, string path)
    {
        Items(window).Single(item => item.DataContext is FileNode { Name: "levels" }).IsExpanded = true;
        window.Scenes.Tree.SelectedItem = FileNodes(window).Single(node => node.Path == path);
    }

    [Fact]
    public void A_folder_menu_makes_new_things_in_it_and_copies_its_path()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            Assert.Equal([null, "New scene…", "New graph…", null, "Copy path"], MenuNames(window));
            Assert.Equal("A new scene in this folder", Note(Item(window.Scenes, "New scene…")));
            Assert.Equal("Copies res://", Note(Item(window.Scenes, "Copy path")));
        });
    }

    [Fact]
    public void A_script_menu_opens_it_and_copies_its_path()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            Pick(window, "res://spin.cs");

            Assert.Equal(["Open", null, "Copy path"], MenuNames(window));
            Assert.Equal("Opens spin.cs", Note(Item(window.Scenes, "Open")));
        });
    }

    [Fact]
    public void A_scene_menu_also_plays_from_it_and_uses_it_inside()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            Pick(window, "res://levels/second.oscn");

            Assert.Equal(["Open", "Play from this scene", "Use inside the open scene", null, "Copy path"], MenuNames(window));
            Assert.Equal("Starts the game here", Note(Item(window.Scenes, "Play from this scene")));
            Assert.Equal("Same as Add a scene inside", Note(Item(window.Scenes, "Use inside the open scene")));
        });
    }

    [Fact]
    public void The_open_scene_cannot_go_inside_itself_and_its_item_says_why()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize(new FakeScene { SceneFile = "res://field.oscn" });

            Pick(window, "res://field.oscn");
            var use = Item(window.Scenes, "Use inside the open scene");

            Assert.False(use.IsEnabled);
            Assert.Equal("You are in field.oscn. A scene can't hold itself.", Note(use));
        });
    }

    [Fact]
    public void Use_inside_puts_a_copy_of_the_scene_in_the_open_one()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene { SceneFile = "res://field.oscn" };
            var window = OpenAtReferenceSize(scene);
            Pick(window, "res://levels/second.oscn");

            Press(window, "Use inside the open scene");

            Assert.Equal([(FakeScene.Field.Handle, "res://levels/second.oscn")], scene.Instanced);
        });
    }

    [Fact]
    public void Play_from_this_scene_starts_the_game_in_that_scene()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene { SceneFile = "res://field.oscn" };
            var window = OpenAtReferenceSize(scene);
            Pick(window, "res://levels/second.oscn");

            Press(window, "Play from this scene");

            Assert.Equal(["--scene", "res://levels/second.oscn"], scene.PlayedFlags.TakeLast(2));
        });
    }

    [Fact]
    public void New_scene_from_a_folder_saves_the_scene_in_that_folder()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            Pick(window, "res://levels/");

            Press(window, "New scene…");
            window.NewScene.NameBox.Text = "third";

            Assert.True(window.NewScene.Dialog.IsVisible);
            Assert.Equal("Saved as res://levels/third.oscn", window.NewScene.SavedLine.Text);
        });
    }

    sealed class MemoryClipboard : ITextClipboard
    {
        public string? Text { get; private set; }

        public Task<string?> GetText() => Task.FromResult(Text);

        public Task SetText(string text)
        {
            Text = text;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public void Copy_path_puts_the_res_path_of_the_selected_row_on_the_clipboard()
    {
        UiThread.Run(() =>
        {
            var clipboard = new MemoryClipboard();
            var panel = new ScenesPanel(KeyModifiers.Meta, clipboard);
            new Window { Content = panel }.Show();
            panel.Show(["res://levels/second.oscn"], new Dictionary<string, DateTime>(), _ => "");

            Item(panel, "Copy path").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Assert.Equal("res://", clipboard.Text);

            panel.FilterBox.Text = "second";
            panel.Tree.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.C, KeyModifiers = KeyModifiers.Meta | KeyModifiers.Alt });

            Assert.Equal("res://levels/second.oscn", clipboard.Text);
        });
    }

    [Fact]
    public void New_graph_opens_the_graph_workspace_on_its_create_form()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            Press(window, "New graph…");

            Assert.Equal(["Graph"], window.Bar.Tabs.Where(tab => tab.IsChecked == true).Select(tab => tab.Label));
            Assert.True(window.Graph.CreateDialog.IsVisible);
        });
    }

    [Fact]
    public void A_script_opened_from_the_files_shows_in_the_script_workspace()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize(new FakeScene { ScriptText = { ["res://spin.cs"] = "class Spin {}" } });
            window.Scenes.Tree.SelectedItem = FileNodes(window).Single(node => node.Name == "spin.cs");

            PressEnter(window);

            Assert.Equal(["Script"], window.Bar.Tabs.Where(tab => tab.IsChecked == true).Select(tab => tab.Label));
            Assert.Equal("res://spin.cs", window.Script.Heading.Text);
        });
    }

    [Fact]
    public void A_graph_opened_from_the_files_shows_in_the_graph_workspace()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize(WithGraph());
            window.Scenes.Tree.SelectedItem = FileNodes(window).Single(node => node.Name == "grow.ograph");

            PressEnter(window);

            Assert.Equal(["Graph"], window.Bar.Tabs.Where(tab => tab.IsChecked == true).Select(tab => tab.Label));
            Assert.Equal("res://grow.ograph", window.Graph.Current);
        });
    }
}
