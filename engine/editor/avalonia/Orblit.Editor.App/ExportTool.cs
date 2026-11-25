using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Orblit.Editor.App.Shell;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App;

/// <summary>What the Export dialog asks of the open project.</summary>
internal interface IExportGame
{
    IReadOnlyList<ExportPreset> Presets();

    /// <summary>Adds a preset for the computer the editor runs on.</summary>
    void AddPreset();

    /// <summary>Unpacks a .tpz of export templates where the engine looks for them.</summary>
    void InstallTemplates(string tpz);

    /// <summary>Downloads the templates for this engine from the site's mirror and unpacks them, and throws <see cref="IOException"/> with the reason when it cannot.</summary>
    Task DownloadTemplates();

    /// <summary>Exports the game with a preset to a path, and throws <see cref="InvalidOperationException"/> with the reason when it fails.</summary>
    Task Export(string preset, string path);
}

/// <summary>
/// The Export game dialog: the places the game will run, which the engine calls export presets, one for this
/// computer, the build parts the engine builds from, and the export itself. The export runs in an engine of its
/// own, so the editor stays usable meanwhile.
/// </summary>
internal sealed class ExportTool
{
    internal const string NoPlace = "Add a place to export to first.";
    internal const string NoPath = "Pick where to save the app first.";

    // The word for the computer the editor runs on, as a place the game will run.
    internal static readonly string ThisMachine = OperatingSystem.IsMacOS() ? "this Mac" : "this computer";

    readonly ComboBox _presets = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
    readonly TextBox _path = new() { PlaceholderText = "Where the app goes" };
    readonly TextBlock _status = new() { FontSize = 11, Foreground = EditorBrushes.TextDim, TextWrapping = TextWrapping.Wrap };
    readonly TextBlock _why = new() { FontSize = 12, Foreground = EditorBrushes.TextDim, VerticalAlignment = VerticalAlignment.Center };
    readonly Button _download = new() { Content = "Download", Classes = { EditorTheme.TextButton, EditorTheme.Ghost }, IsEnabled = false };
    readonly Button _install = new() { Content = "Install from a file…", Classes = { EditorTheme.TextButton, EditorTheme.Ghost }, IsEnabled = false };
    readonly Button _change = new() { Content = "Change…", Classes = { EditorTheme.TextButton, EditorTheme.Ghost } };
    readonly StackPanel _none;
    readonly StackPanel _places;
    IReadOnlyList<ExportPreset> _list = [];
    IExportGame? _project;
    bool _exporting;

    public ExportTool()
    {
        AddPresetButton = new Button { Content = $"Add {ThisMachine}", Classes = { EditorTheme.TextButton, EditorTheme.Ghost }, HorizontalAlignment = HorizontalAlignment.Left };
        Card(AddPresetButton, $"Add {ThisMachine}", "Makes a place the game will run, with its own settings.", $"{Capital(ThisMachine)} already has one.");
        Card(_download, "Download", "Gets the build parts for this version from orblitengine.com.", "Downloading now. It can take a few minutes.");
        Card(_install, "Install from a file…", "Unpacks a .tpz file of build parts you already have.", "Wait for the download to finish.");
        new HoverCard("Change…", "Pick the folder and name of the app.").On(_change);
        _none = new StackPanel
        {
            Spacing = 4,
            Children =
            {
                new TextBlock { Text = "Nowhere to export to yet", FontWeight = FontWeight.SemiBold },
                new TextBlock { Text = $"Add a place the game will run, like {ThisMachine}. Each one keeps its own settings.", FontSize = 12, Foreground = EditorBrushes.TextSoft, TextWrapping = TextWrapping.Wrap },
            },
        };
        _places = Parts.Labeled("Runs on", _presets);
        DockPanel.SetDock(_change, Dock.Right);
        _change.Margin = new Thickness(6, 0, 0, 0);
        var saveTo = new DockPanel { Children = { _change, _path } };
        var parts = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { _download, _install } };
        var partsLine = new TextBlock { Text = "Orblit needs these once per version to build apps. Called export templates.", FontSize = 11, Foreground = EditorBrushes.TextDim, TextWrapping = TextWrapping.Wrap };
        var body = new StackPanel { Spacing = 10, Children = { _none, _places, AddPresetButton, Parts.Labeled("Save the app to", saveTo), Parts.Labeled("Build parts", parts, partsLine), _status } };
        Dialog = new Dialog("Export game", "Builds your game as an app that runs without Orblit.", body) { MainText = "Export", EngineName = "Export", Hint = _why };
        new HoverCard("Export", "Builds the app in the place you picked.").On(Dialog.MainButton);
        _presets.SelectionChanged += (_, _) =>
        {
            _path.Text = Picked?.Path ?? "";
            Refresh();
        };
        // The property changes as the text is set. The TextChanged event waits for a render.
        _path.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty)
            {
                Refresh();
            }
        };
        AddPresetButton.Click += (_, _) => AddPreset();
        _change.Click += async (_, _) =>
        {
            if (await PickPathAsync() is { } path)
            {
                _path.Text = path;
            }
        };
        _download.Click += async (_, _) => await DownloadAsync();
        _install.Click += async (_, _) =>
        {
            if (await PickTpzAsync() is { } tpz)
            {
                InstallFile(tpz);
            }
        };
        Dialog.MainButton.Click += async (_, _) => await ExportAsync();
        Refresh();
    }

    internal Dialog Dialog { get; }

    internal Button AddPresetButton { get; }

    internal Button DownloadButton => _download;

    internal ComboBox PresetBox => _presets;

    internal TextBox PathBox => _path;

    internal TextBlock Status => _status;

    /// <summary>The footer line that says why Export is off.</summary>
    internal TextBlock WhyOff => _why;

    internal Control NoneYet => _none;

    internal Button ChangeButton => _change;

    ExportPreset? Picked => _presets.SelectedIndex >= 0 && _presets.SelectedIndex < _list.Count ? _list[_presets.SelectedIndex] : null;

    public void Show(IExportGame project)
    {
        _project = project;
        _download.IsEnabled = true;
        _install.IsEnabled = true;
    }

    /// <summary>Opens the dialog on the project's presets, with the first picked.</summary>
    public void Open()
    {
        ShowPresets(0);
        // A run that is still going keeps its line.
        if (!_exporting)
        {
            _status.Text = "";
        }

        Dialog.Open();
    }

    void ShowPresets(int pick)
    {
        _list = _project?.Presets() ?? [];
        _presets.ItemsSource = _list.Select(preset => preset.Name).ToArray();
        _presets.SelectedIndex = _list.Count > 0 ? pick : -1;
        _none.IsVisible = _list.Count == 0;
        _places.IsVisible = _list.Count > 0;
        Refresh();
    }

    // The engine finds a preset by its name, so a second preset for this computer would never be used.
    void Refresh()
    {
        Dialog.MainButton.IsEnabled = !_exporting && Picked is not null && !string.IsNullOrWhiteSpace(_path.Text);
        _why.Text = _exporting ? "" : Picked is null ? NoPlace : string.IsNullOrWhiteSpace(_path.Text) ? NoPath : "";
        _why.IsVisible = _why.Text != "";
        AddPresetButton.IsEnabled = _project is not null && _list.All(preset => preset.Platform != ExportPresets.ThisComputer.Platform);
    }

    void AddPreset()
    {
        try
        {
            _project!.AddPreset();
            // The new preset is the last one.
            ShowPresets(_list.Count);
            _status.Text = $"Added {ThisMachine}. It keeps its own settings.";
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            _status.Text = $"{Capital(ThisMachine)} was not added. {error.Message}";
        }
    }

    async Task ExportAsync()
    {
        var preset = Picked!;
        var path = _path.Text!.Trim();
        _exporting = true;
        Refresh();
        _status.Text = $"Exporting {preset.Name}. The engine imports and builds the project first, which can take a minute.";
        try
        {
            await _project!.Export(preset.Name, path);
            _status.Text = $"Exported {preset.Name} to {path}.";
        }
        catch (Exception error) when (error is InvalidOperationException or IOException or UnauthorizedAccessException or Win32Exception)
        {
            _status.Text = $"{preset.Name} did not export. {error.Message}";
        }
        finally
        {
            _exporting = false;
            Refresh();
        }
    }

    /// <summary>Installs a .tpz of export templates and says so in the status line, or says why not.</summary>
    internal void InstallFile(string tpz)
    {
        var name = Path.GetFileName(tpz);
        try
        {
            _project!.InstallTemplates(tpz);
            _status.Text = $"Installed the build parts from {name}.";
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            _status.Text = $"{name} was not installed. {error.Message}";
        }
    }

    async Task DownloadAsync()
    {
        _download.IsEnabled = _install.IsEnabled = false;
        _status.Text = "Downloading the build parts. They are large, so this can take a few minutes.";
        try
        {
            await _project!.DownloadTemplates();
            _status.Text = "Downloaded and installed the build parts.";
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or Win32Exception)
        {
            _status.Text = $"The build parts were not downloaded. {error.Message}";
        }
        finally
        {
            _download.IsEnabled = _install.IsEnabled = true;
        }
    }

    async Task<string?> PickTpzAsync()
    {
        if (TopLevel.GetTopLevel(Dialog) is not { } top)
        {
            return null;
        }

        var options = new FilePickerOpenOptions { Title = "Install build parts", FileTypeFilter = [new FilePickerFileType("Build parts") { Patterns = ["*.tpz"] }] };
        var picked = await top.StorageProvider.OpenFilePickerAsync(options);
        return picked.Count > 0 ? picked[0].TryGetLocalPath() : null;
    }

    async Task<string?> PickPathAsync()
    {
        if (TopLevel.GetTopLevel(Dialog) is not { } top)
        {
            return null;
        }

        var options = new FilePickerSaveOptions { Title = "Save the app to", SuggestedFileName = Path.GetFileName(_path.Text ?? "") };
        var picked = await top.StorageProvider.SaveFilePickerAsync(options);
        return picked?.TryGetLocalPath();
    }

    static void Card(Button button, string title, string does, string whyOff)
    {
        var card = new HoverCard(title, does);
        card.SaysWhyOff(button, whyOff);
        card.On(button);
    }

    static string Capital(string words) => char.ToUpperInvariant(words[0]) + words[1..];
}
