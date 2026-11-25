using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Orblit.Editor.App.Shell;
using static Orblit.Editor.App.Tests.ReferenceWindow;

namespace Orblit.Editor.App.Tests;

public sealed class EditorMenuTests
{
    // The built items of each menu. A submenu's items are indented and "-" is a separator.
    static readonly string[] SceneItems =
    [
        "New Scene", "Open Recent", "  No Recent Scenes",
        "-", "Save Scene",
        "-", "Quick Open Scene...",
        "-", "Undo", "Redo",
    ];

    static readonly string[] DebugItems =
    [
        "Visible Collision Shapes", "Visible Paths", "Visible Navigation", "Visible Avoidance",
        "-", "Debug CanvasItem Redraws",
    ];

    static readonly string[] EditorItems =
    [
        "Command Palette...",
        "-", "Editor Docks", "  Scene", "  Import", "  FileSystem", "  History", "  Inspector", "  Signals",
        "-", "Toggle Fullscreen",
    ];

    static readonly string[] Menus = ["Scene", "Project", "Debug", "Editor"];

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

    static NativeMenuItem Find(NativeMenu bar, string label) => All(bar).Single(item => item.Header == label);

    static void Press(NativeMenuItem item) => item.Command!.Execute(item.CommandParameter);

    static NativeMenuItem Under(NativeMenu bar, string submenu, string label) =>
        Find(bar, submenu).Menu!.Items.OfType<NativeMenuItem>().Single(item => item.Header == label);

    static NativeMenuItem Docks(EditorWindow window, string label) => Under(NativeMenu.GetMenu(window)!, "Editor Docks", label);

    static string[] Recent(EditorWindow window) =>
        [.. Find(NativeMenu.GetMenu(window)!, "Open Recent").Menu!.Items.Select(entry => entry is NativeMenuItem item ? item.Header! : "-")];

    [Fact]
    public void The_bar_has_its_four_menus_in_order()
    {
        UiThread.Run(() =>
        {
            var bar = Build();

            Assert.Equal(Menus, bar.Items.OfType<NativeMenuItem>().Select(item => item.Header));
            Assert.Equal(4, bar.Items.Count);
        });
    }

    [Fact]
    public void The_scene_menu_lists_its_built_items()
    {
        UiThread.Run(() =>
        {
            Assert.Equal(SceneItems, Dump(Top(Build(), "Scene")));
        });
    }

    [Fact]
    public void Quit_joins_the_scene_menu_when_the_menu_is_not_the_screen_bar()
    {
        UiThread.Run(() =>
        {
            Assert.Equal([.. SceneItems, "-", "Quit"], Dump(Top(Build(global: false), "Scene")));
        });
    }

    [Fact]
    public void The_project_menu_holds_Export()
    {
        UiThread.Run(() =>
        {
            Assert.Equal(["Export..."], Dump(Top(Build(), "Project")));
        });
    }

    [Fact]
    public void The_debug_menu_lists_the_run_flags()
    {
        UiThread.Run(() =>
        {
            Assert.Equal(DebugItems, Dump(Top(Build(), "Debug")));
        });
    }

    [Fact]
    public void The_editor_menu_lists_its_built_items()
    {
        UiThread.Run(() =>
        {
            Assert.Equal(EditorItems, Dump(Top(Build(), "Editor")));
        });
    }

    [Fact]
    public void Shortcuts_match_the_old_editor_on_macOS()
    {
        UiThread.Run(() =>
        {
            var shortcuts = All(Build(global: false))
                .Where(item => item.Gesture is not null)
                .Select(item => $"{item.Header}={item.Gesture!.Key}+{item.Gesture.KeyModifiers}");

            Assert.Equal(
                [
                    "New Scene=N+Meta",
                    "Save Scene=S+Meta",
                    "Quick Open Scene...=O+Shift, Meta",
                    "Undo=Z+Meta",
                    "Redo=Z+Shift, Meta",
                    "Quit=Q+Meta",
                    "Command Palette...=P+Shift, Meta",
                    "Toggle Fullscreen=F+Control, Meta",
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

            Assert.Equal(KeyModifiers.Control, Find(bar, "Save Scene").Gesture!.KeyModifiers);
            Assert.Equal(KeyModifiers.Control | KeyModifiers.Shift, Find(bar, "Redo").Gesture!.KeyModifiers);
        });
    }

    [Fact]
    public void The_run_flags_start_off_and_the_docks_start_shown()
    {
        UiThread.Run(() =>
        {
            var checks = All(Build())
                .Where(item => item.ToggleType == MenuItemToggleType.CheckBox)
                .Select(item => $"{item.Header}={item.IsChecked}");

            Assert.Equal(
                [
                    "Visible Collision Shapes=False",
                    "Visible Paths=False",
                    "Visible Navigation=False",
                    "Visible Avoidance=False",
                    "Debug CanvasItem Redraws=False",
                    "Scene=True",
                    "Import=True",
                    "FileSystem=True",
                    "History=True",
                    "Inspector=True",
                    "Signals=True",
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

            Assert.Equal(["No Recent Scenes"], off);
        });
    }

    [Fact]
    public void A_wired_item_runs_its_action()
    {
        UiThread.Run(() =>
        {
            var ran = 0;
            var bar = Build(actions: new() { ["Save Scene"] = new(() => ran++) });

            Press(Find(bar, "Save Scene"));

            Assert.Equal(1, ran);
        });
    }

    [Fact]
    public void A_wired_item_is_on_only_while_its_switch_is()
    {
        UiThread.Run(() =>
        {
            var switchButton = new Button { IsEnabled = false };
            var bar = Build(actions: new() { ["Save Scene"] = new(() => { }, switchButton.GetObservable(InputElement.IsEnabledProperty)) });
            var save = Find(bar, "Save Scene");

            Assert.False(save.IsEnabled);
            switchButton.IsEnabled = true;
            Assert.True(save.IsEnabled);
        });
    }

    [Fact]
    public void The_window_carries_the_bar()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            var bar = NativeMenu.GetMenu(window)!;
            Assert.Equal(Menus, bar.Items.OfType<NativeMenuItem>().Select(item => item.Header));
        });
    }

    [Fact]
    public void Save_Scene_is_off_until_a_scene_is_open_and_then_saves_it()
    {
        UiThread.Run(() =>
        {
            var empty = new EditorWindow();
            Assert.False(Find(NativeMenu.GetMenu(empty)!, "Save Scene").IsEnabled);

            var scene = new FakeScene();
            var window = OpenAtReferenceSize(scene);
            var save = Find(NativeMenu.GetMenu(window)!, "Save Scene");
            Assert.True(save.IsEnabled);

            Press(save);

            Assert.Equal(1, scene.Saves);
            Assert.Equal("Saved.", window.Status);
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

            // The empty recent list holds only its dim heading until a scene opens.
            var idle = All(NativeMenu.GetMenu(window)!).Where(item => item.Menu is null && item is not NativeMenuItemSeparator && item.Command is null);

            Assert.Equal(["No Recent Scenes"], idle.Select(item => item.Header));
        });
    }

    [Fact]
    public void Toggle_Fullscreen_switches_the_window_in_and_out()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var item = Find(NativeMenu.GetMenu(window)!, "Toggle Fullscreen");
            var before = window.WindowState;

            Press(item);
            Assert.Equal(WindowState.FullScreen, window.WindowState);

            Press(item);
            Assert.Equal(before, window.WindowState);
        });
    }

    [Fact]
    public void Export_opens_the_export_dialog_with_the_project_s_presets()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            scene.ExportList.Add(new ExportPreset("Desktop", "Linux", "/games/a/build/Game.x86_64"));
            var window = OpenAtReferenceSize(scene);

            Press(Find(NativeMenu.GetMenu(window)!, "Export..."));

            Assert.True(window.Export.Dialog.IsVisible);
            Assert.Equal(["Desktop"], window.Export.PresetBox.Items.Cast<string>());
        });
    }

    [Fact]
    public void Quit_closes_the_window_on_a_platform_without_the_screen_bar()
    {
        UiThread.Run(() =>
        {
            var window = new EditorWindow(globalMenu: false);
            window.Show();
            var closed = false;
            window.Closed += (_, _) => closed = true;

            Press(Find(NativeMenu.GetMenu(window)!, "Quit"));

            Assert.True(closed);
        });
    }

    [Fact]
    public void The_screen_bar_has_no_quit_because_the_system_app_menu_has_it()
    {
        UiThread.Run(() =>
        {
            var window = new EditorWindow(globalMenu: true);

            Assert.DoesNotContain(All(NativeMenu.GetMenu(window)!), item => item.Header == "Quit");
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

            Assert.Contains(headers, item => item.Header == "Open Recent");
            Assert.All(headers, item => Assert.Null(item.Command));
        });
    }

    [Fact]
    public void The_window_takes_the_screen_bar_layout_on_macOS_only()
    {
        UiThread.Run(() =>
        {
            var window = new EditorWindow();

            Assert.Equal(!OperatingSystem.IsMacOS(), All(NativeMenu.GetMenu(window)!).Any(item => item.Header == "Quit"));
        });
    }

    [Fact]
    public void Open_Recent_lists_the_scenes_opened_this_session_newest_first_and_opens_a_pick()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene { SceneFile = "res://field.oscn" };
            var window = OpenAtReferenceSize(scene);
            OpenScene(window, "res://levels/second.oscn");

            Assert.Equal(["Recent", "second.oscn", "field.oscn", "-", "Clear Recent Scenes"], Recent(window));

            Press(Under(NativeMenu.GetMenu(window)!, "Open Recent", "field.oscn"));

            Assert.Equal("res://field.oscn", scene.SceneFile);
            Assert.Equal(["Recent", "field.oscn", "second.oscn", "-", "Clear Recent Scenes"], Recent(window));
        });
    }

    [Fact]
    public void Clear_Recent_Scenes_empties_the_list_and_an_edit_does_not_refill_it()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize(new FakeScene { SceneFile = "res://field.oscn" });

            Press(Under(NativeMenu.GetMenu(window)!, "Open Recent", "Clear Recent Scenes"));
            MakeAnEdit(window);

            Assert.Equal(["No Recent Scenes"], Recent(window));
        });
    }

    [Theory]
    [InlineData("Scene", "Scene", 0)]
    [InlineData("Import", "Scene", 1)]
    [InlineData("FileSystem", "FileSystem", 0)]
    [InlineData("History", "FileSystem", 1)]
    [InlineData("Inspector", "Inspector", 0)]
    [InlineData("Signals", "Inspector", 1)]
    public void An_Editor_Docks_check_hides_its_own_tab(string label, string dock, int tab)
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            Press(Docks(window, label));

            var docks = new[] { window.SceneDock, window.FileDock, window.InspectorDock };
            Assert.All(docks, each => Assert.Equal([each.Title != dock || tab != 0, each.Title != dock || tab != 1], each.Tabs.Select(border => border.IsVisible)));
            Assert.False(Docks(window, label).IsChecked);
        });
    }

    [Fact]
    public void A_run_flag_in_the_Debug_menu_flips_the_Game_tabs_switch_and_ticks_with_it()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var paths = Find(NativeMenu.GetMenu(window)!, "Visible Paths");

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

            Press(Docks(window, "Import"));
            Assert.True(dock.IsVisible);
            Press(Docks(window, "Scene"));
            Assert.False(dock.IsVisible);

            Press(Docks(window, "Import"));
            Assert.True(dock.IsVisible);
            Assert.Equal(1, dock.SelectedIndex);
            Assert.Equal(DockSlot.UpperLeft, dock.Slot);
            Assert.True(Docks(window, "Import").IsChecked);
        });
    }
}
