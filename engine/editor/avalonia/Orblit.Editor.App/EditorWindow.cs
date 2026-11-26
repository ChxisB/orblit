using System.Globalization;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Orblit.Editor.App.Launcher;
using Orblit.Editor.App.Shell;
using Orblit.Editor.App.Theme;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App;

/// <summary>
/// The editor shell: a title bar over three columns. The scene outline and the project's scenes are on the left,
/// the viewport in the middle and the inspector on the right.
/// </summary>
internal sealed class EditorWindow : Window, IAppMenuTarget
{
    // The engine's log is read by polling a file, and a quarter of a second is fast enough to read a line as it comes.
    // The same tick notices a game that ended on its own.
    static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(250);

    const string NoViewport = "No viewport. The editor was started with --headless.";

    readonly InspectorPanel _inspector = new();
    readonly ContentControl _viewport = new() { Content = ViewportNote(NoViewport) };
    readonly EntityTools _tools = new();
    readonly MoveTools _move = new();
    readonly SignalsPane _signals = new();
    readonly NewSceneTool _newScene = new();
    readonly InstanceTool _instanceScene = new();
    readonly StackPanel _changes = new() { Spacing = Gaps.Joined };
    readonly Dialog _saveChanges;
    readonly ScenesPanel _scenes;
    readonly TimeProvider _time;
    readonly EditHistory _history;
    readonly HistoryPane _historyPane;
    readonly ToastStack _toasts;
    readonly List<string> _recent = [];
    readonly Dictionary<string, DateTimeOffset> _openedAt = [];
    readonly Search _search = new();
    readonly Button _scenesButton = new();
    readonly Button _actionsButton = new();
    // Save, Play this scene and Frame are commands without a place of their own on screen. The shortcuts, the
    // menus and the tests press them, and a button off keeps its key unhandled. The title bar draws Play and Stop.
    readonly Button _save = new() { IsEnabled = false };
    readonly Button _playButton = new();
    readonly Button _playSceneButton = new();
    readonly Button _stopButton = new();
    readonly Button _frame = new() { IsEnabled = false };
    readonly Button _undo = new() { IsEnabled = false };
    readonly Button _redo = new() { IsEnabled = false };
    readonly Button _keysButton = new();
    readonly KeysSheet _keys = new();
    readonly Button _settingsButton = new();
    readonly SettingsSheet _settings;
    readonly ILinkOpener _links;
    readonly bool _globalMenu;
    readonly SceneTabStrip _sceneTabs;
    readonly ViewportToolbar _toolbar;
    readonly TitleBar _titleBar;
    readonly BottomBar _bottomBar = new();
    readonly DispatcherTimer _pollTimer = new() { Interval = PollInterval };
    readonly ScenePane _scenePane;
    readonly DockTabs _sceneDock;
    readonly DockTabs _fileDock;
    readonly InspectorPane _inspectorPane;
    readonly DockTabs _inspectorDock;
    readonly DockArea _docks;
    readonly CenterArea _center;
    readonly ExportTool _export = new();
    readonly ScriptView _script;
    readonly ImportPane _import = new();
    readonly GraphView _graph;
    readonly GameView _game;

    // The workspaces that start with every panel group collapsed. A workspace not listed shows what the user left open.
    static readonly string[] s_folding = [TitleBar.Script, TitleBar.Graph, TitleBar.Game];
    // The middle's least width. A workspace that folds the sides lays its own columns out in the middle, so it needs
    // more of it when a side is opened again.
    const double ViewportMiddle = 480;
    const double WorkspaceMiddle = 700;
    IEditorScene? _scene;
    // The scene once it loads, and the project's folder before that.
    IProjectFiles? _files;
    OutlineNode? _root;
    readonly RulerPoints _ruler = new();
    bool _edited;
    // The edits since the scene opened or was last saved, and when that was, for the Save changes dialog.
    int _edits;
    DateTimeOffset _since;

    // What the open Save changes dialog does once the edits are saved or dropped, and the word its buttons give it.
    (string Verb, Action Then) _afterAsk = ("", () => { });
    // When the game last started, for the time the Game view shows.
    DateTimeOffset _playedAt;
    // The Output lines before the game started, so the error a crash toast names is the game's.
    int _outputAtPlay;
    // Whether the game ran at the last look, so its end gets one toast.
    bool _wasPlaying;
    bool _planar;
    // The bottom panel Hide panels shut, which Show panels opens again, or -1 for none.
    int _shutPanel = -1;
    // The Entity3D the gizmo drags, or zero when the selection has none.
    nint _gizmoTarget;
    // What the drag under way did, such as Moved Box, which names its step in History when it ends.
    string _dragged = "";

    // The screen bar of macOS has its own app menu, so a window on any other system also carries About, Settings
    // and Quit. Settings is the user's own unless the caller names one. Links open in the system browser.
    public EditorWindow(bool? globalMenu = null, TimeProvider? time = null, EditorSettings? settings = null, ILinkOpener? links = null)
    {
        _links = links ?? new WindowLinks(this);
        _time = time ?? TimeProvider.System;
        _history = new EditHistory(_time);
        _history.Changed += ShowHistory;
        _historyPane = new HistoryPane(CommandKey);
        _game = new GameView(CommandKey);
        _script = new ScriptView(CommandKey);
        _graph = new GraphView(CommandKey);
        _historyPane.Picked += GoTo;
        _historyPane.ClearButton.Click += (_, _) => _history.Clear();
        _undo.Click += (_, _) => GoTo(_history.Done - 1);
        _redo.Click += (_, _) => GoTo(_history.Done + 1);
        // 16 in from the right, and 8 clear of the bottom bar, which sits on the body's 8 pixel margin.
        _toasts = new ToastStack(_time) { Margin = new Thickness(0, 0, Gaps.Dialog, CenterArea.BottomBarHeight + Gaps.Dialog) };
        _saveChanges = SaveChangesDialog();
        _titleBar = new TitleBar(_playButton, _playSceneButton, _stopButton, CommandKey);
        _sceneTabs = new SceneTabStrip(_playSceneButton, CommandKey);
        _titleBar.SearchButton.Click += (_, _) => OpenSearch(SearchScope.All);
        _script.FindScriptButton.Click += (_, _) => OpenSearch(SearchScope.All);
        _scenesButton.Click += (_, _) => OpenSearch(SearchScope.Scenes);
        _actionsButton.Click += (_, _) => OpenSearch(SearchScope.Actions);
        _keysButton.Click += (_, _) => OpenKeys();
        _settings = new SettingsSheet(settings ?? ProjectList.ForUser().Settings());
        _settings.KeysAsked += OpenKeys;
        _settingsButton.Click += (_, _) => OpenSettings();
        _globalMenu = globalMenu ?? OperatingSystem.IsMacOS();
        ShowTitle();
        Icon = EditorIcon.ToWindowIcon("Orblit", 64);
        Width = 1440;
        Height = 900;
        MinWidth = 1280;
        MinHeight = 720;
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
        _signals.Changed += Edited;
        _signals.Failed += Fail;
        _instanceScene.Changed += Edited;
        _instanceScene.Failed += Fail;
        _inspector.Edited += () =>
        {
            _history.Commit(_scenePane.Picked.Count > 1 ? $"Edited {_scenePane.Picked.Count} entities" : $"Edited {_scenePane.Selected?.Name}");
            ShowEdited(true);
            HighlightSelected();
            Say("Edited. Save keeps the change.");
        };
        _inspector.Failed += Fail;
        _inspector.Editing += OpenFile;
        _scenes.Chosen += OpenFile;
        _scenes.NewSceneRequested += OpenNewScene;
        _scenes.PlayRequested += path => Play("--scene", path);
        _scenes.UseRequested += _instanceScene.Use;
        _scenes.NewGraphRequested += () =>
        {
            _titleBar.Choose(TitleBar.Graph);
            _graph.ShowCreate();
        };
        WireSceneFiles();
        _playButton.IsEnabled = false;
        _playSceneButton.IsEnabled = false;
        _stopButton.IsEnabled = false;
        _save.Click += (_, _) => SaveAll();
        _playButton.Click += (_, _) => PlayGame();
        _playSceneButton.Click += (_, _) => PlayScene();
        _stopButton.Click += (_, _) => Stop();
        _sceneTabs.PanelsButton.Click += (_, _) => TogglePanels();
        _game.RunRequested += PlayGame;
        _game.RunSceneRequested += PlayScene;
        _game.StopRequested += Stop;
        _game.ShowWindowRequested += () => _scene?.ShowGameWindow();
        _frame.Click += (_, _) => Frame();
        _inspectorPane = new InspectorPane(_inspector);
        _inspectorPane.Picked += _scenePane.Select;
        _sceneDock = new DockTabs(new DockPage("Scene", "SceneFile", _scenePane), new DockPage("Import", "Load", _import)) { Slot = DockSlot.UpperLeft };
        _fileDock = new DockTabs(new DockPage("Files", "Filesystem", _scenes), new DockPage("History", "History", _historyPane, _historyPane.ClearButton)) { Slot = DockSlot.LowerLeft };
        _inspectorDock = new DockTabs(new DockPage("Inspector", "ListSelect", _inspectorPane), new DockPage("Signals", "Signals", _signals)) { Slot = DockSlot.UpperRight };
        _toolbar = new ViewportToolbar(_frame, CommandKey);
        _toolbar.LockButton.Click += (_, _) => ToggleFlag(_scene!.SetLocked, selected => selected.Locked, "Locked", "Unlocked");
        _toolbar.GroupButton.Click += (_, _) => ToggleFlag(_scene!.SetGrouped, selected => selected.Grouped, "Grouped", "Ungrouped");
        _toolbar.KeepChildrenButton.IsCheckedChanged += (_, _) => _scene?.KeepChildren = _toolbar.KeepChildren;
        _center = new CenterArea(new CenterParts(_sceneTabs, _toolbar, _viewport, _bottomBar, CommandKey));
        _docks = new DockArea(_center, _sceneDock, _fileDock, _inspectorDock) { LeastMiddle = ViewportMiddle };
        _docks.Said += message => Say(message);
        _docks.Changed += () => _sceneTabs.ShowPanelsHidden(_docks.AllCollapsed);
        _center.Output.ClearRequested += () =>
        {
            _scene?.ClearOutput();
            _outputAtPlay = 0;
        };
        _center.Output.CopyRequested += text => Copy(text, "the output");
        _center.Debugger.RunRequested += PlayGame;
        _center.Debugger.CopyRequested += id => Copy(id, "the process id");
        _center.NoScene.NewButton.Click += (_, _) => OpenNewScene(FileTree.Root);
        _center.NoScene.FindButton.Click += (_, _) => OpenSearch(SearchScope.Scenes);
        _center.NoScene.OpenRequested += Open;
        _script.Paused += () =>
        {
            _titleBar.Choose(TitleBar.Script);
            _bottomBar.OpenDebugger();
        };
        _bottomBar.VersionCopyRequested += () => Copy(BottomBar.Version, BottomBar.Version);
        _center.Planar.SwitchButton.Click += (_, _) => _titleBar.Choose(TitleBar.Spatial);
        _center.Planar.OpenButton.Click += (_, _) => OpenSearch(SearchScope.Scenes);
        _center.Planar.AddButton.Click += (_, _) => _tools.AddButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        _center.AddBareWorkspace(TitleBar.Script, _script);
        _center.AddBareWorkspace(TitleBar.Graph, _graph);
        _center.AddBareWorkspace(TitleBar.Game, _game);
        _titleBar.WorkspaceChosen += ChooseWorkspace;
        _pollTimer.Tick += (_, _) => Poll();
        Closed += (_, _) =>
        {
            _pollTimer.Stop();
            _toasts.Close();
        };
        WireMenus();
        // The toasts go over the veil, so Try again on a failed save stays in reach while the dialog asks.
        Content = new Panel { Children = { Layout(), _newScene.Dialog, _graph.CreateDialog, _script.CreateDialog, _tools.AddDialog, _instanceScene.Dialog, _move.Dialog, _signals.Dialog, _export.Dialog, _saveChanges, _settings.Dialog, _keys.Dialog, _search.Picker, _toasts } };
        NativeMenu.SetMenu(this, EditorMenu.Build(_globalMenu, CommandKey, MenuActions()));
    }

    internal TitleBar Bar => _titleBar;

    internal KeysSheet Keys => _keys;

    internal SettingsSheet Settings => _settings;

    internal DockTabs SceneDock => _sceneDock;

    internal DockTabs FileDock => _fileDock;

    internal DockTabs InspectorDock => _inspectorDock;

    internal DockSide LeftSide => _docks.Left;

    internal DockSide RightSide => _docks.Right;

    internal CenterArea Center => _center;

    internal ExportTool Export => _export;

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

    internal SignalsPane Signals => _signals;

    internal HistoryPane History => _historyPane;

    internal NewSceneTool NewScene => _newScene;

    internal InstanceTool InstanceScene => _instanceScene;

    internal Dialog SaveChanges => _saveChanges;

    /// <summary>The rows of the Save changes dialog: each change since the save, then each script with edits.</summary>
    internal StackPanel ChangesList => _changes;

    internal ContentControl Viewport => _viewport;

    internal ScenesPanel Scenes => _scenes;

    internal ImportPane Import => _import;

    internal Button SaveButton => _save;

    internal Button PlayButton => _playButton;

    internal Button PlaySceneButton => _playSceneButton;

    internal Button StopButton => _stopButton;

    internal GameView Game => _game;

    internal Button FrameButton => _frame;

    internal string Status => _bottomBar.Status;

    internal ToastStack Toasts => _toasts;

    internal Search Search => _search;


    /// <summary>The modifier that makes a shortcut: Command on macOS, Control elsewhere.</summary>
    internal KeyModifiers CommandKey => this.GetPlatformSettings()?.HotkeyConfiguration.CommandModifiers ?? KeyModifiers.Control;

    /// <summary>
    /// Shows what the project's folder tells while the engine starts. The parts that wait for the engine say so,
    /// and the Files panel takes no press, since no scene is there to open a file in.
    /// </summary>
    public void ShowProject(IProjectFiles project)
    {
        _files = project;
        ShowTitle();
        ShowScenes();
        _scenes.MarkOpen(project.SceneFile);
        _sceneTabs.ShowStarting(project.SceneFile);
        _scenePane.ShowStarting(true);
        _inspectorPane.ShowStarting(true);
        _toolbar.IsVisible = false;
        _viewport.Content = ViewportNote("Loading the view…");
        TakeFilePresses(false);
    }

    /// <summary>The engine did not start. The view says why, the panes point to it, and the Files panel is off.</summary>
    public void FailToStart(string message)
    {
        // The panes keep the places they said loading in, so neither asks for a scene that cannot be opened.
        _scenePane.ShowStarting(true);
        _inspectorPane.ShowStarting(true);
        foreach (var waiting in new[] { _scenePane.Loading, _inspectorPane.Loading })
        {
            Parts.Reword(waiting, "The engine did not start", "The view says why.");
        }

        _sceneTabs.ShowStarting("");
        _scenes.IsEnabled = false;
        _viewport.Content = ViewportNote(message);
        Fail(message);
    }

    void TakeFilePresses(bool take)
    {
        _scenes.IsHitTestVisible = take;
        KeyboardNavigation.SetTabNavigation(_scenes, take ? KeyboardNavigationMode.Continue : KeyboardNavigationMode.None);
    }

    // In the middle, clear of the tools over the top of the view.
    static TextBlock ViewportNote(string text) => new()
    {
        Margin = new Thickness(Gaps.Dialog),
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        TextWrapping = TextWrapping.Wrap,
        Text = text,
    };

    public void Load(IEditorScene editorScene)
    {
        var scene = new UndoableScene(editorScene, _history);
        _scene = scene;
        _files = scene;
        _scenePane.ShowStarting(false);
        _inspectorPane.ShowStarting(false);
        _toolbar.IsVisible = !_planar;
        TakeFilePresses(true);
        if (_viewport.Content is not ViewportControl)
        {
            _viewport.Content = ViewportNote(NoViewport);
        }

        // The button can be pressed while the engine starts, before there is a scene to tell.
        scene.KeepChildren = _toolbar.KeepChildren;
        _script.Show(scene);
        _graph.Show(scene);
        _import.Show(scene);
        _export.Show(scene);
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

    /// <summary>Reads the lines the engine logged since the last call and shows whether the game still runs.</summary>
    internal void Poll()
    {
        _scene?.PollOutput();
        _graph.PollDebugger();
        _script.PollDebugger();
        ShowGame();
    }

    public void ShowViewport(ViewportControl viewport)
    {
        viewport.Ended += () => Fail("The engine window was closed. Restart the editor to see the scene.");
        viewport.Clicked += ClickAt;
        viewport.RulerSource = () => _toolbar.Measures && _scene is not null ? _ruler.Shown(_scene.FramePointOf) : null;
        viewport.PlanarSource = () => _planar;
        viewport.MarkSource = PivotOfSelected;
        viewport.PlanarMoved += DragPlanar;
        viewport.ModeSource = () => _toolbar.Mode;
        viewport.GripSource = () => _center.Planar.Grip;
        viewport.Zoomed += _ => _center.Planar.Refresh();
        viewport.PlanarPanned += _ => _center.Planar.Refresh();
        viewport.TrackballSource = () => _toolbar.Trackball;
        viewport.HandlesSource = () => _gizmoTarget == 0 ? null : _scene?.HandlesOf(_gizmoTarget, _toolbar.Space);
        viewport.RingsSource = () => _gizmoTarget == 0 ? null : _scene?.RingsOf(_gizmoTarget, _toolbar.Space);
        viewport.GizmoDragged += DragSelected;
        viewport.GizmoReleased += () =>
        {
            _scene?.EndDrag();
            _history.Commit(_dragged);
        };
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
        _center.NoScene.IsVisible = _root is null;
        _center.NoScene.ShowRecent(RecentScenes());
        ShowGame();
        _sceneTabs.Show(_root, SceneLabel(), _scene!.SceneFile);
        _center.Planar.ShowScene(_scene!, _root, SceneName());
        ShowTitle();
        ShowSelection();
    }

    // The 2D view is on for the 2D workspace alone, so any other workspace gives the viewport back to 3D.
    void ChooseWorkspace(string name)
    {
        var folds = s_folding.Contains(name);
        _docks.LeastMiddle = folds ? WorkspaceMiddle : ViewportMiddle;
        _docks.UseWorkspaceFolds(folds);
        _center.ShowWorkspace(name);
        _planar = name == TitleBar.Planar;
        _center.Overlay.Planar = _planar;
        _scene?.ShowPlanar(_planar);
    }

    // A click on empty space selects nothing. The 3D tools that change what a click does have no 2D form yet.
    void ClickAt(Vector2 framePoint, KeyModifiers keys)
    {
        if (_planar)
        {
            _scenePane.Select(_scene!.PickPlanarAt(framePoint));
        }
        else if (_toolbar.Measures)
        {
            PutRulerPoint(framePoint);
        }
        else if (_toolbar.ListSelects || keys.HasFlag(KeyModifiers.Alt))
        {
            ChooseFromList(framePoint);
        }
        else
        {
            _scenePane.Select(_scene!.PickAt(framePoint));
        }
    }

    void PutRulerPoint(Vector2 framePoint)
    {
        if (_scene!.WorldPointAt(framePoint) is not { } point)
        {
            Say("The ruler needs a point on an entity or the ground.");
            return;
        }

        _ruler.Add(point);
        Say(_ruler.Length is { } length ? $"Measured {RulerPoints.Label(length)}." : "Click a second point. Esc stops measuring.");
    }

    // One entity or none under the cursor needs no list, so the click picks as usual.
    void ChooseFromList(Vector2 framePoint)
    {
        var under = _scene!.PickAllAt(framePoint).Select(handle => _root?.Find(handle)).OfType<OutlineNode>().ToList();
        if (under.Count < 2)
        {
            _scenePane.Select(under.FirstOrDefault()?.Handle ?? 0);
            return;
        }

        PickList = ViewportToolbar.PickList(under, _scenePane.Select);
        PickList.ShowAt(_viewport, showAtPointer: true);
    }

    /// <summary>The last list Select from a list showed, or null before any.</summary>
    internal MenuFlyout? PickList { get; private set; }

    // The selected Entity2D has a mark unless it is locked, as a locked Entity3D has no handles.
    Vector2? PivotOfSelected() =>
        _scenePane.Selected is { Locked: false } selected ? _scene?.PlanarPivotOf(selected.Handle) : null;

    // A press on the mark of the last tick can land after the selection moved on.
    void DragPlanar(FrameMove move, bool inverts)
    {
        if (PivotOfSelected() is null)
        {
            return;
        }

        // A 2D move snaps to the grid lines, so a dragged entity sits on the grid it is shown on.
        var steps = _toolbar.Steps;
        _scene!.Snap = _toolbar.Snaps != inverts ? steps with { Move = steps.Move > 0 ? (float)_center.Planar.GridStep : 0 } : null;
        var entity = _scenePane.Selected!.Handle;
        switch (_center.Planar.Tool)
        {
            case PlanarTool.Rotate:
                _scene.RotatePlanar(entity, move);
                ShowDragged("Rotated");
                break;
            case PlanarTool.Scale:
                _scene.ScalePlanar(entity, move);
                ShowDragged("Scaled");
                break;
            default:
                _scene.MovePlanar(entity, move);
                ShowDragged("Moved");
                break;
        }
    }

    void ShowSelection()
    {
        var selected = _scenePane.Selected;
        _inspectorPane.Show(_scene, _scenePane.Picked);
        _tools.Show(_scene, _root, selected);
        _move.Show(_scene, _root, selected);
        _signals.Show(_scene, _root, selected);
        _instanceScene.Show(_scene, _root, selected);
        var spatial = selected is not null && _scene is not null && _scene.IsEntity3D(selected.Handle);
        // A locked entity keeps no handles, but it can still be framed.
        _gizmoTarget = spatial && selected?.Locked == false ? selected.Handle : 0;
        _frame.IsEnabled = spatial;
        _toolbar.Select(selected, _scenePane.Picked.Count);
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
        _inspectorPane.Show(_scene, _scenePane.Picked);
        HighlightSelected();
        ShowEdited(true);
        _dragged = $"{verb} {selected.Name}";
        Say($"{_dragged}. Save keeps the change.");
    }

    // The box is drawn from the entity's place and size, so an edit in the inspector moves it too.
    void HighlightSelected() => _scene?.Highlight(_scenePane.Selected?.Handle ?? 0);

    // Closing drops the scene and its history, so edits that no save holds ask first.
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
        if (HasEdits)
        {
            e.Cancel = true;
            AskToSave("close", () =>
            {
                ShowEdited(false);
                Close();
            });
        }
    }

    // Going on without saving drops the edits, so that action sits at the far left, away from Save.
    Dialog SaveChangesDialog()
    {
        var drop = new Button { Content = "Don't save", Classes = { EditorTheme.TextButton, EditorTheme.Danger } };
        var body = new ScrollViewer { MaxHeight = 220, Content = _changes };
        var dialog = new Dialog("", "", body)
        {
            Closable = false,
            CardWidth = 440,
            LeadButton = drop,
        };
        dialog.CancelButton.Content = Parts.Keyed("Cancel", "esc");
        new HoverCard("Cancel", "Goes back to editing, with the changes.", "esc").On(dialog.CancelButton);
        // A failed save leaves the edits, so the dialog asks again with what is still not saved.
        dialog.MainButton.Click += (_, _) =>
        {
            if (!SaveEdits())
            {
                AskToSave(_afterAsk.Verb, _afterAsk.Then);
                return;
            }

            dialog.Close();
            _afterAsk.Then();
        };
        drop.Click += (_, _) =>
        {
            _script.Discard();
            _graph.Discard();
            dialog.Close();
            _afterAsk.Then();
        };
        return dialog;
    }

    // The scene's changes come from History, newest first. An undo back past the save leaves none to list, so
    // the line then says only that the scene changed. The verb names what the buttons do after the save or drop.
    void AskToSave(string verb, Action then)
    {
        _afterAsk = (verb, then);
        var going = char.ToUpperInvariant(verb[0]) + verb[1..];
        var scripts = (_script.IsDirty ? [(_script.OpenPath!, Count(_script.ChangedLines, "line") + " changed")] : Array.Empty<(string, string)>())
            .Concat(_graph.EditedScripts.Select(path => (path, "Changes not saved")))
            .ToList();
        var steps = _edited ? _history.Steps.Take(_history.Done).Where(step => step.At >= _since).Reverse().ToList() : [];
        var since = _since.ToString("HH:mm", CultureInfo.InvariantCulture);
        _changes.Children.Clear();
        _changes.Children.AddRange(steps.Select(step => ChangeRow(step.At.ToString("HH:mm", CultureInfo.InvariantCulture), step.Label, null)));
        _changes.Children.AddRange(scripts.Select(script => ChangeRow(null, $"{Path.GetFileName(script.Item1)}", $"{script.Item1} · {script.Item2}")));
        var main = _edited || scripts.Count == 1 ? $"Save and {verb}" : $"Save {scripts.Count} and {verb}";
        _saveChanges.MainButton.Content = Parts.Keyed(main, "↵");
        new HoverCard(main, $"Saves everything listed here, then {verb}s.", "↵").On(_saveChanges.MainButton);
        new HoverCard("Don't save", $"{going}s and drops every change listed here.").On(_saveChanges.LeadButton!);
        _saveChanges.Title = _edited ? $"Save changes to {SceneLabel()}?"
            : scripts.Count == 1 ? $"Save {Path.GetFileName(scripts[0].Item1)}?"
            : $"Save {scripts.Count} scripts?";
        _saveChanges.Why.Text = !_edited ? $"{(scripts.Count == 1 ? "It has" : "They have")} changes that are not saved."
            : steps.Count > 0 ? $"You made {Count(steps.Count, "change")} since {since}. {going} without saving and they are gone."
            : $"It changed since {since}. {going} without saving and the changes are gone.";
        _saveChanges.Open();
    }

    static string Count(int count, string unit) => count == 1 ? $"1 {unit}" : $"{count} {unit}s";

    // A change shows its time in dim mono at the left. A script shows its path and how much changed under its name.
    static Control ChangeRow(string? at, string name, string? line)
    {
        var row = new DockPanel { Margin = new Thickness(0, Gaps.Tight) };
        if (at is not null)
        {
            var time = new TextBlock { Text = at, Width = 48, FontFamily = EditorTheme.Mono, FontSize = 11, Foreground = EditorBrushes.TextDim, VerticalAlignment = VerticalAlignment.Center };
            DockPanel.SetDock(time, Dock.Left);
            row.Children.Add(time);
        }

        row.Children.Add(Parts.Noted(name, line));
        return row;
    }

    // An item with a button presses it, so the item is on while the button is. Undo and Redo take the button's
    // words as their name. Each Panels item shows and hides its tab. Each Show while playing item flips the
    // Game tab's switch.
    Dictionary<string, MenuAction> MenuActions()
    {
        static MenuAction Pressing(Button button) => new(() => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)), button.GetObservable(IsEnabledProperty));

        static MenuAction Named(Button button, string word) => Pressing(button) with { Name = button.GetObservable(ContentProperty, content => content as string ?? word) };

        var actions = new Dictionary<string, MenuAction>
        {
            ["About Orblit"] = new(CopyVersion),
            ["Editor Settings…"] = Pressing(_settingsButton),
            ["Quit Orblit"] = new(Close),
            ["New scene…"] = new(() => OpenNewScene(FileTree.Root)),
            ["Quick Open Scene…"] = new(() => OpenSearch(SearchScope.Scenes)),
            ["Save"] = Pressing(_save),
            ["Export game…"] = new(_export.Open),
            ["Undo"] = Named(_undo, "Undo"),
            ["Redo"] = Named(_redo, "Redo"),
            ["Search"] = Pressing(_titleBar.SearchButton),
            ["Hide panels"] = Pressing(_sceneTabs.PanelsButton),
            ["Full screen"] = new(() => WindowState = WindowState == WindowState.FullScreen ? WindowState.Normal : WindowState.FullScreen),
            ["Play the game"] = Pressing(_playButton),
            ["Play this scene"] = Pressing(_playSceneButton),
            ["Stop"] = Pressing(_stopButton),
            ["All keys"] = Pressing(_keysButton),
        };
        foreach (var name in GameView.FlagNames)
        {
            var toggle = _game.SwitchOf(name);
            actions[name] = new(() => toggle.IsChecked = toggle.IsChecked != true, Checked: toggle.GetObservable(ToggleButton.IsCheckedProperty, on => on == true));
        }

        foreach (var dock in new[] { _sceneDock, _fileDock, _inspectorDock })
        {
            foreach (var title in dock.Titles)
            {
                actions[title] = new(() => _docks.ToggleTab(dock, title), Checked: dock.Shown(title));
            }
        }

        // The menu bar stays in reach above a dialog's veil, so its items wait for the dialog as the keys do. Quit
        // still closes, as the window's close button does.
        foreach (var (name, action) in actions.Where(pair => pair.Key != "Quit Orblit").ToList())
        {
            actions[name] = action with { Run = () => RunUnveiled(action.Run) };
        }

        return actions;
    }

    void RunUnveiled(Action run)
    {
        if (!Veiled)
        {
            run();
        }
    }

    // An open dialog or picker covers the window, and nothing behind it may act.
    bool Veiled => Content is Panel { Children: var layers } && layers.Any(layer => layer is Dialog or Picker && layer.IsVisible);

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
        (Key.W, var modifiers) when modifiers == CommandKey && _graph.IsEffectivelyVisible => _graph.CloseButton,
        (Key.S, var modifiers) when modifiers == (CommandKey | KeyModifiers.Alt) && _graph.IsEffectivelyVisible => _graph.SaveAllButton,
        (Key.W, var modifiers) when modifiers == CommandKey => _sceneTabs.CloseButton,
        (Key.S, var modifiers) when modifiers == CommandKey => _save,
        (Key.Z, var modifiers) when modifiers == CommandKey && e.Source is not TextBox => _undo,
        (Key.Z, var modifiers) when modifiers == (CommandKey | KeyModifiers.Shift) && e.Source is not TextBox => _redo,
        (Key.B, var modifiers) when modifiers == CommandKey => _playButton,
        (Key.Y, var modifiers) when modifiers == CommandKey => _graph.DebugPanel?.Find<Button>("debugger:pause"),
        (Key.F12, KeyModifiers.None) => _graph.DebugPanel?.Find<Button>("debugger:continue"),
        (Key.F10, KeyModifiers.None) => _graph.DebugPanel?.Find<Button>("debugger:next"),
        (Key.F11, KeyModifiers.None) => _graph.DebugPanel?.Find<Button>("debugger:step"),
        (Key.F11, KeyModifiers.Shift) => _graph.DebugPanel?.Find<Button>("debugger:out"),
        (Key.R, var modifiers) when modifiers == CommandKey => _playSceneButton,
        (Key.OemPeriod, var modifiers) when modifiers == CommandKey => _stopButton,
        ( >= Key.D1 and <= Key.D5, var modifiers) when modifiers == CommandKey => _titleBar.Tabs[e.Key - Key.D1],
        (Key.D, var modifiers) when modifiers == CommandKey => _tools.DuplicateButton,
        (Key.A, var modifiers) when modifiers == CommandKey && e.Source is not TextBox => _tools.AddButton,
        (Key.A, var modifiers) when modifiers == (CommandKey | KeyModifiers.Shift) => _instanceScene.OpenButton,
        (Key.Up, var modifiers) when modifiers == CommandKey && e.Source is not TextBox => _move.UpButton,
        (Key.Down, var modifiers) when modifiers == CommandKey && e.Source is not TextBox => _move.DownButton,
        (Key.F2, KeyModifiers.None) when e.Source is not TextBox => _tools.RenameButton,
        (Key.Back or Key.Delete, KeyModifiers.None) when e.Source is not TextBox && Erases(e.Source) => _tools.RemoveButton,
        (Key.K, var modifiers) when modifiers == CommandKey => _titleBar.SearchButton,
        (Key.P, var modifiers) when modifiers == (CommandKey | KeyModifiers.Shift) => _actionsButton,
        (Key.O, var modifiers) when modifiers == (CommandKey | KeyModifiers.Shift) => _scenesButton,
        (Key.OemPipe, var modifiers) when modifiers == CommandKey => _sceneTabs.PanelsButton,
        (Key.OemQuestion, KeyModifiers.Shift) when e.Source is not (TextBox or ComboBox) => _keysButton,
        (Key.OemComma, var modifiers) when modifiers == CommandKey => _settingsButton,
        (Key.F, var modifiers) when modifiers == CommandKey && _script.IsEffectivelyVisible => _script.FindButton,
        (Key.B, var modifiers) when modifiers == (CommandKey | KeyModifiers.Shift) && _script.IsEffectivelyVisible => _script.CheckButton,
        (Key.F, KeyModifiers.None) when e.Source is not (TextBox or ComboBox) => _frame,
        (Key.Q, KeyModifiers.None) when e.Source is not (TextBox or ComboBox) && _center.Planar.IsVisible => _center.Planar.SelectButton,
        (Key.W, KeyModifiers.None) when e.Source is not (TextBox or ComboBox) && _center.Planar.IsVisible => _center.Planar.MoveButton,
        (Key.E, KeyModifiers.None) when e.Source is not (TextBox or ComboBox) && _center.Planar.IsVisible => _center.Planar.RotateButton,
        (Key.R, KeyModifiers.None) when e.Source is not (TextBox or ComboBox) && _center.Planar.IsVisible => _center.Planar.ScaleButton,
        (Key.OemMinus, var modifiers) when modifiers == CommandKey && _center.Planar.IsVisible => _center.Planar.ZoomOutButton,
        (Key.D0, var modifiers) when modifiers == CommandKey && _center.Planar.IsVisible => _center.Planar.ZoomButton,
        (Key.OemPlus, var modifiers) when modifiers == CommandKey && _center.Planar.IsVisible => _center.Planar.ZoomInButton,
        (Key.Q, KeyModifiers.None) when e.Source is not (TextBox or ComboBox) => _toolbar.SelectButton,
        (Key.W, KeyModifiers.None) when e.Source is not (TextBox or ComboBox) => _toolbar.MoveButton,
        (Key.E, KeyModifiers.None) when e.Source is not (TextBox or ComboBox) => _toolbar.RotateButton,
        (Key.R, KeyModifiers.None) when e.Source is not (TextBox or ComboBox) => _toolbar.ScaleButton,
        (Key.R, KeyModifiers.Alt) when e.Source is not (TextBox or ComboBox) => _toolbar.ResetAllButton,
        (Key.T, KeyModifiers.None) when e.Source is not (TextBox or ComboBox) => _toolbar.LocalButton,
        (Key.Y, KeyModifiers.None) when e.Source is not (TextBox or ComboBox) => _toolbar.SnapButton,
        (Key.L, var modifiers) when modifiers == CommandKey && e.Source is not TextBox => _toolbar.LockButton,
        (Key.G, var modifiers) when modifiers == CommandKey && e.Source is not TextBox => _toolbar.GroupButton,
        (Key.Escape, KeyModifiers.None) when _toolbar.Measures => _toolbar.RulerButton,
        _ => null,
    };

    // An erase key in another pane means that pane's own thing, so erase acts only from the window, the scene pane
    // or the viewport while the scene shows.
    bool Erases(object? source) =>
        _scenePane.IsEffectivelyVisible
        && (source == this || source is Visual visual && (_scenePane.IsVisualAncestorOf(visual) || _viewport.IsVisualAncestorOf(visual)));

    // A tool changed the scene: read it again and show what it did.
    void Edited(EntityEdit edit)
    {
        // The first sentence names the step, such as Renamed Ball to Box. A name can hold a full stop, so the split is on a full stop and a space.
        _history.Commit(edit.Message.Split(". ")[0].TrimEnd('.'));
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
        _toolbar.TrimeshItem.Click += (_, _) => AddCollision(false);
        _toolbar.ConvexItem.Click += (_, _) => AddCollision(true);
        _toolbar.RulerButton.Click += (_, _) => _ruler.Clear();
        _center.Overlay.PreviewsChanged += (sun, sky) => _scene?.ShowPreviews(sun, sky);
        _center.Overlay.ProjectionChanged += orthogonal =>
        {
            _scene?.SetOrthogonal(orthogonal);
            Say(orthogonal ? "Flat projection." : "Perspective projection.");
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
        var project = _files?.ProjectName is { Length: > 0 } name ? name : "Unnamed Project";
        var scene = _files?.SceneFile is { Length: > 0 } file ? Path.GetFileName(file) : null;
        var title = scene is null ? project : $"{scene} - {project}";
        Title = _edited ? $"(*) {title}" : title;
        _titleBar.Show(project, scene, _edited);
    }

    // The tab is named for the scene file. A scene with no file takes the name of its root.
    string SceneLabel() => _scene!.SceneFile is { Length: > 0 } file ? Path.GetFileNameWithoutExtension(file) : _root?.Name ?? "";

    // The file name History and the 2D view give. A scene with no file takes the name of its root.
    string SceneName() => _scene!.SceneFile is { Length: > 0 } file ? Path.GetFileName(file) : _root?.Name ?? "";

    void ShowEdited(bool edited)
    {
        _edited = edited;
        _edits = edited ? _edits + 1 : 0;
        ShowTitle();
        _saveChanges.IsVisible &= edited;
    }

    // Undo, redo and a click in History all come here, so the scene reloads once however many steps it moves.
    void GoTo(int done)
    {
        var was = _history.Done;
        try
        {
            _history.GoTo(done);
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException)
        {
            Fail(error.Message);
            return;
        }

        if (_history.Done == was)
        {
            return;
        }

        Reload(_scenePane.Selected?.Handle ?? 0);
        ShowEdited(true);
        var moved = Math.Abs(_history.Done - was);
        var step = _history.Steps[Math.Min(was, _history.Done)].Label;
        var what = moved == 1 ? step : $"{moved} steps";
        Say($"{(_history.Done < was ? "Undid" : "Redid")} {what}. Save keeps the change.");
    }

    void ShowHistory()
    {
        _undo.IsEnabled = _history.CanUndo;
        _redo.IsEnabled = _history.CanRedo;
        _undo.Content = _history.CanUndo ? $"Undo {_history.Steps[_history.Done - 1].Label}" : "Undo";
        _redo.Content = _history.CanRedo ? $"Redo {_history.Steps[_history.Done].Label}" : "Redo";
        var file = _scene?.SceneFile ?? "";
        _historyPane.Show(_history, $"Opened {(_scene is null ? "" : SceneName())}", _openedAt.GetValueOrDefault(file, _time.GetLocalNow()));
    }

    // The game runs from the files on disk, so a play saves edits first. A scene with none is left alone,
    // because its scripts run while it is open and a save would write what they changed. The arguments go to
    // the game after its flags, such as the scene to run in place of the main one.
    void Play(params string[] arguments)
    {
        if (!SaveEdits())
        {
            return;
        }

        try
        {
            _scene!.Play(_graph.Breakpoints.All, _script.Breakpoints.All, [.. _game.Flags, .. arguments], _game.Copies);
        }
        catch (IOException error)
        {
            Fail(error.Message);
            _toasts.Show(ToastKind.Failed, "The game could not start", error.Message);
            return;
        }

        _playedAt = _time.GetUtcNow();
        _outputAtPlay = _scene.Output.Count;
        ShowGame();
        _bottomBar.OpenOutputUnlessPinned();
        Say("Playing in a second window.");
    }

    void PlayGame()
    {
        if (_game.RunsThisScene)
        {
            PlayScene();
            return;
        }

        Play();
    }

    void PlayScene() => Play("--scene", _scene!.SceneFile);

    void Stop()
    {
        _scene!.Stop();
        ShowGame();
        Say("Stopped the game.");
    }

    void ShowGame()
    {
        var playing = _scene?.IsPlaying == true;
        if (_wasPlaying && !playing && _scene is not null)
        {
            ShowGameEnded(_scene);
        }

        _wasPlaying = playing;
        _playButton.IsEnabled = _root is not null;
        _playSceneButton.IsEnabled = _root is not null && _scene is { SceneFile.Length: > 0 };
        _stopButton.IsEnabled = playing;
        _game.Show(playing, _scene?.ProjectName ?? "", _time.GetUtcNow() - _playedAt);
        _game.Offer(_playButton.IsEnabled, _playSceneButton.IsEnabled);
        _game.ShowLive(_scene is { IsPlaying: true } scene ? scene.Debug.State : DebugState.None);
        _center.Debugger.ShowGame(_scene?.GameId, _root is not null);
        _center.Debugger.ShowSession(_script.IsDebugging ? _script.DebugPanel : null);
    }

    void ShowGameEnded(IEditorScene scene)
    {
        if (!scene.EndedWithError)
        {
            _toasts.Show(ToastKind.Done, "The game stopped", $"Played {scene.ProjectName} for {Took(_time.GetUtcNow() - _playedAt)}.");
            return;
        }

        var lines = scene.Output.Skip(Math.Min(_outputAtPlay, scene.Output.Count)).ToList();
        _toasts.Show(
            ToastKind.Failed,
            "The game closed with an error",
            $"{LastError(lines)}Output has the full message.",
            new ToastAction("Show in Output", () => _bottomBar.Open(0)),
            new ToastAction("Play again", PlayGame, Parts.KeyText(new KeyGesture(Key.B, CommandKey))));
    }

    static string Took(TimeSpan time) => time.TotalMinutes >= 1 ? $"{(int)time.TotalMinutes} min {time.Seconds} s" : $"{time.Seconds} s";

    // The last error the game logged, with the file and line when the log gave them.
    static string LastError(IReadOnlyList<string> lines)
    {
        if (LogEntry.Read(lines).LastOrDefault(entry => entry.Level == LogLevel.Error) is not { } error)
        {
            return "";
        }

        var where = error.Source.Length > 0 ? $"{error.Source}: " : "";
        return $"{where}{error.Text.TrimEnd('.')}. ";
    }

    // Collapses every panel group while any shows, so a second press brings back what was open. The bottom panel
    // shuts with them and the one that was open comes back with them.
    void TogglePanels()
    {
        if (!_docks.AllCollapsed)
        {
            _docks.HidePanels();
            _shutPanel = _bottomBar.OpenPanel;
            _bottomBar.Open(-1);
            return;
        }

        _docks.ShowPanels();
        if (_shutPanel >= 0)
        {
            _bottomBar.Open(_shutPanel);
        }
    }

    void Open(string resourcePath) => Leave("open", () => Change(() => _scene!.Open(resourcePath)));

    // The app menu of macOS stays in reach under a dialog's veil, so its items wait for the dialog to close.
    public void CopyVersion() => RunUnveiled(() => Copy(BottomBar.Version, BottomBar.Version));

    public void OpenSettings() => RunUnveiled(_settings.Open);

    // The menu keys come first, by menu. The cards of the window's own buttons add the keys the menus lack.
    void OpenKeys()
    {
        var menus = EditorMenu.Keys(_globalMenu, CommandKey).ToList();
        var shown = menus.Select(row => row.Key).ToHashSet();
        var cards = this.GetLogicalDescendants().OfType<Control>().Select(ToolTip.GetTip).OfType<HoverCard>();
        // The Where keeps only cards that have a key.
        var window = cards.Where(card => card.Key is not null && !shown.Contains(card.Key)).Select(card => ("In the window", card.Title, card.Key!)).Distinct();
        _keys.Open([.. menus, .. window]);
    }

    void OpenNewScene(string folder) => _newScene.Open(_scene?.Scenes() ?? [], _planar, _scene?.AddableClasses() ?? [], folder);

    // The rows are read at each open, so Search shows the files, things and menu items as they are now. The open
    // scene comes first among the scenes, then the recent ones newest first, then the rest of the project.
    void OpenSearch(SearchScope scope)
    {
        var actions = Search.ActionsOf(NativeMenu.GetMenu(this)!).ToList();
        var scenes = new[] { _scene?.SceneFile ?? "" }.Concat(_recent).Concat(_scene?.Scenes() ?? []);
        IEnumerable<string> scripts = _scene is null ? [] : [.. _scene.Scripts(), .. _scene.GraphScripts()];
        _search.Open([
            .. _recent.Select(path => FileRow(Search.Recent, path)),
            .. TryRows(actions),
            .. Files(scenes).Select(path => FileRow(Search.Scenes, path)),
            .. Files(scripts).Select(path => FileRow(Search.Scripts, path)),
            .. _root is null ? [] : Search.ThingsOf(_root, _scenePane.Select),
            .. actions,
            .. _settings.Rows(),
            .. Search.HelpOf(_links),
        ], scope);

        static IEnumerable<string> Files(IEnumerable<string> paths) => paths.Where(path => path != "" && !path.EndsWith('/')).Distinct();
    }

    PickerRow FileRow(string group, string path) =>
        Search.FileRow(group, path, Search.KindText(path, scene => _scene?.SceneKindOf(scene) ?? ""), OpenFile) with { Key = path == _scene?.SceneFile ? "open now" : "" };

    // First steps for someone new. Each shows only while it can run.
    IEnumerable<PickerRow> TryRows(IEnumerable<PickerRow> actions)
    {
        var add = new PickerRow(Search.Try, "Cube", "Add a thing", () => _tools.AddButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)))
        {
            Note = "Puts a camera, light or shape in the scene",
            Key = Parts.KeyText(new KeyGesture(Key.A, CommandKey)),
        };
        IEnumerable<PickerRow> first = _tools.AddButton.IsEnabled ? [add] : [];
        return first.Concat(actions.Where(row => row.Name is "New scene…" or "Play the game").Select(row => row with { Group = Search.Try }));
    }

    // The dialog that makes a scene file, the flyout that instances one, and the tab's close button.
    void WireSceneFiles()
    {
        _sceneTabs.AddButton.Click += (_, _) => OpenNewScene(FileTree.Root);
        _newScene.Requested += (path, className) => Leave("create", () =>
        {
            var made = Change(() =>
            {
                _scene!.Create(path, className);
                ShowScenes();
            });
            // A failed create says why in the status line, and the dialog stays open with the same words.
            if (made)
            {
                _newScene.Dialog.Close();
            }
            else
            {
                _newScene.Say(Status);
            }
        });
        _sceneTabs.CloseButton.Click += (_, _) => Leave("close", () => Change(() => _scene!.Close()));
        _sceneTabs.ShowInFilesButton.Click += (_, _) =>
        {
            _docks.Show(_fileDock, 0);
            _scenes.Reveal(_scene!.SceneFile);
        };
        _graph.ShowInFilesButton.Click += (_, _) =>
        {
            _docks.Show(_fileDock, 0);
            _scenes.Reveal(_graph.Current!);
        };
    }

    // Opening, making or closing a scene drops the open one and its history, so edits that no save holds ask
    // first, as a close of the window does. Open scripts are asked about too, as the list matches the quit one.
    void Leave(string verb, Action leave)
    {
        if (HasEdits)
        {
            AskToSave(verb, leave);
        }
        else
        {
            leave();
        }
    }

    // Answers whether the change was made. A scene that opens has no edits yet, even when Don't save left some behind.
    bool Change(Action change)
    {
        try
        {
            change();
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or IOException)
        {
            Fail(error.Message);
            return false;
        }

        ShowEdited(false);
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

    void ShowRecent()
    {
        EditorMenu.ShowRecent(NativeMenu.GetMenu(this)!, _recent, Open, () =>
        {
            _recent.Clear();
            _openedAt.Clear();
            ShowRecent();
        });
        _center.NoScene.ShowRecent(RecentScenes());
    }

    // Read at each call, so the times say how long ago from now.
    IEnumerable<(string, string)> RecentScenes()
    {
        var now = _time.GetLocalNow();
        return _recent.Select(path => (path, NoScene.Opened(_openedAt[path], now)));
    }

    // shortcut: one file stat per scene on each refresh. Read the times only for a newest first sort when a project has thousands.
    void ShowScenes()
    {
        var project = _files!;
        IReadOnlyList<string> files = [.. project.Scenes(), .. project.Scripts(), .. project.GraphScripts(), .. project.ImportedFiles()];
        _scenes.Show(files, files.Where(path => !path.EndsWith('/')).Distinct().ToDictionary(path => path, project.ModifiedOf), project.SceneKindOf);
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
            case FileKind.Imported:
                _import.Open(resourcePath);
                _docks.Show(_sceneDock, 1);
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
            FailScript(_script.Status.Text ?? "");
            return false;
        }

        if (!_graph.Save())
        {
            FailScript(_graph.Status.Text ?? "");
            return false;
        }

        return true;
    }

    // A save from Play or the Save changes dialog happens where the script view may be hidden, so the toast says it too.
    void FailScript(string why)
    {
        Fail(why);
        _toasts.Show(ToastKind.Failed, "Couldn't save the script", why, new ToastAction("Try again", SaveAll));
    }

    bool Save()
    {
        try
        {
            _scene!.Save();
            var count = _edits;
            ShowEdited(false);
            _since = _time.GetLocalNow();
            Say("Saved.", StatusKind.Done);
            var at = $"At {_since.ToString("HH:mm", CultureInfo.InvariantCulture)}.";
            _toasts.Show(ToastKind.Done, $"Saved {SceneLabel()}", count == 0 ? at : $"{at} {count} {(count == 1 ? "change" : "changes")} saved.");
            return true;
        }
        catch (IOException error)
        {
            Fail(error.Message);
            _toasts.Show(ToastKind.Failed, $"Couldn't save {SceneLabel()}", error.Message, new ToastAction("Try again", SaveAll));
            return false;
        }
    }

    Grid Layout()
    {
        Grid.SetRow(_docks, 1);
        return new Grid { RowDefinitions = new RowDefinitions($"{TitleBar.BarHeight},*"), Children = { _titleBar, _docks } };
    }
}
