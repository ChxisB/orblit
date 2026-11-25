using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Orblit.Editor.App.Shell;
using static Orblit.Editor.App.Tests.ReferenceWindow;

namespace Orblit.Editor.App.Tests;

public sealed class CommandPaletteTests
{
    static FakeScene FieldScene() => new() { SceneFile = "res://field.oscn" };

    static void Press(Control target, Key key, KeyModifiers modifiers = KeyModifiers.None) =>
        target.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key, KeyModifiers = modifiers });

    static Picker Opened(EditorWindow window, string query = "")
    {
        Press(window, Key.K, window.CommandKey);
        window.Palette.SearchBox.Text = query;
        return window.Palette;
    }

    // Logical, because the rows sit in a scroll viewer that has no visual children before a layout pass.
    static IEnumerable<string> Texts(Control control) => control.GetLogicalDescendants().OfType<TextBlock>().Select(text => text.Text ?? "");

    [Fact]
    public void Command_K_opens_commands_files_and_the_scenes_entities_in_one_list()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize(FieldScene());

            var palette = Opened(window);

            Assert.True(palette.IsVisible);
            Assert.Equal(["Commands", "Files", "Entities in field.oscn"], palette.Shown.Select(row => row.Group).Distinct());
            Assert.Contains("ENTITIES IN FIELD.OSCN", Texts(palette.Card));
        });
    }

    [Fact]
    public void The_Search_button_and_the_menu_item_open_the_palette()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            Click(window.Bar.SearchButton);
            Assert.True(window.Palette.IsVisible);
            Assert.Equal("Opened Field.", window.Status);

            Press(window.Palette.SearchBox, Key.Escape);
            Assert.False(window.Palette.IsVisible);

            var item = MenuItems(NativeMenu.GetMenu(window)!).Single(item => item.Header == "Command Palette...");
            item.Command!.Execute(item.CommandParameter);
            Assert.True(window.Palette.IsVisible);
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
    public void A_leading_angle_bracket_shows_commands_only()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize(FieldScene());

            var palette = Opened(window, "> s");

            Assert.All(palette.Shown, row => Assert.Equal("Commands", row.Group));
            Assert.Contains(palette.Shown, row => row.Name == "Save Scene");
        });
    }

    [Fact]
    public void Commands_are_the_menus_items_with_their_keys_and_their_submenu()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            var commands = Opened(window, ">").Shown;

            var quickOpen = commands.Single(row => row.Name == "Quick Open Scene...");
            Assert.Equal(Parts.KeyText(new KeyGesture(Key.O, window.CommandKey | KeyModifiers.Shift)), quickOpen.Key);
            Assert.Equal("", quickOpen.Note);
            Assert.Equal("Editor Docks", commands.Single(row => row.Name == "Inspector").Note);
            Assert.DoesNotContain(commands, row => row.Name is "Layouts" or "Recent" or "Scene" && row.Note == "");
        });
    }

    [Fact]
    public void Enter_runs_the_lit_row_and_closes_the_palette()
    {
        UiThread.Run(() =>
        {
            var scene = FieldScene();
            var window = OpenAtReferenceSize(scene);
            MakeAnEdit(window);
            var palette = Opened(window, "save");

            Assert.Equal("Save Scene", palette.Shown[palette.Lit].Name);
            Press(palette.SearchBox, Key.Enter);

            Assert.Equal(1, scene.Saves);
            Assert.False(palette.IsVisible);
        });
    }

    [Fact]
    public void The_arrows_move_the_light_and_stop_at_the_ends()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize(FieldScene());
            var palette = Opened(window, "save");

            Press(palette.SearchBox, Key.Up);
            Assert.Equal(0, palette.Lit);
            for (var i = 0; i < palette.Shown.Count + 2; i++)
            {
                Press(palette.SearchBox, Key.Down);
            }

            Assert.Equal(palette.Shown.Count - 1, palette.Lit);

            palette.SearchBox.Text = "save s";
            Assert.Equal(0, palette.Lit);
        });
    }

    [Fact]
    public void A_file_row_opens_its_scene()
    {
        UiThread.Run(() =>
        {
            var scene = FieldScene();
            var window = OpenAtReferenceSize(scene);
            var palette = Opened(window, "second");

            var row = palette.Shown.Single();
            Assert.Equal(("Files", "second.oscn", "res://levels/"), (row.Group, row.Name, row.Note));
            Press(palette.SearchBox, Key.Enter);

            Assert.Equal("res://levels/second.oscn", scene.Opened.Last());
        });
    }

    [Fact]
    public void An_entity_row_selects_its_entity()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize(FieldScene());
            var palette = Opened(window, "lamp");

            Assert.Equal("Field", palette.Shown.Single().Note);
            Click(palette.RowButtons.Single());

            Assert.Equal(FakeScene.Lamp.Handle, window.ScenePane.Selected?.Handle);
            Assert.False(palette.IsVisible);
        });
    }

    [Fact]
    public void A_query_with_no_rows_says_so()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            var palette = Opened(window, "zzzz");

            Assert.Empty(palette.Shown);
            Assert.Contains("Nothing matches.", Texts(palette.Card));
            Press(palette.SearchBox, Key.Enter);
            Assert.True(palette.IsVisible);
        });
    }

    [Fact]
    public void A_click_outside_the_card_closes_the_palette()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var palette = Opened(window);
            window.UpdateLayout();

            window.MouseDown(new Point(100, 1000), MouseButton.Left);
            window.MouseUp(new Point(100, 1000), MouseButton.Left);

            Assert.False(palette.IsVisible);
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
