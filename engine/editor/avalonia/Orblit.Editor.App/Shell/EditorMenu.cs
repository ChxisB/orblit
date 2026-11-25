using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Input;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Shell;

/// <summary>What a menu item does, and optionally the switch that says when it is on and the one that ticks it.</summary>
internal sealed record MenuAction(Action Run, IObservable<bool>? On = null, IObservable<bool>? Checked = null);

/// <summary>
/// The menu bar: Scene, Project, Debug and Editor, with the items the editor has built and their shortcuts.
/// Each item runs the action the window gives its label.
/// </summary>
internal static class EditorMenu
{
    // Meta stands for the command key here. Build swaps in the platform's own command key.
    const KeyModifiers Cmd = KeyModifiers.Meta;
    const KeyModifiers CmdShift = KeyModifiers.Meta | KeyModifiers.Shift;

    // shortcut: the macOS key set only. Add the Windows and Linux overrides when the editor ships there.
    sealed record Entry(string Label, Key Key = Key.None, KeyModifiers Modifiers = KeyModifiers.None, bool? Check = null, bool Off = false, Entry[]? Items = null);

    static readonly Entry Separator = new("-");

    const string OpenRecent = "Open Recent";

    const string NoRecent = "No Recent Scenes";

    /// <summary>
    /// Builds the bar. The screen bar of macOS has its own app menu, so it leaves out Quit. An item runs the
    /// action that <paramref name="actions"/> holds for its label.
    /// </summary>
    internal static NativeMenu Build(bool global, KeyModifiers command, IReadOnlyDictionary<string, MenuAction> actions)
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
            // The window gives every label an action, and a test walks its bar to hold it to that.
            if (!actions.TryGetValue(label, out var action))
            {
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
                item.ToggleType = MenuItemToggleType.CheckBox;
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
            new(OpenRecent, Items: [new(NoRecent, Off: true)]),
            Separator,
            new("Save Scene", Key.S, Cmd),
            Separator,
            new("Quick Open Scene...", Key.O, CmdShift),
            Separator,
            new("Undo", Key.Z, Cmd),
            new("Redo", Key.Z, CmdShift),
            .. quit,
        ];
    }

    static Entry[] Project() => [new("Export...")];

    static Entry[] Debug() =>
    [
        new("Visible Collision Shapes", Check: false),
        new("Visible Paths", Check: false),
        new("Visible Navigation", Check: false),
        new("Visible Avoidance", Check: false),
        Separator,
        new("Debug CanvasItem Redraws", Check: false),
    ];

    static Entry[] Editor() =>
    [
        new("Command Palette...", Key.P, CmdShift),
        Separator,
        new("Editor Docks", Items: [.. new[] { "Scene", "Import", "FileSystem", "History", "Inspector", "Signals" }.Select(dock => new Entry(dock, Check: true))]),
        Separator,
        new("Toggle Fullscreen", Key.F, Cmd | KeyModifiers.Control),
    ];

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
