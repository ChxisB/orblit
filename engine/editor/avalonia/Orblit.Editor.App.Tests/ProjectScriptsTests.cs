using Orblit.Editor.App;

namespace Orblit.Editor.App.Tests;

public sealed class ProjectScriptsTests : IDisposable
{
    readonly string _folder = Path.Combine(Path.GetTempPath(), $"orblit_scripts_{Guid.NewGuid():N}");

    public ProjectScriptsTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, true);

    void Touch(string relative)
    {
        var path = Path.Combine(_folder, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "");
    }

    [Fact]
    public void C_sharp_files_are_res_paths_in_order()
    {
        Touch("spin.cs");
        Touch("enemies/slime.cs");
        Touch("enemies/deep/boss.cs");
        Touch("alpha.cs");

        Assert.Equal(
            ["res://alpha.cs", "res://enemies/deep/boss.cs", "res://enemies/slime.cs", "res://spin.cs"],
            ProjectScripts.Find(_folder));
    }

    [Fact]
    public void Other_files_the_cache_and_build_folders_are_left_out()
    {
        Touch("spin.cs");
        Touch("spin.csproj");
        Touch("notes.txt");
        Touch("main.oscn");
        Touch(".orblit/imported/cached.cs");
        Touch("bin/Debug/built.cs");
        Touch("obj/generated.cs");
        Touch("game/obj/generated.cs");

        Assert.Equal(["res://spin.cs"], ProjectScripts.Find(_folder));
    }

    [Theory]
    [InlineData("res://spin.cs", "spin.cs")]
    [InlineData("res://enemies/deep/boss.cs", "enemies/deep/boss.cs")]
    public void A_res_path_names_the_file_under_the_project(string resourcePath, string relative)
    {
        Assert.Equal(Path.GetFullPath(Path.Combine(_folder, relative)), ProjectScripts.FileOf(_folder, resourcePath));
    }

    [Fact]
    public void A_project_folder_that_ends_in_a_separator_gives_the_same_file()
    {
        Assert.Equal(ProjectScripts.FileOf(_folder, "res://spin.cs"), ProjectScripts.FileOf(_folder + Path.DirectorySeparatorChar, "res://spin.cs"));
    }

    [Theory]
    [InlineData("spin.cs")]
    [InlineData("file:///etc/spin.cs")]
    [InlineData("res://spin.txt")]
    [InlineData("res://spin")]
    [InlineData("res://../outside.cs")]
    [InlineData("res://enemies/../../outside.cs")]
    public void A_path_that_is_no_script_of_the_project_is_refused(string resourcePath)
    {
        Assert.Throws<ArgumentException>(() => ProjectScripts.FileOf(_folder, resourcePath));
    }

    [Fact]
    public void A_sibling_folder_that_starts_with_the_project_name_is_refused()
    {
        var name = Path.GetFileName(_folder);

        Assert.Throws<ArgumentException>(() => ProjectScripts.FileOf(_folder, $"res://../{name}_next/spin.cs"));
    }

    [Fact]
    public void A_project_with_no_script_lists_none()
    {
        Assert.Empty(ProjectScripts.Find(_folder));
    }
}
