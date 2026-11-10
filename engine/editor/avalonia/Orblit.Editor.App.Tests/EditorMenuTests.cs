using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Orblit.Editor.App.Shell;
using static Orblit.Editor.App.Tests.ReferenceWindow;

namespace Orblit.Editor.App.Tests;

public sealed class EditorMenuTests
{
    // The old editor's menus, read from its source. A submenu's items are indented and "-" is a separator.
    static readonly string[] SceneItems =
    [
        "New Scene", "New Inherited Scene...", "Open Scene...", "Reopen Closed Scene",
        "Open Recent", "  No Recent Scenes",
        "-", "Save Scene", "Save Scene As...", "Save All Scenes",
        "-", "Quick Open...", "Quick Open Scene...", "Quick Open Script...",
        "-", "Export As...", "  MeshLibrary...",
        "-", "Undo", "Redo",
        "-", "Reload Saved Scene", "Close Scene", "Close All Scenes",
    ];

    static readonly string[] ProjectItems =
    [
        "Project Settings...", "Find in Files...", "Replace in Files...",
        "-", "Version Control", "  Create/Override Version Control Metadata...", "  Version Control Settings...",
        "-", "Export...", "Pack Project as ZIP...", "Setup Android Build...", "Open User Data Folder",
        "-", "Tools", "  Orphan Resource Explorer...", "  Engine Compilation Configuration Editor...", "  Upgrade Project Files...", "  Clear Project Cache...",
        "-", "Reload Current Project", "Quit to Project List",
    ];

    static readonly string[] DebugItems =
    [
        "Deploy with Remote Debug", "Small Deploy with Network Filesystem",
        "-", "Visible Collision Shapes", "Visible Paths", "Visible Navigation", "Visible Avoidance",
        "-", "Debug CanvasItem Redraws",
        "-", "Synchronize Scene Changes", "Synchronize Script Changes", "Keep Debug Server Open",
        "-", "Customize Run Instances...",
    ];

    static readonly string[] EditorItems =
    [
        "Editor Settings...", "Command Palette...",
        "-", "Editor Docks", "  Scene", "  Import", "  FileSystem", "  History", "  Inspector", "  Signals",
        "Editor Layout", "  Save Layout...", "  Delete Layout...", "  -", "  Default",
        "-", "Take Screenshot", "Toggle Fullscreen",
        "-", "Open Editor Data Folder", "Open Editor Settings Folder",
        "-", "Manage Editor Features...", "Manage Export Templates...", "Configure FBX Importer...",
    ];

    static readonly string[] HelpItems =
    [
        "Search Help...",
        "-", "Online Documentation", "Forum", "Community",
        "-", "Copy System Info", "Report a Bug", "Suggest a Feature", "Send Docs Feedback",
        "-", "Support Orblit Development",
    ];

    static NativeMenu Build(bool global = true, KeyModifiers command = KeyModifiers.Meta, Dictionary<string, MenuAction>? actions = null, Action<string>? unbuilt = null) =>
        EditorMenu.Build(global, command, actions ?? [], unbuilt ?? (_ => { }));

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

    [Fact]
    public void The_bar_has_the_five_menus_of_the_old_editor_in_order()
    {
        UiThread.Run(() =>
        {
            var bar = Build();

            Assert.Equal(["Scene", "Project", "Debug", "Editor", "Help"], bar.Items.OfType<NativeMenuItem>().Select(item => item.Header));
            Assert.Equal(5, bar.Items.Count);
        });
    }

    [Fact]
    public void The_scene_menu_lists_the_old_items()
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
    public void The_project_menu_lists_the_old_items()
    {
        UiThread.Run(() =>
        {
            Assert.Equal(ProjectItems, Dump(Top(Build(), "Project")));
        });
    }

    [Fact]
    public void The_debug_menu_lists_the_old_items()
    {
        UiThread.Run(() =>
        {
            Assert.Equal(DebugItems, Dump(Top(Build(), "Debug")));
        });
    }

    [Fact]
    public void The_editor_menu_lists_the_old_items()
    {
        UiThread.Run(() =>
        {
            Assert.Equal(EditorItems, Dump(Top(Build(), "Editor")));
        });
    }

    [Fact]
    public void The_help_menu_lists_the_old_items()
    {
        UiThread.Run(() =>
        {
            Assert.Equal(HelpItems, Dump(Top(Build(), "Help")));
        });
    }

    [Fact]
    public void About_joins_the_help_menu_when_the_menu_is_not_the_screen_bar()
    {
        UiThread.Run(() =>
        {
            var items = Dump(Top(Build(global: false), "Help"));

            Assert.Equal(["-", "About Orblit...", "Support Orblit Development"], items[^3..]);
            Assert.Equal(HelpItems.Length + 1, items.Length);
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
                    "New Inherited Scene...=N+Shift, Meta",
                    "Open Scene...=O+Meta",
                    "Reopen Closed Scene=T+Shift, Meta",
                    "Save Scene=S+Meta",
                    "Save Scene As...=S+Shift, Meta",
                    "Save All Scenes=S+Alt, Shift, Meta",
                    "Quick Open...=O+Control, Meta",
                    "Quick Open Scene...=O+Shift, Meta",
                    "Quick Open Script...=O+Alt, Meta",
                    "Undo=Z+Meta",
                    "Redo=Z+Shift, Meta",
                    "Close Scene=W+Meta",
                    "Quit=Q+Meta",
                    "Project Settings...=OemComma+Shift, Meta",
                    "Find in Files...=F+Shift, Meta",
                    "Replace in Files...=R+Shift, Meta",
                    "Quit to Project List=Q+Alt, Control, Meta",
                    "Editor Settings...=OemComma+Meta",
                    "Command Palette...=P+Shift, Meta",
                    "Take Screenshot=F12+Meta",
                    "Toggle Fullscreen=F+Control, Meta",
                    "Search Help...=Space+Alt",
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
            Assert.Equal(KeyModifiers.Control | KeyModifiers.Alt, Find(bar, "Quit to Project List").Gesture!.KeyModifiers);
        });
    }

    [Fact]
    public void The_debug_options_are_check_items_and_the_two_sync_options_start_on()
    {
        UiThread.Run(() =>
        {
            var checks = All(Build())
                .Where(item => item.ToggleType == MenuItemToggleType.CheckBox)
                .Select(item => $"{item.Header}={item.IsChecked}");

            Assert.Equal(
                [
                    "Deploy with Remote Debug=False",
                    "Small Deploy with Network Filesystem=False",
                    "Visible Collision Shapes=False",
                    "Visible Paths=False",
                    "Visible Navigation=False",
                    "Visible Avoidance=False",
                    "Debug CanvasItem Redraws=False",
                    "Synchronize Scene Changes=True",
                    "Synchronize Script Changes=True",
                    "Keep Debug Server Open=False",
                ],
                checks);
        });
    }

    [Fact]
    public void The_empty_recent_list_is_dim_and_everything_else_is_live()
    {
        UiThread.Run(() =>
        {
            var off = All(Build()).Where(item => !item.IsEnabled).Select(item => item.Header);

            Assert.Equal(["No Recent Scenes"], off);
        });
    }

    [Fact]
    public void An_item_with_no_feature_answers_that_it_is_not_built()
    {
        UiThread.Run(() =>
        {
            var said = new List<string>();
            var bar = Build(unbuilt: said.Add);

            Press(Find(bar, "Open Scene..."));
            Press(Find(bar, "Engine Compilation Configuration Editor..."));
            Press(Find(bar, "Forum"));

            Assert.Equal(
                [
                    "Open Scene is not built yet.",
                    "Engine Compilation Configuration Editor is not built yet.",
                    "Forum is not built yet.",
                ],
                said);
        });
    }

    [Fact]
    public void A_wired_item_runs_its_action_and_says_nothing()
    {
        UiThread.Run(() =>
        {
            var said = new List<string>();
            var ran = 0;
            var bar = Build(actions: new() { ["Save Scene"] = new(() => ran++) }, unbuilt: said.Add);

            Press(Find(bar, "Save Scene"));

            Assert.Equal(1, ran);
            Assert.Empty(said);
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
            Assert.Equal(["Scene", "Project", "Debug", "Editor", "Help"], bar.Items.OfType<NativeMenuItem>().Select(item => item.Header));
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

    [Fact]
    public void A_menu_item_with_no_feature_shows_its_answer_in_the_status_bar()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            Press(Find(NativeMenu.GetMenu(window)!, "Command Palette..."));

            Assert.Equal("Command Palette is not built yet.", window.Status);
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
}
