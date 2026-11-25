using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using Orblit.Editor.App.Shell;
using static Orblit.Editor.App.Tests.ReferenceWindow;

namespace Orblit.Editor.App.Tests;

public sealed class EditorMenuTests
{
    // The built items of each menu. A submenu's items are indented and "-" is a separator.
    static readonly Dictionary<string, string[]> Items = new()
    {
        ["Orblit"] = ["About Orblit", "-", "Quit Orblit"],
        ["File"] =
        [
            "New scene…", "Find a scene…", "Open recent", "  Scenes you open show here",
            "-", "Save",
            "-", "Export game…",
        ],
        ["Edit"] = ["Undo", "Redo", "-", "Search"],
        ["View"] =
        [
            "Panels", "  Scene", "  Import", "  Files", "  History", "  Inspector", "  Signals",
            "-", "Hide panels", "Full screen",
        ],
        ["Play"] =
        [
            "Play the game", "Play this scene", "Stop",
            "-", "Show while playing", "  Collision shapes", "  Paths", "  Navigation", "  Avoidance", "  -", "  2D redraws",
        ],
        ["Help"] = ["All keys"],
    };

    static readonly string[] ScreenBarMenus = ["File", "Edit", "View", "Play", "Help"];

    static string[] MenusOn(bool global) => global ? ScreenBarMenus : ["Orblit", .. ScreenBarMenus];

    static NativeMenu Build(bool global = true, KeyModifiers command = KeyModifiers.Meta, Dictionary<string, MenuAction>? actions = null) =>
        EditorMenu.Build(global, command, actions ?? []);

    static NativeMenuItem Top(NativeMenu bar, string title) => bar.Items.OfType<NativeMenuItem>().Single(item => item.Header == title);

    static string[] Dump(NativeMenuItem menu) => [.. Lines(menu.Menu!, "")];

    static IEnumerable<string> Lines(NativeMenu menu, string indent)
    {
        foreach (var entry in menu.Items)
        {
            if (entry is not NativeMenuItem item)
            {
                yield return indent + "-";
                continue;
            }

            yield return indent + item.Header;
            foreach (var line in item.Menu is { } sub ? Lines(sub, indent + "  ") : [])
            {
                yield return line;
            }
        }
    }

    // Every leaf and submenu header of the bar, so a test can find an item by its label.
    static IEnumerable<NativeMenuItem> All(NativeMenu menu) =>
        menu.Items.OfType<NativeMenuItem>().SelectMany(item => item.Menu is { } sub ? All(sub).Prepend(item) : [item]);

    // Every item that is not a separator or a menu of the bar itself.
    static IEnumerable<NativeMenuItem> Named(NativeMenu bar) =>
        bar.Items.OfType<NativeMenuItem>().SelectMany(menu => All(menu.Menu!)).Where(item => item is not NativeMenuItemSeparator);

    static NativeMenuItem Find(NativeMenu bar, string label) => All(bar).Single(item => item.Header == label);

    static void Press(NativeMenuItem item) => item.Command!.Execute(item.CommandParameter);

    static NativeMenuItem Under(NativeMenu bar, string submenu, string label) =>
        Find(bar, submenu).Menu!.Items.OfType<NativeMenuItem>().Single(item => item.Header == label);

    static NativeMenuItem Panel(EditorWindow window, string label) => Under(NativeMenu.GetMenu(window)!, "Panels", label);

    static string[] Recent(EditorWindow window) =>
        [.. Find(NativeMenu.GetMenu(window)!, "Open recent").Menu!.Items.Select(entry => entry is NativeMenuItem item ? item.Header! : "-")];

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void The_bar_has_its_menus_in_order_and_Orblit_only_off_the_screen_bar(bool global)
    {
        UiThread.Run(() =>
        {
            Assert.Equal(MenusOn(global), Build(global).Items.OfType<NativeMenuItem>().Select(item => item.Header));
        });
    }

    [Theory]
    [InlineData("Orblit")]
    [InlineData("File")]
    [InlineData("Edit")]
    [InlineData("View")]
    [InlineData("Play")]
    [InlineData("Help")]
    public void Each_menu_lists_its_built_items(string menu)
    {
        UiThread.Run(() =>
        {
            Assert.Equal(Items[menu], Dump(Top(Build(global: false), menu)));
        });
    }

    [Fact]
    public void Each_item_notes_what_it_does()
    {
        UiThread.Run(() =>
        {
            var notes = Named(Build(global: false)).Select(item => $"{item.Header}: {EditorMenu.NoteOf(item)}");

            Assert.Equal(
                [
                    $"About Orblit: Version {BottomBar.Version}. Click to copy it for a bug report",
                    "Quit Orblit: Asks first if anything is unsaved",
                    "New scene…: Pick what it is for and where it goes",
                    "Find a scene…: Type part of a name to open it",
                    "Open recent: ",
                    "Scenes you open show here: ",
                    "Save: Saves the open scene",
                    "Export game…: Makes a game people can play on Mac, Windows or Linux",
                    "Undo: Every change can be undone. History lists them.",
                    "Redo: Puts back the change you undid",
                    "Search: Files, things, settings and actions in one place",
                    "Panels: Show or hide a panel",
                    "Scene: Things in the open scene",
                    "Import: How the picked file comes in",
                    "Files: Every file in the project",
                    "History: Every change, to undo back to",
                    "Inspector: Settings for the picked thing",
                    "Signals: Messages things send each other",
                    "Hide panels: Gives the view the whole window",
                    "Full screen: ",
                    "Play the game: Starts from the main scene",
                    "Play this scene: Starts from the scene you are in",
                    "Stop: Closes the game window",
                    "Show while playing: Draws hidden shapes and paths in the game",
                    "Collision shapes: The shapes things bump into",
                    "Paths: Lines that things follow",
                    "Navigation: Where characters can walk",
                    "Avoidance: How characters steer round others",
                    "2D redraws: Flashes each 2D thing as it redraws",
                    "All keys: Every shortcut on one sheet",
                ],
                notes);
        });
    }

    [Fact]
    public void An_item_with_a_new_name_keeps_the_engine_s_name_beside_it()
    {
        UiThread.Run(() =>
        {
            var engines = All(Build()).Where(item => EditorMenu.EngineOf(item) is not null).Select(item => $"{item.Header}={EditorMenu.EngineOf(item)}");

            Assert.Equal(
                [
                    "Find a scene…=Quick Open Scene…",
                    "Collision shapes=Visible Collision Shapes",
                    "Paths=Visible Paths",
                    "Navigation=Visible Navigation",
                    "Avoidance=Visible Avoidance",
                    "2D redraws=Debug CanvasItem Redraws",
                ],
                engines);
        });
    }

    [Fact]
    public void The_screen_bar_tip_holds_the_note_and_then_the_engine_s_name()
    {
        UiThread.Run(() =>
        {
            var bar = Build();

            Assert.Equal("Type part of a name to open it\nQuick Open Scene…", Find(bar, "Find a scene…").ToolTip);
            Assert.Equal("Saves the open scene", Find(bar, "Save").ToolTip);
            Assert.Null(Find(bar, "Full screen").ToolTip);
        });
    }

    [Fact]
    public void The_All_keys_note_names_its_key_where_the_screen_bar_cannot_hold_it()
    {
        UiThread.Run(() =>
        {
            var item = Find(Build(global: true), "All keys");

            Assert.Null(item.Gesture);
            Assert.Equal("Every shortcut on one sheet. Press ? to open it", EditorMenu.NoteOf(item));
        });
    }

    [Fact]
    public void Shortcuts_follow_the_macOS_editors()
    {
        UiThread.Run(() =>
        {
            var shortcuts = All(Build(global: false))
                .Where(item => item.Gesture is not null)
                .Select(item => $"{item.Header}={Parts.KeyText(item.Gesture!)}");

            Assert.Equal(
                [
                    "Quit Orblit=⌘Q",
                    "New scene…=⌘N",
                    "Find a scene…=⇧⌘O",
                    "Save=⌘S",
                    "Undo=⌘Z",
                    "Redo=⇧⌘Z",
                    "Search=⌘K",
                    "Hide panels=⌘\\",
                    "Full screen=⌃⌘F",
                    "Play the game=⌘B",
                    "Play this scene=⌘R",
                    "Stop=⌘.",
                    "All keys=?",
                ],
                shortcuts);
        });
    }

    [Fact]
    public void The_command_key_stands_in_for_Command()
    {
        UiThread.Run(() =>
        {
            var bar = Build(command: KeyModifiers.Control);

            Assert.Equal(KeyModifiers.Control, Find(bar, "Save").Gesture!.KeyModifiers);
            Assert.Equal(KeyModifiers.Control | KeyModifiers.Shift, Find(bar, "Redo").Gesture!.KeyModifiers);
        });
    }

    [Fact]
    public void The_keys_list_holds_each_keyed_item_under_its_menu_in_bar_order()
    {
        UiThread.Run(() =>
        {
            var keys = EditorMenu.Keys(global: true, KeyModifiers.Meta).ToList();

            Assert.Equal(("File", "New scene…", "⌘N"), keys[0]);
            Assert.Contains(("Play", "Play the game", "⌘B"), keys);
            Assert.DoesNotContain(keys, key => key.Menu == "Orblit" || key.Name == "All keys");
            Assert.Contains(("Help", "All keys", "?"), EditorMenu.Keys(global: false, KeyModifiers.Meta));
        });
    }

    [Fact]
    public void The_run_flags_start_off_and_the_panels_start_shown()
    {
        UiThread.Run(() =>
        {
            var checks = All(Build())
                .Where(item => item.ToggleType == MenuItemToggleType.CheckBox)
                .Select(item => $"{item.Header}={item.IsChecked}");

            Assert.Equal(
                [
                    "Scene=True",
                    "Import=True",
                    "Files=True",
                    "History=True",
                    "Inspector=True",
                    "Signals=True",
                    "Collision shapes=False",
                    "Paths=False",
                    "Navigation=False",
                    "Avoidance=False",
                    "2D redraws=False",
                ],
                checks);
        });
    }

    [Fact]
    public void Only_the_empty_recent_list_is_dim()
    {
        UiThread.Run(() =>
        {
            var off = All(Build()).Where(item => !item.IsEnabled).Select(item => item.Header);

            Assert.Equal(["Scenes you open show here"], off);
        });
    }

    [Fact]
    public void A_wired_item_runs_its_action()
    {
        UiThread.Run(() =>
        {
            var ran = 0;
            var bar = Build(actions: new() { ["Save"] = new(() => ran++) });

            Press(Find(bar, "Save"));

            Assert.Equal(1, ran);
        });
    }

    [Fact]
    public void An_item_with_the_engine_s_name_runs_the_action_under_that_name()
    {
        UiThread.Run(() =>
        {
            var ran = 0;
            var bar = Build(actions: new() { ["Quick Open Scene…"] = new(() => ran++) });

            Press(Find(bar, "Find a scene…"));

            Assert.Equal(1, ran);
        });
    }

    [Fact]
    public void A_wired_item_is_on_only_while_its_switch_is_and_its_note_says_why_while_off()
    {
        UiThread.Run(() =>
        {
            var switchButton = new Button { IsEnabled = false };
            var bar = Build(actions: new() { ["Save"] = new(() => { }, switchButton.GetObservable(InputElement.IsEnabledProperty)) });
            var save = Find(bar, "Save");

            Assert.False(save.IsEnabled);
            Assert.Equal("Open a scene first.", EditorMenu.NoteOf(save));
            Assert.Equal("Open a scene first.", save.ToolTip);

            switchButton.IsEnabled = true;

            Assert.True(save.IsEnabled);
            Assert.Equal("Saves the open scene", EditorMenu.NoteOf(save));
        });
    }

    [Fact]
    public void A_named_item_takes_the_name_its_action_gives()
    {
        UiThread.Run(() =>
        {
            var source = new Button { Content = "Undo Moved Box" };
            var bar = Build(actions: new() { ["Undo"] = new(() => { }, Name: source.GetObservable(ContentControl.ContentProperty, content => content as string ?? "")) });

            Assert.Contains(All(bar), item => item.Header == "Undo Moved Box");

            source.Content = "Undo";

            Assert.Contains(All(bar), item => item.Header == "Undo");
        });
    }

    [Fact]
    public void The_window_carries_the_bar()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            var bar = NativeMenu.GetMenu(window)!;
            Assert.Equal(MenusOn(OperatingSystem.IsMacOS()), bar.Items.OfType<NativeMenuItem>().Select(item => item.Header));
        });
    }

    [Fact]
    public void Save_is_off_until_a_scene_is_open_and_then_saves_it()
    {
        UiThread.Run(() =>
        {
            var empty = new EditorWindow();
            var off = Find(NativeMenu.GetMenu(empty)!, "Save");
            Assert.False(off.IsEnabled);
            Assert.Equal("Open a scene first.", EditorMenu.NoteOf(off));

            var scene = new FakeScene();
            var window = OpenAtReferenceSize(scene);
            var save = Find(NativeMenu.GetMenu(window)!, "Save");
            Assert.True(save.IsEnabled);

            Press(save);

            Assert.Equal(1, scene.Saves);
            Assert.Equal("Saved.", window.Status);
        });
    }

    [Fact]
    public void Undo_and_Redo_name_the_step_they_act_on_and_say_why_while_off()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var bar = NativeMenu.GetMenu(window)!;
            var undo = Find(bar, "Undo");
            var redo = Find(bar, "Redo");
            Assert.Equal("Nothing to undo yet.", EditorMenu.NoteOf(undo));
            Assert.Equal("Nothing to redo yet.", EditorMenu.NoteOf(redo));

            MakeAnEdit(window);

            Assert.StartsWith("Undo Duplicated ", undo.Header);
            Assert.Equal("Every change can be undone. History lists them.", EditorMenu.NoteOf(undo));
            var step = undo.Header!["Undo ".Length..];

            Press(undo);

            Assert.Equal(("Undo", $"Redo {step}"), (undo.Header, redo.Header));
            Assert.Equal("Puts back the change you undid", EditorMenu.NoteOf(redo));
        });
    }

    [Fact]
    public void Play_and_Stop_are_off_with_a_reason_until_they_can_run()
    {
        UiThread.Run(() =>
        {
            var bar = NativeMenu.GetMenu(new EditorWindow())!;

            Assert.Equal(
                [
                    ("Play the game", false, "Open a scene first."),
                    ("Play this scene", false, "Open a saved scene first."),
                    ("Stop", false, "Nothing is playing."),
                ],
                new[] { "Play the game", "Play this scene", "Stop" }.Select(label => Find(bar, label)).Select(item => (item.Header, item.IsEnabled, EditorMenu.NoteOf(item))));
        });
    }

    [Fact]
    public void Play_the_game_runs_the_project()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            var window = OpenAtReferenceSize(scene);

            Press(Find(NativeMenu.GetMenu(window)!, "Play the game"));

            Assert.Equal(1, scene.Plays);
        });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Every_item_of_the_window_s_bar_runs_something(bool global)
    {
        UiThread.Run(() =>
        {
            var window = new EditorWindow(globalMenu: global);

            // The empty recent list holds only its dim line until a scene opens.
            var idle = All(NativeMenu.GetMenu(window)!).Where(item => item.Menu is null && item is not NativeMenuItemSeparator && item.Command is null);

            Assert.Equal(["Scenes you open show here"], idle.Select(item => item.Header));
        });
    }

    [Fact]
    public void Full_screen_switches_the_window_in_and_out()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var item = Find(NativeMenu.GetMenu(window)!, "Full screen");
            var before = window.WindowState;

            Press(item);
            Assert.Equal(WindowState.FullScreen, window.WindowState);

            Press(item);
            Assert.Equal(before, window.WindowState);
        });
    }

    [Fact]
    public void Hide_panels_folds_both_sides_and_brings_them_back()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var item = Find(NativeMenu.GetMenu(window)!, "Hide panels");

            Press(item);
            Assert.True(window.LeftSide.IsFolded && window.RightSide.IsFolded);

            Press(item);
            Assert.False(window.LeftSide.IsFolded || window.RightSide.IsFolded);
        });
    }

    [Fact]
    public void Search_opens_the_search()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            Press(Find(NativeMenu.GetMenu(window)!, "Search"));

            Assert.True(window.Search.Picker.IsVisible);
        });
    }

    [Fact]
    public void Export_game_opens_the_export_dialog_with_the_project_s_presets()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            scene.ExportList.Add(new ExportPreset("Desktop", "Linux", "/games/a/build/Game.x86_64"));
            var window = OpenAtReferenceSize(scene);

            Press(Find(NativeMenu.GetMenu(window)!, "Export game…"));

            Assert.True(window.Export.Dialog.IsVisible);
            Assert.Equal(["Desktop"], window.Export.PresetBox.Items.Cast<string>());
        });
    }

    [Fact]
    public void About_Orblit_copies_the_version()
    {
        UiThread.Run(async () =>
        {
            var window = new EditorWindow(globalMenu: false);
            window.Show();

            Press(Find(NativeMenu.GetMenu(window)!, "About Orblit"));
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(BottomBar.Version, await window.Clipboard!.TryGetTextAsync());
        });
    }

    [Fact]
    public void Quit_Orblit_closes_the_window_on_a_platform_without_the_screen_bar()
    {
        UiThread.Run(() =>
        {
            var window = new EditorWindow(globalMenu: false);
            window.Show();
            var closed = false;
            window.Closed += (_, _) => closed = true;

            Press(Find(NativeMenu.GetMenu(window)!, "Quit Orblit"));

            Assert.True(closed);
        });
    }

    [Fact]
    public void A_dash_in_the_menu_is_a_separator_and_not_an_item_named_with_a_dash()
    {
        UiThread.Run(() =>
        {
            // A separator is an item whose header is a dash, so only its type tells it from a named item.
            var dashes = All(Build()).Where(item => item.Header == "-").ToList();

            Assert.NotEmpty(dashes);
            Assert.All(dashes, item => Assert.IsType<NativeMenuItemSeparator>(item));
        });
    }

    [Fact]
    public void A_submenu_header_opens_its_menu_and_runs_nothing()
    {
        UiThread.Run(() =>
        {
            var headers = All(Build(global: false)).Where(item => item.Menu is not null).ToList();

            Assert.Equal(["Orblit", "File", "Open recent", "Edit", "View", "Panels", "Play", "Show while playing", "Help"], headers.Select(item => item.Header));
            Assert.All(headers, item => Assert.Null(item.Command));
        });
    }

    [Fact]
    public void Open_recent_lists_the_scenes_opened_this_session_newest_first_and_opens_a_pick()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene { SceneFile = "res://field.oscn" };
            var window = OpenAtReferenceSize(scene);
            OpenScene(window, "res://levels/second.oscn");

            Assert.Equal(["second", "field", "-", "Clear the list"], Recent(window));
            Assert.Equal("levels/second.oscn", EditorMenu.NoteOf(Under(NativeMenu.GetMenu(window)!, "Open recent", "second")));

            Press(Under(NativeMenu.GetMenu(window)!, "Open recent", "field"));

            Assert.Equal("res://field.oscn", scene.SceneFile);
            Assert.Equal(["field", "second", "-", "Clear the list"], Recent(window));
        });
    }

    [Fact]
    public void Clear_the_list_empties_it_and_an_edit_does_not_refill_it()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize(new FakeScene { SceneFile = "res://field.oscn" });

            Press(Under(NativeMenu.GetMenu(window)!, "Open recent", "Clear the list"));
            MakeAnEdit(window);

            Assert.Equal(["Scenes you open show here"], Recent(window));
        });
    }

    [Theory]
    [InlineData("Scene", "Scene", 0)]
    [InlineData("Import", "Scene", 1)]
    [InlineData("Files", "Files", 0)]
    [InlineData("History", "Files", 1)]
    [InlineData("Inspector", "Inspector", 0)]
    [InlineData("Signals", "Inspector", 1)]
    public void A_Panels_check_hides_its_own_tab(string label, string dock, int tab)
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            Press(Panel(window, label));

            var docks = new[] { window.SceneDock, window.FileDock, window.InspectorDock };
            Assert.All(docks, each => Assert.Equal([each.Title != dock || tab != 0, each.Title != dock || tab != 1], each.Tabs.Select(border => border.IsVisible)));
            Assert.False(Panel(window, label).IsChecked);
        });
    }

    [Fact]
    public void A_Show_while_playing_check_flips_the_Game_tabs_switch_and_ticks_with_it()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var paths = Find(NativeMenu.GetMenu(window)!, "Paths");

            Press(paths);

            Assert.True(window.Game.SwitchOf("Visible Paths").IsChecked);
            Assert.True(paths.IsChecked);

            window.Game.SwitchOf("Visible Paths").IsChecked = false;

            Assert.False(paths.IsChecked);
        });
    }

    [Fact]
    public void A_dock_with_no_tab_shown_hides_and_comes_back_in_its_place()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var dock = window.SceneDock;

            Press(Panel(window, "Import"));
            Assert.True(dock.IsVisible);
            Press(Panel(window, "Scene"));
            Assert.False(dock.IsVisible);

            Press(Panel(window, "Import"));
            Assert.True(dock.IsVisible);
            Assert.Equal(1, dock.SelectedIndex);
            Assert.Equal(DockSlot.UpperLeft, dock.Slot);
            Assert.True(Panel(window, "Import").IsChecked);
        });
    }

    [Fact]
    public void All_keys_opens_a_sheet_of_the_menu_keys_and_the_keys_of_the_window_s_own_buttons()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            Press(Find(NativeMenu.GetMenu(window)!, "All keys"));

            Assert.True(window.Keys.Dialog.IsVisible);
            var key = (Key k) => Parts.KeyText(new KeyGesture(k, window.CommandKey));
            var rows = window.Keys.Rows.ToList();
            Assert.Contains(("Play", "Play the game", key(Key.B)), rows);
            Assert.Contains(("In the window", "3D", key(Key.D2)), rows);
            Assert.Single(rows, row => row.Key == key(Key.B));
        });
    }

    [Fact]
    public void A_question_mark_opens_the_keys_sheet_but_not_from_a_text_box()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            window.ScenePane.FilterBox.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.OemQuestion, KeyModifiers = KeyModifiers.Shift });
            Assert.False(window.Keys.Dialog.IsVisible);

            window.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.OemQuestion, KeyModifiers = KeyModifiers.Shift });
            Assert.True(window.Keys.Dialog.IsVisible);

            window.Keys.Dialog.MainButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Assert.False(window.Keys.Dialog.IsVisible);
        });
    }
}
