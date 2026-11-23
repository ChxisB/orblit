using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
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

    // A project folder with these settings, listed, and last edited this many hours ago.
    string Project(string folderName, string settings, int hoursAgo)
    {
        var folder = Path.Combine(_root, folderName);
        Directory.CreateDirectory(folder);
        var file = Path.Combine(folder, "project.orblit");
        File.WriteAllText(file, settings);
        File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddHours(-hoursAgo));
        List.Add(folder);
        return folder;
    }

    LauncherWindow Open()
    {
        var launcher = new LauncherWindow(List, _gitStarted.Add, new Links(_links));
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

    static void Click(Button button) => button.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

    static void ClickOn(Control control)
    {
        var window = (Window)TopLevel.GetTopLevel(control)!;
        window.UpdateLayout();
        var center = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;
        window.MouseDown(center, MouseButton.Left);
        window.MouseUp(center, MouseButton.Left);
    }

    [Fact]
    public void It_shows_each_listed_project_newest_first_with_its_folder_and_tags_and_hides_a_missing_one()
    {
        UiThread.Run(() =>
        {
            Project("crate", "config/name=\"Crate\"\nconfig/tags=PackedStringArray(\"3D\", \"C#\")\n", 5);
            Project("runner", "config/name=\"Runner\"\n", 1);
            List.Add(Path.Combine(_root, "gone"));

            var launcher = Open();

            Assert.Equal(["Runner", "Crate"], Names(launcher));
            var crate = launcher.Cards.Single(card => card.Tag is ProjectEntry { Name: "Crate" });
            Assert.Equal(["Crate", Path.Combine(_root, "crate"), "3D", "C#"], Texts(crate)[^4..]);
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
            launcher.SortBox.SelectedIndex = 1;
            Assert.Equal(["Alpha", "Crate", "Runner"], Names(launcher));
            launcher.SearchBox.Text = "nothing";
            Assert.Empty(launcher.Shown);
            Assert.Contains("Nothing matches", Texts(launcher));
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
    public void A_click_on_a_card_opens_its_project()
    {
        UiThread.Run(() =>
        {
            var crate = Project("crate", "config/name=\"Crate\"\n", 1);
            var launcher = Open();

            ClickOn(launcher.Cards.Single());

            Assert.Equal([crate], _opened);
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
    public void Docs_opens_the_site_and_the_places_not_built_are_off_and_say_so()
    {
        UiThread.Run(() =>
        {
            Project("crate", "config/name=\"Crate\"\n", 1);
            var launcher = Open();

            Click(launcher.Nav.Docs);

            Assert.Equal([HelpMenu.Docs], _links);
            var more = launcher.Cards.Single().GetVisualDescendants().OfType<Button>().Single();
            Button[] waiting = [launcher.Nav.Templates, launcher.Nav.Settings, more];
            Assert.All(waiting, button => Assert.False(button.IsEnabled));
            Assert.All(waiting, button => Assert.Equal("Not built yet", (string?)ToolTip.GetTip(button)));
            Assert.True(launcher.Nav.Projects.IsEnabled);
        });
    }
}
