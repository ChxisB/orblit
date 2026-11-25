using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Orblit.Editor.App.Shell;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Launcher;

/// <summary>
/// The New project dialog: a name, a new folder, what the project starts with, a first script and Git, each with
/// a line on what it does. Create writes the project and raises Made with its folder. An empty name turns Create
/// off and says why. A fault is said under the folder and nothing is written.
/// </summary>
internal sealed class NewProjectPanel : StackPanel
{
    const string FirstFolder = "~/Games/my-game";

    static readonly (string Icon, string Name, string Line)[] s_starts =
    [
        ("Cube", "3D game", "An empty 3D scene with a camera view."),
        ("Triangle", "2D game", "An empty 2D scene. Also good for menus."),
        ("Square", "Empty project", "No scene. Add your own later."),
    ];

    readonly Action<string> _initGit;
    readonly TextBox _name = new() { Text = "My game", PlaceholderText = "My game", Classes = { EditorTheme.Field } };
    readonly TextBlock _nameFault = Parts.FaultLine();
    readonly TextBox _folder = new() { Text = FirstFolder, FontFamily = EditorTheme.Mono, Classes = { EditorTheme.Field } };
    readonly Button _change = new() { Content = "Change…", Margin = new Thickness(0, 0, 3, 0), VerticalAlignment = VerticalAlignment.Center, Classes = { EditorTheme.TextButton, EditorTheme.Small } };
    readonly TextBlock _fault = Parts.FaultLine();
    readonly TemplateCard[] _cards;
    readonly Segments _script = new("C#", "Graph", "None");
    readonly ToggleSwitch _git = new() { IsChecked = true, OnContent = null, OffContent = null, MinWidth = 0, VerticalAlignment = VerticalAlignment.Center };
    string? _written = FirstFolder;

    public NewProjectPanel(Action<string> initGit)
    {
        _initGit = initGit;
        _cards = [.. s_starts.Select(start => new TemplateCard { Content = TemplateFace(start.Icon, start.Name, start.Line), Classes = { EditorTheme.TextButton, EditorTheme.Card } })];
        _cards[0].IsChecked = true;
        _nameFault.Text = "Give the project a name.";
        _folder.InnerRightContent = new HoverCard("Change…", "Picks where the project goes. It gets its own folder there, named after the project.").On(_change);
        var starts = new StackPanel { Spacing = 8 };
        starts.Children.AddRange(_cards);
        Spacing = 14;
        Children.AddRange(
        [
            Parts.Labeled("Name", _name, _nameFault, Hint("Shown in your project list.")),
            Parts.Labeled("Folder", _folder, _fault, Hint("A new, empty folder. Orblit makes it for you.")),
            Parts.Labeled("Start with", starts),
            Parts.Labeled("First script", _script, Hint("Graph joins steps with wires, no code needed. C# is written code. You can add the other later.")),
            GitRow(),
        ]);

        Dialog = new Dialog("New project", "Makes a folder with everything a game needs.", this);
        Dialog.MainButton.Content = Parts.Keyed("Create and open", "↵");
        var create = new HoverCard("Create and open", "Makes the folder and its files, then opens the project.", "↵");
        create.On(Dialog.MainButton);
        create.SaysWhyOff(Dialog.MainButton, "Give the project a name first.");
        Dialog.MainButton.Click += (_, _) => Create();
        // TextChanged waits for a render, so the property is the one that fires at once.
        _name.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty)
            {
                Named();
            }
        };
        _change.Click += async (_, _) =>
        {
            if (await PickFolderAsync(this, "Choose where the project folder goes") is { } parent)
            {
                WriteFolder(HomeFolder.Shorten(parent));
            }
        };
        Named();
    }

    /// <summary>Raised with the new project's folder once its files are written.</summary>
    public event Action<string>? Made;

    internal Dialog Dialog { get; }

    internal TextBox NameBox => _name;

    /// <summary>The line under the name that asks for one while it is empty.</summary>
    internal TextBlock NameFault => _nameFault;

    internal TextBox FolderBox => _folder;

    internal Button ChangeButton => _change;

    internal TextBlock Fault => _fault;

    /// <summary>The cards for what the project starts with, in the order of <see cref="ProjectTemplate"/>.</summary>
    internal IReadOnlyList<RadioButton> TemplateCards => _cards;

    internal Segments ScriptChoice => _script;

    internal ToggleSwitch Git => _git;

    internal Button CreateButton => Dialog.MainButton;

    /// <summary>Opens the dialog on this template, with no fault.</summary>
    public void Start(ProjectTemplate template)
    {
        _cards[(int)template].IsChecked = true;
        _fault.IsVisible = false;
        Dialog.Open();
    }

    /// <summary>A template's icon on a soft accent tile, beside its name and a dim line on what it starts with.</summary>
    internal static StackPanel TemplateFace(string icon, string name, string line) => new()
    {
        Orientation = Orientation.Horizontal,
        Spacing = 12,
        Children =
        {
            new Border { Width = 36, Height = 36, CornerRadius = new CornerRadius(8), Background = EditorBrushes.AccentSoft, Child = Parts.FieldIcon(icon, EditorBrushes.Accent) },
            new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                Children =
                {
                    new TextBlock { Text = name, FontWeight = FontWeight.SemiBold },
                    new TextBlock { Text = line, FontSize = 12, Foreground = EditorBrushes.TextDim, TextWrapping = TextWrapping.Wrap },
                },
            },
        },
    };

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

    static TextBlock Hint(string text) => new() { Text = text, FontSize = 11, Foreground = EditorBrushes.TextDim, TextWrapping = TextWrapping.Wrap };

    // The switch sits at the right of the words that say what it does.
    DockPanel GitRow()
    {
        DockPanel.SetDock(_git, Dock.Right);
        var needs = $"Lets you go back to any saved point. Needs Git on this {(OperatingSystem.IsMacOS() ? "Mac" : "computer")}.";
        var words = new StackPanel { Spacing = 2, Children = { new TextBlock { Text = "Keep a history with Git" }, Hint(needs) } };
        return new DockPanel { Children = { _git, words } };
    }

    // A folder typed by hand is the user's choice, so only the folder the dialog wrote follows the name. Create waits
    // for a name. The folder is checked as Create writes it, since only the disk can say.
    void Named()
    {
        if (_folder.Text == _written)
        {
            WriteFolder(Path.GetDirectoryName(_folder.Text ?? "") ?? "");
        }

        var named = (_name.Text ?? "").Trim().Length > 0;
        _name.Classes.Set(EditorTheme.Bad, !named);
        _nameFault.IsVisible = !named;
        Dialog.MainButton.IsEnabled = named;
    }

    // Puts the project's own folder in this parent, named after the project.
    void WriteFolder(string parent)
    {
        _written = Path.Combine(parent, HomeFolder.Slug(_name.Text ?? ""));
        _folder.Text = _written;
    }

    void Create()
    {
        try
        {
            var project = new NewProject(_name.Text ?? "", HomeFolder.Expand((_folder.Text ?? "").Trim()))
            {
                Template = (ProjectTemplate)Array.FindIndex(_cards, card => card.IsChecked == true),
                Script = (FirstScript)_script.SelectedIndex,
                Git = _git.IsChecked == true,
            };
            project.Make(_initGit);
            Dialog.Close();
            Made?.Invoke(project.Folder);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
        {
            _fault.Text = error.Message;
            _fault.IsVisible = true;
        }
    }

    // A radio choice drawn as a worded button, so the cards share one look and a check moves between them.
    sealed class TemplateCard : RadioButton
    {
        protected override Type StyleKeyOverride => typeof(Button);
    }
}
