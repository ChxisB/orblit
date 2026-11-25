using Orblit.Editor.App.Launcher;

namespace Orblit.Editor.App.Tests;

public sealed class EditorSettingsTests : IDisposable
{
    readonly string _folder = Path.Combine(Path.GetTempPath(), $"orblit_settings_{Guid.NewGuid():N}");

    string File => Path.Combine(_folder, "data", "editor_settings.cfg");

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, true);
        }
    }

    [Fact]
    public void A_missing_file_puts_new_projects_in_Games()
    {
        Assert.Equal("~/Games", new EditorSettings(File).ProjectsFolder());
    }

    [Fact]
    public void A_written_folder_reads_back_and_the_file_has_a_section_and_a_key()
    {
        new EditorSettings(File).SetProjectsFolder("~/Work/my \"best\" games");

        Assert.Equal("~/Work/my \"best\" games", new EditorSettings(File).ProjectsFolder());
        Assert.Equal("[editor]\n\nprojects_folder=\"~/Work/my \\\"best\\\" games\"\n", System.IO.File.ReadAllText(File));
    }

    [Fact]
    public void A_Windows_folder_keeps_its_backslashes()
    {
        new EditorSettings(File).SetProjectsFolder(@"C:\Users\sam\Games");

        Assert.Equal(@"C:\Users\sam\Games", new EditorSettings(File).ProjectsFolder());
    }

    [Theory]
    [InlineData("")]
    [InlineData("[editor]\n\ntheme=\"dark\"\n")]
    [InlineData("projects_folder=\n")]
    [InlineData("not a settings file at all")]
    public void A_file_with_no_folder_in_it_puts_new_projects_in_Games(string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(File)!);
        System.IO.File.WriteAllText(File, text);

        Assert.Equal("~/Games", new EditorSettings(File).ProjectsFolder());
    }

    [Fact]
    public void The_settings_sit_beside_the_project_list()
    {
        var list = new ProjectList(Path.Combine(_folder, "data", "projects.cfg"));

        list.Settings().SetProjectsFolder("~/Beside");

        Assert.Equal("~/Beside", new EditorSettings(File).ProjectsFolder());
    }
}
