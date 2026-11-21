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
    public void The_main_scene_is_run_main_scene_or_main_oscn_when_it_is_missing()
    {
        Assert.Equal("res://main.oscn", ProjectSettings.MainSceneOf(_folder));
        Write("[application]\n\nrun/main_scene=\"res://levels/start.oscn\"\n");

        Assert.Equal("res://levels/start.oscn", ProjectSettings.MainSceneOf(_folder));
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

    [Fact]
    public void The_tags_are_the_quoted_values_of_config_tags()
    {
        Write("config/name=\"crate\"\nconfig/tags=PackedStringArray(\"3D\", \"C#\")\n");

        Assert.Equal(["3D", "C#"], ProjectSettings.TagsOf(_folder));
    }

    [Fact]
    public void A_missing_file_or_key_gives_no_tags()
    {
        Assert.Empty(ProjectSettings.TagsOf(_folder));

        Write("config/name=\"crate\"\n");

        Assert.Empty(ProjectSettings.TagsOf(_folder));
    }

    string Text() => File.ReadAllText(Path.Combine(_folder, "project.orblit"));

    [Fact]
    public void A_key_set_in_a_section_the_file_has_goes_under_its_heading()
    {
        Write("[application]\n\nconfig/name=\"crate\"\n\n[rendering]\n\nlights/max=8\n");

        ProjectSettings.SetValue(_folder, "rendering", "textures/etc2=true");

        Assert.Equal("[application]\n\nconfig/name=\"crate\"\n\n[rendering]\n\ntextures/etc2=true\nlights/max=8\n", Text());
    }

    [Fact]
    public void A_key_the_file_has_takes_the_new_value_in_its_place()
    {
        Write("[rendering]\n\ntextures/etc2=false\nlights/max=8\n");

        ProjectSettings.SetValue(_folder, "rendering", "textures/etc2=true");

        Assert.Equal("[rendering]\n\ntextures/etc2=true\nlights/max=8\n", Text());
    }

    [Fact]
    public void A_key_in_a_section_the_file_lacks_adds_the_section_at_the_end()
    {
        Write("[application]\n\nconfig/name=\"crate\"\n");

        ProjectSettings.SetValue(_folder, "rendering", "textures/etc2=true");

        Assert.Equal("[application]\n\nconfig/name=\"crate\"\n\n[rendering]\n\ntextures/etc2=true\n", Text());
    }
}
