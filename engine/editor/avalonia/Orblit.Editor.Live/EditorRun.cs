using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.VisualTree;
using Orblit.Editor.App;
using Orblit.Editor.App.Shell;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.Live;

/// <summary>
/// Drives the editor window with injected pointer and key events and reads every result back from the engine.
/// A step passes when the engine holds the value the gesture should have made. Each step saves what the window drew.
/// </summary>
internal sealed partial class EditorRun(EditorWindow window, EditorEngine engine, string project, string output)
{
    const int PollMilliseconds = 50;
    const int TimeoutMilliseconds = 15000;
    const int SettleMilliseconds = 400;
    const double ArrowShare = 0.5;
    const double MovePixels = 70;
    const double SnappedMovePixels = 150;
    const double TurnPixels = 50;
    const float Close = 1e-3f;

    readonly InjectedInput _input = new();
    int _number;
    bool _failed;
    nint _box;

    ViewportControl Viewport => (ViewportControl)window.Viewport.Content!;

    /// <summary>Runs every step and answers whether all passed.</summary>
    public async Task<bool> Run()
    {
        await Step("the window shows the engine's first frame", ShowsTheFrame);
        await Step("a click on an outline row selects the entity", SelectsOnAClick);
        await Step("View, then Focus Selection, puts the pivot in the middle of the frame", FocusesFromTheMenu);
        await Step("a drag along the Move arrow moves the entity along that axis only", MovesAlongAnArrow);
        await Step("a drag on the Scale center handle scales all three axes together", ScalesFromTheCentre);
        await Step("the E key lights Rotate, and a drag on a ring turns the entity about that axis only", TurnsOnARing);
        await Step("Local and Snap by click, then a drag moves the entity by whole steps", SnapsInLocalSpace);
        await Step("Command and S write the live transform to the scene file", SaveWritesTheTransform);
        await Step("Add Metadata makes a value on the entity and Command and S writes it to the scene file", KeepsMetadata);
        await Step("the Output button opens the panel with the engine's log lines", OpensTheEngineLog);
        await Step("Clear empties the Output panel and a second click on its button shuts it", ClearsAndShutsTheLog);
        await Step("Play starts the game, opens the Output panel and shows the game's own log line", PlayShowsTheGameLog);
        await Step("the Game tab runs and stops the game, and shows a game the user closed as stopped", RunsFromTheGameTab);
        await Step("the Asset Store tab shows the store's kits and the 3D tab brings the viewport back", ShowsTheStore);
        await Step("the Script tab lists the project's C# files, and Save and Command S write an edit to the file", EditsAScript);
        await Step("the Graph tab makes a graph script with File and New, lists it, opens it and closes it again", MakesAGraphScript);
        await Step("the 2D tab draws a 2D scene flat, and a click, a drag on the mark, a pan and the wheel work", WorksIn2D);
        await Step("the operating system shows the editor's windows", ShowsInTheDesktop);
        return !_failed;
    }

    async Task ShowsTheFrame()
    {
        await Wait(() => Viewport.Frame is { PixelSize.Width: > 0 }, "a frame in the viewport");
        Expect(Viewport.Frame!.PixelSize.Height > 0, "the frame has no height");
    }

    async Task SelectsOnAClick()
    {
        await Wait(() => RowNamed("Box") is not null, "the Box row in the outline");
        await _input.Click(RowNamed("Box")!);
        var selected = window.Outline.SelectedItem as OutlineNode;
        Expect(selected?.Name == "Box", $"the selection is {selected?.Name ?? "nothing"}");
        _box = selected!.Handle;
    }

    async Task FocusesFromTheMenu()
    {
        await _input.Click(window.Toolbar.ViewButton);
        await Wait(() => window.Toolbar.ViewButton.Flyout?.IsOpen == true && window.Toolbar.FocusItem.IsAttachedToVisualTree(), "the View menu to open");
        await _input.Click(window.Toolbar.FocusItem);
        await Settle();
        var middle = new Vector2(Viewport.Frame!.PixelSize.Width, Viewport.Frame.PixelSize.Height) / 2;
        var origin = Handles().Origin;
        Expect(Vector2.Distance(origin, middle) < 4, $"the pivot is at {origin} and the middle is {middle}");
    }

    async Task MovesAlongAnArrow()
    {
        await ChooseMode(window.Toolbar.MoveButton, GizmoMode.Move);
        var before = engine.PositionOf(_box);
        await DragAlongAnArrow(GizmoAxis.X, GizmoPart.Move, MovePixels);
        var moved = engine.PositionOf(_box) - before;
        Expect(moved.X > 0.01f && MathF.Abs(moved.Y) < Close && MathF.Abs(moved.Z) < Close, $"the entity moved by {moved}");
    }

    async Task ScalesFromTheCentre()
    {
        await ChooseMode(window.Toolbar.ScaleButton, GizmoMode.Scale);
        var before = engine.ScaleOf(_box);
        var center = Local(Handles().Origin);
        Expect(HitAt(center)?.Part == GizmoPart.StretchAll, $"a press on the pivot takes {HitAt(center)}");
        await DragFrom(center, new Vector2(60, 0));
        var factor = engine.ScaleOf(_box) / before;
        Expect(MathF.Abs(factor.X - 1) > 0.05f, $"the scale did not change, factor {factor}");
        Expect(MathF.Abs(factor.X - factor.Y) < Close && MathF.Abs(factor.X - factor.Z) < Close, $"the axes scaled apart, factor {factor}");
    }

    async Task TurnsOnARing()
    {
        await _input.Tap(window, Key.E);
        Expect(window.Toolbar.Mode == GizmoMode.Rotate, $"the mode is {window.Toolbar.Mode}");
        await Settle();
        var ring = Viewport.RingsSource!()!.Value.Of(GizmoAxis.Y);
        var onRing = Enumerable.Range(0, ring.Length).First(i => HitAt(Local(ring[i])) == new GizmoHit(GizmoPart.Turn, GizmoAxis.Y));
        var from = Local(ring[onRing]);
        var along = Vector2.Normalize(Local(ring[(onRing + 1) % ring.Length]) - from);
        var before = engine.RotationOf(_box);
        await DragFrom(from, along * (float)TurnPixels);
        var turned = engine.RotationOf(_box) - before;
        Expect(MathF.Abs(turned.Y) > 2 && MathF.Abs(turned.X) < 0.5f && MathF.Abs(turned.Z) < 0.5f, $"the entity turned by {turned} degrees");
    }

    async Task SnapsInLocalSpace()
    {
        await _input.Tap(window, Key.W);
        await _input.Click(window.Toolbar.LocalButton);
        await _input.Click(window.Toolbar.SnapButton);
        Expect(window.Toolbar.Space == GizmoSpace.Local && window.Toolbar.Snaps, $"the space is {window.Toolbar.Space}, snap is {window.Toolbar.Snaps}");
        // The step box takes text, which this run does not type. Setting it is the only part that is not injected.
        window.Toolbar.SnapMoveBox.Text = "0.25";
        await Settle();
        var before = engine.PositionOf(_box);
        await DragAlongAnArrow(GizmoAxis.Y, GizmoPart.Move, SnappedMovePixels);
        var moved = engine.PositionOf(_box) - before;
        var steps = moved.Y / window.Toolbar.Steps.Move;
        Expect(MathF.Abs(steps) >= 1 && MathF.Abs(steps - MathF.Round(steps)) < Close, $"the move was {moved.Y}, which is {steps} steps");
        Expect(MathF.Abs(moved.X) < Close && MathF.Abs(moved.Z) < Close, $"the entity left its axis, moved by {moved}");
    }

    async Task SaveWritesTheTransform()
    {
        var file = Path.Combine(project, "main.oscn");
        var before = OriginIn(File.ReadAllText(file));
        Expect(Edited, $"the title shows no unsaved edits: {window.Title}");
        // The Save button is a command with no place in the window, so the shortcut is the way a person reaches it.
        await _input.Tap(window, Key.S, RawInputModifiers.Meta);
        await Settle();
        var saved = OriginIn(File.ReadAllText(file));
        var live = engine.PositionOf(_box);
        Expect(saved != before, $"the file kept the origin {before}");
        Expect(Vector3.Distance(saved, live) < 1e-2f, $"the file holds {saved} and the engine holds {live}");
        Expect(!Edited, $"the title still shows unsaved edits: {window.Title}");
    }

    async Task KeepsMetadata()
    {
        var tool = window.Inspector.MetadataTool;
        await _input.Click(window.Inspector.MetadataButton);
        await Wait(() => window.Inspector.MetadataFlyout.IsOpen && tool.AddButton.IsAttachedToVisualTree(), "the Add Metadata flyout to open");
        // The name box takes text, which this run does not type. Setting it is the only part that is not injected.
        tool.NameBox.Text = "tag";
        await _input.Click(tool.AddButton);
        await Settle();
        Expect(engine.MetadataOf(_box).Any(entry => entry.Name == "tag"), "the engine holds no metadata named tag");
        Expect(window.Inspector.MetadataRows.Rows.Any(row => row.Key == "tag"), "the inspector shows no row for tag");
        Expect(Edited, $"the title shows no unsaved edits: {window.Title}");
        await _input.Tap(window, Key.S, RawInputModifiers.Meta);
        await Settle();
        var block = File.ReadAllText(Path.Combine(project, "main.oscn")).Split("\n\n").Single(part => part.Contains("name=\"Box\""));
        Expect(block.Contains("metadata/tag = \"\""), "the file holds no metadata/tag line for Box");
        Expect(!Edited, $"the title still shows unsaved edits: {window.Title}");
    }

    async Task ShowsInTheDesktop()
    {
        var shots = await Shots.OfProcess(output, "desktop");
        if (shots.Count == 0)
        {
            throw new SkipException("no window picture: the window id tool is not set, or Screen Recording is not granted");
        }
    }

    async Task DragAlongAnArrow(GizmoAxis axis, GizmoPart part, double pixels)
    {
        await Settle();
        var handles = Handles();
        var origin = Local(handles.Origin);
        var tip = Local(handles.Tip(axis));
        var press = origin + (tip - origin) * (float)ArrowShare;
        Expect(HitAt(press) == new GizmoHit(part, axis), $"a press on the {axis} arrow takes {HitAt(press)}");
        var direction = Vector2.Normalize(tip - origin);
        await DragFrom(press, direction * (float)pixels);
    }

    async Task DragFrom(Vector2 local, Vector2 by)
    {
        await _input.Drag(window, InWindow(local), InWindow(local + by));
        await Settle();
    }

    async Task ChooseMode(Control button, GizmoMode mode)
    {
        await _input.Click(button);
        Expect(window.Toolbar.Mode == mode, $"the mode is {window.Toolbar.Mode}");
        await Settle();
    }

    GizmoHit? HitAt(Vector2 local) => Viewport.HitAt(new Point(local.X, local.Y));

    bool Edited => window.Title?.StartsWith("(*)") == true;

    GizmoHandles Handles() => Viewport.HandlesSource!()!.Value;

    Vector2 Local(Vector2 framePoint) => ViewportControl.ControlAt(framePoint, Viewport.Frame!.Size, Viewport.Bounds.Size);

    Point InWindow(Vector2 local) => Viewport.TranslatePoint(new Point(local.X, local.Y), window)!.Value;

    OutlineRow? RowNamed(string name) => window.Outline.GetVisualDescendants().OfType<OutlineRow>()
        .FirstOrDefault(row => row.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == name));

    // The origin is the last three numbers of the node's transform line. A node at the identity has no line.
    static Vector3 OriginIn(string scene)
    {
        var block = scene.Split("\n\n").Single(part => part.Contains("name=\"Box\""));
        var line = TransformOf().Match(block);
        if (!line.Success)
        {
            return Vector3.Zero;
        }
        var numbers = line.Groups[1].Value.Split(',').Select(n => float.Parse(n, CultureInfo.InvariantCulture)).ToArray();
        return new Vector3(numbers[9], numbers[10], numbers[11]);
    }

    [GeneratedRegex(@"Transform3D\(([^)]*)\)")]
    private static partial Regex TransformOf();

    static async Task Wait(Func<bool> condition, string what)
    {
        for (var waited = 0; waited < TimeoutMilliseconds; waited += PollMilliseconds)
        {
            if (condition())
            {
                return;
            }
            await Task.Delay(PollMilliseconds);
        }
        throw new TimeoutException($"timed out waiting for {what}");
    }

    static Task Settle() => Task.Delay(SettleMilliseconds);

    static void Expect(bool ok, string failure)
    {
        if (!ok)
        {
            throw new InvalidOperationException(failure);
        }
    }

    BottomBar Bar => (BottomBar)window.Center.BottomBar.Child!;

    // The engine writes its start up lines and the save errors of the earlier steps to the log file the editor reads.
    async Task OpensTheEngineLog()
    {
        await _input.Click(Bar.OutputTab);
        Expect(window.Center.Output.IsVisible, "the Output button did not open the panel");
        await Wait(() => window.Center.Output.List.ItemCount > 0, "a line of the engine's log in the panel");
        await Settle();
    }

    async Task ClearsAndShutsTheLog()
    {
        var output = window.Center.Output;
        var before = output.List.ItemCount;

        await _input.Click(output.ClearButton);
        Expect(output.List.ItemCount < before, $"{output.List.ItemCount} lines after Clear, {before} before");

        await _input.Click(Bar.OutputTab);
        Expect(!output.IsVisible, "a second click did not shut the panel");
    }

    // The game prints its graphics line at start. The editor's own copy of it was cleared by the step before.
    async Task PlayShowsTheGameLog()
    {
        try
        {
            await _input.Click(window.PlayButton);
            Expect(window.Status == "Playing in a second window.", $"the status is \"{window.Status}\"");
            Expect(window.Center.Output.IsVisible, "Play did not open the Output panel");
            await Wait(() => window.Center.Output.List.Items.OfType<string>().Any(line => line.Contains("Metal")), "the game's graphics line in the panel");
            await _input.Click(window.StopButton);
            Expect(!engine.IsPlaying, "the Stop button did not end the game");
        }
        finally
        {
            engine.Stop();
        }
    }

    async Task RunsFromTheGameTab()
    {
        try
        {
            await _input.Click(Tab("Game"));
            Expect(window.Game.IsVisible && !window.Viewport.IsEffectivelyVisible, "the game view did not replace the viewport");
            Expect(window.Game.RunButton.IsEnabled && !window.Game.StopButton.IsEnabled, "the game view offered the wrong buttons");

            await _input.Click(window.Game.RunButton);
            await Wait(() => engine.IsPlaying && window.Game.State.Text!.Contains("is running"), "the game to start");
            await Settle();
            Shots.Save(window, Path.Combine(output, "game-running.png"));
            await _input.Click(window.Game.StopButton);
            Expect(!engine.IsPlaying && window.Game.State.Text!.StartsWith("No game"), "Stop did not end the game");

            // The user closes the game's own window, which the editor learns only at its next poll.
            await _input.Click(window.PlayButton);
            await Wait(() => engine.IsPlaying, "a second game to start");
            CloseTheGame();
            await Wait(() => !window.StopButton.IsEnabled && window.Game.State.Text!.StartsWith("No game"), "the view to show the closed game as stopped");

            // Play after a game that ended on its own runs Stop on a process that has exited.
            await _input.Click(window.PlayButton);
            await Wait(() => engine.IsPlaying && window.StopButton.IsEnabled, "a third game to start");
            await _input.Click(window.StopButton);
            Expect(!engine.IsPlaying, "the title bar Stop did not end the third game");

            await _input.Click(Tab("3D"));
            await Wait(() => window.Viewport.IsEffectivelyVisible && Viewport.Bounds.Width > 0, "the viewport to show again");
            Expect(!window.Game.IsVisible, "the game view stayed over the viewport");
        }
        finally
        {
            engine.Stop();
        }
    }

    // A user closes the game by its window. Ending the process from outside is the closest this run can do.
    static void CloseTheGame()
    {
        using var kill = Process.Start("pkill", ["-f", "orblit.macos.editor.dev.arm64 --path"]);
        kill?.WaitForExit();
    }

    async Task ShowsTheStore()
    {
        await _input.Click(Tab("Asset Store"));
        Expect(window.Store.IsVisible && !window.Viewport.IsEffectivelyVisible, "the store did not replace the viewport");
        await _input.Click(window.Store.LoadButton);
        await Wait(() => window.Store.Status.Text == "2 kits.", "the two kits of the store");
        await Settle();
        Shots.Save(window, Path.Combine(output, "store-loaded.png"));

        await _input.Click(Tab("3D"));
        await Wait(() => window.Viewport.IsEffectivelyVisible && Viewport.Bounds.Width > 0, "the viewport to show again");
        Expect(!window.Store.IsVisible, "the store stayed over the viewport");
        // The frame bitmap is reused, so only its pixels show that the engine still draws. An orbit from the empty bottom left changes them.
        // The top left holds the Perspective button, which would take the press.
        var before = FrameHash();
        await DragFrom(new Vector2(60, (float)Viewport.Bounds.Height - 60), new Vector2(120, 0));
        Expect(FrameHash() != before, "an orbit after the switch did not change the picture");
    }

    async Task EditsAScript()
    {
        const string Resource = "res://spin.cs";
        var file = Path.Combine(project, "spin.cs");
        File.WriteAllText(file, "class Spin\n{\n}\n");
        await _input.Click(Tab("Script"));
        await Wait(() => window.Script.IsVisible && window.Script.List.ContainerFromItem(Resource) is not null, "the script list to show spin.cs");
        Expect(!window.Viewport.IsEffectivelyVisible, "the script view did not replace the viewport");
        await _input.Click(window.Script.List.ContainerFromItem(Resource)!);
        await Wait(() => window.Script.Editor.Text == "class Spin\n{\n}\n", "the text of spin.cs in the box");

        // The box takes text, which this run does not type. Setting it is the only part that is not injected.
        const string Typed = "class Spin\n{\n    int turns;\n}\n";
        window.Script.Editor.Text = Typed;
        Expect(window.Script.IsDirty && window.Script.SaveButton.IsEnabled, "the edit did not turn the Save button on");
        await _input.Click(window.Script.SaveButton);
        await Wait(() => !window.Script.IsDirty, "the Save button to write the script");
        Expect(File.ReadAllText(file) == Typed, $"the file holds {File.ReadAllText(file)}");
        await Settle();
        Shots.Save(window, Path.Combine(output, "script-saved.png"));

        window.Script.Editor.Text += "// by key\n";
        await _input.Tap(window, Key.S, RawInputModifiers.Meta);
        await Wait(() => !window.Script.IsDirty, "Command and S to write the script");
        Expect(File.ReadAllText(file) == Typed + "// by key\n", $"the file holds {File.ReadAllText(file)}");

        await _input.Click(Tab("3D"));
        await Wait(() => window.Viewport.IsEffectivelyVisible && Viewport.Bounds.Width > 0, "the viewport to show again");
        Expect(!window.Script.IsVisible, "the script view stayed over the viewport");
    }

    async Task MakesAGraphScript()
    {
        const string Resource = "res://live.ograph";
        var graph = window.Graph;
        await _input.Click(Tab("Graph"));
        await Wait(() => graph.IsVisible && graph.FileButton.IsAttachedToVisualTree(), "the Graph view to show");
        Expect(!window.Viewport.IsEffectivelyVisible, "the graph view did not replace the viewport");
        Expect(graph.Hint.IsVisible, "the hint did not show with no graph open");

        await _input.Click(graph.FileButton);
        await Wait(() => graph.FileMenu.IsOpen && NewItem(graph).IsAttachedToVisualTree(), "the File menu to open");
        await _input.Click(NewItem(graph));
        await Wait(() => graph.CreateFlyout.IsOpen && graph.Form.CreateButton.IsAttachedToVisualTree(), "the Create form to open");
        // The boxes take text, which this run does not type. Setting it is the only part that is not injected.
        graph.Form.PathBox.Text = Resource;
        await _input.Click(graph.Form.CreateButton);
        await Wait(() => graph.Current == Resource, $"the new graph to open, the form said {graph.Form.Notice}");
        Expect(File.Exists(Path.Combine(project, "live.ograph")), "the engine wrote no live.ograph");
        Expect(graph.List.Items.Cast<string>().Contains(Resource), "the list does not show the new graph");
        Expect(graph.Tabs.Select(tab => tab.Label.Text).SequenceEqual(["live.ograph"]), "the tab row does not show live.ograph");
        await Settle();
        Shots.Save(window, Path.Combine(output, "graph-created.png"));

        await _input.Click(graph.FileButton);
        await Wait(() => graph.FileMenu.IsOpen && CloseItem(graph).IsAttachedToVisualTree(), "the File menu to open again");
        await _input.Click(CloseItem(graph));
        await Wait(() => graph.Current is null, "File, Close to close the graph");
        Expect(graph.Hint.IsVisible, "the hint did not come back");

        await _input.Click(Tab("3D"));
        await Wait(() => window.Viewport.IsEffectivelyVisible && Viewport.Bounds.Width > 0, "the viewport to show again");
    }

    static MenuItem NewItem(GraphView graph) => graph.FileMenu.Items.OfType<MenuItem>().Single(item => (string?)item.Header == "New...");

    static MenuItem CloseItem(GraphView graph) => graph.FileMenu.Items.OfType<MenuItem>().Single(item => (string?)item.Header == "Close");

    WorkspaceTab Tab(string label) => window.Bar.Tabs.Single(tab => tab.Label == label);

    string FrameHash() => Convert.ToHexString(SHA256.HashData(FrameBytes(out _)));

    async Task Step(string name, Func<Task> body)
    {
        string? failure = null;
        var skipped = false;
        try
        {
            await body();
        }
        catch (SkipException skip)
        {
            failure = skip.Message;
            skipped = true;
        }
        // A failed step is reported and the run goes on, so one report shows every step that broke.
        catch (Exception error)
        {
            failure = error.Message;
            _failed = true;
        }
        var shot = Path.Combine(output, $"{++_number:00}-{Slug(name)}.png");
        Shots.Save(window, shot);
        Console.WriteLine(failure is null ? $"PASS {name}" : skipped ? $"SKIP {name}: {failure}" : $"FAIL {name}: {failure} (shot {shot})");
    }

    sealed class SkipException(string reason) : Exception(reason);

    static string Slug(string name) => string.Concat(name.Take(40).Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '-')).Trim('-');
}
