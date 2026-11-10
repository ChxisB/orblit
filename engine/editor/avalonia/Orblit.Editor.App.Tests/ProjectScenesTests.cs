using Orblit.Editor.App;

namespace Orblit.Editor.App.Tests;

public sealed class ProjectScenesTests : IDisposable
{
    readonly string _folder = Path.Combine(Path.GetTempPath(), $"orblit_scenes_{Guid.NewGuid():N}");

    public ProjectScenesTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, true);

    void Touch(string relative)
    {
        var path = Path.Combine(_folder, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "");
    }

    [Fact]
    public void Scenes_and_folders_are_res_paths_in_order_and_a_folder_ends_in_a_slash()
    {
        Touch("zeta.oscn");
        Touch("levels/two.oscn");
        Touch("levels/deep/three.oscn");
        Touch("alpha.oscn");

        Assert.Equal(
            ["res://alpha.oscn", "res://levels/", "res://levels/deep/", "res://levels/deep/three.oscn", "res://levels/two.oscn", "res://zeta.oscn"],
            ProjectScenes.Find(_folder));
    }

    [Fact]
    public void Other_files_and_the_cache_folder_are_left_out()
    {
        Touch("main.oscn");
        Touch("main.oscnb");
        Touch("notes.txt");
        Touch(".orblit/imported/cached.oscn");

        Assert.Equal(["res://main.oscn"], ProjectScenes.Find(_folder));
    }

    [Fact]
    public void A_project_with_nothing_in_it_lists_none()
    {
        Assert.Empty(ProjectScenes.Find(_folder));
    }

    [Fact]
    public void An_empty_folder_still_has_a_row()
    {
        Directory.CreateDirectory(Path.Combine(_folder, "scripts"));

        Assert.Equal(["res://scripts/"], ProjectScenes.Find(_folder));
    }

    [Fact]
    public void The_first_scene_counts_when_it_is_a_file_of_the_project()
    {
        Touch("levels/one.oscn");

        Assert.True(new EditorOptions(_folder, "res://levels/one.oscn", true).HasScene);
        Assert.False(new EditorOptions(_folder, "res://levels/missing.oscn", true).HasScene);
        Assert.False(new EditorOptions(_folder, "levels/one.oscn", true).HasScene);
    }
}
