using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Reactive;
using Avalonia.VisualTree;

namespace Orblit.Editor.App.Shell;

/// <summary>
/// What a menu item does, and optionally the switch that says when it is on, the one that ticks it and the name
/// it takes in place of its label, such as Undo naming what it undoes.
/// </summary>
internal sealed record MenuAction(Action Run, IObservable<bool>? On = null, IObservable<bool>? Checked = null, IObservable<string>? Name = null);

/// <summary>
/// The menu bar: Orblit where the system has no app menu, then File, Edit, View, Play and Help. Each item has a
/// plain name, a note on what it does, its key and the engine's name where that differs. Only built items show.
/// </summary>
internal static class EditorMenu
{
    // Meta stands for the command key here. Build swaps in the platform's own command key.
    const KeyModifiers Cmd = KeyModifiers.Meta;
    const KeyModifiers CmdShift = KeyModifiers.Meta | KeyModifiers.Shift;

    // shortcut: the macOS key set only. Add the Windows and Linux overrides when the editor ships there.
    sealed record Entry(string Label, Key Key = Key.None, KeyModifiers Modifiers = KeyModifiers.None, bool? Check = null, bool Off = false, Entry[]? Items = null)
    {
        /// <summary>What the item does, or which thing it acts on.</summary>
        public string? Note { get; init; }

        /// <summary>What the note says while the item is off, so it tells what to do first.</summary>
        public string? WhyOff { get; init; }

        /// <summary>The engine's name for the item. The window knows the item by it too.</summary>
        public string? Engine { get; init; }
    }

    static readonly Entry Separator = new("-");

    internal const string OpenRecent = "Open recent";

    const string NoRecent = "Scenes you open show here";

    static readonly AttachedProperty<string?> NoteProperty = AvaloniaProperty.RegisterAttached<NativeMenuItem, string?>("Note", typeof(EditorMenu));

    static readonly AttachedProperty<string?> EngineProperty = AvaloniaProperty.RegisterAttached<NativeMenuItem, string?>("Engine", typeof(EditorMenu));

    /// <summary>The header of each item in the in-window bar: its name, the engine's name and its note.</summary>
    internal static readonly IDataTemplate BarHeader = new FuncDataTemplate<string>((name, _) => new BarLine(name));

    internal static string? NoteOf(NativeMenuItem item) => item.GetValue(NoteProperty);

    internal static string? EngineOf(NativeMenuItem item) => item.GetValue(EngineProperty);

    /// <summary>The note of the panel with this title, as the View menu gives it, so a dock's menu says the same.</summary>
    internal static string? PanelNote(string title) => Panels().FirstOrDefault(entry => (entry.Engine ?? entry.Label) == title)?.Note;

    /// <summary>
    /// Builds the bar. The screen bar of macOS sits beside the system's own app menu, so it leaves out the
    /// Orblit menu. An item runs the action that <paramref name="actions"/> holds for its engine name, or for
    /// its label when it has none.
    /// </summary>
    internal static NativeMenu Build(bool global, KeyModifiers command, IReadOnlyDictionary<string, MenuAction> actions)
    {
        NativeMenu Menu(IEnumerable<Entry> entries)
        {
            var menu = new NativeMenu();
            foreach (var entry in entries)
            {
                menu.Add(Convert(entry));
            }

            return menu;
        }

        NativeMenuItemBase Convert(Entry entry)
        {
            if (entry == Separator)
            {
                return new NativeMenuItemSeparator();
            }

            var item = new NativeMenuItem(entry.Label) { IsEnabled = !entry.Off };
            Describe(item, entry.Note, entry.Engine);
            if (entry.Items is { } children)
            {
                item.Menu = Menu(children);
                return item;
            }

            item.Gesture = Gesture(entry, command);
            if (entry.Check is { } on)
            {
                item.ToggleType = MenuItemToggleType.CheckBox;
                item.IsChecked = on;
            }

            // A command that can run would switch a dim item back on. The window gives every other item an
            // action, and a test walks its bar to hold it to that.
            if (!entry.Off && actions.TryGetValue(entry.Engine ?? entry.Label, out var action))
            {
                Wire(item, entry, action);
            }

            return item;
        }

        return Menu(Menus(global));
    }

    /// <summary>Each keyed item of the bar with its menu, in bar order, for the sheet of every key.</summary>
    internal static IEnumerable<(string Menu, string Name, string Key)> Keys(bool global, KeyModifiers command)
    {
        IEnumerable<(string, string, string)> Under(string menu, Entry entry) => entry.Items is { } items
            ? items.SelectMany(item => Under(menu, item))
            : Gesture(entry, command) is { } gesture ? [(menu, entry.Label, Parts.KeyText(gesture))] : [];

        return Menus(global).SelectMany(menu => menu.Items!.SelectMany(entry => Under(menu.Label, entry)));
    }

    /// <summary>
    /// Fills Open recent with these scenes, newest first, each noting its path. A pick opens its scene and Clear
    /// the list empties it. The menu keeps its items between calls, so the window calls this on a change.
    /// </summary>
    internal static void ShowRecent(NativeMenu bar, IReadOnlyList<string> scenes, Action<string> open, Action clear)
    {
        NativeMenu Under(NativeMenu menu, string header) => menu.Items.OfType<NativeMenuItem>().Single(item => item.Header == header).Menu!;

        var recent = Under(Under(bar, "File"), OpenRecent);
        recent.Items.Clear();
        if (scenes.Count == 0)
        {
            recent.Add(new NativeMenuItem(NoRecent) { IsEnabled = false });
            return;
        }

        foreach (var scene in scenes)
        {
            var item = new NativeMenuItem(Path.GetFileNameWithoutExtension(scene)) { Command = new MenuCommand(() => open(scene)) };
            Describe(item, scene.Replace("res://", ""), null);
            recent.Add(item);
        }

        recent.Add(new NativeMenuItemSeparator());
        recent.Add(new NativeMenuItem("Clear the list") { Command = new MenuCommand(clear) });
    }

    // The screen bar of macOS draws one line an item, so its tip carries the note and the engine's name.
    static void Describe(NativeMenuItem item, string? note, string? engine)
    {
        item.SetValue(NoteProperty, note);
        item.SetValue(EngineProperty, engine);
        item.ToolTip = string.Join("\n", new[] { note, engine }.OfType<string>()) is { Length: > 0 } tip ? tip : null;
    }

    static void Wire(NativeMenuItem item, Entry entry, MenuAction action)
    {
        item.Command = new MenuCommand(action.Run);
        if (action.On is { } on)
        {
            item.Bind(NativeMenuItem.IsEnabledProperty, on);
            on.Subscribe(new AnonymousObserver<bool>(enabled => Describe(item, enabled ? entry.Note : entry.WhyOff ?? entry.Note, entry.Engine)));
        }

        if (action.Checked is { } ticked)
        {
            item.Bind(NativeMenuItem.IsCheckedProperty, ticked);
        }

        if (action.Name is { } name)
        {
            item.Bind(NativeMenuItem.HeaderProperty, name);
        }
    }

    static KeyGesture? Gesture(Entry entry, KeyModifiers command) => entry.Key == Key.None
        ? null
        : new KeyGesture(entry.Key, (entry.Modifiers & Cmd) == 0 ? entry.Modifiers : (entry.Modifiers & ~Cmd) | command);

    static Entry[] Menus(bool global) =>
    [
        .. global ? [] : new[] { new Entry("Orblit", Items: Orblit()) },
        new("File", Items: File()),
        new("Edit", Items: Edit()),
        new("View", Items: View()),
        new("Play", Items: Play()),
        new("Help", Items: Help(global)),
    ];

    static Entry[] Orblit() =>
    [
        new("About Orblit") { Note = $"Version {BottomBar.Version}. Click to copy it for a bug report" },
        Separator,
        new("Quit Orblit", Key.Q, Cmd) { Note = "Asks first if anything is unsaved" },
    ];

    static Entry[] File() =>
    [
        new("New scene…", Key.N, Cmd) { Note = "Pick what it is for and where it goes" },
        new("Find a scene…", Key.O, CmdShift) { Note = "Type part of a name to open it", Engine = "Quick Open Scene…" },
        new(OpenRecent, Items: [new(NoRecent, Off: true)]),
        Separator,
        new("Save", Key.S, Cmd) { Note = "Saves the open scene", WhyOff = "Open a scene first." },
        Separator,
        new("Export game…") { Note = "Makes a game people can play on Mac, Windows or Linux" },
    ];

    static Entry[] Edit() =>
    [
        new("Undo", Key.Z, Cmd) { Note = "Every change can be undone. History lists them.", WhyOff = "Nothing to undo yet." },
        new("Redo", Key.Z, CmdShift) { Note = "Puts back the change you undid", WhyOff = "Nothing to redo yet." },
        Separator,
        new("Search", Key.K, Cmd) { Note = "Files, things, settings and actions in one place" },
    ];

    static Entry[] View() =>
    [
        new("Panels", Items: Panels()) { Note = "Show or hide a panel" },
        Separator,
        new("Hide panels", Key.OemPipe, Cmd) { Note = "Gives the view the whole window" },
        new("Full screen", Key.F, Cmd | KeyModifiers.Control),
    ];

    static Entry[] Panels() =>
    [
        new("Scene", Check: true) { Note = "Things in the open scene" },
        new("Import", Check: true) { Note = "How the picked file comes in" },
        new("Files", Check: true) { Note = "Every file in the project" },
        new("History", Check: true) { Note = "Every change, to undo back to" },
        new("Inspector", Check: true) { Note = "Settings for the picked thing" },
        new("Signals", Check: true) { Note = "Messages things send each other" },
    ];

    static Entry[] Play() =>
    [
        new("Play the game", Key.B, Cmd) { Note = "Starts the scene Play settings pick", WhyOff = "Open a scene first." },
        new("Play this scene", Key.R, Cmd) { Note = "Starts from the scene you are in", WhyOff = "Open a saved scene first." },
        new("Stop", Key.OemPeriod, Cmd) { Note = "Closes the game window", WhyOff = "Nothing is playing." },
        Separator,
        new("Show while playing", Items: ShowWhilePlaying()) { Note = "Draws hidden shapes and paths in the game" },
    ];

    static Entry[] ShowWhilePlaying() =>
    [
        new("Collision shapes", Check: false) { Note = "The shapes things bump into", Engine = "Visible Collision Shapes" },
        new("Paths", Check: false) { Note = "Lines that things follow", Engine = "Visible Paths" },
        new("Navigation", Check: false) { Note = "Where characters can walk", Engine = "Visible Navigation" },
        new("Avoidance", Check: false) { Note = "How characters steer round others", Engine = "Visible Avoidance" },
        Separator,
        new("2D redraws", Check: false) { Note = "Flashes each 2D thing as it redraws", Engine = "Debug CanvasItem Redraws" },
    ];

    // A plain key in the screen bar of macOS fires while a text box has the focus, so there the window keeps it.
    static Entry[] Help(bool global) =>
    [
        global
            ? new("All keys") { Note = "Every shortcut on one sheet. Press ? to open it" }
            : new("All keys", Key.OemQuestion, KeyModifiers.Shift) { Note = "Every shortcut on one sheet" },
    ];

    // The template gets only the header, so the line reads the item's note and engine name from its menu item
    // once it shows. A menu opens its items again each time, so a note that changed shows on the next open.
    sealed class BarLine(string name) : Decorator
    {
        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            var item = this.FindAncestorOfType<MenuItem>()?.DataContext as NativeMenuItem;
            Child = Parts.Noted(name, item is null ? null : NoteOf(item), item is null ? null : EngineOf(item));
        }
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
