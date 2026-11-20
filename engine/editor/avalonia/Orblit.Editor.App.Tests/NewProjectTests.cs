using Orblit.Editor.App.Launcher;

namespace Orblit.Editor.App.Tests;

public sealed class NewProjectTests : IDisposable
{
    readonly string _folder = Path.Combine(Path.GetTempPath(), $"orblit_new_{Guid.NewGuid():N}", "space-ranger");

    public void Dispose()
    {
        var parent = Path.GetDirectoryName(_folder)!;
        if (Directory.Exists(parent))
        {
            Directory.Delete(parent, true);
        }
    }

    string Read(string file) => File.ReadAllText(Path.Combine(_folder, file));

    string[] Made() => [.. Directory.GetFiles(_folder).Select(Path.GetFileName).Order(StringComparer.Ordinal).OfType<string>()];

    [Fact]
    public void A_3D_project_with_CSharp_writes_settings_a_script_a_csproj_and_a_lit_scene()
    {
        new NewProject("Space \"Ranger\"", _folder) { Template = ProjectTemplate.Spatial, Script = FirstScript.Csharp }.Make(_ => { });

        Assert.Equal(["Main.cs", "SpaceRanger.csproj", "main.oscn", "project.orblit"], Made());
        Assert.Equal(
            "config_version=5\n\n[application]\n\nconfig/name=\"Space \\\"Ranger\\\"\"\nconfig/tags=PackedStringArray(\"3D\", \"C#\")\n"
            + "run/main_scene=\"res://main.oscn\"\nconfig/features=PackedStringArray(\"1.0\", \"C#\")\n\n[dotnet]\n\nproject/assembly_name=\"SpaceRanger\"\n",
            Read("project.orblit"));
        Assert.Contains("public partial class Main : Entity3D", Read("Main.cs"));
        Assert.Contains("<Project Sdk=\"Orblit.NET.Sdk/1.0.0\">", Read("SpaceRanger.csproj"));
        var scene = Read("main.oscn");
        Assert.StartsWith("[orb_scene format=3]\n\n[ext_resource type=\"Script\" path=\"res://Main.cs\" id=\"1\"]\n\n[sub_resource type=\"PlaneMesh\"", scene);
        Assert.Contains("[node name=\"Main\" type=\"Entity3D\"]\nscript = ExtResource(\"1\")\n\n[node name=\"Camera\" type=\"Camera3D\" parent=\".\"]", scene);
        Assert.Contains("[node name=\"Sun\" type=\"DirectionalLight3D\" parent=\".\"]", scene);
        Assert.EndsWith("[node name=\"Ground\" type=\"MeshInstance3D\" parent=\".\"]\nmesh = SubResource(\"PlaneMesh_ground\")\n", scene);
    }

    [Fact]
    public void A_2D_project_with_a_graph_writes_the_graph_and_a_sprite_scene()
    {
        new NewProject("Runner", _folder) { Template = ProjectTemplate.Planar, Script = FirstScript.Graph }.Make(_ => { });

        Assert.Equal(["main.ograph", "main.oscn", "project.orblit"], Made());
        Assert.Equal("[orb_resource type=\"GraphScript\" format=3]\n\n[resource]\nbase_type = &\"Entity2D\"\n", Read("main.ograph"));
        Assert.Contains("config/tags=PackedStringArray(\"2D\", \"Graph\")\n", Read("project.orblit"));
        Assert.DoesNotContain("[dotnet]", Read("project.orblit"));
        var scene = Read("main.oscn");
        Assert.Contains("[ext_resource type=\"Script\" path=\"res://main.ograph\" id=\"1\"]", scene);
        Assert.Contains("[node name=\"Main\" type=\"Entity2D\"]\nscript = ExtResource(\"1\")\n", scene);
        Assert.EndsWith("[node name=\"Sprite\" type=\"Sprite2D\" parent=\".\"]\ntexture = SubResource(\"GradientTexture2D_sprite\")\n", scene);
    }

    [Fact]
    public void An_empty_project_with_no_script_writes_only_the_settings_with_no_main_scene_or_tags()
    {
        new NewProject("Blank", _folder) { Template = ProjectTemplate.Empty, Script = FirstScript.None }.Make(_ => { });

        Assert.Equal(["project.orblit"], Made());
        Assert.Equal("config_version=5\n\n[application]\n\nconfig/name=\"Blank\"\nconfig/features=PackedStringArray(\"1.0\", \"C#\")\n", Read("project.orblit"));
    }

    [Fact]
    public void Git_writes_the_ignore_files_and_starts_the_repository_in_the_folder()
    {
        var started = new List<string>();

        new NewProject("Crate", _folder) { Template = ProjectTemplate.Empty, Script = FirstScript.None, Git = true }.Make(started.Add);

        Assert.Equal([_folder], started);
        Assert.Contains(".orblit/\n", File.ReadAllText(Path.Combine(_folder, ".gitignore")));
        Assert.Contains("* text=auto eol=lf\n", File.ReadAllText(Path.Combine(_folder, ".gitattributes")));
    }

    [Fact]
    public void A_Git_failure_leaves_no_folder_so_it_can_be_tried_again()
    {
        var project = new NewProject("Crate", _folder) { Git = true };

        Assert.Throws<IOException>(() => project.Make(_ => throw new IOException("no git")));

        Assert.False(Directory.Exists(_folder));
        project.Make(_ => { });
        Assert.Contains("project.orblit", Made());
    }

    [Fact]
    public void A_write_that_fails_after_Git_leaves_no_folder_behind()
    {
        var project = new NewProject("Crate", _folder) { Git = true };

        // A folder where project.orblit goes makes the first write fail after Git has run.
        Assert.Throws<UnauthorizedAccessException>(() => project.Make(folder =>
        {
            Directory.CreateDirectory(Path.Combine(folder, ".git"));
            Directory.CreateDirectory(Path.Combine(folder, "project.orblit"));
        }));

        Assert.False(Directory.Exists(_folder));
    }

    [Fact]
    public void A_write_that_fails_in_an_empty_folder_keeps_the_folder_and_empties_it()
    {
        Directory.CreateDirectory(_folder);
        var project = new NewProject("Crate", _folder) { Git = true };

        Assert.Throws<UnauthorizedAccessException>(() => project.Make(folder => Directory.CreateDirectory(Path.Combine(folder, "project.orblit"))));

        Assert.True(Directory.Exists(_folder));
        Assert.Empty(Directory.EnumerateFileSystemEntries(_folder));
    }

    [Fact]
    public void A_folder_that_holds_anything_is_refused_and_left_as_it_was()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(Path.Combine(_folder, "keep.txt"), "mine");
        var started = 0;

        Assert.Throws<IOException>(() => new NewProject("Crate", _folder) { Git = true }.Make(_ => started++));

        Assert.Equal(["keep.txt"], Made());
        Assert.Equal(0, started);
    }

    [Theory]
    [InlineData("", "/games/a")]
    [InlineData("  ", "/games/a")]
    [InlineData("Crate", "games/a")]
    [InlineData("Crate", "~/games/a")]
    public void A_blank_name_or_a_folder_that_is_not_a_full_path_is_refused(string name, string folder)
    {
        Assert.Throws<ArgumentException>(() => new NewProject(name, folder));
    }

    [Theory]
    [InlineData("space ranger", "SpaceRanger")]
    [InlineData("2nd try", "Game2ndTry")]
    [InlineData("???", "Game")]
    public void The_assembly_name_is_the_name_in_pascal_case_led_by_a_letter(string name, string assembly)
    {
        Assert.Equal(assembly, new NewProject(name, "/games/a").Assembly);
    }
}
