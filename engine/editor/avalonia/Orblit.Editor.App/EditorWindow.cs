using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
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
    readonly Flyout _newSceneFlyout = new();
    readonly Flyout _instanceFlyout = new();
    readonly CloseBar _closeBar = new();
    readonly ScenesPanel _scenes = new();
    readonly TextBlock _status = new() { Text = "Starting the engine." };
    // Save and Frame are commands without a place of their own on screen. The shortcuts, the menus and the
    // tests press them, and a button off keeps its key unhandled.
    readonly Button _save = new() { IsEnabled = false };
    readonly Button _playButton = Parts.IconButton("MainPlay", "Run the project");
    readonly Button _stopButton = Parts.IconButton("Stop", "Stop the game");
    readonly Button _frame = new() { IsEnabled = false };
    readonly SceneTabStrip _sceneTabs = new();
    readonly ViewportToolbar _toolbar;
    readonly TitleBar _titleBar;
    readonly BottomBar _bottomBar;
    readonly DispatcherTimer _pollTimer = new() { Interval = PollInterval };
    readonly ScenePane _scenePane;
    readonly DockTabs _sceneDock;
    readonly DockTabs _fileDock;
    readonly InspectorPane _inspectorPane;
    readonly DockTabs _inspectorDock;
    readonly CenterArea _center;
    readonly StoreView _store;
    readonly ScriptView _script = new();
    readonly GraphView _graph = new();
    readonly GameView _game = new();
    readonly Dictionary<DockSlot, Border> _slots = Enum.GetValues<DockSlot>().ToDictionary(slot => slot, _ => new Border());
    IEditorScene? _scene;
    OutlineNode? _root;
    bool _edited;
    bool _planar;
    // The Entity3D the gizmo drags, or zero when the selection has none.
    nint _gizmoTarget;

    // The screen bar of macOS has its own app menu, so a window on any other system also carries Quit and About.
    public EditorWindow(bool? globalMenu = null, Func<CancellationToken, Task<string>>? readStore = null)
    {
        _store = new StoreView(readStore ?? new KitIndexSource().ReadAsync);
        Title = TitleText();
        Icon = EditorIcon.ToWindowIcon("Orblit", 64);
        Width = 1440;
        Height = 900;
        ExtendClientAreaToDecorationsHint = true;
        ExtendClientAreaTitleBarHeightHint = TitleBar.BarHeight;
        _scenePane = new ScenePane(_tools, _move);
        _scenePane.SelectionChanged += ShowSelection;
        _scenePane.EyeClicked += ToggleEye;
        _tools.Changed += Edited;
        _tools.Failed += message => _status.Text = message;
        _move.Changed += Edited;
        _move.Failed += message => _status.Text = message;
        _inspector.Edited += () =>
        {
            ShowEdited(true);
            HighlightSelected();
            _status.Text = "Edited. Save keeps the change.";
        };
        _inspector.Failed += message => _status.Text = message;
        // A failed save leaves the edits, so the close asks again.
        _closeBar.SaveRequested += () =>
        {
            SaveEdits();
            Close();
        };
        _closeBar.DiscardRequested += () =>
        {
            _script.Discard();
            _graph.Discard();
            ShowEdited(false);
            Close();
        };
        _scenes.Chosen += Open;
        WireSceneFiles();
        _playButton.IsEnabled = false;
        _stopButton.IsEnabled = false;
        _save.Click += (_, _) => SaveAll();
        _playButton.Click += (_, _) => Play();
        _stopButton.Click += (_, _) => Stop();
        _game.RunRequested += Play;
        _game.StopRequested += Stop;
        _frame.Click += (_, _) => Frame();
        AddHandler(Unbuilt.Event, (_, e) => _status.Text = e.Message);
        _titleBar = new TitleBar(_playButton, _stopButton);
        _titleBar.Title.Bind(TextBlock.TextProperty, this.GetObservable(TitleProperty));
        _sceneDock = new DockTabs(("Scene", _scenePane), ("Import", new Panel())) { Slot = DockSlot.UpperLeft };
        _fileDock = new DockTabs(("FileSystem", _scenes), ("History", new Panel())) { Slot = DockSlot.LowerLeft };
        _inspectorPane = new InspectorPane(_inspector);
        _inspectorDock = new DockTabs(("Inspector", _inspectorPane), ("Signals", new Panel())) { Slot = DockSlot.Right };
        WireDocks();
        _toolbar = new ViewportToolbar(_frame);
        _toolbar.LockButton.Click += (_, _) => ToggleFlag(_scene!.SetLocked, selected => selected.Locked, "Locked", "Unlocked");
        _toolbar.GroupButton.Click += (_, _) => ToggleFlag(_scene!.SetGrouped, selected => selected.Grouped, "Grouped", "Ungrouped");
        _toolbar.KeepChildrenButton.IsCheckedChanged += (_, _) => _scene?.KeepChildren = _toolbar.KeepChildren;
        _bottomBar = new BottomBar(_status);
        _center = new CenterArea(_sceneTabs, _toolbar, _viewport, _bottomBar);
        _center.Output.ClearRequested += () => _scene?.ClearOutput();
        _center.AddWorkspace(TitleBar.Script, _script);
        _center.AddWorkspace(TitleBar.Graph, _graph);
        _center.AddWorkspace(TitleBar.Game, _game);
        _center.AddWorkspace(TitleBar.Store, _store);
        _titleBar.WorkspaceChosen += ChooseWorkspace;
        _pollTimer.Tick += (_, _) => Poll();
        Closed += (_, _) => _pollTimer.Stop();
        WireMenus();
        Content = Layout();
        NativeMenu.SetMenu(this, EditorMenu.Build(globalMenu ?? OperatingSystem.IsMacOS(), CommandKey, MenuActions(), message => _status.Text = message));
    }

    internal TitleBar Bar => _titleBar;

    internal DockTabs SceneDock => _sceneDock;

    internal DockTabs FileDock => _fileDock;

    internal DockTabs InspectorDock => _inspectorDock;

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

    internal Flyout NewSceneFlyout => _newSceneFlyout;

    internal Flyout InstanceFlyout => _instanceFlyout;

    internal CloseBar CloseBar => _closeBar;

    internal ContentControl Viewport => _viewport;

    internal ScenesPanel Scenes => _scenes;

    internal Button SaveButton => _save;

    internal Button PlayButton => _playButton;

    internal Button StopButton => _stopButton;

    internal GameView Game => _game;

    internal Button FrameButton => _frame;

    internal string Status => _status.Text ?? "";

    /// <summary>The modifier that makes a shortcut: Command on macOS, Control elsewhere.</summary>
    internal KeyModifiers CommandKey => this.GetPlatformSettings()?.HotkeyConfiguration.CommandModifiers ?? KeyModifiers.Control;

    public void Load(IEditorScene scene)
    {
        _scene = scene;
        // The button can be pressed while the engine starts, before there is a scene to tell.
        scene.KeepChildren = _toolbar.KeepChildren;
        _script.Show(scene);
        _graph.Show(scene);
        _center.Output.Show(scene.Output);
        _pollTimer.Start();
        ShowScenes();
        Reload(0);
        _status.Text = OpenedStatus();
    }

    public void Fail(string message) => _status.Text = message;

    /// <summary>Reads the lines the engine logged since the last call and shows whether the game still runs.</summary>
    internal void Poll()
    {
        _scene?.PollOutput();
        _graph.PollDebugger();
        ShowGame();
    }

    public void ShowViewport(ViewportControl viewport)
    {
        viewport.Ended += () => _status.Text = "The engine window was closed. Restart the editor to see the scene.";
        viewport.Clicked += SelectAt;
        viewport.PlanarSource = () => _planar;
        viewport.MarkSource = PivotOfSelected;
        viewport.PlanarMoved += MoveSelected;
        viewport.ModeSource = () => _toolbar.Mode;
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
        Title = TitleText();
        ShowSelection();
    }

    // The 2D view is on for the 2D workspace alone, so any other workspace gives the viewport back to 3D.
    void ChooseWorkspace(string name)
    {
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
        _toolbar.TransformMenuButton.IsEnabled = spatial;
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
        _status.Text = $"{verb} {selected.Name}. Save keeps the change.";
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
            _closeBar.IsVisible = true;
        }
    }

    // Save is on while the Save button is. Quit and fullscreen are the other menu items the editor can do.
    Dictionary<string, MenuAction> MenuActions() => new()
    {
        ["Save Scene"] = new(() => _save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)), _save.GetObservable(IsEnabledProperty)),
        ["Quit"] = new(Close),
        ["Toggle Fullscreen"] = new(() => WindowState = WindowState == WindowState.FullScreen ? WindowState.Normal : WindowState.FullScreen),
    };

    // A shortcut presses the button it stands for, so a key and a click run the same code. A button that is
    // off leaves the key unhandled.
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (ShortcutFor(e) is { IsEnabled: true } button)
        {
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            e.Handled = true;
        }
    }

    // F, Q, W, E, R and T are plain letters, so a box that takes typing keeps them.
    Button? ShortcutFor(KeyEventArgs e) => (e.Key, e.KeyModifiers) switch
    {
        (Key.S, var modifiers) when modifiers == CommandKey => _save,
        (Key.D, var modifiers) when modifiers == CommandKey => _tools.DuplicateButton,
        (Key.F, KeyModifiers.None) when e.Source is not (TextBox or ComboBox) => _frame,
        (Key.Q, KeyModifiers.None) when e.Source is not (TextBox or ComboBox) => _toolbar.SelectButton,
        (Key.W, KeyModifiers.None) when e.Source is not (TextBox or ComboBox) => _toolbar.MoveButton,
        (Key.E, KeyModifiers.None) when e.Source is not (TextBox or ComboBox) => _toolbar.RotateButton,
        (Key.R, KeyModifiers.None) when e.Source is not (TextBox or ComboBox) => _toolbar.ScaleButton,
        (Key.T, KeyModifiers.None) when e.Source is not (TextBox or ComboBox) => _toolbar.LocalButton,
        _ => null,
    };

    // A tool changed the scene: read it again and show what it did.
    void Edited(EntityEdit edit)
    {
        Reload(edit.Select);
        ShowEdited(true);
        _status.Text = edit.Message;
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
            _status.Text = error.Message;
        }
    }

    // The docks sit in three slots. A dock sent to a slot trades places with the dock that sits there.
    void WireDocks()
    {
        foreach (var dock in new[] { _sceneDock, _fileDock, _inspectorDock })
        {
            _slots[dock.Slot].Child = dock;
            dock.MoveRequested += to => MoveDock(dock, to);
        }
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
        _status.Text = $"Moved {dock.Title} to the {to.Label().ToLowerInvariant()} slot. The layout is not saved.";
    }

    // The Transform menu changes the selected entity's place, the Mesh menu gives a mesh a collision shape, and the
    // projection menu switches the viewport's camera.
    void WireMenus()
    {
        _toolbar.ResetPositionItem.Click += (_, _) => Transform("Reset the position of", entity => _scene!.MoveTo(entity, Vector3.Zero));
        _toolbar.ResetRotationItem.Click += (_, _) => Transform("Reset the rotation of", entity => _scene!.RotateTo(entity, Vector3.Zero));
        _toolbar.ResetScaleItem.Click += (_, _) => Transform("Reset the scale of", entity => _scene!.ScaleTo(entity, Vector3.One));
        _toolbar.SnapPositionItem.Click += (_, _) =>
            Transform("Snapped the position of", entity => _scene!.MoveTo(entity, GizmoMath.Stepped(_scene.PositionOf(entity), _toolbar.Steps.Move)));
        _toolbar.SnapRotationItem.Click += (_, _) =>
            Transform("Snapped the rotation of", entity => _scene!.RotateTo(entity, GizmoMath.Stepped(_scene.RotationOf(entity), _toolbar.Steps.Turn)));
        _toolbar.TrimeshItem.Click += (_, _) => AddCollision(false);
        _toolbar.ConvexItem.Click += (_, _) => AddCollision(true);
        _center.Overlay.ProjectionChanged += orthogonal =>
        {
            _scene?.SetOrthogonal(orthogonal);
            _status.Text = orthogonal ? "Orthogonal projection." : "Perspective projection.";
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
        _status.Text = $"Framed {selected.Name}.";
    }

    // The title of the old editor: the scene file, then the project, with a mark in front for edits that
    // the file does not hold yet.
    string TitleText()
    {
        var project = _scene?.ProjectName is { Length: > 0 } name ? name : "Unnamed Project";
        var title = _scene?.SceneFile is { Length: > 0 } file ? $"{Path.GetFileName(file)} - {project}" : project;
        return _edited ? $"(*) {title}" : title;
    }

    // The tab is named for the scene file. A scene with no file takes the name of its root.
    string SceneLabel() => _scene!.SceneFile is { Length: > 0 } file ? Path.GetFileNameWithoutExtension(file) : _root?.Name ?? "";

    void ShowEdited(bool edited)
    {
        _edited = edited;
        Title = TitleText();
        _closeBar.IsVisible &= edited;
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
            _status.Text = error.Message;
            return;
        }

        ShowGame();
        _bottomBar.OpenOutputUnlessPinned();
        _status.Text = "Playing in a second window.";
    }

    void Stop()
    {
        _scene!.Stop();
        ShowGame();
        _status.Text = "Stopped the game.";
    }

    void ShowGame()
    {
        var playing = _scene?.IsPlaying == true;
        _playButton.IsEnabled = _root is not null;
        _stopButton.IsEnabled = playing;
        _game.Show(playing, _root is not null);
    }

    void Open(string resourcePath) => Leave(() => _scene!.Open(resourcePath));

    // The flyouts that make a scene file and instance one, and the tab's close button.
    void WireSceneFiles()
    {
        _newSceneFlyout.Content = _newScene;
        _sceneTabs.AddButton.Flyout = _newSceneFlyout;
        _newScene.Requested += (name, className) =>
        {
            var left = Leave(() =>
            {
                _scene!.Create($"{FileTree.Root}{name}.oscn", className);
                ShowScenes();
            });
            if (left)
            {
                _newSceneFlyout.Hide();
                _newScene.Clear();
            }
        };
        _instanceFlyout.Content = _instanceScene;
        _scenePane.InstanceButton.Flyout = _instanceFlyout;
        _instanceFlyout.Opening += (_, _) => _instanceScene.LoadScenes();
        _instanceScene.Changed += edit =>
        {
            _instanceFlyout.Hide();
            Edited(edit);
        };
        _instanceScene.Failed += message => _status.Text = message;
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
            _status.Text = error.Message;
            return false;
        }

        Reload(0);
        _status.Text = OpenedStatus();
        return true;
    }

    // shortcut: one file stat per scene on each refresh. Read the times only for a newest first sort when a project has thousands.
    void ShowScenes()
    {
        var scenes = _scene!.Scenes();
        _scenes.Show(scenes, scenes.Where(path => !path.EndsWith('/')).ToDictionary(path => path, _scene.ModifiedOf));
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
            _status.Text = _script.Status.Text ?? "";
            return false;
        }

        if (!_graph.Save())
        {
            _status.Text = _graph.Status.Text ?? "";
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
            _status.Text = "Saved.";
            return true;
        }
        catch (IOException error)
        {
            _status.Text = error.Message;
            return false;
        }
    }

    // The title bar on top, then the columns with the 4 pixel margin and the 3 pixel gaps of the reference.
    Control Layout()
    {
        var left = new Grid { RowDefinitions = new RowDefinitions("*,4,*") };
        Fill(left, _slots[DockSlot.UpperLeft], 0);
        Fill(left, Splitter(false), 1);
        Fill(left, _slots[DockSlot.LowerLeft], 2);
        var body = new Grid { Margin = new Thickness(4, 0, 4, 4), ColumnDefinitions = new ColumnDefinitions("280,3,*,3,280") };
        Fill(body, left, 0);
        Fill(body, Splitter(true), 1);
        Fill(body, _center, 2);
        Fill(body, Splitter(true), 3);
        Fill(body, _slots[DockSlot.Right], 4);
        var root = new Grid { RowDefinitions = new RowDefinitions($"{TitleBar.BarHeight},Auto,*") };
        Fill(root, _titleBar, 0);
        Fill(root, _closeBar, 1);
        Fill(root, body, 2);
        return root;
    }

    static GridSplitter Splitter(bool vertical) => new()
    {
        Background = Brushes.Transparent,
        ResizeDirection = vertical ? GridResizeDirection.Columns : GridResizeDirection.Rows,
    };

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
