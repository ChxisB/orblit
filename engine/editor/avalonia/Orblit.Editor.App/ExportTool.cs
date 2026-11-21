using System.ComponentModel;
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

    /// <summary>Exports the game with a preset to a path, and throws <see cref="InvalidOperationException"/> with the reason when it fails.</summary>
    Task Export(string preset, string path);
}

/// <summary>
/// The Export dialog: the project's export presets, a preset for this computer, the templates the engine builds
/// from, and the export itself. The export runs in an engine of its own, so the editor stays usable meanwhile.
/// </summary>
internal sealed class ExportTool
{
    const string NoPresets = "The project has no export presets. Add one for this computer.";

    readonly ComboBox _presets = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
    readonly TextBox _path = new() { PlaceholderText = "Where the game goes" };
    readonly TextBlock _status = new() { FontSize = 11, Foreground = EditorBrushes.TextDim, TextWrapping = TextWrapping.Wrap };
    readonly Button _install = new() { Content = "Install Templates...", Classes = { EditorTheme.TextButton, EditorTheme.Ghost }, IsEnabled = false };
    IReadOnlyList<ExportPreset> _list = [];
    IExportGame? _project;
    bool _exporting;

    public ExportTool()
    {
        AddPresetButton = new Button { Content = "Add Preset for This Computer", Classes = { EditorTheme.TextButton, EditorTheme.Ghost }, HorizontalAlignment = HorizontalAlignment.Left };
        ToolTip.SetTip(_install, "Unpack a .tpz of export templates for this version of the engine");
        var body = new StackPanel { Spacing = 10, Children = { Parts.Labeled("Preset", _presets, AddPresetButton), Parts.Labeled("Export to", _path), _status } };
        Dialog = new Dialog("Export", "Build the game as a program that runs without the editor.", body) { MainText = "Export", LeadButton = _install };
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

    internal ComboBox PresetBox => _presets;

    internal TextBox PathBox => _path;

    internal TextBlock Status => _status;

    ExportPreset? Picked => _presets.SelectedIndex >= 0 && _presets.SelectedIndex < _list.Count ? _list[_presets.SelectedIndex] : null;

    public void Show(IExportGame project)
    {
        _project = project;
        _install.IsEnabled = true;
    }

    /// <summary>Opens the dialog on the project's presets, with the first picked.</summary>
    public void Open()
    {
        ShowPresets(0);
        // A run that is still going keeps its line.
        if (!_exporting)
        {
            _status.Text = _list.Count == 0 ? NoPresets : "";
        }

        Dialog.Open();
    }

    void ShowPresets(int pick)
    {
        _list = _project?.Presets() ?? [];
        _presets.ItemsSource = _list.Select(preset => preset.Name).ToArray();
        _presets.SelectedIndex = _list.Count > 0 ? pick : -1;
        Refresh();
    }

    // The engine finds a preset by its name, so a second preset for this computer would never be used.
    void Refresh()
    {
        Dialog.MainButton.IsEnabled = !_exporting && Picked is not null && !string.IsNullOrWhiteSpace(_path.Text);
        AddPresetButton.IsEnabled = _project is not null && _list.All(preset => preset.Platform != ExportPresets.ThisComputer.Platform);
    }

    void AddPreset()
    {
        try
        {
            _project!.AddPreset();
            // The new preset is the last one.
            ShowPresets(_list.Count);
            _status.Text = $"Added a preset for {ExportPresets.ThisComputer.Platform}.";
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            _status.Text = $"The preset was not added. {error.Message}";
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
            _status.Text = $"Installed the templates from {name}.";
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            _status.Text = $"{name} was not installed. {error.Message}";
        }
    }

    async Task<string?> PickTpzAsync()
    {
        if (TopLevel.GetTopLevel(Dialog) is not { } top)
        {
            return null;
        }

        var options = new FilePickerOpenOptions { Title = "Install export templates", FileTypeFilter = [new FilePickerFileType("Export templates") { Patterns = ["*.tpz"] }] };
        var picked = await top.StorageProvider.OpenFilePickerAsync(options);
        return picked.Count > 0 ? picked[0].TryGetLocalPath() : null;
    }
}
