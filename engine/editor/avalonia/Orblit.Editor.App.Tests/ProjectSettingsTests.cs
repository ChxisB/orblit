using Orblit.Editor.App;

namespace Orblit.Editor.App.Tests;

public sealed class ProjectSettingsTests : IDisposable
{
    readonly string _folder = Path.Combine(Path.GetTempPath(), $"orblit_settings_{Guid.NewGuid():N}");

    public ProjectSettingsTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, true);

    void Write(string text) => File.WriteAllText(Path.Combine(_folder, "project.orblit"), text);

    [Fact]
    public void The_name_is_the_quoted_value_of_config_name()
    {
        Write("config_version=5\n\n[application]\n\nconfig/name=\"view_demo\"\nrun/main_scene=\"res://main.oscn\"\n");

        Assert.Equal("view_demo", ProjectSettings.NameOf(_folder));
    }

    [Fact]
    public void A_name_with_spaces_is_kept_whole()
    {
        Write("config/name=\"Space Ranger\"\n");

        Assert.Equal("Space Ranger", ProjectSettings.NameOf(_folder));
    }

    [Fact]
    public void A_key_that_only_starts_with_the_name_is_not_the_name()
    {
        Write("config/name_localized={\n}\nconfig/name=\"real\"\n");

        Assert.Equal("real", ProjectSettings.NameOf(_folder));
    }

    [Fact]
    public void A_missing_file_or_key_gives_an_empty_name()
    {
        Assert.Equal("", ProjectSettings.NameOf(_folder));

        Write("config_version=5\n");

        Assert.Equal("", ProjectSettings.NameOf(_folder));
    }
}
