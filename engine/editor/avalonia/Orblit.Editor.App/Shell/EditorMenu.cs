using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Input;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Shell;

/// <summary>What a menu item does, and optionally the switch that says when it is on and the one that ticks it.</summary>
internal sealed record MenuAction(Action Run, IObservable<bool>? On = null, IObservable<bool>? Checked = null);

/// <summary>
/// The menu bar of the old editor: Scene, Project, Debug, Editor and Help, with its items, order and shortcuts.
/// An item with a feature runs its action. The others answer that they are not built.
/// </summary>
internal static class EditorMenu
{
    // Meta stands for the command key here. Build swaps in the platform's own command key.
    const KeyModifiers Cmd = KeyModifiers.Meta;
    const KeyModifiers CmdShift = KeyModifiers.Meta | KeyModifiers.Shift;

    // shortcut: the macOS key set only. Add the Windows and Linux overrides when the editor ships there.
    sealed record Entry(string Label, Key Key = Key.None, KeyModifiers Modifiers = KeyModifiers.None, bool? Check = null, bool Off = false, Entry[]? Items = null, MenuItemToggleType Toggle = MenuItemToggleType.CheckBox);

    static readonly Entry Separator = new("-");

    const string OpenRecent = "Open Recent";

    const string NoRecent = "No Recent Scenes";

    /// <summary>
    /// Builds the bar. The screen bar of macOS has its own app menu, so it leaves out the items the old editor
    /// moved there. An item whose label is in <paramref name="actions"/> runs it. Any other item sends the
    /// answer to <paramref name="unbuilt"/>.
    /// </summary>
    internal static NativeMenu Build(bool global, KeyModifiers command, IReadOnlyDictionary<string, MenuAction> actions, Action<string> unbuilt)
    {
        KeyModifiers Modifiers(KeyModifiers wanted) => (wanted & KeyModifiers.Meta) == 0 ? wanted : (wanted & ~KeyModifiers.Meta) | command;

        NativeMenu Menu(IEnumerable<Entry> entries)
        {
            var menu = new NativeMenu();
            foreach (var entry in entries)
            {
                menu.Add(Convert(entry));
            }

            return menu;
        }

        void Wire(NativeMenuItem item, string label)
        {
            if (!actions.TryGetValue(label, out var action))
            {
                item.Command = new MenuCommand(() => unbuilt($"{label.TrimEnd('.')} is not built yet."));
                EditorTheme.MarkSoon(item);
                return;
            }

            item.Command = new MenuCommand(action.Run);
            if (action.On is { } on)
            {
                item.Bind(NativeMenuItem.IsEnabledProperty, on);
            }

            if (action.Checked is { } ticked)
            {
                item.Bind(NativeMenuItem.IsCheckedProperty, ticked);
            }
        }

        NativeMenuItemBase Convert(Entry entry)
        {
            if (entry == Separator)
            {
                return new NativeMenuItemSeparator();
            }

            var item = new NativeMenuItem(entry.Label) { IsEnabled = !entry.Off };
            if (entry.Items is { } children)
            {
                item.Menu = Menu(children);
                return item;
            }

            if (entry.Key != Key.None)
            {
                item.Gesture = new KeyGesture(entry.Key, Modifiers(entry.Modifiers));
            }

            if (entry.Check is { } on)
            {
                item.ToggleType = entry.Toggle;
                item.IsChecked = on;
            }

            // A command that can run would switch a dim item back on.
            if (!entry.Off)
            {
                Wire(item, entry.Label);
            }

            return item;
        }

        return Menu([
            new("Scene", Items: Scene(global)),
            new("Project", Items: Project()),
            new("Debug", Items: Debug()),
            new("Editor", Items: Editor()),
            new("Help", Items: Help(global)),
        ]);
    }

    /// <summary>
    /// Fills Open Recent with these scenes, newest first, under a dim heading. A pick opens its scene and Clear
    /// Recent Scenes empties the list. The menu keeps its items between calls, so the window calls this on a change.
    /// </summary>
    internal static void ShowRecent(NativeMenu bar, IReadOnlyList<string> scenes, Action<string> open, Action clear)
    {
        NativeMenu Under(NativeMenu menu, string header) => menu.Items.OfType<NativeMenuItem>().Single(item => item.Header == header).Menu!;

        var recent = Under(Under(bar, "Scene"), OpenRecent);
        recent.Items.Clear();
        if (scenes.Count == 0)
        {
            recent.Add(new NativeMenuItem(NoRecent) { IsEnabled = false });
            return;
        }

        recent.Add(new NativeMenuItem("Recent") { IsEnabled = false });
        foreach (var scene in scenes)
        {
            recent.Add(new NativeMenuItem(Path.GetFileName(scene)) { Command = new MenuCommand(() => open(scene)) });
        }

        recent.Add(new NativeMenuItemSeparator());
        recent.Add(new NativeMenuItem("Clear Recent Scenes") { Command = new MenuCommand(clear) });
    }

    static Entry[] Scene(bool global)
    {
        Entry[] quit = global ? [] : [Separator, new("Quit", Key.Q, Cmd)];
        return
        [
            new("New Scene", Key.N, Cmd),
            new("New Inherited Scene...", Key.N, CmdShift),
            new("Open Scene...", Key.O, Cmd),
            new("Reopen Closed Scene", Key.T, CmdShift),
            new(OpenRecent, Items: [new(NoRecent, Off: true)]),
            Separator,
            new("Save Scene", Key.S, Cmd),
            new("Save Scene As...", Key.S, CmdShift),
            new("Save All Scenes", Key.S, CmdShift | KeyModifiers.Alt),
            Separator,
            new("Quick Open...", Key.O, Cmd | KeyModifiers.Control),
            new("Quick Open Scene...", Key.O, CmdShift),
            new("Quick Open Script...", Key.O, Cmd | KeyModifiers.Alt),
            Separator,
            new("Export As...", Items: [new("MeshLibrary...")]),
            Separator,
            new("Undo", Key.Z, Cmd),
            new("Redo", Key.Z, CmdShift),
            Separator,
            new("Reload Saved Scene"),
            new("Close Scene", Key.W, Cmd),
            new("Close All Scenes"),
            .. quit,
        ];
    }

    static Entry[] Project() =>
    [
        new("Project Settings...", Key.OemComma, CmdShift),
        new("Find in Files...", Key.F, CmdShift),
        new("Replace in Files...", Key.R, CmdShift),
        Separator,
        new("Version Control", Items: [new("Create/Override Version Control Metadata..."), new("Version Control Settings...")]),
        Separator,
        new("Export..."),
        new("Pack Project as ZIP..."),
        new("Setup Android Build..."),
        new("Open User Data Folder"),
        Separator,
        new("Tools", Items:
        [
            new("Orphan Resource Explorer..."),
            new("Engine Compilation Configuration Editor..."),
            new("Upgrade Project Files..."),
            new("Clear Project Cache..."),
        ]),
        Separator,
        new("Reload Current Project"),
        new("Quit to Project List", Key.Q, Cmd | KeyModifiers.Control | KeyModifiers.Alt),
    ];

    // shortcut: the two sync options start on by my reading of the old editor, not by a run of it.
    static Entry[] Debug() =>
    [
        new("Deploy with Remote Debug", Check: false),
        new("Small Deploy with Network Filesystem", Check: false),
        Separator,
        new("Visible Collision Shapes", Check: false),
        new("Visible Paths", Check: false),
        new("Visible Navigation", Check: false),
        new("Visible Avoidance", Check: false),
        Separator,
        new("Debug CanvasItem Redraws", Check: false),
        Separator,
        new("Synchronize Scene Changes", Check: true),
        new("Synchronize Script Changes", Check: true),
        new("Keep Debug Server Open", Check: false),
        Separator,
        new("Customize Run Instances..."),
    ];

    static Entry[] Editor() =>
    [
        new("Editor Settings...", Key.OemComma, Cmd),
        new("Command Palette...", Key.P, CmdShift),
        Separator,
        new("Editor Docks", Items: [.. new[] { "Scene", "Import", "FileSystem", "History", "Inspector", "Signals" }.Select(dock => new Entry(dock, Check: true))]),
        new("Editor Layout", Items:
        [
            new("Save Layout..."),
            new("Delete Layout..."),
            Separator,
            new("Layouts", Off: true),
            new("Default", Check: true, Toggle: MenuItemToggleType.Radio),
        ]),
        Separator,
        new("Take Screenshot", Key.F12, Cmd),
        new("Toggle Fullscreen", Key.F, Cmd | KeyModifiers.Control),
        Separator,
        new("Open Editor Data Folder"),
        new("Open Editor Settings Folder"),
        Separator,
        new("Manage Editor Features..."),
        new("Manage Export Templates..."),
        new("Configure FBX Importer..."),
    ];

    static Entry[] Help(bool global)
    {
        Entry[] about = global ? [] : [new("About Orblit...")];
        return
        [
            new("Search Help...", Key.Space, KeyModifiers.Alt),
            Separator,
            new("Online Documentation"),
            new("Forum"),
            new("Community"),
            Separator,
            new("Copy System Info"),
            new("Report a Bug"),
            new("Suggest a Feature"),
            new("Send Docs Feedback"),
            Separator,
            .. about,
            new("Support Orblit Development"),
        ];
    }

    // NativeMenuItem runs a command and has no way to raise its click from outside, so the tests press this.
    sealed class MenuCommand(Action run) : ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => run();
    }
}
