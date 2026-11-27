using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.VisualTree;
using Orblit.Editor.App;
using Orblit.Editor.App.Shell;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.Live;

// The steps for what reads the engine: a class's properties, undo and History, signals, import options and run flags.
internal sealed partial class EditorRun
{
    const string SwatchResource = "res://swatch.png";
    // A one pixel PNG, small enough to keep in the source.
    const string SwatchPng = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==";
    const int ImportMilliseconds = 60000;

    /// <summary>
    /// Writes an image into the project and imports it. Files lists the imported files only when the editor
    /// opens, so this runs before it does.
    /// </summary>
    internal static void WriteSwatch(string project)
    {
        File.WriteAllBytes(Path.Combine(project, "swatch.png"), Convert.FromBase64String(SwatchPng));
        using var import = Process.Start(EditorEngine.PlayerPath(EngineLibrary.Find()), ["--headless", "--path", project, "--import"]);
        // This runs before the editor's start clock, so a hung import would hold the run for ever.
        if (!import.WaitForExit(ImportMilliseconds))
        {
            import.Kill();
            throw new TimeoutException($"The engine did not import swatch.png in {ImportMilliseconds / 1000} s.");
        }

        if (import.ExitCode != 0)
        {
            throw new InvalidOperationException($"The engine's import of swatch.png exited with {import.ExitCode}.");
        }
    }

    async Task ShowsTheClassProperties()
    {
        var chain = engine.ClassChainOf(_box);
        Expect(window.Inspector.Chain.SequenceEqual(chain), $"the inspector shows {string.Join(", ", window.Inspector.Chain)} and the engine {string.Join(", ", chain)}");
        Expect(window.Inspector.Rows.Any(row => row.PropertyName == "mesh"), "the inspector has no row for the mesh");
        await Settle();
    }

    // History's rows and the two keys all move through the same steps, so each must put the engine where it was.
    async Task UndoesAndRedoes()
    {
        var moved = engine.PositionOf(_box);
        await _input.Tap(window, Key.Z, RawInputModifiers.Meta);
        await Settle();
        Expect(Vector3.Distance(engine.PositionOf(_box), moved) > Close, "Command Z left the entity where it was");
        await _input.Tap(window, Key.Z, RawInputModifiers.Meta | RawInputModifiers.Shift);
        await Settle();
        Expect(Vector3.Distance(engine.PositionOf(_box), moved) < Close, $"Command Shift Z put the entity at {engine.PositionOf(_box)}, not {moved}");

        await _input.Click(window.FileDock.Tabs[1]);
        var rows = window.History.Rows.Children;
        Expect(rows.Count >= 5, $"History lists {rows.Count} rows for four edits");
        // History lists the newest step first, so the scene as it opened is the last row.
        await _input.Click(rows[^1]);
        await Settle();
        Expect(Vector3.Distance(engine.PositionOf(_box), moved) > Close, "a click on the opened scene's row left the entity where it was");
        await _input.Click(rows[0]);
        await Settle();
        Expect(Vector3.Distance(engine.PositionOf(_box), moved) < Close, "a click on the newest History row did not bring the edits back");
        Shots.Save(window, Path.Combine(output, "history.png"));
        await _input.Click(window.FileDock.Tabs[0]);
    }

    async Task ConnectsASignal()
    {
        var signals = window.Signals;
        await _input.Click(window.InspectorDock.Tabs[1]);
        await Wait(() => signals.IsEffectivelyVisible && signals.List.Children.Count > 0, "the Signals tab to list the entity's signals");
        var shown = signals.List.GetVisualDescendants().OfType<TextBlock>().Select(text => text.Text ?? "").ToList();
        var missing = engine.SignalsOf(_box).Where(signal => !shown.Any(text => text.StartsWith(signal.Name + "("))).Select(signal => signal.Name).ToList();
        Expect(missing.Count == 0, $"the tab does not list {string.Join(", ", missing)}");

        var signal = engine.SignalsOf(_box).First(signal => signal.Links.Count == 0);
        var connect = ConnectButtonOf(signals, signal.Name);
        connect.BringIntoView();
        await Settle();
        await _input.Click(connect);
        await Wait(() => signals.Dialog.MainButton.IsEffectivelyVisible && signals.Dialog.MainButton.Bounds.Width > 0, "the Connect dialog to open");
        await _input.Click(signals.TargetList.RowButtons.First());
        await _input.Click(signals.Dialog.MainButton);
        await Settle();
        var method = SignalsPane.MethodFor(signal.Name);
        Expect(LinksOf(signal.Name).Any(link => link.Method == method), $"the engine holds no link from {signal.Name} to {method}");
        Shots.Save(window, Path.Combine(output, "signal-connected.png"));

        await _input.Tap(window, Key.Z, RawInputModifiers.Meta);
        await Settle();
        Expect(LinksOf(signal.Name).Count == 0, $"Command Z kept the link from {signal.Name}");
        await _input.Click(window.InspectorDock.Tabs[0]);
    }

    IReadOnlyList<SignalConnection> LinksOf(string signal) => engine.SignalsOf(_box).Single(each => each.Name == signal).Links;

    static Button ConnectButtonOf(SignalsPane signals, string signal) => signals.List.GetVisualDescendants().OfType<DockPanel>()
        .First(row => row.Children.OfType<TextBlock>().Any(text => text.Text?.StartsWith(signal + "(") == true))
        .Children.OfType<Button>().First();

    // The engine rewrites the imported texture only when it imports the file again, so its time shows the import ran.
    async Task SetsImportOptions()
    {
        await Wait(() => SwatchRow() is not null, "swatch.png in Files");
        // Two clicks in a row are the double click that opens a file.
        await _input.Click(SwatchRow()!);
        await _input.Click(SwatchRow()!);
        var import = window.Import;
        try
        {
            await Wait(() => import.IsEffectivelyVisible && import.Groups.Children.Count > 0, "the Import tab to show the options of swatch.png");
            Expect(import.Importer == "Texture", $"the importer is {import.Importer}");

            var texture = ImportedOf(File.ReadAllText(Path.Combine(project, "swatch.png.import")));
            var before = File.GetLastWriteTimeUtc(texture);
            var toggle = import.Groups.GetVisualDescendants().OfType<ToggleSwitch>().First();
            toggle.BringIntoView();
            await Settle();
            await _input.Click(toggle);
            Expect(import.ReimportButton.IsEnabled, "a changed option did not turn Reimport on");
            await _input.Click(import.ReimportButton);
            await Wait(() => File.GetLastWriteTimeUtc(texture) > before, "the engine to import swatch.png again", ImportMilliseconds);
            Shots.Save(window, Path.Combine(output, "import-options.png"));
        }
        finally
        {
            // The later steps find their rows in the Scene tab, which the Import tab hides.
            await _input.Click(window.SceneDock.Tabs[0]);
        }
    }

    TreeViewItem? SwatchRow() => window.Scenes.Tree.GetVisualDescendants().OfType<TreeViewItem>()
        .FirstOrDefault(item => item.DataContext is FileNode { Path: SwatchResource });

    // The .import file names what the import wrote in its dest_files line.
    string ImportedOf(string importFile)
    {
        var line = importFile.Split('\n').Single(line => line.StartsWith("dest_files="));
        var resource = line.Split('"')[1];
        return Path.Combine(project, resource["res://".Length..]);
    }

    // The flag shows only in the game's own arguments, so the step reads them from the process list.
    async Task RunsWithAFlag()
    {
        var collisions = window.Game.SwitchOf("Visible Collision Shapes");
        try
        {
            await _input.Click(Tab("Game"));
            collisions.BringIntoView();
            await Settle();
            await _input.Click(collisions);
            Expect(collisions.IsChecked == true, "a click did not turn Visible Collision Shapes on");
            await _input.Click(window.Game.RunButton);
            await Wait(() => engine.IsPlaying, "the game to start");
            var arguments = await GameArguments();
            Expect(arguments.Contains(" --debug-collisions"), $"the game runs as {arguments}");
            await _input.Click(window.Game.StopButton);
            await _input.Click(collisions);
            Expect(collisions.IsChecked == false, "a second click did not turn the switch off");
        }
        finally
        {
            engine.Stop();
        }

        await _input.Click(Tab("3D"));
        await Wait(() => window.Viewport.IsEffectivelyVisible && Viewport.Bounds.Width > 0, "the viewport to show again");
    }

    async Task<string> GameArguments()
    {
        if (engine.GameId is not { } id)
        {
            return "no game process";
        }

        using var list = Process.Start(new ProcessStartInfo("ps", ["-o", "args=", "-p", id.ToString(CultureInfo.InvariantCulture)]) { RedirectStandardOutput = true })!;
        var line = await list.StandardOutput.ReadToEndAsync();
        await list.WaitForExitAsync();
        return line.Trim();
    }
}
