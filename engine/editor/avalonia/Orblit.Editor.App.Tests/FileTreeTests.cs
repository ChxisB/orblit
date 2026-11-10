namespace Orblit.Editor.App.Tests;

public sealed class FileTreeTests
{
    static readonly string[] Scenes = ["res://main.oscn", "res://levels/b.oscn", "res://levels/a.oscn", "res://levels/deep/c.oscn", "res://Zed.oscn", "res://Zoo/z.oscn"];

    static string[] Names(IReadOnlyList<FileNode> nodes) => [.. nodes.Select(node => node.Name)];

    static DateTime Day(int day) => new(2026, 11, day);

    // c comes before b, so a tie in time that sorted by position and not by name would show.
    static readonly string[] Dated = ["res://a.oscn", "res://c.oscn", "res://b.oscn", "res://levels/x.oscn", "res://levels/y.oscn"];

    static readonly Dictionary<string, DateTime> Times = new()
    {
        ["res://a.oscn"] = Day(1),
        ["res://b.oscn"] = Day(3),
        ["res://c.oscn"] = Day(3),
        ["res://levels/x.oscn"] = Day(1),
        ["res://levels/y.oscn"] = Day(2),
    };

    [Fact]
    public void Newest_first_orders_the_files_of_each_folder_and_keeps_folders_first()
    {
        var project = FileTree.Build(Dated, FileTree.Newest(Times))[1];

        Assert.Equal(["levels", "b.oscn", "c.oscn", "a.oscn"], Names(project.Children));
        Assert.Equal(["y.oscn", "x.oscn"], Names(project.Children[0].Children));
    }

    [Fact]
    public void Newest_first_orders_a_flat_match_list_across_folders()
    {
        var project = FileTree.Matches(Dated, "oscn", FileTree.Newest(Times))[1];

        Assert.Equal(["b.oscn", "c.oscn", "levels/y.oscn", "a.oscn", "levels/x.oscn"], Names(project.Children));
    }

    [Fact]
    public void A_file_with_no_time_is_the_oldest()
    {
        var times = new Dictionary<string, DateTime> { ["res://b.oscn"] = Day(3) };

        var project = FileTree.Build(["res://a.oscn", "res://b.oscn", "res://c.oscn"], FileTree.Newest(times))[1];

        Assert.Equal(["b.oscn", "a.oscn", "c.oscn"], Names(project.Children));
    }

    [Fact]
    public void The_tree_is_the_favorites_row_over_the_project_folder()
    {
        var roots = FileTree.Build(Scenes);

        Assert.Equal([FileKind.Favorites, FileKind.Folder], roots.Select(node => node.Kind));
        Assert.Equal(["Favorites", "res://"], Names(roots));
        Assert.Empty(roots[0].Children);
        Assert.Equal("res://", roots[1].Path);
    }

    [Fact]
    public void Folders_come_before_files_and_each_group_is_in_name_order()
    {
        var project = FileTree.Build(Scenes)[1];

        Assert.Equal(["levels", "Zoo", "main.oscn", "Zed.oscn"], Names(project.Children));
        Assert.Equal([FileKind.Folder, FileKind.Folder, FileKind.Scene, FileKind.Scene], project.Children.Select(node => node.Kind));
    }

    [Fact]
    public void A_folder_with_no_scene_still_has_a_row_with_nothing_under_it()
    {
        var project = FileTree.Build(["res://scripts/", "res://main.oscn"])[1];

        Assert.Equal(["scripts", "main.oscn"], Names(project.Children));
        Assert.Equal(FileKind.Folder, project.Children[0].Kind);
        Assert.Empty(project.Children[0].Children);
    }

    [Fact]
    public void A_filter_matches_scenes_and_never_a_folder()
    {
        var roots = FileTree.Matches(["res://scripts/", "res://scripts/run.oscn"], "script");

        Assert.Equal(["scripts/run.oscn"], Names(roots[1].Children));
    }

    [Fact]
    public void A_folder_holds_its_scenes_and_its_own_folders()
    {
        var levels = FileTree.Build(Scenes)[1].Children[0];

        Assert.Equal("res://levels/", levels.Path);
        Assert.Equal(["deep", "a.oscn", "b.oscn"], Names(levels.Children));
        Assert.Equal("res://levels/deep/c.oscn", levels.Children[0].Children.Single().Path);
    }

    [Fact]
    public void A_scene_row_carries_the_path_to_open()
    {
        var main = FileTree.Build(Scenes)[1].Children.Single(node => node.Name == "main.oscn");

        Assert.Equal("res://main.oscn", main.Path);
        Assert.Empty(main.Children);
    }

    [Fact]
    public void No_scenes_leave_an_empty_project_folder()
    {
        var project = FileTree.Build([])[1];

        Assert.Empty(project.Children);
    }

    [Fact]
    public void A_path_outside_res_is_not_a_project_file()
    {
        var project = FileTree.Build(["user://save.oscn", "res://ok.oscn"])[1];

        Assert.Equal(["ok.oscn"], Names(project.Children));
    }

    [Fact]
    public void Matches_lists_the_scenes_with_the_text_in_their_path_without_folders()
    {
        var roots = FileTree.Matches(Scenes, "LEVELS/");

        Assert.Equal(["Favorites", "res://"], Names(roots));
        Assert.Equal(["levels/a.oscn", "levels/b.oscn", "levels/deep/c.oscn"], Names(roots[1].Children));
        Assert.All(roots[1].Children, node => Assert.Equal(FileKind.Scene, node.Kind));
        Assert.Equal("res://levels/a.oscn", roots[1].Children[0].Path);
    }

    [Fact]
    public void Matches_are_in_name_order_ignoring_case()
    {
        var roots = FileTree.Matches(Scenes, "oscn");

        Assert.Equal(["levels/a.oscn", "levels/b.oscn", "levels/deep/c.oscn", "main.oscn", "Zed.oscn", "Zoo/z.oscn"], Names(roots[1].Children));
    }

    [Fact]
    public void Matches_with_nothing_found_leaves_the_two_rows()
    {
        var roots = FileTree.Matches(Scenes, "zzz");

        Assert.Empty(roots[1].Children);
    }
}
