using Avalonia.Controls;
using Avalonia.LogicalTree;
using Orblit.Editor.App;
using Orblit.Editor.App.Shell;
using Orblit.Editor.App.Tests;
using Orblit.Editor.App.Tests.Graph;
using Orblit.Editor.Bridge;
using static Orblit.Editor.App.Tests.ReferenceWindow;

namespace Orblit.Editor.Layout.Checks;

/// <summary>Every workspace, panel and dialog of the editor window, filled, at the smallest window and a large one.</summary>
internal static class EditorViews
{
    // The smallest window the editor allows, and a common large screen.
    static readonly (int Width, int Height)[] s_sizes = [(1280, 720), (1920, 1080)];

    static readonly (string Name, Action<EditorWindow, FakeScene> Show)[] s_views =
    [
        ("3D", (window, _) => Select(window)),
        ("3D, sides narrow", (window, _) => Then(window, Select, w => Sides(w, wide: false))),
        ("3D, sides wide", (window, _) => Then(window, Select, w => Sides(w, wide: true))),
        ("3D, sides folded", (window, _) => Fold(window)),
        ("2D", (window, _) => Tab(window, "2D")),
        ("Script", (window, _) => Tab(window, "Script")),
        ("Script open", (window, _) => OpenScript(window)),
        ("Graph", (window, _) => Tab(window, "Graph")),
        ("Graph open", (window, _) => OpenGraph(window)),
        ("Game", (window, _) => Tab(window, "Game")),
        ("Game running", RunGame),
        ("Output", (window, _) => BarOf(window).Open(0)),
        ("Debugger", (window, _) => BarOf(window).Open(1)),
        ("2D, middle narrow", Narrow((window, _) => Tab(window, "2D"))),
        ("Script open, middle narrow", Narrow((window, _) => OpenScript(window))),
        ("Graph open, middle narrow", Narrow((window, _) => OpenGraph(window))),
        ("Game running, middle narrow", Narrow(RunGame)),
        ("Output, middle narrow", Narrow((window, _) => BarOf(window).Open(0))),
        ("Debugger, middle narrow", Narrow((window, _) => BarOf(window).Open(1))),
        ("Long status, middle narrow", Narrow((window, _) => window.Fail("Could not save res://levels/a/very/long/path/to/something/deep/inside/the/project/folder.oscn because the disk is full."))),
        ("Add a thing", (window, _) => Then(window, Select, w => Click(w.Tools.AddButton))),
        ("Move under", (window, _) => Then(window, Select, w => Click(w.Move.MoveUnderButton))),
        ("Instance a scene", (window, _) => Then(window, Select, w => Click(w.InstanceScene.OpenButton))),
        ("Connect a signal", (window, _) => Then(window, Select, w => Click(ConnectButton(w)))),
        ("Add a script", (window, _) => Then(window, Select, w => Click(w.Inspector.AttachButton))),
        ("Add a custom value", (window, _) => Then(window, Select, w => Click(w.Inspector.MetadataButton))),
        ("New C# script", (window, _) => Then(window, w => Tab(w, "Script"), w => Click(w.Script.NewButton))),
        ("New graph", (window, _) => Then(window, w => Tab(w, "Graph"), w => w.Graph.ShowCreate())),
        ("New scene", (window, _) => Menu(window, "New scene…")),
        ("Find a scene", (window, _) => Menu(window, "Find a scene…")),
        ("Search", (window, _) => Menu(window, "Search")),
        ("Export", (window, _) => Menu(window, "Export game…")),
        ("All keys", (window, _) => Menu(window, "All keys")),
        ("Settings", (window, _) => window.OpenSettings()),
        ("Save changes", (window, _) => Then(window, MakeAnEdit, w => w.Close())),
    ];

    // The same views on a project with no scene open.
    static readonly (string Name, Action<EditorWindow, FakeScene> Show)[] s_emptyViews =
    [
        ("No scene", (_, _) => { }),
        ("No scene, Game", (window, _) => Tab(window, "Game")),
    ];

    // Views of a project with many files open and long names.
    static readonly (string Name, Action<EditorWindow, FakeScene> Show)[] s_crowdedViews =
    [
        ("Long names, 3D", (window, _) => Select(window)),
        ("Many scripts open", Narrow((window, scene) => OpenAll(window, "Script", scene.ScriptText.Keys, path => window.Script.List.SelectedItem = path))),
        ("Many graphs open", Narrow((window, scene) => OpenAll(window, "Graph", scene.Graphs.Keys, path => window.Graph.List.SelectedItem = path))),
    ];

    public static IEnumerable<(string Name, IReadOnlyList<string> Faults)> All(Look look) =>
        from size in s_sizes
        from view in s_views.Select(view => (view, Scene: (Func<FakeScene>)(() => Filled(false))))
            .Concat(s_emptyViews.Select(view => (view, Scene: (Func<FakeScene>)(() => Filled(true)))))
            .Concat(s_crowdedViews.Select(view => (view, Scene: (Func<FakeScene>)Crowded)))
        select Run(look, size, view.view, view.Scene());

    static (string, IReadOnlyList<string>) Run(Look look, (int Width, int Height) size, (string Name, Action<EditorWindow, FakeScene> Show) view, FakeScene scene)
    {
        var window = new EditorWindow { Width = size.Width, Height = size.Height };
        window.Show();
        window.Load(scene);
        window.Poll();
        var label = $"{size.Width}x{size.Height} {view.Name}";
        var faults = look.Try(window, label, () => view.Show(window, scene));
        Shut(window);
        return (label, faults);
    }

    // A script, a graph and more output than fits, so every list and every line is long.
    static FakeScene Filled(bool empty) => Fill(new FakeScene { StartsEmpty = empty });

    // A long project name and scene name, and more scripts and graphs than their tab rows hold.
    static FakeScene Crowded()
    {
        var scene = Fill(new FakeScene
        {
            ProjectName = "A project with a very long name that keeps going on and on",
            SceneFile = "res://levels/a_scene_with_a_very_long_name_that_keeps_going_on_and_on.oscn",
        });
        foreach (var word in new[] { "player", "enemy", "pickup", "door", "camera", "menu" })
        {
            var script = $"res://{word}_behaviour_with_a_very_long_name_that_keeps_going.cs";
            scene.ScriptPaths.Add(script);
            scene.ScriptText[script] = "class Part\n{\n    public void A_method_with_a_very_long_name_that_keeps_going_and_going() { }\n}";
            scene.Graphs[$"res://{word}_behaviour_with_a_very_long_name_that_keeps_going.ograph"] = FakeGraphSource.Sample();
        }

        return scene;
    }

    static FakeScene Fill(FakeScene scene)
    {
        scene.ScriptText["res://spin.cs"] = "class Spin\n{\n    public override void _Process(double delta) { Rotation += new Vector3(0, (float)delta, 0); }\n}";
        scene.Graphs["res://logic.ograph"] = FakeGraphSource.Sample();
        for (var line = 0; line < 40; line++)
        {
            scene.Unread.Enqueue($"Line {line}: res://levels/a/very/long/path/to/something/deep/inside/the/project/folder/that/does/not/fit.oscn was loaded and checked");
        }

        return scene;
    }

    static void Then(EditorWindow window, Action<EditorWindow> first, Action<EditorWindow> second)
    {
        first(window);
        second(window);
    }

    static void Select(EditorWindow window) => window.Outline.SelectedItem = window.Outline.ItemsSource!.Cast<OutlineNode>().Single().Children[0];

    static void Tab(EditorWindow window, string workspace) => ClickOn(window.Bar.Tabs.Single(tab => tab.Label == workspace), window);

    static BottomBar BarOf(EditorWindow window) => (BottomBar)window.Center.BottomBar.Child!;

    static void OpenScript(EditorWindow window)
    {
        Tab(window, "Script");
        window.Script.List.SelectedItem = "res://spin.cs";
    }

    static void OpenGraph(EditorWindow window)
    {
        Tab(window, "Graph");
        window.Graph.List.SelectedItem = "res://logic.ograph";
    }

    static void OpenAll(EditorWindow window, string workspace, IEnumerable<string> paths, Action<string> open)
    {
        Tab(window, workspace);
        foreach (var path in paths.ToList())
        {
            open(path);
        }
    }

    static void RunGame(EditorWindow window, FakeScene scene)
    {
        Tab(window, "Game");
        Click(window.Game.RunButton);
        scene.Channel.Next = FakeDebugChannel.Running() with
        {
            Monitors = new DebugMonitors(Fps: 60, Process: 0.004, Physics: 0.001, DrawCalls: 12, Objects: 340, VideoMemory: 50 * 1024 * 1024),
            Scene = [new("Level", "Entity3D", 0), new("Props", "Entity3D", 1), new("Crate", "MeshInstance3D", 2), new("Sun", "DirectionalLight3D", 1)],
        };
        window.Poll();
    }

    // The view with both sides as wide as they go.
    static Action<EditorWindow, FakeScene> Narrow(Action<EditorWindow, FakeScene> show) => (window, scene) =>
    {
        show(window, scene);
        Sides(window, wide: true);
    };

    // Both sides at the least width a splitter allows, or both as wide as they go with the middle at its least.
    static void Sides(EditorWindow window, bool wide)
    {
        window.LeftSide.Fold(false);
        window.RightSide.Fold(false);
        window.UpdateLayout();
        var body = (Grid)window.LeftSide.Parent!;
        var (left, middle, right) = (body.ColumnDefinitions[0], body.ColumnDefinitions[2], body.ColumnDefinitions[4]);
        var spare = body.Bounds.Width - body.ColumnDefinitions[1].ActualWidth - body.ColumnDefinitions[3].ActualWidth - middle.MinWidth;
        left.Width = new GridLength(wide ? Math.Min(left.MaxWidth, spare / 2) : left.MinWidth);
        right.Width = new GridLength(wide ? Math.Min(right.MaxWidth, spare / 2) : right.MinWidth);
    }

    static void Fold(EditorWindow window)
    {
        window.LeftSide.Fold(true);
        window.RightSide.Fold(true);
    }

    static Button ConnectButton(EditorWindow window) =>
        window.Signals.List.GetLogicalDescendants().OfType<DockPanel>().First().Children.OfType<Button>().First();

    static void Menu(EditorWindow window, string header)
    {
        var item = Items(NativeMenu.GetMenu(window)!).Single(item => item.Header == header);
        item.Command!.Execute(item.CommandParameter);
    }

    static IEnumerable<NativeMenuItem> Items(NativeMenu menu) =>
        menu.Items.OfType<NativeMenuItem>().SelectMany(item => item.Menu is { } inner ? Items(inner).Prepend(item) : [item]);

    // Closes what a view opened. A window with edits asks first, so the view's edits are dropped.
    static void Shut(EditorWindow window)
    {
        foreach (var dialog in ((Panel)window.Content!).Children.OfType<Dialog>())
        {
            dialog.Close();
        }

        window.Close();
        if (window.SaveChanges.IsVisible)
        {
            Click(window.SaveChanges.LeadButton!);
        }
    }
}
