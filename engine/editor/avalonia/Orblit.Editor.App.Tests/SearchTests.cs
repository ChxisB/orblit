using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Orblit.Editor.App.Shell;
using static Orblit.Editor.App.Tests.ReferenceWindow;

namespace Orblit.Editor.App.Tests;

public sealed class SearchTests
{
    static FakeScene FieldScene() => new() { SceneFile = "res://field.oscn" };

    static void Press(Control target, Key key, KeyModifiers modifiers = KeyModifiers.None) =>
        target.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key, KeyModifiers = modifiers });

    static Picker Opened(EditorWindow window, string query = "")
    {
        Press(window, Key.K, window.CommandKey);
        window.Search.Picker.SearchBox.Text = query;
        return window.Search.Picker;
    }

    // Logical, because the rows sit in a scroll viewer that has no visual children before a layout pass.
    static IEnumerable<string> Texts(Control control) => control.GetLogicalDescendants().OfType<TextBlock>().Select(text => text.Text ?? "");

    // Field opens first and second after it. Arena was never opened.
    static (EditorWindow Window, FakeScene Scene) TwoOpened()
    {
        var scene = FieldScene();
        scene.ScenePaths.Add("res://arena.oscn");
        var window = OpenAtReferenceSize(scene);
        OpenScene(window, "res://levels/second.oscn");
        return (window, scene);
    }

    [Fact]
    public void An_empty_box_shows_the_recent_scenes_and_things_to_try()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize(FieldScene());

            var search = Opened(window);

            Assert.True(search.IsVisible);
            Assert.Equal([Search.Recent, Search.Try], search.Shown.Select(row => row.Group).Distinct());
            Assert.Equal(("field", "res://field.oscn", "Scene", "open now"), search.Shown.Select(row => (row.Name, row.Note, row.Kind, row.Key)).First());
            Assert.Equal(["Add a thing", "New scene…", "Play the game"], search.Shown.Where(row => row.Group == Search.Try).Select(row => row.Name));
            Assert.Contains("Puts a camera, light or shape in the scene", Texts(search.Card));
            Assert.Contains("TRY", Texts(search.Card));
        });
    }

    [Fact]
    public void A_typed_query_groups_scenes_scripts_things_and_actions()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize(FieldScene());

            var search = Opened(window, "s");

            Assert.Equal([Search.Scenes, Search.Scripts, Search.Things, Search.Actions], search.Shown.Select(row => row.Group).Distinct());
            Assert.Contains(search.Shown, row => (row.Group, row.Name, row.Kind) == (Search.Scripts, "slime", "C# script"));
        });
    }

    [Fact]
    public void Shift_Command_O_shows_the_scenes_alone_with_their_kind_and_the_open_one()
    {
        UiThread.Run(() =>
        {
            var scene = FieldScene();
            scene.Kinds["res://field.oscn"] = "3D";
            scene.Kinds["res://levels/second.oscn"] = "2D";
            var window = OpenAtReferenceSize(scene);

            Press(window, Key.O, window.CommandKey | KeyModifiers.Shift);

            var search = window.Search;
            Assert.True(search.Picker.IsVisible);
            Assert.True(search.ScenesChip.IsVisible);
            Assert.Equal(
                [(Search.InProject, "field", "3D scene", "open now"), (Search.InProject, "second", "2D scene", "")],
                search.Picker.Shown.Select(row => (row.Group, row.Name, row.Kind, row.Key)));
            Assert.Contains("SCENES IN THIS PROJECT", Texts(search.Picker.Card));
        });
    }

    [Fact]
    public void The_open_scene_comes_first_then_the_recent_ones_then_the_rest()
    {
        UiThread.Run(() =>
        {
            var (window, _) = TwoOpened();

            Press(window, Key.O, window.CommandKey | KeyModifiers.Shift);

            Assert.Equal(["second", "field", "arena"], window.Search.Picker.Shown.Select(row => row.Name));
        });
    }

    [Fact]
    public void Shift_Command_P_opens_on_the_actions_and_Command_K_on_everything()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize(FieldScene());
            Press(window, Key.O, window.CommandKey | KeyModifiers.Shift);
            Press(window.Search.Picker.SearchBox, Key.Escape);

            Press(window, Key.P, window.CommandKey | KeyModifiers.Shift);

            var search = window.Search;
            Assert.True(search.Picker.IsVisible);
            Assert.False(search.ScenesChip.IsVisible);
            Assert.Equal(">", search.Picker.SearchBox.Text);
            Assert.All(search.Picker.Shown, row => Assert.Equal(Search.Actions, row.Group));

            Press(search.Picker.SearchBox, Key.Escape);
            Press(window, Key.K, window.CommandKey);
            Assert.True(search.Picker.IsVisible);
            Assert.Equal("", search.Picker.SearchBox.Text);
        });
    }

    [Fact]
    public void The_Search_button_and_the_menu_item_open_it()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            Click(window.Bar.SearchButton);
            Assert.True(window.Search.Picker.IsVisible);
            Assert.Equal("Opened Field.", window.Status);

            Press(window.Search.Picker.SearchBox, Key.Escape);
            Assert.False(window.Search.Picker.IsVisible);

            var item = MenuItems(NativeMenu.GetMenu(window)!).Single(item => item.Header == "Search");
            item.Command!.Execute(item.CommandParameter);
            Assert.True(window.Search.Picker.IsVisible);
        });
    }

    [Fact]
    public void Find_a_scene_opens_it_on_the_scenes_and_Clear_the_list_empties_Recent()
    {
        UiThread.Run(() =>
        {
            var (window, _) = TwoOpened();
            var items = MenuItems(NativeMenu.GetMenu(window)!).ToArray();

            items.Single(item => item.Header == "Find a scene…").Command!.Execute(null);
            Assert.True(window.Search.ScenesChip.IsVisible);
            Press(window.Search.Picker.SearchBox, Key.Escape);

            items.Single(item => item.Header == "Clear the list").Command!.Execute(null);
            Assert.DoesNotContain(Search.Recent, Opened(window).Shown.Select(row => row.Group));
        });
    }

    [Fact]
    public void The_Search_button_shows_its_key()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            Assert.Equal([Parts.KeyText(new KeyGesture(Key.K, window.CommandKey)), "Search"], Texts(window.Bar.SearchButton));
        });
    }

    [Fact]
    public void A_leading_angle_bracket_shows_actions_only()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize(FieldScene());

            var search = Opened(window, "> s");

            Assert.All(search.Shown, row => Assert.Equal(Search.Actions, row.Group));
            Assert.Contains(search.Shown, row => row.Name == "Save");
        });
    }

    [Fact]
    public void Actions_are_the_menus_items_with_their_notes_keys_and_engine_names()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            var actions = Opened(window, ">").Shown;

            var find = actions.Single(row => row.Name == "Find a scene…");
            Assert.Equal(Parts.KeyText(new KeyGesture(Key.O, window.CommandKey | KeyModifiers.Shift)), find.Key);
            Assert.Equal(("Type part of a name to open it", "Quick Open Scene…", ""), (find.Note, find.Engine, find.Kind));
            Assert.Equal(("Settings for the picked thing", "Panels"), actions.Single(row => row.Name == "Inspector") is var inspector ? (inspector.Note, inspector.Kind) : default);
            Assert.DoesNotContain(actions, row => row.Name is "Panels" or "Open recent" or "Show while playing" or "Clear the list" or "field");
        });
    }

    [Fact]
    public void The_engine_s_name_finds_a_row_too()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            var search = Opened(window, "quick open");

            Assert.Equal("Find a scene…", search.Shown.Single().Name);
        });
    }

    [Fact]
    public void Enter_runs_the_lit_row_and_closes_the_card()
    {
        UiThread.Run(() =>
        {
            var scene = FieldScene();
            var window = OpenAtReferenceSize(scene);
            MakeAnEdit(window);
            var search = Opened(window, "save");

            Assert.Equal("Save", search.Shown[search.Lit].Name);
            Press(search.SearchBox, Key.Enter);

            Assert.Equal(1, scene.Saves);
            Assert.False(search.IsVisible);
        });
    }

    [Fact]
    public void The_arrows_move_the_light_and_stop_at_the_ends()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize(FieldScene());
            var search = Opened(window, "s");

            Press(search.SearchBox, Key.Up);
            Assert.Equal(0, search.Lit);
            for (var i = 0; i < search.Shown.Count + 2; i++)
            {
                Press(search.SearchBox, Key.Down);
            }

            Assert.Equal(search.Shown.Count - 1, search.Lit);

            search.SearchBox.Text = "sa";
            Assert.Equal(0, search.Lit);
        });
    }

    [Fact]
    public void A_scene_row_opens_its_scene()
    {
        UiThread.Run(() =>
        {
            var scene = FieldScene();
            var window = OpenAtReferenceSize(scene);
            var search = Opened(window, "second");

            var row = search.Shown.Single();
            Assert.Equal((Search.Scenes, "second", "res://levels/second.oscn"), (row.Group, row.Name, row.Note));
            Press(search.SearchBox, Key.Enter);

            Assert.Equal("res://levels/second.oscn", scene.Opened.Last());
        });
    }

    [Fact]
    public void A_script_row_opens_the_script_workspace()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize(FieldScene());
            var search = Opened(window, "slime");

            Press(search.SearchBox, Key.Enter);

            Assert.True(window.Script.IsVisible);
        });
    }

    [Fact]
    public void A_thing_row_selects_its_thing()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize(FieldScene());
            var search = Opened(window, "lamp");

            Assert.Equal((Search.Things, "Field", "Entity3D"), search.Shown.Single() is var row ? (row.Group, row.Note, row.Engine) : default);
            Click(search.RowButtons.Single());

            Assert.Equal(FakeScene.Lamp.Handle, window.ScenePane.Selected?.Handle);
            Assert.False(search.IsVisible);
        });
    }

    [Fact]
    public void A_query_with_no_rows_says_what_to_try()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            var search = Opened(window, "zzzz");

            Assert.Empty(search.Shown);
            Assert.Contains("Nothing matches. Try fewer letters, or > for actions.", Texts(search.Card));
            Press(search.SearchBox, Key.Enter);
            Assert.True(search.IsVisible);
        });
    }

    [Fact]
    public void A_click_outside_the_card_closes_it()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var search = Opened(window);
            window.UpdateLayout();

            window.MouseDown(new Point(100, 1000), MouseButton.Left);
            window.MouseUp(new Point(100, 1000), MouseButton.Left);

            Assert.False(search.IsVisible);
        });
    }

    [Theory]
    [InlineData(Key.S, KeyModifiers.Meta | KeyModifiers.Shift | KeyModifiers.Alt, "⌥⇧⌘S")]
    [InlineData(Key.O, KeyModifiers.Meta | KeyModifiers.Control, "⌃⌘O")]
    [InlineData(Key.OemComma, KeyModifiers.Meta, "⌘,")]
    [InlineData(Key.F12, KeyModifiers.None, "F12")]
    public void A_key_is_written_with_its_modifiers_in_the_macOS_order(Key key, KeyModifiers modifiers, string text)
    {
        Assert.Equal(text, Parts.KeyText(new KeyGesture(key, modifiers)));
    }

    static IEnumerable<NativeMenuItem> MenuItems(NativeMenu menu) =>
        menu.Items.OfType<NativeMenuItem>().SelectMany(item => item.Menu is { } under ? MenuItems(under).Prepend(item) : [item]);
}
