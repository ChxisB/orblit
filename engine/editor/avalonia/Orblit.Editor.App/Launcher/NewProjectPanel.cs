using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using Orblit.Editor.App.Shell;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Launcher;

/// <summary>
/// The New project panel beside the projects: a name, a new folder, a template, a first script and Git. Create
/// writes the project and raises Made with its folder. A fault is said under the folder and nothing is written.
/// </summary>
internal sealed class NewProjectPanel : Border
{
    readonly Action<string> _initGit;
    readonly TextBox _name = new() { Text = "My game", Classes = { EditorTheme.Field } };
    readonly TextBox _folder = new() { Text = "~/Games/my-game", FontFamily = EditorTheme.Mono, Classes = { EditorTheme.Field } };
    readonly Button _choose = Parts.IconButton("Folder", "Choose a folder");
    readonly TextBlock _fault = Parts.FaultLine();
    readonly Segments _template = new("3D scene", "2D scene", "Empty");
    readonly Segments _script = new("C#", "Graph", "None");
    readonly CheckBox _git = new() { Content = "Start a Git repository", IsChecked = true };
    readonly Button _cancel = new() { Content = "Cancel", Classes = { EditorTheme.TextButton, EditorTheme.Ghost }, Margin = new Thickness(0, 0, 8, 0) };
    readonly Button _create = new() { Content = "Create and open", Classes = { EditorTheme.TextButton, EditorTheme.Primary } };

    public NewProjectPanel(Action<string> initGit)
    {
        _initGit = initGit;
        Classes.Add(EditorTheme.Island);
        Width = 340;
        Margin = new Thickness(12, 0, 0, 0);
        IsVisible = false;
        Head = new CardHead("New project", "");
        DockPanel.SetDock(_choose, Dock.Right);
        _choose.Margin = new Thickness(4, 0, 0, 0);
        var hint = new TextBlock { Text = "The folder is new and empty.", FontSize = 11, Foreground = EditorBrushes.TextDim };
        var folder = new StackPanel { Spacing = 4, Children = { new DockPanel { Children = { _choose, _folder } }, hint, _fault } };
        var body = new StackPanel
        {
            Spacing = 14,
            Children =
            {
                Parts.Labeled("Name", _name),
                Parts.Labeled("Folder", folder),
                Parts.Labeled("Template", _template),
                Parts.Labeled("First script", _script),
                _git,
            },
        };
        DockPanel.SetDock(_create, Dock.Right);
        DockPanel.SetDock(_cancel, Dock.Right);
        var footer = new Border
        {
            Padding = new Thickness(12, 10),
            BorderBrush = EditorBrushes.Line,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Child = new DockPanel { LastChildFill = false, Children = { _create, _cancel } },
        };
        DockPanel.SetDock(Head, Dock.Top);
        DockPanel.SetDock(footer, Dock.Bottom);
        Child = new DockPanel { Children = { Head, footer, new ScrollViewer { Content = new Border { Padding = new Thickness(14, 12), Child = body } } } };
        // TextChanged waits for a render, so the property is the one that fires at once.
        _name.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty)
            {
                _folder.Text = InFolder(Path.GetDirectoryName(_folder.Text ?? "") ?? "");
            }
        };
        _choose.Click += async (_, _) =>
        {
            if (await PickFolderAsync(this, "Choose where the project folder goes") is { } parent)
            {
                _folder.Text = InFolder(HomeFolder.Shorten(parent));
            }
        };
        _create.Click += (_, _) => Create();
        _cancel.Click += (_, _) => IsVisible = false;
        Head.CloseButton.Click += (_, _) => IsVisible = false;
    }

    /// <summary>Raised with the new project's folder once its files are written.</summary>
    public event Action<string>? Made;

    internal CardHead Head { get; }

    internal TextBox NameBox => _name;

    internal TextBox FolderBox => _folder;

    internal TextBlock Fault => _fault;

    internal Segments TemplateChoice => _template;

    internal Segments ScriptChoice => _script;

    internal CheckBox Git => _git;

    internal Button CreateButton => _create;

    /// <summary>Shows the panel on this template, with no fault.</summary>
    public void Start(ProjectTemplate template)
    {
        _template.SelectedIndex = (int)template;
        _fault.IsVisible = false;
        IsVisible = true;
    }

    /// <summary>The local folder chosen in the system's folder picker, or null when none is chosen.</summary>
    internal static async Task<string?> PickFolderAsync(Control owner, string title)
    {
        if (TopLevel.GetTopLevel(owner) is not { } top)
        {
            return null;
        }

        var picked = await top.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = title });
        return picked.Count > 0 ? picked[0].TryGetLocalPath() : null;
    }

    // The project's own folder in this parent, named after the project.
    string InFolder(string parent) => Path.Combine(parent, HomeFolder.Slug(_name.Text ?? ""));

    void Create()
    {
        try
        {
            var project = new NewProject(_name.Text ?? "", HomeFolder.Expand((_folder.Text ?? "").Trim()))
            {
                Template = (ProjectTemplate)_template.SelectedIndex,
                Script = (FirstScript)_script.SelectedIndex,
                Git = _git.IsChecked == true,
            };
            project.Make(_initGit);
            IsVisible = false;
            Made?.Invoke(project.Folder);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
        {
            _fault.Text = error.Message;
            _fault.IsVisible = true;
        }
    }
}
