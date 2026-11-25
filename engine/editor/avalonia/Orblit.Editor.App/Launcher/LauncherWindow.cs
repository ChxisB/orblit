using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using Orblit.Editor.App.Shell;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Launcher;

/// <summary>
/// The window the editor opens on when no project is named. It shows the projects in projects.cfg as cards,
/// with a search and a sort over them. Open a folder… adds a project folder from disk and New project writes a
/// new one. Either raises Opening with the folder. Each card has a menu that opens, shows, copies or removes it.
/// </summary>
internal sealed class LauncherWindow : Window
{
    const double CardWidth = 230;
    // Half the 14 pixel gap between cards, as a margin on every side of each.
    const double Half = 7;

    // The sorts the menu offers: the name of each, which the button shows while it is in use, and its note.
    static readonly (string Name, string Note)[] s_sorts =
    [
        ("Last opened", "Newest first"),
        ("Name", "A to Z"),
        ("Folder", "A to Z, by where it lives"),
    ];

    readonly ProjectList _list;
    readonly ILinkOpener _links;
    readonly TimeProvider _time;
    readonly TextBox _search = new() { PlaceholderText = "Find a project by name or folder", InnerLeftContent = Parts.FieldIcon("Lens", EditorBrushes.TextDim), Height = 36, Classes = { EditorTheme.Field } };
    readonly TextBlock _sortWord = new() { Text = s_sorts[0].Name, VerticalAlignment = VerticalAlignment.Center };
    readonly MenuItem[] _sorts;
    readonly Button _sort;
    readonly UniformGrid _cards = new() { Margin = new Thickness(-Half) };
    readonly Decorator _note = new();
    readonly StackPanel _noMatch = Parts.EmptyState("Search", "", "Search looks at project names and folders. Check the spelling, or open the folder if it is not in the list.");
    readonly TextBlock _fault = Parts.FaultLine();
    readonly Button _open;
    readonly Button _new;
    readonly NewProjectPanel _panel;
    List<ProjectEntry> _projects = [];
    int _order;

    public LauncherWindow(ProjectList list, Action<string> initGit, ILinkOpener? links = null, TimeProvider? time = null)
    {
        _list = list;
        _links = links ?? new WindowLinks(this);
        _time = time ?? TimeProvider.System;
        _panel = new NewProjectPanel(initGit);
        var openKey = Parts.KeyText(new KeyGesture(Key.O, CommandKey));
        _open = new HoverCard("Open a folder…", "Adds a project folder from disk to this list, then opens it.", openKey).On(new Button
        {
            Content = Parts.Worded("FolderLine", "Open a folder…", openKey),
            Margin = new Thickness(0, 0, 8, 0),
            Classes = { EditorTheme.TextButton, EditorTheme.Ghost },
        });
        var newKey = Parts.KeyText(new KeyGesture(Key.N, CommandKey));
        _new = new HoverCard("New project", "Makes a new project folder and opens it.", newKey).On(new Button
        {
            Content = Parts.Worded("Add", "New project", newKey),
            Classes = { EditorTheme.TextButton, EditorTheme.Primary },
        });
        NoMatchOpenButton = new Button { Content = Parts.Keyed("Open a folder…", openKey), Classes = { EditorTheme.TextButton, EditorTheme.Small } };
        _search.InnerRightContent = new Border { Margin = new Thickness(0, 0, 8, 0), Child = Parts.Kbd(Parts.KeyText(new KeyGesture(Key.F, CommandKey))) };
        _sorts = [.. s_sorts.Select((sort, at) => Parts.ActionItem(sort.Name, () => SortBy(at), note: sort.Note))];
        _sort = SortMenu(_sortWord, _sorts);
        Title = "Orblit";
        Icon = EditorIcon.ToWindowIcon("Orblit", 64);
        Width = 1120;
        Height = 740;
        MinWidth = 820;
        MinHeight = 560;
        Background = EditorBrushes.Window;
        Nav = new Nav(() => _links.Open(HelpMenu.Docs), () => Copy(BottomBar.Version));
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
        _search.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape && _search.Text is { Length: > 0 })
            {
                e.Handled = true;
                _search.Text = "";
            }
        };
        ClearSearchButton.Click += (_, _) => _search.Text = "";
        var ways = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 6, 0, 0), HorizontalAlignment = HorizontalAlignment.Center, Children = { ClearSearchButton, NoMatchOpenButton } };
        _noMatch.Children.Add(ways);
        _open.Click += (_, _) => PickAndOpen();
        NoMatchOpenButton.Click += (_, _) => PickAndOpen();
        _new.Click += (_, _) => _panel.Start(ProjectTemplate.Spatial);
        _panel.Made += Open;
        Load();
    }

    /// <summary>Raised with a project's folder when it opens. The folder holds a project.orblit.</summary>
    public event Action<string>? Opening;

    internal Nav Nav { get; }

    internal TextBox SearchBox => _search;

    internal Button SortButton => _sort;

    internal IReadOnlyList<MenuItem> SortItems => _sorts;

    /// <summary>Empties the search. It shows while no project matches.</summary>
    internal Button ClearSearchButton { get; } = new() { Content = Parts.Keyed("Clear the search", "esc"), Classes = { EditorTheme.TextButton, EditorTheme.Small } };

    /// <summary>Picks a project folder from disk. It shows while no project matches, as the other way to find one.</summary>
    internal Button NoMatchOpenButton { get; }

    /// <summary>The card menu's item that opens a project's folder, named for this system's file browser.</summary>
    internal static string ShowInFolder { get; } = OperatingSystem.IsMacOS() ? "Show in Finder" : OperatingSystem.IsWindows() ? "Show in Explorer" : "Show in file manager";

    internal KeyModifiers CommandKey =>this.GetPlatformSettings()?.HotkeyConfiguration.CommandModifiers ?? KeyModifiers.Control;

    internal TextBlock Fault => _fault;

    internal Button OpenButton => _open;

    internal Button NewButton => _new;

    internal NewProjectPanel NewPanel => _panel;

    /// <summary>The projects the cards show, in their order.</summary>
    internal IEnumerable<ProjectEntry> Shown => _cards.Children.Select(card => card.Tag).OfType<ProjectEntry>();

    /// <summary>The project cards, each tagged with its project.</summary>
    internal IEnumerable<Control> Cards => _cards.Children;

    /// <summary>The three template buttons, in the order 3D game, 2D game and Empty project.</summary>
    internal List<Button> TemplateButtons { get; } = [];

    /// <summary>
    /// Opens the project in this folder, lists it and marks it opened now. A folder with no project.orblit is said
    /// as a fault.
    /// </summary>
    public void Open(string folder)
    {
        var settings = Path.Combine(folder, ProjectEntry.SettingsFile);
        if (!File.Exists(settings))
        {
            ShowFault($"No {ProjectEntry.SettingsFile} in {folder}.");
            return;
        }

        try
        {
            _list.Add(folder);
            // A card says when its project was last opened by this time, as the engine's own project list does.
            File.SetLastWriteTimeUtc(settings, _time.GetUtcNow().UtcDateTime);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Both only change what the launcher shows next time, so a failure does not stop the project opening.
        }

        Opening?.Invoke(folder);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || e.KeyModifiers != CommandKey)
        {
            return;
        }

        if (e.Key == Key.F)
        {
            _search.Focus();
            e.Handled = true;
        }
        else if ((e.Key switch { Key.O => _open, Key.N => _new, _ => null }) is { } button)
        {
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            e.Handled = true;
        }
    }

    async void PickAndOpen()
    {
        if (await NewProjectPanel.PickFolderAsync(this, "Open a project folder") is { } folder)
        {
            Open(folder);
        }
    }

    async void Copy(string text)
    {
        // An async void that throws ends the app, and each platform's clipboard fails with its own type.
        try
        {
            if (Clipboard is { } clipboard)
            {
                await clipboard.SetTextAsync(text);
            }
        }
        catch (Exception error)
        {
            ShowFault(error.Message);
        }
    }

    // shortcut: a listed folder with no project.orblit is hidden and stays in the file. Show it as a missing card with Remove from this list if a hidden line gets in the way.
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
                new TextBlock { Text = "Your projects", FontSize = 26, FontWeight = FontWeight.SemiBold },
                new TextBlock { Text = "Pick one to keep working, or start a new one below.", Foreground = EditorBrushes.TextDim, TextWrapping = TextWrapping.Wrap },
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
            Children = { _cards, _note, StartsHead(), Templates() },
        };
        var scroll = new ScrollViewer { Content = body };
        // The cards fill the row: as many 230 pixel columns as fit, each stretched to share what is left.
        scroll.SizeChanged += (_, e) => _cards.Columns = Math.Max(1, (int)((e.NewSize.Width - body.Margin.Left - body.Margin.Right + 2 * Half) / (CardWidth + 2 * Half)));
        return new DockPanel { Children = { head, scroll } };
    }

    static StackPanel StartsHead() => new()
    {
        Orientation = Orientation.Horizontal,
        Spacing = 10,
        Margin = new Thickness(0, 16, 0, 0),
        Children =
        {
            new TextBlock { Text = "Start something new", FontSize = 13, FontWeight = FontWeight.SemiBold },
            new TextBlock { Text = "Each one makes a new project folder.", FontSize = 12, Foreground = EditorBrushes.TextDim, VerticalAlignment = VerticalAlignment.Center },
        },
    };

    // The word of the sort in use, and an arrow that says a menu opens.
    static Button SortMenu(TextBlock word, MenuItem[] sorts)
    {
        var chevron = new EditorIcon("GuiDropdown") { VerticalAlignment = VerticalAlignment.Center };
        TextElement.SetForeground(chevron, EditorBrushes.TextDim);
        foreach (var item in sorts)
        {
            item.ToggleType = MenuItemToggleType.Radio;
        }

        sorts[0].IsChecked = true;
        var button = new Button
        {
            Height = 36,
            Margin = new Thickness(8, 0, 0, 0),
            Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { new EditorIcon("Sort") { VerticalAlignment = VerticalAlignment.Center }, word, chevron } },
            Classes = { EditorTheme.TextButton, EditorTheme.Ghost },
            Flyout = new MenuFlyout { ItemsSource = sorts },
        };
        return new HoverCard("Sort by", "Picks the order of the projects.").On(button);
    }

    void SortBy(int order)
    {
        _order = order;
        _sortWord.Text = s_sorts[order].Name;
        foreach (var (item, at) in _sorts.Select((item, at) => (item, at)))
        {
            item.IsChecked = at == order;
        }

        ShowCards();
    }

    UniformGrid Templates()
    {
        var grid = new UniformGrid { Columns = 3, Margin = new Thickness(-Half) };
        (string Icon, string Name, string Line, ProjectTemplate Template)[] templates =
        [
            ("Cube", "3D game", "Opens with an empty 3D scene.", ProjectTemplate.Spatial),
            ("Triangle", "2D game", "Opens with an empty 2D scene.", ProjectTemplate.Planar),
            ("Square", "Empty project", "No scene yet. Add your own.", ProjectTemplate.Empty),
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
        // The title is the second line of the empty state, under its mark.
        ((TextBlock)_noMatch.Children[1]).Text = $"No project matches \"{words}\"";
        _note.Child = shown.Count > 0 ? null
            : _projects.Count == 0 ? Parts.EmptyState("FolderLine", "No projects yet", "New project makes one. Open a folder… adds one from disk.")
            : _noMatch;
    }

    List<ProjectEntry> Sorted(IEnumerable<ProjectEntry> projects) => _order switch
    {
        1 => [.. projects.OrderBy(project => project.Name, StringComparer.CurrentCultureIgnoreCase)],
        2 => [.. projects.OrderBy(project => project.Folder, StringComparer.Ordinal)],
        _ => [.. projects.OrderByDescending(project => project.Edited)],
    };

    Border Card(ProjectEntry project)
    {
        var menu = CardMenu(project);
        var more = new HoverCard("More", "Open, show its folder, copy its path or remove it from this list. A right-click on the card opens the same menu.").On(Parts.IconButton("GuiEllipsis", "More"));
        var name = new TextBlock { Text = project.Name, FontSize = 15, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        var body = new StackPanel
        {
            Spacing = 4,
            Margin = new Thickness(14, 10, 10, 14),
            Children =
            {
                new DockPanel { Children = { Parts.Right(more), name } },
                new TextBlock { Text = HomeFolder.Shorten(project.Folder), FontFamily = EditorTheme.Mono, FontSize = 11, Foreground = EditorBrushes.TextDim, TextTrimming = TextTrimming.CharacterEllipsis },
                new TextBlock { Text = OpenedLine(project), FontSize = 11, Foreground = EditorBrushes.TextDim },
            },
        };
        var card = new Border
        {
            Tag = project,
            Classes = { EditorTheme.Island },
            Margin = new Thickness(Half),
            Cursor = new Cursor(StandardCursorType.Hand),
            ClipToBounds = true,
            Focusable = true,
            ContextMenu = menu,
            // A project's place in the whole list keeps its banner tone the same under any search or sort.
            Child = new StackPanel { Children = { Parts.Banner(IconOf(project), _projects.IndexOf(project), 136), body } },
        };
        card.Tapped += (_, _) => Open(project.Folder);
        // A context menu opens only on the control that holds it. It opens at the pointer, so by the button.
        more.Click += (_, _) => menu.Open(card);
        // The tap would rise to the card and open the project as well.
        more.Tapped += (_, e) => e.Handled = true;
        card.KeyDown += (_, e) => RunKey(menu, e);
        return card;
    }

    // A focused card takes the keys its menu shows, so each key works where it is written.
    static void RunKey(ContextMenu menu, KeyEventArgs e)
    {
        if (menu.Items.OfType<MenuItem>().FirstOrDefault(item => item.InputGesture?.Matches(e) == true) is { } item)
        {
            e.Handled = true;
            item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        }
    }

    ContextMenu CardMenu(ProjectEntry project) => new()
    {
        Items =
        {
            Parts.ActionItem("Open", () => Open(project.Folder), new KeyGesture(Key.Enter), "Opens the project in the editor"),
            Parts.ActionItem(ShowInFolder, () => _links.Open(new Uri(project.Folder)), note: "Opens its folder"),
            Parts.ActionItem("Copy folder path", () => Copy(project.Folder), new KeyGesture(Key.C, CommandKey | KeyModifiers.Alt)),
            new Separator(),
            Parts.ActionItem("Remove from this list", () => Remove(project), note: "The project stays on disk. Open a folder… brings it back."),
        },
    };

    void Remove(ProjectEntry project)
    {
        try
        {
            _list.Remove(project.Folder);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            ShowFault(error.Message);
            return;
        }

        _projects.Remove(project);
        ShowCards();
    }

    // The template's icon, read back from the tags the launcher writes.
    static string IconOf(ProjectEntry project) =>
        project.Tags.Contains("2D") ? "Triangle" : project.Tags.Contains("3D") ? "Cube" : "Square";

    // In the local zone, so yesterday is the day before the one on the person's clock.
    string OpenedLine(ProjectEntry project)
    {
        var when = NoScene.Opened(TimeZoneInfo.ConvertTime(new DateTimeOffset(project.Edited), _time.LocalTimeZone), _time.GetLocalNow());
        return char.ToUpperInvariant(when[0]) + when[1..];
    }
}

/// <summary>The launcher's left column: the logo, the places it can show, and the version at the foot.</summary>
internal sealed class Nav : DockPanel
{
    public Nav(Action openLearn, Action copyVersion)
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
        Projects = new HoverCard("Projects", "Every project you made or opened here.").On(Place(Parts.Worded("FolderLine", "Projects")));
        Projects.Background = EditorBrushes.Raised;
        // The arrow says the guide opens outside the app.
        var arrow = new EditorIcon("ExternalLink") { Width = 12, Height = 12, VerticalAlignment = VerticalAlignment.Center };
        TextElement.SetForeground(arrow, EditorBrushes.TextDim);
        Learn = new HoverCard("Learn", "Opens the guide on orblitengine.com in your browser.").On(Place(new DockPanel { Children = { Parts.Right(arrow), Parts.Worded("Book", "Learn") } }));
        Learn.Click += (_, _) => openLearn();
        Version = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Classes = { EditorTheme.TextButton, EditorTheme.Card },
            Content = new DockPanel
            {
                Children =
                {
                    Parts.Right(Parts.FieldIcon("ActionCopy", EditorBrushes.TextDim)),
                    new StackPanel
                    {
                        Spacing = 2,
                        Children =
                        {
                            new TextBlock { Text = $"Orblit {BottomBar.Version}", FontSize = 12, FontWeight = FontWeight.SemiBold },
                            new TextBlock { Text = "Click to copy the version for a bug report.", FontSize = 11, Foreground = EditorBrushes.TextDim, TextWrapping = TextWrapping.Wrap },
                        },
                    },
                },
            },
        };
        Version.Click += (_, _) => copyVersion();
        SetDock(logo, Dock.Top);
        SetDock(Version, Dock.Bottom);
        Children.Add(logo);
        Children.Add(Version);
        Children.Add(new StackPanel { Spacing = 2, Children = { Projects, Learn } });
    }

    internal Button Projects { get; }

    internal Button Learn { get; }

    /// <summary>The version card. A click copies the version.</summary>
    internal Button Version { get; }

    static Button Place(Control content) => new()
    {
        Content = content,
        Height = 36,
        CornerRadius = new CornerRadius(8),
        HorizontalAlignment = HorizontalAlignment.Stretch,
        HorizontalContentAlignment = HorizontalAlignment.Stretch,
        Classes = { EditorTheme.TextButton, EditorTheme.Ghost },
    };
}
