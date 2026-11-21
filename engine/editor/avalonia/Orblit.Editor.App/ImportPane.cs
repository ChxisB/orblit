using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Orblit.Editor.App.Shell;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App;

/// <summary>What the Import tab needs from the project: the files the engine imports and their .import files.</summary>
internal interface IImportFiles
{
    /// <summary>The res:// path of every file with a .import file beside it.</summary>
    IReadOnlyList<string> ImportedFiles();

    /// <summary>The text of a file's .import file. Throws <see cref="IOException"/> when it cannot be read.</summary>
    string ReadImport(string resourcePath);

    /// <summary>Replaces the text of a file's .import file. Throws <see cref="IOException"/> when it cannot be written.</summary>
    void WriteImport(string resourcePath, string text);

    /// <summary>Starts the engine's import of every file whose .import file changed.</summary>
    void Reimport();
}

/// <summary>
/// The Import tab: the options of the file picked in FileSystem, in groups. Reimport writes them to the file's
/// .import file and asks the engine to import it again. Reset puts back the options the file held.
/// </summary>
internal sealed class ImportPane : DockPanel
{
    readonly StackPanel _empty = Parts.EmptyState("ImportArrow", "Nothing to import", "Select a model, image or sound in FileSystem to set how it imports.");
    readonly TextBlock _name = new() { FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
    readonly Border _badge = Parts.Badge("");
    readonly StackPanel _groups = new() { Spacing = 4 };
    readonly TextBlock _status = new() { FontSize = 11, Foreground = EditorBrushes.TextDim, TextWrapping = TextWrapping.Wrap };
    readonly DockPanel _form = new() { Margin = new Thickness(12) };
    IImportFiles? _files;
    string? _open;
    ImportFile? _read;
    ImportFile? _shown;

    public ImportPane()
    {
        var head = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Margin = new Thickness(0, 0, 0, 8) };
        var icon = Parts.RowIcon("ImportArrow", EditorBrushes.TreeIcon);
        Grid.SetColumn(_name, 1);
        Grid.SetColumn(_badge, 2);
        head.Children.AddRange([icon, _name, _badge]);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0), Children = { ResetButton, ReimportButton } };
        _status.Margin = new Thickness(0, 6, 0, 0);
        SetDock(head, Dock.Top);
        SetDock(_status, Dock.Bottom);
        SetDock(actions, Dock.Bottom);
        _form.Children.AddRange([head, _status, actions, new ScrollViewer { Content = _groups }]);
        Children.AddRange([_empty, _form]);
        ToolTip.SetTip(ResetButton, "Put back the options the file holds");
        ToolTip.SetTip(ReimportButton, "Write the options and import the file again");
        ResetButton.Click += (_, _) => Show(_read!);
        ReimportButton.Click += (_, _) => Reimport();
        Show((ImportFile?)null);
    }

    internal Button ResetButton { get; } = new() { Content = "Reset", Classes = { EditorTheme.TextButton, EditorTheme.Ghost, EditorTheme.Small } };

    internal Button ReimportButton { get; } = new() { Content = "Reimport", Classes = { EditorTheme.TextButton, EditorTheme.Primary, EditorTheme.Small } };

    internal StackPanel Groups => _groups;

    internal string Importer => ImporterText.Text ?? "";

    TextBlock ImporterText => (TextBlock)_badge.Child!;

    internal string Status => _status.Text ?? "";

    /// <summary>Starts from this project's files, with none picked.</summary>
    public void Show(IImportFiles files)
    {
        _files = files;
        _open = null;
        _read = null;
        _status.Text = "";
        Show((ImportFile?)null);
    }

    /// <summary>Reads the file's options and shows them.</summary>
    public void Open(string resourcePath)
    {
        _open = resourcePath;
        _name.Text = Path.GetFileName(resourcePath);
        _status.Text = "";
        try
        {
            _read = new ImportFile(_files!.ReadImport(resourcePath));
            Show(_read);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            _read = null;
            Show((ImportFile?)null);
            _status.Text = error.Message;
        }
    }

    void Show(ImportFile? file)
    {
        _shown = file;
        _empty.IsVisible = file is null && _status.Text is null or "";
        _form.IsVisible = !_empty.IsVisible;
        _groups.Children.Clear();
        ImporterText.Text = file?.Importer ?? "";
        foreach (var group in file?.Options().GroupBy(option => option.Property.Group) ?? [])
        {
            var rows = new StackPanel();
            foreach (var option in group)
            {
                var row = new PropertyRow(option.Property);
                row.Entered += value => Change(option.Key, value);
                rows.Children.Add(row);
            }
            _groups.Children.Add(new PropertyGroup(group.Key, rows));
        }
        ShowChanged();
    }

    void Change(string key, object value)
    {
        _shown = _shown!.With(key, value);
        ShowChanged();
    }

    // shortcut: the engine imports a file again only when its options changed, so Reimport waits for a change. Force an import when a source file can change outside the editor unseen.
    void ShowChanged()
    {
        var changed = _shown is not null && _shown.Text != _read?.Text;
        ResetButton.IsEnabled = changed;
        ReimportButton.IsEnabled = changed;
    }

    void Reimport()
    {
        try
        {
            _files!.WriteImport(_open!, _shown!.Text);
            _files.Reimport();
            _read = _shown;
            ShowChanged();
            _status.Text = $"Reimporting {_name.Text}. Output shows the engine's import.";
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            _status.Text = error.Message;
        }
    }
}
