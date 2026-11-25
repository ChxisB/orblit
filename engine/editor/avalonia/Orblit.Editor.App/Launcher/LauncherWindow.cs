using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Orblit.Editor.App.Shell;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Launcher;

/// <summary>
/// The window the editor opens on when no project is named. It shows the projects in projects.cfg as cards,
/// with a search and a sort over them. Open adds a project folder from disk and New project writes a new one.
/// Either raises Opening with the folder.
/// </summary>
internal sealed class LauncherWindow : Window
{
    const double CardWidth = 230;
    // Half the 14 pixel gap between cards, as a margin on every side of each.
    const double Half = 7;

    readonly ProjectList _list;
    readonly ILinkOpener _links;
    readonly TextBox _search = new() { PlaceholderText = "Search projects", InnerLeftContent = Parts.FieldIcon("Lens", EditorBrushes.TextDim), Height = 36, Classes = { EditorTheme.Field } };
    readonly ComboBox _sort = new() { ItemsSource = new[] { "Last edited", "Name", "Folder" }, SelectedIndex = 0, Width = 150, Height = 36, Margin = new Thickness(8, 0, 0, 0), Classes = { EditorTheme.Field } };
    readonly UniformGrid _cards = new() { Margin = new Thickness(-Half) };
    readonly Decorator _note = new();
    readonly TextBlock _fault = Parts.FaultLine();
    readonly Button _open = new() { Content = Parts.Worded("FolderLine", "Open"), Margin = new Thickness(0, 0, 8, 0), Classes = { EditorTheme.TextButton, EditorTheme.Ghost } };
    readonly Button _new = new() { Content = Parts.Worded("Add", "New project"), Classes = { EditorTheme.TextButton, EditorTheme.Primary } };
    readonly NewProjectPanel _panel;
    List<ProjectEntry> _projects = [];

    public LauncherWindow(ProjectList list, Action<string> initGit, ILinkOpener? links = null)
    {
        _list = list;
        _links = links ?? new WindowLinks(this);
        _panel = new NewProjectPanel(initGit);
        Title = "Orblit";
        Icon = EditorIcon.ToWindowIcon("Orblit", 64);
        Width = 1120;
        Height = 740;
        MinWidth = 820;
        MinHeight = 560;
        Background = EditorBrushes.Window;
        Nav = new Nav(() => _links.Open(HelpMenu.Docs));
        var main = new Border
        {
            Classes = { EditorTheme.Island },
            Margin = new Thickness(12, 0, 0, 0),
            ClipToBounds = true,
            Child = Projects(),
        };
        Grid.SetColumn(main, 1);
        Grid.SetColumn(_panel, 2);
        Content = new Grid { Margin = new Thickness(12), ColumnDefinitions = new ColumnDefinitions("200,*,Auto"), Children = { Nav, main, _panel } };
        // The property changes as the text is set. The TextChanged event waits for a render.
        _search.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty)
            {
                ShowCards();
            }
        };
        _sort.SelectionChanged += (_, _) => ShowCards();
        _open.Click += async (_, _) =>
        {
            if (await NewProjectPanel.PickFolderAsync(this, "Open a project folder") is { } folder)
            {
                Open(folder);
            }
        };
        _new.Click += (_, _) => _panel.Start(ProjectTemplate.Spatial);
        _panel.Made += Open;
        Load();
    }

    /// <summary>Raised with a project's folder when it opens. The folder holds a project.orblit.</summary>
    public event Action<string>? Opening;

    internal Nav Nav { get; }

    internal TextBox SearchBox => _search;

    internal ComboBox SortBox => _sort;

    internal TextBlock Fault => _fault;

    internal Button OpenButton => _open;

    internal Button NewButton => _new;

    internal NewProjectPanel NewPanel => _panel;

    /// <summary>The projects the cards show, in their order.</summary>
    internal IEnumerable<ProjectEntry> Shown => _cards.Children.Select(card => card.Tag).OfType<ProjectEntry>();

    /// <summary>The project cards, each tagged with its project.</summary>
    internal IEnumerable<Control> Cards => _cards.Children;

    /// <summary>The three template buttons, in the order 3D scene, 2D scene and Empty.</summary>
    internal List<Button> TemplateButtons { get; } = [];

    /// <summary>Opens the project in this folder and lists it. A folder with no project.orblit is said as a fault.</summary>
    public void Open(string folder)
    {
        if (!File.Exists(Path.Combine(folder, ProjectEntry.SettingsFile)))
        {
            ShowFault($"No {ProjectEntry.SettingsFile} in {folder}.");
            return;
        }

        try
        {
            _list.Add(folder);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // The list only finds the project again next time, so a list that cannot be written does not stop it opening.
        }

        Opening?.Invoke(folder);
    }

    // shortcut: a listed folder with no project.orblit is hidden and stays in the file. Show it as missing with a Remove once the card has a menu.
    void Load()
    {
        try
        {
            _projects = [.. _list.Folders().Where(folder => File.Exists(Path.Combine(folder, ProjectEntry.SettingsFile))).Select(ProjectEntry.Read)];
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            ShowFault(error.Message);
        }

        ShowCards();
    }

    void ShowFault(string message)
    {
        _fault.Text = message;
        _fault.IsVisible = true;
    }

    DockPanel Projects()
    {
        var title = new StackPanel
        {
            Spacing = 4,
            Children =
            {
                new TextBlock { Text = "Projects", FontSize = 26, FontWeight = FontWeight.SemiBold },
                new TextBlock { Text = "Pick up where you left off, or start something new.", Foreground = EditorBrushes.TextDim, TextWrapping = TextWrapping.Wrap },
            },
        };
        var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Children = { _open, _new } };
        DockPanel.SetDock(actions, Dock.Right);
        DockPanel.SetDock(_sort, Dock.Right);
        var head = new StackPanel
        {
            Spacing = 18,
            Margin = new Thickness(28, 28, 28, 14),
            Children = { new DockPanel { Children = { actions, title } }, new DockPanel { Children = { _sort, _search } }, _fault },
        };
        DockPanel.SetDock(head, Dock.Top);
        var body = new StackPanel
        {
            Spacing = 12,
            Margin = new Thickness(28, 4, 28, 32),
            Children = { Label("Recent"), _cards, _note, Label("Start from a template"), Templates() },
        };
        var scroll = new ScrollViewer { Content = body };
        // The cards fill the row: as many 230 pixel columns as fit, each stretched to share what is left.
        scroll.SizeChanged += (_, e) => _cards.Columns = Math.Max(1, (int)((e.NewSize.Width - body.Margin.Left - body.Margin.Right + 2 * Half) / (CardWidth + 2 * Half)));
        return new DockPanel { Children = { head, scroll } };
    }

    static TextBlock Label(string text) => new() { Text = text, FontSize = 12, FontWeight = FontWeight.SemiBold, Foreground = EditorBrushes.TextSoft, Margin = new Thickness(0, 8, 0, 0) };

    UniformGrid Templates()
    {
        var grid = new UniformGrid { Columns = 3, Margin = new Thickness(-Half) };
        (string Icon, string Name, string Line, ProjectTemplate Template)[] templates =
        [
            ("Cube", "3D scene", "A camera, a light and ground.", ProjectTemplate.Spatial),
            ("Triangle", "2D scene", "A camera and a sprite.", ProjectTemplate.Planar),
            ("Square", "Empty", "No scene. Bring your own.", ProjectTemplate.Empty),
        ];
        foreach (var (icon, name, line, template) in templates)
        {
            var button = new Button
            {
                Margin = new Thickness(Half),
                Classes = { EditorTheme.TextButton, EditorTheme.Card },
                Content = new StackPanel
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
                },
            };
            button.Click += (_, _) => _panel.Start(template);
            TemplateButtons.Add(button);
            grid.Children.Add(button);
        }

        return grid;
    }

    void ShowCards()
    {
        var words = (_search.Text ?? "").Trim();
        var shown = Sorted(_projects.Where(project => words.Length == 0
            || project.Name.Contains(words, StringComparison.OrdinalIgnoreCase)
            || project.Folder.Contains(words, StringComparison.OrdinalIgnoreCase)));
        _cards.Children.Clear();
        _cards.Children.AddRange(shown.Select(Card));
        _note.Child = shown.Count > 0 ? null
            : _projects.Count == 0 ? Parts.EmptyState("FolderLine", "No projects yet", "New project makes one. Open adds a project folder from disk.")
            : Parts.EmptyState("Search", "Nothing matches", "No project's name or folder has these words.");
    }

    List<ProjectEntry> Sorted(IEnumerable<ProjectEntry> projects) => _sort.SelectedIndex switch
    {
        1 => [.. projects.OrderBy(project => project.Name, StringComparer.CurrentCultureIgnoreCase)],
        2 => [.. projects.OrderBy(project => project.Folder, StringComparer.Ordinal)],
        _ => [.. projects.OrderByDescending(project => project.Edited)],
    };

    Border Card(ProjectEntry project)
    {
        var tags = new WrapPanel();
        foreach (var tag in project.Tags)
        {
            tags.Children.Add(Chip(tag));
        }

        var body = new StackPanel
        {
            Spacing = 4,
            Margin = new Thickness(14, 10, 10, 14),
            Children =
            {
                new TextBlock { Text = project.Name, FontSize = 15, FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis },
                new TextBlock { Text = HomeFolder.Shorten(project.Folder), FontFamily = EditorTheme.Mono, FontSize = 11, Foreground = EditorBrushes.TextDim, TextTrimming = TextTrimming.CharacterEllipsis },
                tags,
            },
        };
        var card = new Border
        {
            Tag = project,
            Classes = { EditorTheme.Island },
            Margin = new Thickness(Half),
            Cursor = new Cursor(StandardCursorType.Hand),
            ClipToBounds = true,
            // A project's place in the whole list keeps its banner tone the same under any search or sort.
            Child = new StackPanel { Children = { Parts.Banner(IconOf(project), _projects.IndexOf(project), 136), body } },
        };
        card.Tapped += (_, _) => Open(project.Folder);
        return card;
    }

    // The template's icon, read back from the tags the launcher writes.
    static string IconOf(ProjectEntry project) =>
        project.Tags.Contains("2D") ? "Triangle" : project.Tags.Contains("3D") ? "Cube" : "Square";

    static Border Chip(string text) => new()
    {
        Padding = new Thickness(7, 2),
        Margin = new Thickness(0, 4, 4, 0),
        CornerRadius = new CornerRadius(999),
        Background = EditorBrushes.Raised,
        Child = new TextBlock { Text = text, FontSize = 11, Foreground = EditorBrushes.TextSoft },
    };
}

/// <summary>The launcher's left column: the logo, the places it can show, and the version at the foot.</summary>
internal sealed class Nav : DockPanel
{
    public Nav(Action openDocs)
    {
        var logo = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 9,
            Margin = new Thickness(10, 6, 0, 22),
            Children =
            {
                Parts.LogoMark(22),
                new TextBlock { FontSize = 17, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center, Inlines = new InlineCollection { new Run("Orb"), new Run("lit") { Foreground = EditorBrushes.Accent } } },
            },
        };
        Projects = Place("FolderLine", "Projects");
        Projects.Background = EditorBrushes.Raised;
        Docs = Place("Book", "Docs");

        Docs.Click += (_, _) => openDocs();
        var version = new TextBlock { Text = $"Orblit {BottomBar.Version}", FontFamily = EditorTheme.Mono, FontSize = 11, Foreground = EditorBrushes.TextDim, Margin = new Thickness(10, 0, 0, 4) };
        SetDock(logo, Dock.Top);
        SetDock(version, Dock.Bottom);
        Children.Add(logo);
        Children.Add(version);
        Children.Add(new StackPanel { Spacing = 2, Children = { Projects, Docs } });
    }

    internal Button Projects { get; }

    internal Button Docs { get; }

    static Button Place(string icon, string name) => new()
    {
        Content = Parts.Worded(icon, name),
        Height = 36,
        CornerRadius = new CornerRadius(8),
        HorizontalAlignment = HorizontalAlignment.Stretch,
        HorizontalContentAlignment = HorizontalAlignment.Left,
        Classes = { EditorTheme.TextButton, EditorTheme.Ghost },
    };
}
