using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
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
        MenuItems(window).Single(item => HeaderOf(item) == header).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

    static string[] Names(EditorWindow window) => [.. Rows(window).Select(NameOf)];

    // A name with the filter's match marked is held as runs, not as text.
    static string NameOf(OutlineRow row)
    {
        var name = row.GetVisualDescendants().OfType<TextBlock>().First();
        return name.Inlines is { Count: > 0 } runs ? string.Concat(runs.OfType<Run>().Select(run => run.Text)) : name.Text!;
    }

    static void Press(Control target, Key key, KeyModifiers modifiers = KeyModifiers.None) =>
        target.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key, KeyModifiers = modifiers });

    static MenuItem[] OptionItems(EditorWindow window) =>
        [.. ((MenuFlyout)window.ScenePane.MenuButton.Flyout!).Items.OfType<MenuItem>()];

    static string HeaderOf(MenuItem item) => AutomationProperties.GetName(item) ?? (string)item.Header!;

    [Fact]
    public void Each_row_has_a_highlight_4_pixels_in_from_each_side_of_its_line_at_every_depth()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            foreach (var row in Rows(window))
            {
                var highlight = row.GetVisualDescendants().OfType<RowHighlight>().Single();
                var line = BoxOf(row.GetVisualAncestors().OfType<TreeViewItem>().First().GetVisualDescendants().OfType<Border>().First(border => border.Name == "PART_LayoutRoot"), window);
                var area = highlight.Area();

                Assert.Equal(new Point(line.X + 4, line.Y), highlight.TranslatePoint(area.TopLeft, window));
                Assert.Equal(new Size(line.Width - 8, 26), area.Size);
            }
        });
    }

    [Fact]
    public void A_click_in_the_middle_of_a_row_selects_its_entity()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var row = Rows(window).Last();
            var name = row.GetVisualDescendants().OfType<TextBlock>().Single().Text;
            var middle = row.TranslatePoint(new Point(row.Bounds.Width / 2, row.Bounds.Height / 2), window)!.Value;

            window.MouseDown(middle, MouseButton.Left);
            window.MouseUp(middle, MouseButton.Left);

            Assert.Equal(name, (window.Outline.SelectedItem as OutlineNode)?.Name);
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

            AssertBox(83, 90, 168, 28, BoxOf(pane.FilterBox, window));
            Assert.Equal(17, BoxOf(window.Tools.AddButton, window).X);
            Assert.Equal(49, BoxOf(window.InstanceScene.OpenButton, window).X);
            Assert.Equal(253, BoxOf(pane.MenuButton, window).X);
            Assert.Equal("Filter entities", pane.FilterBox.PlaceholderText);
        });
    }

    [Fact]
    public void The_tree_starts_under_the_button_row_with_rows_26_high()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            window.UpdateLayout();
            var items = window.Outline.GetVisualDescendants().OfType<TreeViewItem>().ToArray();

            Assert.Equal(3, items.Length);
            Assert.Equal(130, BoxOf(items[0], window).Y);
            Assert.Equal(26, BoxOf(items[1], window).Y - BoxOf(items[0], window).Y);
            Assert.Equal(26, BoxOf(items[2], window).Y - BoxOf(items[1], window).Y);
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

            Assert.Equal(15, BoxOf(arrow, window).X);
            Assert.Equal(31, BoxOf(RowIconOf(rows[0]), window).X);
            Assert.Equal(51, BoxOf(rows[0].GetVisualDescendants().OfType<TextBlock>().First(), window).X);
            Assert.Equal(47, BoxOf(RowIconOf(rows[1]), window).X);
            Assert.Equal(67, BoxOf(rows[1].GetVisualDescendants().OfType<TextBlock>().First(), window).X);
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
            AssertBox(15, BoxOf(arrow, window).Y, 12, 12, BoxOf(icons[1], window));
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
    public void The_eye_ends_seven_pixels_inside_the_edge_of_the_dock()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var eye = Rows(window)[1].GetVisualDescendants().OfType<Button>().Single();

            Assert.Equal(287 - 7, BoxOf(eye.GetVisualDescendants().OfType<EditorIcon>().Single(), window).Right);
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
    public void The_add_button_opens_add_a_thing_with_the_most_used_kinds_by_their_plain_names()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            window.ScenePane.Select(FakeScene.Lamp.Handle);

            Click(window.Tools.AddButton);

            var tools = window.Tools;
            Assert.True(tools.AddDialog.IsVisible);
            Assert.Equal("Add a thing", tools.AddDialog.Title);
            Assert.Equal("Add Child Entity", tools.AddDialog.EngineName);
            Assert.Equal("Pick what to add. It goes inside the thing you had picked.", tools.AddDialog.Why.Text);
            Assert.Equal("Lamp", ((OutlineNode)tools.Inside.SelectedItem!).Name);
            Assert.True(tools.Inside.IsEnabled);
            Assert.Equal(["Empty 3D point", "Shape you can see", "Camera", "Sun light", "Marker", "Empty 2D point", "Filled box"], tools.ClassList.Shown.Select(choice => choice.Name));
            Assert.Equal(EntityTools.MostUsed, tools.ClassList.Shown.Select(EntityTools.ClassOf));
            Assert.Equal("Light from far away, like the sun.", tools.ClassList.Shown[3].Line);
            Assert.Single(Texts(tools.ClassList), "MOST USED");
            Assert.Equal(["All kinds, A to Z", "5 more, for experts"], Texts(tools.ClassList.FoldRows.Single()));
            Assert.Equal(0, tools.ClassList.Lit);
            Assert.True(tools.ClassList.SearchBox.IsFocused);
        });
    }

    [Fact]
    public void The_add_button_names_the_lit_thing_and_the_side_tells_what_it_is_good_for()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            Click(window.Tools.AddButton);
            var tools = window.Tools;

            Press(tools.ClassList.SearchBox, Key.Down);
            Press(tools.ClassList.SearchBox, Key.Down);

            Assert.Equal("Add Camera", Texts(tools.AddDialog.MainButton).First());
            Assert.Equal("Camera", tools.Preview.Title.Text);
            Assert.Equal("Shows the world from where it sits. The game draws what the current camera sees.", tools.Preview.About.Text);
            Assert.Equal("Good for: the player's view, a cutscene, a security screen.", tools.Preview.GoodFor.Text);
            Assert.Contains("Camera3D", Texts(tools.Preview));
        });
    }

    [Fact]
    public void All_kinds_unfolds_a_to_z_and_a_class_with_no_plain_name_goes_by_its_class_name()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            Click(window.Tools.AddButton);
            var list = window.Tools.ClassList;

            Click(list.FoldRows.Single());

            Assert.Equal(["Empty thing", "Lamp", "Skeleton3D", "Spotlight", "Timer"], list.Shown.Skip(EntityTools.MostUsed.Length).Select(choice => choice.Name));
            Assert.Equal("Empty thing", list.Picked!.Name);
            Assert.Empty(list.FoldRows);
            var skeleton = list.Shown.Single(choice => choice.Name == "Skeleton3D");
            Assert.Null(skeleton.Engine);
            Assert.Equal("", skeleton.Line);

            Click(list.RowButtons.ElementAt(list.Shown.ToList().IndexOf(skeleton)));

            Assert.Equal("Add Skeleton3D", Texts(window.Tools.AddDialog.MainButton).First());
            Assert.Equal(KindPreview.NoRow, window.Tools.Preview.About.Text);
            Assert.False(window.Tools.Preview.GoodFor.IsVisible);
        });
    }

    [Fact]
    public void A_search_for_light_finds_sun_light_lamp_and_spotlight_under_matches()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            Click(window.Tools.AddButton);
            var list = window.Tools.ClassList;

            list.SearchBox.Text = "light";

            Assert.Equal(["Sun light", "Lamp", "Spotlight"], list.Shown.Select(choice => choice.Name));
            Assert.Contains("MATCHES", Texts(list));
            Assert.DoesNotContain("MOST USED", Texts(list));
            Press(list.SearchBox, Key.Down);
            Press(list.SearchBox, Key.Enter);
            Assert.Equal("Added OmniLight3D.", window.Status);
        });
    }

    [Fact]
    public void The_search_and_the_arrows_pick_a_class_and_enter_adds_it_under_the_selection()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            window.ScenePane.Select(FakeScene.Lamp.Handle);
            Click(window.Tools.AddButton);
            var search = window.Tools.ClassList.SearchBox;

            search.Text = "3d";
            Press(search, Key.Down);
            Press(search, Key.Enter);

            Assert.False(window.Tools.AddDialog.IsVisible);
            Assert.Equal("Added MeshInstance3D.", window.Status);
            Assert.Equal("MeshInstance3D", window.ScenePane.Selected!.Name);
            Assert.Equal(["Field", "Lamp", "MeshInstance3D", "Sound"], Names(window));
        });
    }

    [Fact]
    public void Inside_says_where_the_new_thing_goes_and_can_change_it()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            var window = OpenAtReferenceSize(scene);
            window.ScenePane.Select(FakeScene.Lamp.Handle);
            Click(window.Tools.AddButton);
            var inside = window.Tools.Inside;
            Assert.Equal(["Field", "Lamp", "Sound"], inside.Items.Cast<OutlineNode>().Select(node => node.Name));

            inside.SelectedItem = inside.Items.Cast<OutlineNode>().Single(node => node.Name == "Sound");
            Click(window.Tools.AddDialog.MainButton);

            Assert.Equal(["Entity3D"], scene.Outline()!.Children.Single(child => child.Name == "Sound").Children.Select(child => child.Name));
        });
    }

    [Fact]
    public void A_new_parent_takes_the_selected_entitys_place_and_holds_it_where_it_was_drawn()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            var window = OpenAtReferenceSize(scene);
            window.ScenePane.Select(FakeScene.Lamp.Handle);
            ClickItem(window, "Wrap in a new thing…");
            Assert.Equal("Add a parent", window.Tools.AddDialog.Title);
            Assert.Equal("Reparent to New Entity", window.Tools.AddDialog.EngineName);
            Assert.Equal("Pick what to add. It takes the place of Lamp, and Lamp goes inside it.", window.Tools.AddDialog.Why.Text);
            Assert.False(window.Tools.Inside.IsEnabled);
            Assert.Equal("Field", ((OutlineNode)window.Tools.Inside.SelectedItem!).Name);

            window.Tools.ClassList.SearchBox.Text = "Marker3D";
            Click(window.Tools.AddDialog.MainButton);

            var made = scene.Outline()!.Children[0];
            Assert.Equal(["Marker3D", "Sound"], scene.Outline()!.Children.Select(child => child.Name));
            Assert.Equal(["Lamp"], made.Children.Select(child => child.Name));
            Assert.True(scene.KeptGlobal);
            Assert.Equal("Marker3D", window.ScenePane.Selected!.Name);
            Assert.Equal("Added Marker3D over Lamp.", window.Status);
        });
    }

    [Fact]
    public void The_scene_root_gets_no_new_parent()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            window.ScenePane.Select(FakeScene.Field.Handle);

            Assert.False(window.Tools.NewParentButton.IsEnabled);
        });
    }

    [Fact]
    public void A_new_entity_opens_its_row_for_a_name_and_enter_keeps_it()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            window.ScenePane.Select(FakeScene.Lamp.Handle);
            Click(window.Tools.AddButton);
            var search = window.Tools.ClassList.SearchBox;
            search.Text = "Marker";
            Press(search, Key.Enter);

            var box = window.ScenePane.Rows[window.ScenePane.Selected!.Handle].Editing!;
            Assert.Equal("Marker3D", box.Text);
            box.Text = "Spawn";
            Press(box, Key.Enter);

            Assert.Equal(["Field", "Lamp", "Spawn", "Sound"], Names(window));
        });
    }

    [Fact]
    public void Escape_after_an_add_keeps_the_class_name()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            Click(window.Tools.AddButton);
            window.Tools.ClassList.SearchBox.Text = "Marker";
            Press(window.Tools.ClassList.SearchBox, Key.Enter);

            var row = window.ScenePane.Rows[window.ScenePane.Selected!.Handle];
            Press(row.Editing!, Key.Escape);

            Assert.Null(row.Editing);
            Assert.Equal("Added Marker3D.", window.Status);
        });
    }

    [Fact]
    public void A_search_with_no_match_says_so_and_turns_the_add_button_off()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            Click(window.Tools.AddButton);

            window.Tools.ClassList.SearchBox.Text = "zzz";

            Assert.Empty(window.Tools.ClassList.Shown);
            Assert.False(window.Tools.AddDialog.MainButton.IsEnabled);
            Assert.Contains(window.Tools.ClassList.GetLogicalDescendants().OfType<TextBlock>(), text => text.Text == "Nothing matches.");
            Assert.Equal("Pick a thing to add first.", ((HoverCard)ToolTip.GetTip(window.Tools.AddDialog.MainButton)!).Line);
            Assert.Equal(KindPreview.NothingLit, window.Tools.Preview.About.Text);
        });
    }

    [Fact]
    public void The_scene_options_menu_says_what_each_item_does_and_starts_with_plain_names()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var items = OptionItems(window);

            Assert.Equal(["Open all", "Close all", "Show engine names", "Show only what matches the search"], items.Select(HeaderOf));
            Assert.Equal(
                ["Open all", "Shows every thing inside every other", "Close all", "Shows only the top level", "Show engine names", "Adds the engine's kind, like MeshInstance3D, beside each name", "Show only what matches the search", "Hides the rest while you type"],
                items.SelectMany(Texts));
            Assert.Equal([false, true], items.Skip(2).Select(item => item.IsChecked));
        });
    }

    [Fact]
    public void With_only_matches_off_the_filter_marks_matches_and_keeps_every_row()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            OptionItems(window).Single(item => HeaderOf(item) == "Show only what matches the search").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

            window.ScenePane.FilterBox.Text = "lam";

            Assert.Equal(["Field", "Lamp", "Sound"], Names(window));
            Assert.NotEmpty(Rows(window)[1].GetVisualDescendants().OfType<TextBlock>().First().Inlines!);
        });
    }

    [Fact]
    public void Collapse_all_closes_every_row_and_expand_all_opens_them_again()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var items = OptionItems(window);
            TreeViewItem Field() => window.Outline.GetVisualDescendants().OfType<TreeViewItem>().First();

            items.Single(item => HeaderOf(item) == "Close all").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Assert.False(Field().IsExpanded);

            items.Single(item => HeaderOf(item) == "Open all").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Assert.True(Field().IsExpanded);
        });
    }

    [Fact]
    public void Show_class_names_puts_the_class_after_each_name_until_pressed_again()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var toggle = OptionItems(window).Single(item => HeaderOf(item) == "Show engine names");
            string[] Words() => [.. Rows(window)[1].GetVisualDescendants().OfType<TextBlock>().Select(text => text.Text!)];

            toggle.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Assert.Equal(["Lamp", "Empty 3D point", "Entity3D"], Words());
            Assert.True(toggle.IsChecked);

            toggle.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Assert.Equal(["Lamp", "Empty 3D point"], Words());
            Assert.False(toggle.IsChecked);
        });
    }

    [Fact]
    public void The_filter_marks_the_part_of_the_name_it_matched()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            window.ScenePane.FilterBox.Text = "am";
            window.UpdateLayout();
            var runs = window.ScenePane.Rows[FakeScene.Lamp.Handle].GetVisualDescendants().OfType<TextBlock>().First().Inlines!.OfType<Run>().ToArray();

            Assert.Equal(["L", "am", "p"], runs.Select(run => run.Text));
            Assert.Equal(EditorBrushes.AccentSoft, runs[1].Background);
            Assert.Equal(EditorBrushes.Accent, runs[1].Foreground);
            Assert.Null(runs[0].Background);
        });
    }

    [Fact]
    public void F2_edits_the_name_in_the_row_and_enter_keeps_it()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            window.ScenePane.Select(FakeScene.Lamp.Handle);

            Press(window.Outline, Key.F2);
            var box = window.ScenePane.Rows[FakeScene.Lamp.Handle].Editing!;
            Assert.Equal("Lamp", box.Text);
            Assert.True(box.IsFocused);
            Assert.Equal(["↵", "keep", "esc", "cancel"], Texts((Control)box.Parent!));
            box.Text = "Bulb";
            Press(box, Key.Enter);

            Assert.Equal("Renamed Lamp.", window.Status);
            Assert.Equal(["Field", "Bulb", "Sound"], Names(window));
        });
    }

    [Fact]
    public void Escape_leaves_the_name_as_it_was()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            window.ScenePane.Select(FakeScene.Lamp.Handle);

            Click(window.Tools.RenameButton);
            var row = window.ScenePane.Rows[FakeScene.Lamp.Handle];
            row.Editing!.Text = "Bulb";
            Press(row.Editing, Key.Escape);

            Assert.Null(row.Editing);
            Assert.Equal(["Field", "Lamp", "Sound"], Names(window));
            Assert.NotEqual("Renamed Lamp.", window.Status);
        });
    }

    [Fact]
    public void Move_under_lists_the_tree_without_the_entity_and_turns_its_parent_off()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            window.ScenePane.Select(FakeScene.Lamp.Handle);

            Click(window.Move.MoveUnderButton);

            Assert.True(window.Move.Dialog.IsVisible);
            Assert.Equal("Move Lamp into…", window.Move.Dialog.Title);
            Assert.Equal("Reparent", window.Move.Dialog.EngineName);
            Assert.Equal("Pick the new home for Lamp. It keeps its place in the world unless you turn that off.", window.Move.Dialog.Why.Text);
            Assert.Equal(["Field", "Sound"], window.Move.ParentList.Shown.Select(choice => choice.Name));
            Assert.Equal([true, false], window.Move.ParentList.Shown.Select(choice => choice.Off));
            Assert.Equal(["Lamp is already here.", "Audio"], window.Move.ParentList.Shown.Select(choice => choice.Line));
            Assert.Equal([0, 1], window.Move.ParentList.Shown.Select(choice => choice.Depth));
            Assert.Equal("Sound", window.Move.ParentList.Picked!.Name);
            Assert.Equal(["Move into Sound", "↵"], Texts(window.Move.Dialog.MainButton));
            Assert.Equal("Moves it to the end of the lit thing.", ((HoverCard)ToolTip.GetTip(window.Move.Dialog.MainButton)!).Line);
            Assert.Equal("Keep where it is in the world", window.Move.KeepGlobal.OnContent);
        });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Move_into_passes_the_keep_global_switch_to_the_scene(bool keepGlobal)
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            var window = OpenAtReferenceSize(scene);
            window.ScenePane.Select(FakeScene.Lamp.Handle);
            Click(window.Move.MoveUnderButton);

            window.Move.KeepGlobal.IsChecked = keepGlobal;
            Click(window.Move.Dialog.MainButton);

            Assert.Equal(keepGlobal, scene.KeptGlobal);
            Assert.False(window.Move.Dialog.IsVisible);
            Assert.Equal("Moved Lamp into Field/Sound.", window.Status);
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

            window.Tools.Rename("Bulb");
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

            window.ScenePane.Select(FakeScene.Sound.Handle);

            Assert.Equal(
                ["Add inside Sound…", "Add a scene inside…", "Duplicate", "Rename", "Move up", "Move down", "Move into…", "Wrap in a new thing…", "Delete"],
                MenuItems(window).Select(HeaderOf));
            Assert.Equal(
                ["A camera, light, shape or anything else", "Reuse a scene you already made", "Makes a copy right below it", "", "", "Sound is already last.", "Put Sound inside another thing", "Puts Sound inside a new empty point", $"Undo with {Parts.KeyText(new KeyGesture(Key.Z, window.CommandKey))}"],
                window.ScenePane.ThingMenu.Select(item => ((StackPanel)item.Header!).Children[1] is TextBlock { IsVisible: true } note ? note.Text : ""));
            var command = window.CommandKey;
            Assert.Equal(
                [new KeyGesture(Key.A, command), new KeyGesture(Key.A, command | KeyModifiers.Shift), new KeyGesture(Key.D, command), new KeyGesture(Key.F2), new KeyGesture(Key.Up, command), new KeyGesture(Key.Down, command), null, null, new KeyGesture(Key.Back)],
                MenuItems(window).Select(item => item.InputGesture));
        });
    }

    [Fact]
    public void The_top_things_menu_says_why_each_item_is_off()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            window.ScenePane.Select(FakeScene.Field.Handle);

            string Note(string item) => Texts(MenuItems(window).Single(menu => HeaderOf(menu) == item)).ElementAtOrDefault(1) ?? "";
            Assert.Equal("Add inside Field…", HeaderOf(MenuItems(window)[0]));
            Assert.Equal("Field is the whole scene. Copy the scene file in Files instead.", Note("Duplicate"));
            Assert.Equal("The top thing has nothing above it.", Note("Move up"));
            Assert.Equal("The top thing can't go inside another.", Note("Move into…"));
            Assert.Equal("A scene always needs a top thing.", Note("Delete"));
            window.ScenePane.Select(FakeScene.Lamp.Handle);
            Assert.Equal("Lamp is already first.", Note("Move up"));
        });
    }

    [Fact]
    public void Menu_items_are_on_only_when_their_command_can_run()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            // In menu order: add, instance, duplicate, rename, up, down, into, wrap, delete.
            bool[] On() => MenuItems(window).Select(item => item.IsEnabled).ToArray();

            window.ScenePane.Select(0);
            Assert.Equal([true, true, false, false, false, false, false, false, false], On());
            window.ScenePane.Select(FakeScene.Field.Handle);
            Assert.Equal([true, true, false, true, false, false, false, false, false], On());
            window.ScenePane.Select(FakeScene.Lamp.Handle);
            Assert.Equal([true, true, true, true, false, true, true, true, true], On());
            window.ScenePane.Select(FakeScene.Sound.Handle);
            Assert.Equal([true, true, true, true, true, false, true, true, true], On());
        });
    }

    [Fact]
    public void A_menu_item_runs_its_command_on_the_selected_entity()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            window.ScenePane.Select(FakeScene.Lamp.Handle);

            ClickItem(window, "Move down");
            Assert.Equal("Moved Lamp down.", window.Status);

            ClickItem(window, "Move up");
            Assert.Equal("Moved Lamp up.", window.Status);

            ClickItem(window, "Duplicate");
            Assert.Equal("Duplicated Lamp.", window.Status);

            ClickItem(window, "Delete");
            Assert.Equal("Removed Lamp2.", window.Status);
        });
    }

    [Fact]
    public void The_menu_items_that_need_more_open_the_dialog_or_the_row_box()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            window.ScenePane.Select(FakeScene.Lamp.Handle);

            ClickItem(window, "Add inside Lamp…");
            Assert.True(window.Tools.AddDialog.IsVisible);
            ClickItem(window, "Add a scene inside…");
            Assert.True(window.InstanceScene.Dialog.IsVisible);
            ClickItem(window, "Move into…");
            Assert.True(window.Move.Dialog.IsVisible);
            ClickItem(window, "Rename");
            Assert.NotNull(window.ScenePane.Rows[FakeScene.Lamp.Handle].Editing);
        });
    }

    [Fact]
    public void A_right_click_on_a_row_selects_it_and_opens_the_menu()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var lamp = Rows(window).Single(row => NameOf(row) == "Lamp");
            var point = lamp.TranslatePoint(new Point(40, 8), window)!.Value;

            window.MouseDown(point, MouseButton.Right);
            window.MouseUp(point, MouseButton.Right);

            Assert.Equal("Lamp", window.ScenePane.Selected!.Name);
            Assert.True(window.ScenePane.Outline.ContextMenu!.IsOpen);
        });
    }
}
