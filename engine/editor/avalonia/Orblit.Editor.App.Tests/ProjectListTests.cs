using Orblit.Editor.App.Launcher;

namespace Orblit.Editor.App.Tests;

public sealed class ProjectListTests : IDisposable
{
    readonly string _folder = Path.Combine(Path.GetTempPath(), $"orblit_list_{Guid.NewGuid():N}");

    string File => Path.Combine(_folder, "data", "projects.cfg");

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, true);
        }
    }

    [Fact]
    public void A_missing_file_lists_no_projects()
    {
        Assert.Empty(new ProjectList(File).Folders());
    }

    [Fact]
    public void Add_makes_the_folder_and_writes_a_section_as_the_engine_does()
    {
        var list = new ProjectList(File);

        list.Add("/games/a");
        list.Add("/games/b");

        Assert.Equal("[/games/a]\n\nfavorite=false\n\n[/games/b]\n\nfavorite=false\n", System.IO.File.ReadAllText(File));
        Assert.Equal(["/games/a", "/games/b"], list.Folders());
    }

    [Fact]
    public void Add_keeps_the_sections_and_keys_already_there()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(File)!);
        System.IO.File.WriteAllText(File, "[/games/old]\n\nfavorite=true");

        new ProjectList(File).Add("/games/new");

        Assert.Equal("[/games/old]\n\nfavorite=true\n\n[/games/new]\n\nfavorite=false\n", System.IO.File.ReadAllText(File));
    }

    [Fact]
    public void A_folder_already_listed_is_not_added_twice()
    {
        var list = new ProjectList(File);
        list.Add("/games/a");

        list.Add("/games/a");

        Assert.Equal(["/games/a"], list.Folders());
    }

    [Fact]
    public void A_bracket_in_a_folder_name_is_escaped_in_the_file_and_read_back_whole()
    {
        var list = new ProjectList(File);

        list.Add("/games/[wip]");

        Assert.StartsWith("[/games/[wip\\]]\n", System.IO.File.ReadAllText(File));
        Assert.Equal(["/games/[wip]"], list.Folders());
    }

    [Fact]
    public void An_entry_reads_the_name_and_tags_and_falls_back_to_the_folder_name()
    {
        var named = Path.Combine(_folder, "crate");
        var unnamed = Path.Combine(_folder, "runner");
        Directory.CreateDirectory(named);
        Directory.CreateDirectory(unnamed);
        System.IO.File.WriteAllText(Path.Combine(named, "project.orblit"), "config/name=\"Crate\"\nconfig/tags=PackedStringArray(\"3D\")\n");
        System.IO.File.WriteAllText(Path.Combine(unnamed, "project.orblit"), "config_version=5\n");

        Assert.Equal("Crate", ProjectEntry.Read(named).Name);
        Assert.Equal(["3D"], ProjectEntry.Read(named).Tags);
        Assert.Equal("runner", ProjectEntry.Read(unnamed).Name);
        Assert.Empty(ProjectEntry.Read(unnamed).Tags);
    }

    [Theory]
    [InlineData("My game", "my-game")]
    [InlineData("  Space -- Ranger 2! ", "space-ranger-2")]
    [InlineData("???", "my-game")]
    public void The_folder_slug_is_the_name_in_lower_case_with_dashes(string name, string slug)
    {
        Assert.Equal(slug, HomeFolder.Slug(name));
    }

    [Fact]
    public void A_folder_under_home_shortens_to_a_tilde_and_expands_back()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var folder = Path.Combine(home, "Games", "crate");

        Assert.Equal("~/Games/crate".Replace('/', Path.DirectorySeparatorChar), HomeFolder.Shorten(folder));
        Assert.Equal(folder, HomeFolder.Expand("~/Games/crate"));
        Assert.Equal("/elsewhere/crate", HomeFolder.Shorten("/elsewhere/crate"));
    }
}
