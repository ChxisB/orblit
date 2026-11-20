using System.Globalization;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Orblit.Editor.App.Shell;
using Orblit.Editor.App.Theme;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App;

/// <summary>
/// The editor shell: a title bar over three columns. The scene outline and the project's scenes are on the left,
/// the viewport in the middle and the inspector on the right.
/// </summary>
internal sealed class EditorWindow : Window
{
    // The engine's log is read by polling a file, and a quarter of a second is fast enough to read a line as it comes.
    // The same tick notices a game that ended on its own.
    static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(250);

    readonly InspectorPanel _inspector = new();
    readonly ContentControl _viewport = new()
    {
        Content = new TextBlock { Margin = new Thickness(8), TextWrapping = Avalonia.Media.TextWrapping.Wrap, Text = "No viewport. The editor was started with --headless." },
    };
    readonly EntityTools _tools = new();
    readonly MoveTools _move = new();
    readonly NewSceneTool _newScene = new();
    readonly InstanceTool _instanceScene = new();
    readonly TextBlock _changesLine = new() { FontSize = 12, Foreground = EditorBrushes.TextSoft, VerticalAlignment = VerticalAlignment.Center };
    readonly Dialog _saveChanges;
    readonly ScenesPanel _scenes;
    readonly TimeProvider _time;
    readonly ToastStack _toasts;
    readonly List<string> _recent = [];
    readonly Dictionary<string, DateTimeOffset> _openedAt = [];
    readonly Picker _palette = CommandPalette.Build();
    readonly Picker _quickOpen;
    readonly Button _quickOpenButton = new();
    // Save and Frame are commands without a place of their own on screen. The shortcuts, the menus and the
    // tests press them, and a button off keeps its key unhandled.
    readonly Button _save = new() { IsEnabled = false };
    readonly Button _playButton = Parts.IconButton("MainPlay", "Run the project");
    readonly Button _stopButton = Parts.IconButton("Stop", "Stop the game");
    readonly Button _frame = new() { IsEnabled = false };
    readonly SceneTabStrip _sceneTabs = new();
    readonly ViewportToolbar _toolbar;
    readonly TitleBar _titleBar;
    readonly BottomBar _bottomBar = new();
    readonly DispatcherTimer _pollTimer = new() { Interval = PollInterval };
    readonly ScenePane _scenePane;
    readonly DockTabs _sceneDock;
    readonly DockTabs _fileDock;
    readonly InspectorPane _inspectorPane;
    readonly DockTabs _inspectorDock;
    readonly DockSide _leftSide;
    readonly DockSide _rightSide;
    readonly CenterArea _center;
    readonly StoreView _store;
    readonly ScriptView _script = new();
    readonly GraphView _graph = new();
    readonly GameView _game = new();
    readonly Dictionary<DockSlot, Border> _slots = Enum.GetValues<DockSlot>().ToDictionary(slot => slot, _ => new Border());
    readonly Dictionary<DockTabs, DockSlot> _homes = [];

    // The sides a workspace folds to their rails, by workspace. A workspace not listed keeps both open.
    static readonly Dictionary<string, (bool Left, bool Right)> s_folds = new()
    {
        [TitleBar.Script] = (true, true),
        [TitleBar.Graph] = (true, true),
        [TitleBar.Game] = (true, true),
        [TitleBar.Store] = (true, true),
    };
    IEditorScene? _scene;
    OutlineNode? _root;
    bool _edited;
    // The edits since the scene opened or was last saved, and when that was, for the Save changes dialog.
    int _edits;
    DateTimeOffset _since;
    // When the game last started, for the time the Game view shows.
    DateTimeOffset _playedAt;
    bool _planar;
    // The Entity3D the gizmo drags, or zero when the selection has none.
    nint _gizmoTarget;

    // The screen bar of macOS has its own app menu, so a window on any other system also carries Quit and About.
    public EditorWindow(bool? globalMenu = null, Func<CancellationToken, Task<string>>? readStore = null, TimeProvider? time = null)
    {
        // 16 in from the right, and 10 clear of the bottom bar, which sits on the body's 6 pixel margin.
        _time = time ?? TimeProvider.System;
        _toasts = new ToastStack(_time) { Margin = new Thickness(0, 0, 16, CenterArea.BottomBarHeight + 16) };
        _saveChanges = SaveChangesDialog();
        _store = new StoreView(readStore ?? new KitIndexSource().ReadAsync);
        _titleBar = new TitleBar(_playButton, _stopButton, CommandKey);
        _titleBar.SearchButton.Click += (_, _) => OpenPalette();
        _quickOpen = QuickOpen.Build(CommandKey);
        _quickOpenButton.Click += (_, _) => OpenQuickOpen();
        ShowTitle();
        Icon = EditorIcon.ToWindowIcon("Orblit", 64);
        Width = 1440;
        Height = 900;
        ExtendClientAreaToDecorationsHint = true;
        ExtendClientAreaTitleBarHeightHint = TitleBar.BarHeight;
        _scenePane = new ScenePane(_tools, _move, _instanceScene, CommandKey);
        _scenes = new ScenesPanel(CommandKey);
        _scenePane.SelectionChanged += ShowSelection;
        _scenePane.EyeClicked += ToggleEye;
        _tools.Changed += Edited;
        _tools.Failed += Fail;
        _move.Changed += Edited;
        _move.Failed += Fail;
        _instanceScene.Changed += Edited;
        _instanceScene.Failed += Fail;
        _inspector.Edited += () =>
        {
            ShowEdited(true);
            HighlightSelected();
            Say("Edited. Save keeps the change.");
        };
        _inspector.Failed += Fail;
        _inspector.Editing += OpenFile;
        _scenes.Chosen += OpenFile;
        _scenes.NewGraphRequested += () =>
        {
            _titleBar.Choose(TitleBar.Graph);
            _graph.ShowCreate();
        };
        WireSceneFiles();
        _playButton.IsEnabled = false;
        _stopButton.IsEnabled = false;
        _save.Click += (_, _) => SaveAll();
        _playButton.Click += (_, _) => Play();
        _stopButton.Click += (_, _) => Stop();
        _game.RunRequested += Play;
        _game.StopRequested += Stop;
        _frame.Click += (_, _) => Frame();
        AddHandler(Unbuilt.Event, (_, e) => ShowUnbuilt(e.Message));
        _sceneDock = new DockTabs(("Scene", _scenePane), ("Import", Parts.EmptyState("ImportArrow", "Nothing to import", "Select a model, image or sound in FileSystem to set how it imports."))) { Slot = DockSlot.UpperLeft, Icon = "SceneFile" };
        _fileDock = new DockTabs(("FileSystem", _scenes), ("History", Parts.EmptyState("Clock", "Nothing to undo yet", "Undo is not built yet. Its steps will be listed here."))) { Slot = DockSlot.LowerLeft, Icon = "Filesystem" };
        _inspectorPane = new InspectorPane(_inspector);
        _inspectorPane.Picked += _scenePane.Select;
        _inspectorDock = new DockTabs(("Inspector", _inspectorPane), ("Signals", Parts.EmptyState("Pulse", "Signals are not built yet", "The signals of the selected entity and where they go will show here."))) { Slot = DockSlot.Right, Icon = "ListSelect" };
        WireDocks();
        _leftSide = new DockSide(LeftColumn(), () => DocksIn(DockSlot.UpperLeft, DockSlot.LowerLeft));
        _rightSide = new DockSide(_slots[DockSlot.Right], () => DocksIn(DockSlot.Right));
        _toolbar = new ViewportToolbar(_frame);
        _toolbar.LockButton.Click += (_, _) => ToggleFlag(_scene!.SetLocked, selected => selected.Locked, "Locked", "Unlocked");
        _toolbar.GroupButton.Click += (_, _) => ToggleFlag(_scene!.SetGrouped, selected => selected.Grouped, "Grouped", "Ungrouped");
        _toolbar.KeepChildrenButton.IsCheckedChanged += (_, _) => _scene?.KeepChildren = _toolbar.KeepChildren;
        _center = new CenterArea(_sceneTabs, _toolbar, _viewport, _bottomBar);
        _center.Output.ClearRequested += () => _scene?.ClearOutput();
        _center.Output.CopyRequested += text => Copy(text, "the output");
        _center.Debugger.RunRequested += Play;
        _bottomBar.VersionCopyRequested += () => Copy(BottomBar.Version, BottomBar.Version);
        _center.Planar.SwitchButton.Click += (_, _) => _titleBar.Choose(TitleBar.Spatial);
        _center.Planar.OpenButton.Click += (_, _) => OpenQuickOpen();
        _center.AddBareWorkspace(TitleBar.Script, _script);
        _center.AddBareWorkspace(TitleBar.Graph, _graph);
        _center.AddBareWorkspace(TitleBar.Game, _game);
        _center.AddBareWorkspace(TitleBar.Store, _store);
        _titleBar.WorkspaceChosen += ChooseWorkspace;
        _pollTimer.Tick += (_, _) => Poll();
        Closed += (_, _) =>
        {
            _pollTimer.Stop();
            _toasts.Close();
        };
        WireMenus();
        // The toasts go over the veil, so Try again on a failed save stays in reach while the dialog asks.
        Content = new Panel { Children = { Layout(), _newScene.Dialog, _graph.CreateDialog, _tools.AddDialog, _instanceScene.Dialog, _move.Dialog, _saveChanges, _palette, _quickOpen, _toasts } };
        NativeMenu.SetMenu(this, EditorMenu.Build(globalMenu ?? OperatingSystem.IsMacOS(), CommandKey, MenuActions(), ShowUnbuilt));
    }

    internal TitleBar Bar => _titleBar;

    internal DockTabs SceneDock => _sceneDock;

    internal DockTabs FileDock => _fileDock;

    internal DockTabs InspectorDock => _inspectorDock;

    internal DockSide LeftSide => _leftSide;

    internal DockSide RightSide => _rightSide;

    internal CenterArea Center => _center;

    internal StoreView Store => _store;

    internal ScriptView Script => _script;

    internal GraphView Graph => _graph;

    internal SceneTabStrip SceneTabs => _sceneTabs;

    internal ViewportToolbar Toolbar => _toolbar;

    internal ScenePane ScenePane => _scenePane;

    internal InspectorPane InspectorPane => _inspectorPane;

    internal TreeView Outline => _scenePane.Outline;

    internal InspectorPanel Inspector => _inspector;

    internal EntityTools Tools => _tools;

    internal MoveTools Move => _move;

    internal NewSceneTool NewScene => _newScene;

    internal InstanceTool InstanceScene => _instanceScene;

    internal Dialog SaveChanges => _saveChanges;

    internal TextBlock ChangesLine => _changesLine;

    internal ContentControl Viewport => _viewport;

    internal ScenesPanel Scenes => _scenes;

    internal Button SaveButton => _save;

    internal Button PlayButton => _playButton;

    internal Button StopButton => _stopButton;

    internal GameView Game => _game;

    internal Button FrameButton => _frame;

    internal string Status => _bottomBar.Status;

    internal ToastStack Toasts => _toasts;

    internal Picker Palette => _palette;

    internal Picker QuickOpener => _quickOpen;

    /// <summary>The modifier that makes a shortcut: Command on macOS, Control elsewhere.</summary>
    internal KeyModifiers CommandKey => this.GetPlatformSettings()?.HotkeyConfiguration.CommandModifiers ?? KeyModifiers.Control;

    public void Load(IEditorScene scene)
    {
        _scene = scene;
        // The button can be pressed while the engine starts, before there is a scene to tell.
        scene.KeepChildren = _toolbar.KeepChildren;
        _script.Show(scene);
        _graph.Show(scene);
        _center.Output.Show(scene.Output, _time);
        _pollTimer.Start();
        ShowScenes();
        Reload(0);
        Remember(scene.SceneFile);
        _since = _time.GetLocalNow();
        Say(OpenedStatus());
    }

    public void Fail(string message) => _bottomBar.Say(message, StatusKind.Failed);

    void Say(string message, StatusKind kind = StatusKind.Info) => _bottomBar.Say(message, kind);

    async void Copy(string text, string what)
    {
        if (Clipboard is not { } clipboard)
        {
            return;
        }

        // An async void that throws ends the editor, and each platform's clipboard fails with its own type.
        try
        {
            await clipboard.SetTextAsync(text);
            Say($"Copied {what}.", StatusKind.Done);
        }
        catch (Exception error)
        {
            Fail($"Could not copy {what}: {error.Message}");
        }
    }

    // The status line keeps the answer after the toast goes. A toast title has no full stop.
    void ShowUnbuilt(string message)
    {
        Say(message);
        _toasts.Show(ToastKind.Unbuilt, message.TrimEnd('.'));
    }

    /// <summary>Reads the lines the engine logged since the last call and shows whether the game still runs.</summary>
    internal void Poll()
    {
        _scene?.PollOutput();
        _graph.PollDebugger();
        ShowGame();
    }

    public void ShowViewport(ViewportControl viewport)
    {
        viewport.Ended += () => Fail("The engine window was closed. Restart the editor to see the scene.");
        viewport.Clicked += SelectAt;
        viewport.PlanarSource = () => _planar;
        viewport.MarkSource = PivotOfSelected;
        viewport.PlanarMoved += MoveSelected;
        viewport.ModeSource = () => _toolbar.Mode;
        viewport.GripAnywhereSource = () => _center.Planar.MovesAnywhere;
        viewport.Zoomed += _ => _center.Planar.Refresh();
        viewport.PlanarPanned += _ => _center.Planar.Refresh();
        viewport.TrackballSource = () => _toolbar.Trackball;
        viewport.HandlesSource = () => _gizmoTarget == 0 ? null : _scene?.HandlesOf(_gizmoTarget, _toolbar.Space);
        viewport.RingsSource = () => _gizmoTarget == 0 ? null : _scene?.RingsOf(_gizmoTarget, _toolbar.Space);
        viewport.GizmoDragged += DragSelected;
        viewport.GizmoReleased += () => _scene?.EndDrag();
        viewport.Orbited += _center.Overlay.Orbit;
        _center.Overlay.IsVisible = true;
        _viewport.Content = viewport;
    }

    // Reads the outline again and selects the entity with this handle. Zero selects nothing.
    void Reload(nint select)
    {
        _root = _scene!.Outline();
        _scenePane.Show(_root, select);
        _scenes.MarkOpen(_scene!.SceneFile);
        _save.IsEnabled = _root is not null;
        ShowGame();
        _sceneTabs.Show(_root, SceneLabel());
        _center.Planar.ShowScene(_scene!, _root, SceneName());
        ShowTitle();
        ShowSelection();
    }

    // The 2D view is on for the 2D workspace alone, so any other workspace gives the viewport back to 3D.
    void ChooseWorkspace(string name)
    {
        var (left, right) = s_folds.GetValueOrDefault(name);
        _leftSide.Fold(left);
        _rightSide.Fold(right);
        _center.ShowWorkspace(name);
        _planar = name == TitleBar.Planar;
        _center.Overlay.Planar = _planar;
        _scene?.ShowPlanar(_planar);
    }

    // A click on empty space selects nothing.
    void SelectAt(Vector2 framePoint)
    {
        var picked = _planar ? _scene!.PickPlanarAt(framePoint) : _scene!.PickAt(framePoint);
        _scenePane.Select(picked);
    }

    // The selected Entity2D has a mark unless it is locked, as a locked Entity3D has no handles.
    Vector2? PivotOfSelected() =>
        _scenePane.Selected is { Locked: false } selected ? _scene?.PlanarPivotOf(selected.Handle) : null;

    // A press on the mark of the last tick can land after the selection moved on.
    void MoveSelected(FrameMove move)
    {
        if (PivotOfSelected() is null)
        {
            return;
        }

        _scene!.MovePlanar(_scenePane.Selected!.Handle, move);
        ShowDragged("Moved");
    }

    void ShowSelection()
    {
        var selected = _scenePane.Selected;
        _inspectorPane.Show(_scene, selected);
        _tools.Show(_scene, _root, selected);
        _move.Show(_scene, _root, selected);
        _instanceScene.Show(_scene, _root, selected);
        var spatial = selected is not null && _scene is not null && _scene.IsEntity3D(selected.Handle);
        // A locked entity keeps no handles, but it can still be framed.
        _gizmoTarget = spatial && selected?.Locked == false ? selected.Handle : 0;
        _frame.IsEnabled = spatial;
        _toolbar.Select(selected);
        HighlightSelected();
    }

    // A drag of a gizmo part moves, stretches or turns the entity. The inspector reads the new value, and the
    // dragged entity stays selected, so the tree is not read again.
    void DragSelected(GizmoDrag drag)
    {
        // A press on the gizmo of the last tick can land after the selection or the mode moved on.
        if (_gizmoTarget == 0 || !GizmoZones.Offers(_toolbar.Mode, drag.Hit.Part))
        {
            return;
        }

        // The snap key flips the Snap button for the drag it is held in.
        _scene!.Snap = _toolbar.Snaps != drag.Inverts ? _toolbar.Steps : null;
        Apply(drag);
        ShowDragged(Verb(drag.Hit.Part));
    }

    void Apply(GizmoDrag drag)
    {
        var axis = drag.Hit.Axis;
        var direction = new GizmoDirection(axis, _toolbar.Space);
        switch (drag.Hit.Part)
        {
            case GizmoPart.Move:
                _scene!.Drag(_gizmoTarget, direction, drag.Move);
                break;
            case GizmoPart.Stretch:
                _scene!.ScaleDrag(_gizmoTarget, axis, drag.Move);
                break;
            case GizmoPart.StretchAll:
                _scene!.ScaleAllDrag(_gizmoTarget, drag.Move);
                break;
            case GizmoPart.Turn:
                _scene!.RotateDrag(_gizmoTarget, direction, drag.Move);
                break;
            case GizmoPart.Spin:
                _scene!.SpinDrag(_gizmoTarget, drag.Move);
                break;
            default:
                _scene!.TrackballDrag(_gizmoTarget, drag.Move);
                break;
        }
    }

    static string Verb(GizmoPart part) => part switch
    {
        GizmoPart.Move => "Moved",
        GizmoPart.Stretch or GizmoPart.StretchAll => "Scaled",
        _ => "Rotated",
    };

    void ShowDragged(string verb)
    {
        var selected = _scenePane.Selected!;
        _inspectorPane.Show(_scene, selected);
        HighlightSelected();
        ShowEdited(true);
        Say($"{verb} {selected.Name}. Save keeps the change.");
    }

    // The box is drawn from the entity's place and size, so an edit in the inspector moves it too.
    void HighlightSelected() => _scene?.Highlight(_scenePane.Selected?.Handle ?? 0);

    // Closing drops the scene and there is no undo, so edits that no save holds ask first.
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
        if (HasEdits)
        {
            e.Cancel = true;
            AskToSave();
        }
    }

    // Closing without saving drops the edits, so that action sits at the far left, away from Save and close.
    Dialog SaveChangesDialog()
    {
        var warn = new EditorIcon("WarnTriangle") { Width = 15, Height = 15, Margin = new Thickness(0, 0, 10, 0) };
        TextElement.SetForeground(warn, EditorBrushes.Warn);
        var drop = new Button { Content = "Close without saving", Classes = { EditorTheme.TextButton, EditorTheme.Danger } };
        var body = new StackPanel { Orientation = Orientation.Horizontal, Children = { warn, _changesLine } };
        var dialog = new Dialog("", "Closing without saving drops them.", body)
        {
            MainText = "Save and close",
            CancelText = "Keep editing",
            Closable = false,
            CardWidth = 400,
            LeadButton = drop,
        };
        // A failed save leaves the edits, so the close asks again.
        dialog.MainButton.Click += (_, _) =>
        {
            SaveEdits();
            Close();
        };
        drop.Click += (_, _) =>
        {
            _script.Discard();
            _graph.Discard();
            ShowEdited(false);
            Close();
        };
        return dialog;
    }

    // An open script that was typed in counts as one change, as the scene does not count its keys.
    void AskToSave()
    {
        _saveChanges.Title = $"Save changes to {(_edited ? SceneName() : "the open scripts")}?";
        var count = _edits + (_script.IsDirty ? 1 : 0) + (_graph.IsDirty ? 1 : 0);
        _changesLine.Text = $"{count} {(count == 1 ? "change" : "changes")} since {_since.ToString("HH:mm", CultureInfo.InvariantCulture)}.";
        _saveChanges.Open();
    }

    // Save is on while the Save button is. Quit and fullscreen are the other menu items the editor can do.
    // Each Editor Docks item shows and hides its tab.
    Dictionary<string, MenuAction> MenuActions()
    {
        var actions = new Dictionary<string, MenuAction>
        {
            ["New Scene"] = new(OpenNewScene),
            ["Command Palette..."] = new(OpenPalette),
            ["Quick Open Scene..."] = new(OpenQuickOpen),
            ["Save Scene"] = new(() => _save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)), _save.GetObservable(IsEnabledProperty)),
            ["Quit"] = new(Close),
            ["Toggle Fullscreen"] = new(() => WindowState = WindowState == WindowState.FullScreen ? WindowState.Normal : WindowState.FullScreen),
        };
        foreach (var dock in new[] { _sceneDock, _fileDock, _inspectorDock })
        {
            foreach (var title in dock.Titles)
            {
                actions[title] = new(() => dock.ToggleTab(title), Checked: dock.Shown(title));
            }
        }

        return actions;
    }

    // A shortcut presses the button it stands for, so a key and a click run the same code. A button that is
    // off leaves the key unhandled.
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        // An open dialog or picker holds the keys, so nothing it hides acts behind it.
        if (e.Source is Visual source && source.GetSelfAndVisualAncestors().Any(visual => visual is Dialog or Picker))
        {
            return;
        }

        if (ShortcutFor(e) is { IsEnabled: true } button)
        {
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            e.Handled = true;
        }
    }

    // Plain letters, the arrows, select all and the erase keys belong to a box that takes typing.
    Button? ShortcutFor(KeyEventArgs e) => (e.Key, e.KeyModifiers) switch
    {
        (Key.N, var modifiers) when modifiers == CommandKey => _sceneTabs.AddButton,
        (Key.S, var modifiers) when modifiers == CommandKey => _save,
        (Key.B, var modifiers) when modifiers == CommandKey => _playButton,
        (Key.D, var modifiers) when modifiers == CommandKey => _tools.DuplicateButton,
        (Key.A, var modifiers) when modifiers == CommandKey && e.Source is not TextBox => _tools.AddButton,
        (Key.A, var modifiers) when modifiers == (CommandKey | KeyModifiers.Shift) => _instanceScene.OpenButton,
        (Key.Up, var modifiers) when modifiers == CommandKey && e.Source is not TextBox => _move.UpButton,
        (Key.Down, var modifiers) when modifiers == CommandKey && e.Source is not TextBox => _move.DownButton,
        (Key.F2, KeyModifiers.None) when e.Source is not TextBox => _tools.RenameButton,
        (Key.Back or Key.Delete, KeyModifiers.None) when e.Source is not TextBox && Erases(e.Source) => _tools.RemoveButton,
        (Key.K, var modifiers) when modifiers == CommandKey => _titleBar.SearchButton,
        (Key.P, var modifiers) when modifiers == (CommandKey | KeyModifiers.Shift) => _titleBar.SearchButton,
        (Key.O, var modifiers) when modifiers == (CommandKey | KeyModifiers.Shift) => _quickOpenButton,
        (Key.F, KeyModifiers.None) when e.Source is not (TextBox or ComboBox) => _frame,
        (Key.Q, KeyModifiers.None) when e.Source is not (TextBox or ComboBox) => _toolbar.SelectButton,
        (Key.W, KeyModifiers.None) when e.Source is not (TextBox or ComboBox) => _toolbar.MoveButton,
        (Key.E, KeyModifiers.None) when e.Source is not (TextBox or ComboBox) => _toolbar.RotateButton,
        (Key.R, KeyModifiers.None) when e.Source is not (TextBox or ComboBox) => _toolbar.ScaleButton,
        (Key.R, KeyModifiers.Alt) when e.Source is not (TextBox or ComboBox) => _toolbar.ResetAllButton,
        (Key.T, KeyModifiers.None) when e.Source is not (TextBox or ComboBox) => _toolbar.LocalButton,
        _ => null,
    };

    // Erase drops an entity with no undo, so it acts only from the window, the scene pane or the viewport while
    // the scene shows.
    bool Erases(object? source) =>
        _scenePane.IsEffectivelyVisible
        && (source == this || source is Visual visual && (_scenePane.IsVisualAncestorOf(visual) || _viewport.IsVisualAncestorOf(visual)));

    // A tool changed the scene: read it again and show what it did.
    void Edited(EntityEdit edit)
    {
        Reload(edit.Select);
        ShowEdited(true);
        Say(edit.Message);
    }

    // The buttons show the flags the engine holds, so a press flips the flag and the tree is read again.
    void ToggleFlag(Action<nint, bool> set, Func<OutlineNode, bool> held, string on, string off)
    {
        var selected = _scenePane.Selected!;
        var was = held(selected);
        Change(() =>
        {
            set(selected.Handle, !was);
            return new EntityEdit(selected.Handle, $"{(was ? off : on)} {selected.Name}. Save keeps the change.");
        });
    }

    // The eye of a row hides or shows that row's entity. The selection stays where it was.
    void ToggleEye(OutlineNode node) => Change(() =>
    {
        _scene!.SetHidden(node.Handle, !node.Hidden);
        return new EntityEdit(_scenePane.Selected?.Handle ?? 0, $"{(node.Hidden ? "Showed" : "Hid")} {node.Name}. Save keeps the change.");
    });

    void Change(Func<EntityEdit> change)
    {
        try
        {
            Edited(change());
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException)
        {
            Fail(error.Message);
        }
    }

    // The docks sit in three slots. A dock sent to a slot trades places with the dock that sits there. The slot each
    // starts in is its home, where Reset Layout puts it back.
    void WireDocks()
    {
        foreach (var dock in new[] { _sceneDock, _fileDock, _inspectorDock })
        {
            _slots[dock.Slot].Child = dock;
            _homes[dock] = dock.Slot;
            dock.MoveRequested += to => MoveDock(dock, to);
            dock.ResetRequested += ResetLayout;
        }
    }

    void ResetLayout()
    {
        foreach (var slot in _slots.Values)
        {
            slot.Child = null;
        }

        foreach (var (dock, home) in _homes)
        {
            _slots[home].Child = dock;
            dock.Slot = home;
            dock.ShowAllTabs();
        }

        Say("Reset the layout. Every dock is back in its place with all its tabs.");
    }

    void MoveDock(DockTabs dock, DockSlot to)
    {
        var from = dock.Slot;
        var other = (DockTabs)_slots[to].Child!;
        _slots[from].Child = null;
        _slots[to].Child = null;
        _slots[to].Child = dock;
        _slots[from].Child = other;
        dock.Slot = to;
        other.Slot = from;
        Say($"Moved {dock.Title} to the {to.Label()} slot. The layout is not saved.");
    }

    // The Reset menu and the snap actions change the selected entity's place, the Collision menu gives a mesh a
    // collision shape, and the projection menu switches the viewport's camera.
    void WireMenus()
    {
        _toolbar.ResetPositionItem.Click += (_, _) => Transform("Reset the position of", entity => _scene!.MoveTo(entity, Vector3.Zero));
        _toolbar.ResetRotationItem.Click += (_, _) => Transform("Reset the rotation of", entity => _scene!.RotateTo(entity, Vector3.Zero));
        _toolbar.ResetScaleItem.Click += (_, _) => Transform("Reset the scale of", entity => _scene!.ScaleTo(entity, Vector3.One));
        _toolbar.ResetAllButton.Click += (_, _) => Transform("Reset the transform of", entity =>
        {
            _scene!.MoveTo(entity, Vector3.Zero);
            _scene.RotateTo(entity, Vector3.Zero);
            _scene.ScaleTo(entity, Vector3.One);
        });
        _toolbar.SnapPositionButton.Click += (_, _) =>
            Transform("Snapped the position of", entity => _scene!.MoveTo(entity, GizmoMath.Stepped(_scene.PositionOf(entity), _toolbar.Steps.Move)));
        _toolbar.SnapRotationButton.Click += (_, _) =>
            Transform("Snapped the rotation of", entity => _scene!.RotateTo(entity, GizmoMath.Stepped(_scene.RotationOf(entity), _toolbar.Steps.Turn)));
        _toolbar.TrimeshItem.Click += (_, _) => AddCollision(false);
        _toolbar.ConvexItem.Click += (_, _) => AddCollision(true);
        _center.Overlay.ProjectionChanged += orthogonal =>
        {
            _scene?.SetOrthogonal(orthogonal);
            Say(orthogonal ? "Orthogonal projection." : "Perspective projection.");
        };
    }

    void Transform(string done, Action<nint> change) => Change(() =>
    {
        var selected = _scenePane.Selected!;
        change(selected.Handle);
        return new EntityEdit(selected.Handle, $"{done} {selected.Name}. Save keeps the change.");
    });

    void AddCollision(bool convex) => Change(() =>
    {
        var selected = _scenePane.Selected!;
        _scene!.AddCollision(selected.Handle, convex);
        return new EntityEdit(selected.Handle, $"Made a {(convex ? "convex" : "trimesh")} collision shape under {selected.Name}. Save keeps the change.");
    });

    void Frame()
    {
        var selected = _scenePane.Selected!;
        _scene!.FrameOn(selected.Handle);
        Say($"Framed {selected.Name}.");
    }

    // The window title is the scene file, then the project, with a mark in front for edits that the file does
    // not hold yet. The bar draws a dot for those edits, so the mark goes in the window title alone.
    void ShowTitle()
    {
        var project = _scene?.ProjectName is { Length: > 0 } name ? name : "Unnamed Project";
        var scene = _scene?.SceneFile is { Length: > 0 } file ? Path.GetFileName(file) : null;
        var title = scene is null ? project : $"{scene} - {project}";
        Title = _edited ? $"(*) {title}" : title;
        _titleBar.Show(project, scene, _edited);
    }

    // The tab is named for the scene file. A scene with no file takes the name of its root.
    string SceneLabel() => _scene!.SceneFile is { Length: > 0 } file ? Path.GetFileNameWithoutExtension(file) : _root?.Name ?? "";

    // The file name the toasts and the Save changes dialog give. A scene with no file takes the name of its root.
    string SceneName() => _scene!.SceneFile is { Length: > 0 } file ? Path.GetFileName(file) : _root?.Name ?? "";

    void ShowEdited(bool edited)
    {
        _edited = edited;
        _edits = edited ? _edits + 1 : 0;
        ShowTitle();
        _saveChanges.IsVisible &= edited;
    }

    // The game runs from the files on disk, so a play saves edits first. A scene with none is left alone,
    // because its scripts run while it is open and a save would write what they changed.
    void Play()
    {
        if (!SaveEdits())
        {
            return;
        }

        try
        {
            _scene!.Play(_graph.Breakpoints.All);
        }
        catch (IOException error)
        {
            Fail(error.Message);
            _toasts.Show(ToastKind.Failed, "The game could not start", error.Message);
            return;
        }

        _playedAt = _time.GetUtcNow();
        ShowGame();
        _bottomBar.OpenOutputUnlessPinned();
        Say("Playing in a second window.");
    }

    void Stop()
    {
        _scene!.Stop();
        ShowGame();
        Say("Stopped the game.");
    }

    void ShowGame()
    {
        var playing = _scene?.IsPlaying == true;
        _playButton.IsEnabled = _root is not null;
        _stopButton.IsEnabled = playing;
        _game.Show(playing, _root is not null, _scene?.ProjectName ?? "", _time.GetUtcNow() - _playedAt);
        _center.Debugger.ShowGame(playing, _root is not null);
    }

    void Open(string resourcePath) => Leave(() => _scene!.Open(resourcePath));

    void OpenNewScene() => _newScene.Open(_scene?.Scenes() ?? []);

    // The rows are read at each open, so the palette shows the menus, files and entities as they are now.
    void OpenPalette()
    {
        IEnumerable<PickerRow> entities = _root is null ? [] : CommandPalette.EntitiesOf(_root, _scene!.SceneFile, _scenePane.Select);
        _palette.Open(CommandPalette.Finder([
            .. CommandPalette.CommandsOf(NativeMenu.GetMenu(this)!),
            .. CommandPalette.FilesOf(_scene?.Scenes() ?? [], Open),
            .. entities,
        ]));
    }

    // The open scene comes first, then the recent ones newest first, then the rest of the project.
    void OpenQuickOpen()
    {
        var files = (_scene?.Scenes() ?? []).Where(path => !path.EndsWith('/'));
        var scenes = new[] { _scene?.SceneFile ?? "" }.Concat(_recent).Concat(files).Where(path => path != "").Distinct();
        var rows = QuickOpen.Rows(scenes, OpenedText, Open);
        _quickOpen.Open(query => Picker.Matching(rows, query));
    }

    string OpenedText(string path) =>
        path == _scene?.SceneFile ? "Open now"
        : _openedAt.TryGetValue(path, out var at) ? $"Opened {at.ToString("HH:mm", CultureInfo.InvariantCulture)}"
        : "";

    // The dialog that makes a scene file, the flyout that instances one, and the tab's close button.
    void WireSceneFiles()
    {
        _sceneTabs.AddButton.Click += (_, _) => OpenNewScene();
        _newScene.Requested += (path, className) =>
        {
            var left = Leave(() =>
            {
                _scene!.Create(path, className);
                ShowScenes();
            });
            // A failed save or create says why in the status line, and the dialog stays open with the same words.
            if (left)
            {
                _newScene.Dialog.Close();
            }
            else
            {
                _newScene.Say(Status);
            }
        };
        _sceneTabs.CloseButton.Click += (_, _) => Leave(() => _scene!.Close());
    }

    // Opening, making or closing a scene drops the open one and there is no undo, so the file takes its edits
    // first, as in a play. Answers whether the change was made.
    bool Leave(Action change)
    {
        if (!SaveEdits())
        {
            return false;
        }

        try
        {
            change();
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or IOException)
        {
            Fail(error.Message);
            return false;
        }

        Reload(0);
        Remember(_scene!.SceneFile);
        _since = _time.GetLocalNow();
        Say(OpenedStatus());
        return true;
    }

    // Open Recent lists the scenes opened this session, newest first. An edit reloads the scene and opens nothing,
    // so only a load and a change of scene call this.
    void Remember(string sceneFile)
    {
        if (sceneFile == "")
        {
            return;
        }

        _recent.Remove(sceneFile);
        _recent.Insert(0, sceneFile);
        _openedAt[sceneFile] = _time.GetLocalNow();
        ShowRecent();
    }

    void ShowRecent() => EditorMenu.ShowRecent(NativeMenu.GetMenu(this)!, _recent, Open, () =>
    {
        _recent.Clear();
        _openedAt.Clear();
        ShowRecent();
    });

    // shortcut: one file stat per scene on each refresh. Read the times only for a newest first sort when a project has thousands.
    void ShowScenes()
    {
        IReadOnlyList<string> files = [.. _scene!.Scenes(), .. _scene.Scripts(), .. _scene.GraphScripts()];
        _scenes.Show(files, files.Where(path => !path.EndsWith('/')).Distinct().ToDictionary(path => path, _scene.ModifiedOf));
    }

    // A script opens in its workspace. It is opened before the workspace shows, so the workspace's list shows it chosen.
    void OpenFile(string resourcePath)
    {
        switch (FileTree.KindOf(resourcePath))
        {
            case FileKind.Script:
                _script.Choose(resourcePath);
                _titleBar.Choose(TitleBar.Script);
                break;
            case FileKind.Graph:
                _graph.Open(resourcePath);
                _titleBar.Choose(TitleBar.Graph);
                break;
            default:
                Open(resourcePath);
                break;
        }
    }

    string OpenedStatus() => _root is null ? "No scene is open." : $"Opened {_root.Name}.";

    // An open script that was typed in is an edit too. Its file is written first, and the scene file only when the scene has edits.
    bool SaveEdits() => SaveScript() && (!_edited || Save());

    bool HasEdits => _edited || _script.IsDirty || _graph.IsDirty;

    // The Save key and menu item keep the scene and the open script, whichever workspace shows.
    void SaveAll()
    {
        if (SaveScript())
        {
            Save();
        }
    }

    // A refused write is said in the view of the file, and in the window's own line too, because the view may be hidden.
    bool SaveScript()
    {
        if (!_script.Save())
        {
            Fail(_script.Status.Text ?? "");
            return false;
        }

        if (!_graph.Save())
        {
            Fail(_graph.Status.Text ?? "");
            return false;
        }

        return true;
    }

    bool Save()
    {
        try
        {
            _scene!.Save();
            ShowEdited(false);
            _since = _time.GetLocalNow();
            Say("Saved.", StatusKind.Done);
            _toasts.Show(ToastKind.Done, $"Saved {SceneName()}");
            return true;
        }
        catch (IOException error)
        {
            Fail(error.Message);
            _toasts.Show(ToastKind.Failed, "The scene could not be saved", error.Message, new ToastAction("Try again", SaveAll));
            return false;
        }
    }

    // The title bar on top, then the columns with the 4 pixel margin and the 3 pixel gaps of the reference.
    Grid LeftColumn()
    {
        var left = new Grid { RowDefinitions = new RowDefinitions("*,6,*") };
        Fill(left, _slots[DockSlot.UpperLeft], 0);
        Fill(left, RowSplitter(), 1);
        Fill(left, _slots[DockSlot.LowerLeft], 2);
        return left;
    }

    IEnumerable<DockTabs> DocksIn(params DockSlot[] slots) => slots.Select(slot => _slots[slot].Child).OfType<DockTabs>();

    Control Layout()
    {
        var body = new Grid { Margin = new Thickness(6, 0, 6, 6), ColumnDefinitions = new ColumnDefinitions("280,6,*,6,316") };
        Fill(body, _leftSide, 0);
        Fill(body, _leftSide.Splitter, 1);
        Fill(body, _center, 2);
        Fill(body, _rightSide.Splitter, 3);
        Fill(body, _rightSide, 4);
        var root = new Grid { RowDefinitions = new RowDefinitions($"{TitleBar.BarHeight},*") };
        Fill(root, _titleBar, 0);
        Fill(root, body, 1);
        return root;
    }

    // Between the left side's two docks. Each side has its own splitter to the middle.
    static GridSplitter RowSplitter() => new() { Background = Brushes.Transparent, ResizeDirection = GridResizeDirection.Rows };

    // Puts the child in the next row of a vertical grid, or the next column of a horizontal one.
    static void Fill(Grid grid, Control child, int slot)
    {
        if (grid.ColumnDefinitions.Count > 0)
        {
            Grid.SetColumn(child, slot);
        }
        else
        {
            Grid.SetRow(child, slot);
        }

        grid.Children.Add(child);
    }
}
