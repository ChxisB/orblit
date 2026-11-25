using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Orblit.Editor.App.Launcher;
using Orblit.Editor.App.Shell;

namespace Orblit.Editor.App.Tests;

public sealed class LauncherTests : IDisposable
{
    readonly string _root = Path.Combine(Path.GetTempPath(), $"orblit_launcher_{Guid.NewGuid():N}");
    readonly List<string> _opened = [];
    readonly List<string> _gitStarted = [];
    readonly List<Uri> _links = [];
    readonly ManualTime _time = new();

    ProjectList List => new(Path.Combine(_root, "data", "projects.cfg"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    sealed class Links(List<Uri> opened) : ILinkOpener
    {
        public void Open(Uri link) => opened.Add(link);
    }

    // A project folder with these settings, listed, and last opened this many hours before the clock's start.
    string Project(string folderName, string settings, int hoursAgo)
    {
        var folder = Path.Combine(_root, folderName);
        Directory.CreateDirectory(folder);
        var file = Path.Combine(folder, "project.orblit");
        File.WriteAllText(file, settings);
        File.SetLastWriteTimeUtc(file, ManualTime.Start.UtcDateTime.AddHours(-hoursAgo));
        List.Add(folder);
        return folder;
    }

    LauncherWindow Open()
    {
        var launcher = new LauncherWindow(List, _gitStarted.Add, new Links(_links), _time);
        launcher.Opening += _opened.Add;
        launcher.Show();
        Dispatcher.UIThread.RunJobs();
        return launcher;
    }

    static string[] Names(LauncherWindow launcher) => [.. launcher.Shown.Select(project => project.Name)];

    static string[] Texts(Control control)
    {
        ((Window)TopLevel.GetTopLevel(control)!).UpdateLayout();
        return [.. control.GetVisualDescendants().OfType<TextBlock>().Select(text => text.Text).OfType<string>()];
    }

    static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    static void Press(Control control, Key key, KeyModifiers modifiers = KeyModifiers.None) =>
        control.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key, KeyModifiers = modifiers });

    static string KeyOf(LauncherWindow launcher, Key key) => Parts.KeyText(new KeyGesture(key, launcher.CommandKey));

    static string? Note(MenuItem item) => item.Header is StackPanel { Children: [_, TextBlock note] } ? note.Text : null;

    static void SortBy(LauncherWindow launcher, string item) =>
        launcher.SortItems.Single(sort => AutomationProperties.GetName(sort) == item).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

    static void ClickOn(Control control)
    {
        var window = (Window)TopLevel.GetTopLevel(control)!;
        window.UpdateLayout();
        var center = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;
        window.MouseDown(center, MouseButton.Left);
        window.MouseUp(center, MouseButton.Left);
    }

    [Fact]
    public void It_shows_each_listed_project_newest_first_with_its_folder_and_when_it_was_opened_and_hides_a_missing_one()
    {
        UiThread.Run(() =>
        {
            Project("crate", "config/name=\"Crate\"\nconfig/tags=PackedStringArray(\"3D\", \"C#\")\n", 5);
            Project("runner", "config/name=\"Runner\"\n", 1);
            List.Add(Path.Combine(_root, "gone"));

            var launcher = Open();

            Assert.Equal(["Runner", "Crate"], Names(launcher));
            var crate = launcher.Cards.Single(card => card.Tag is ProjectEntry { Name: "Crate" });
            Assert.Equal(["Crate", Path.Combine(_root, "crate"), "Opened 5 hours ago"], Texts(crate)[^3..]);
        });
    }

    [Fact]
    public void Search_keeps_projects_whose_name_or_folder_has_the_words_and_sort_orders_by_name()
    {
        UiThread.Run(() =>
        {
            Project("crate", "config/name=\"Crate\"\n", 5);
            Project("runner", "config/name=\"Runner\"\n", 1);
            Project("boxes", "config/name=\"Alpha\"\n", 3);
            var launcher = Open();

            launcher.SearchBox.Text = "BOX";
            Assert.Equal(["Alpha"], Names(launcher));
            launcher.SearchBox.Text = "";
            SortBy(launcher, "Name");
            Assert.Equal(["Alpha", "Crate", "Runner"], Names(launcher));
            SortBy(launcher, "Folder");
            Assert.Equal(["Alpha", "Crate", "Runner"], Names(launcher));
            SortBy(launcher, "Last opened");
            Assert.Equal(["Runner", "Alpha", "Crate"], Names(launcher));
        });
    }

    [Fact]
    public void The_sort_button_names_the_order_in_use_and_each_choice_says_which_way_it_sorts()
    {
        UiThread.Run(() =>
        {
            var launcher = Open();
            var items = launcher.SortItems;
            Assert.Equal(["Last opened", "Name", "Folder"], items.Select(AutomationProperties.GetName));
            Assert.Equal(["Newest first", "A to Z", "A to Z, by where it lives"], items.Select(Note));
            Assert.Equal(["Last opened"], Texts(launcher.SortButton));

            SortBy(launcher, "Folder");

            Assert.Equal([false, false, true], items.Select(item => item.IsChecked));
            Assert.Equal(["Folder"], Texts(launcher.SortButton));
        });
    }

    [Fact]
    public void A_search_with_no_match_repeats_the_words_and_offers_the_two_ways_forward()
    {
        UiThread.Run(() =>
        {
            Project("crate", "config/name=\"Crate\"\n", 1);
            var launcher = Open();

            launcher.SearchBox.Text = "spaceship";

            Assert.Empty(launcher.Shown);
            var texts = Texts(launcher);
            Assert.Contains("No project matches \"spaceship\"", texts);
            Assert.Contains("Search looks at project names and folders. Check the spelling, or open the folder if it is not in the list.", texts);
            Assert.Equal(["Clear the search", "esc"], Texts(launcher.ClearSearchButton));
            Assert.Equal(["Open a folder…", KeyOf(launcher, Key.O)], Texts(launcher.NoMatchOpenButton));

            Click(launcher.ClearSearchButton);
            Assert.Equal(["Crate"], Names(launcher));
            launcher.SearchBox.Text = "spaceship";
            Press(launcher.SearchBox, Key.Escape);
            Assert.Equal("", launcher.SearchBox.Text);
        });
    }

    [Fact]
    public void The_head_says_what_the_window_is_for_and_each_button_carries_the_key_that_runs_it()
    {
        UiThread.Run(() =>
        {
            var launcher = Open();

            var texts = Texts(launcher);
            Assert.Contains("Your projects", texts);
            Assert.Contains("Pick one to keep working, or start a new one below.", texts);
            Assert.Equal(["Open a folder…", KeyOf(launcher, Key.O)], Texts(launcher.OpenButton));
            Assert.Equal(["New project", KeyOf(launcher, Key.N)], Texts(launcher.NewButton));
            Assert.Equal("Find a project by name or folder", launcher.SearchBox.PlaceholderText);

            Press(launcher, Key.F, launcher.CommandKey);
            Assert.True(launcher.SearchBox.IsFocused);
            Press(launcher, Key.N, launcher.CommandKey);
            Assert.True(launcher.NewPanel.IsVisible);
        });
    }

    [Fact]
    public void Start_something_new_says_what_each_start_opens_with()
    {
        UiThread.Run(() =>
        {
            var launcher = Open();

            Assert.Contains("Each one makes a new project folder.", Texts(launcher));
            Assert.Equal(
                [["3D game", "Opens with an empty 3D scene."], ["2D game", "Opens with an empty 2D scene."], ["Empty project", "No scene yet. Add your own."]],
                launcher.TemplateButtons.Select(Texts));
        });
    }

    [Fact]
    public void An_empty_list_says_there_are_no_projects_yet()
    {
        UiThread.Run(() =>
        {
            var launcher = Open();

            Assert.Empty(launcher.Shown);
            Assert.Contains("No projects yet", Texts(launcher));
        });
    }

    [Fact]
    public void A_click_on_a_card_opens_its_project_and_marks_it_opened_now()
    {
        UiThread.Run(() =>
        {
            var crate = Project("crate", "config/name=\"Crate\"\n", 30);
            var launcher = Open();

            ClickOn(launcher.Cards.Single());

            Assert.Equal([crate], _opened);
            Assert.Equal(ManualTime.Start.UtcDateTime, File.GetLastWriteTimeUtc(Path.Combine(crate, "project.orblit")));
        });
    }

    [Fact]
    public void Open_on_a_folder_with_no_project_says_so_and_opens_nothing()
    {
        UiThread.Run(() =>
        {
            var launcher = Open();
            var empty = Path.Combine(_root, "empty");
            Directory.CreateDirectory(empty);

            launcher.Open(empty);

            Assert.True(launcher.Fault.IsVisible);
            Assert.Equal($"No project.orblit in {empty}.", launcher.Fault.Text);
            Assert.Empty(_opened);
            Assert.Empty(List.Folders());
        });
    }

    [Fact]
    public void A_template_button_starts_the_panel_and_Create_writes_lists_and_opens_the_project()
    {
        UiThread.Run(() =>
        {
            var launcher = Open();
            var panel = launcher.NewPanel;
            var folder = Path.Combine(_root, "games", "space-ranger");

            Click(launcher.TemplateButtons[1]);
            panel.NameBox.Text = "Space Ranger";
            panel.ScriptChoice.SelectedIndex = 1;
            Assert.True(panel.IsVisible);
            Assert.Equal(1, panel.TemplateChoice.SelectedIndex);
            panel.FolderBox.Text = folder;
            Click(panel.CreateButton);

            Assert.False(panel.IsVisible);
            Assert.Equal([folder], _opened);
            Assert.Equal([folder], List.Folders());
            Assert.Equal([folder], _gitStarted);
            Assert.Contains("config/tags=PackedStringArray(\"2D\", \"Graph\")", File.ReadAllText(Path.Combine(folder, "project.orblit")));
            Assert.True(File.Exists(Path.Combine(folder, "main.ograph")));
        });
    }

    [Fact]
    public void Git_starts_only_when_the_box_is_ticked()
    {
        UiThread.Run(() =>
        {
            var launcher = Open();
            var panel = launcher.NewPanel;

            Click(launcher.NewButton);
            panel.Git.IsChecked = false;
            panel.FolderBox.Text = Path.Combine(_root, "plain");
            Click(panel.CreateButton);

            Assert.Single(_opened);
            Assert.Empty(_gitStarted);
            Assert.False(File.Exists(Path.Combine(_root, "plain", ".gitignore")));
        });
    }

    [Fact]
    public void A_folder_that_holds_anything_is_said_in_the_panel_and_nothing_is_written()
    {
        UiThread.Run(() =>
        {
            var launcher = Open();
            var panel = launcher.NewPanel;
            var folder = Path.Combine(_root, "taken");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "keep.txt"), "mine");

            Click(launcher.NewButton);
            panel.FolderBox.Text = folder;
            Click(panel.CreateButton);

            Assert.True(panel.IsVisible);
            Assert.True(panel.Fault.IsVisible);
            Assert.Equal($"{folder} is not empty. Choose a new folder.", panel.Fault.Text);
            Assert.Equal(["keep.txt"], Directory.GetFiles(folder).Select(Path.GetFileName));
            Assert.Empty(_opened);
            Assert.Empty(_gitStarted);
        });
    }

    [Fact]
    public void The_name_names_the_project_folder()
    {
        UiThread.Run(() =>
        {
            var panel = Open().NewPanel;

            panel.NameBox.Text = "Space Ranger 2!";

            Assert.Equal(Path.Combine("~/Games", "space-ranger-2"), panel.FolderBox.Text);
        });
    }

    [Fact]
    public void A_folder_typed_by_hand_is_kept_when_the_name_changes()
    {
        UiThread.Run(() =>
        {
            var panel = Open().NewPanel;

            panel.FolderBox.Text = "~/Work/kept";
            panel.NameBox.Text = "Space Ranger";

            Assert.Equal("~/Work/kept", panel.FolderBox.Text);
        });
    }

    [Fact]
    public void Learn_opens_the_guide_in_the_browser_and_the_version_card_copies_the_version()
    {
        UiThread.Run(async () =>
        {
            var launcher = Open();
            var learn = (HoverCard)ToolTip.GetTip(launcher.Nav.Learn)!;

            Click(launcher.Nav.Learn);
            Click(launcher.Nav.Version);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(("Learn", "Opens the guide on orblitengine.com in your browser."), (learn.Title, learn.Line));
            Assert.Equal([HelpMenu.Docs], _links);
            Assert.Equal([$"Orblit {BottomBar.Version}", "Click to copy the version for a bug report."], Texts(launcher.Nav.Version));
            Assert.Equal(BottomBar.Version, await launcher.Clipboard!.TryGetTextAsync());
            Assert.Equal([launcher.Nav.Version, launcher.Nav.Projects, launcher.Nav.Learn], launcher.Nav.GetVisualDescendants().OfType<Button>());
        });
    }
}
