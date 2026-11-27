using System.Globalization;
using System.Text;

namespace Orblit.Editor.App.Launcher;

internal enum ProjectTemplate
{
    Spatial,
    Planar,
    Empty,
}

internal enum FirstScript
{
    Csharp,
    Graph,
    None,
}

/// <summary>
/// A project the launcher is about to write: its settings, a main scene from the template, a first script on
/// the scene's root, and the files Git needs. The folder must be new or empty, so nothing is overwritten.
/// </summary>
internal sealed class NewProject
{
    public NewProject(string name, string folder)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Give the project a name.");
        }

        if (!Path.IsPathFullyQualified(folder))
        {
            throw new ArgumentException("Give the folder as a full path.");
        }

        Name = name.Trim();
        Folder = folder;
    }

    public string Name { get; }

    public string Folder { get; }

    public ProjectTemplate Template { get; init; }

    public FirstScript Script { get; init; }

    public bool Git { get; init; }

    /// <summary>The C# assembly name: the name's letters and digits in PascalCase, and never empty or led by a digit.</summary>
    public string Assembly
    {
        get
        {
            var joined = new StringBuilder();
            var wordStart = true;
            foreach (var c in Name)
            {
                if (char.IsAsciiLetterOrDigit(c))
                {
                    joined.Append(wordStart ? char.ToUpperInvariant(c) : c);
                }

                wordStart = !char.IsAsciiLetterOrDigit(c);
            }

            return joined.Length == 0 || char.IsAsciiDigit(joined[0]) ? "Game" + joined : joined.ToString();
        }
    }

    /// <summary>
    /// Writes the project. Throws <see cref="IOException"/> when the folder already holds anything. A failure
    /// takes back what was made: a folder this made is removed and an empty one is emptied again.
    /// </summary>
    public void Make(Action<string> initGit)
    {
        var existed = Directory.Exists(Folder);
        if (existed && Directory.EnumerateFileSystemEntries(Folder).Any())
        {
            throw new IOException($"{Folder} is not empty. Choose a new folder.");
        }

        Directory.CreateDirectory(Folder);
        try
        {
            if (Git)
            {
                initGit(Folder);
            }

            foreach (var (file, text) in Files())
            {
                File.WriteAllText(Path.Combine(Folder, file), text);
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            TakeBack(existed);
            throw;
        }
    }

    // The folder held nothing before Make, so everything in it now is Make's own.
    void TakeBack(bool existed)
    {
        if (!existed)
        {
            Directory.Delete(Folder, true);
            return;
        }

        foreach (var entry in new DirectoryInfo(Folder).EnumerateFileSystemInfos())
        {
            if (entry is DirectoryInfo folder)
            {
                folder.Delete(true);
            }
            else
            {
                entry.Delete();
            }
        }
    }

    IEnumerable<(string File, string Text)> Files()
    {
        yield return ("project.orblit", Settings());
        if (Script == FirstScript.Csharp)
        {
            yield return ("Main.cs", $"using Orblit;\n\npublic partial class Main : {RootType}\n{{\n    public override void OnStart() => Orb.Print(\"Main started\");\n}}\n");
            yield return ($"{Assembly}.csproj", "<Project Sdk=\"Orblit.NET.Sdk/1.4.0\">\n  <PropertyGroup>\n    <TargetFramework>net10.0</TargetFramework>\n    <EnableDynamicLoading>true</EnableDynamicLoading>\n  </PropertyGroup>\n</Project>\n");
            // The engine's C# export builds the solution, and refuses a project without one.
            yield return ($"{Assembly}.slnx", $"<Solution>\n  <Project Path=\"{Assembly}.csproj\" />\n</Solution>\n");
        }

        if (Script == FirstScript.Graph)
        {
            // The text the engine writes for an empty graph, less the uid it adds on first import.
            yield return ("main.ograph", $"[orb_resource type=\"GraphScript\" format=3]\n\n[resource]\nbase_type = &\"{RootType}\"\n");
        }

        if (Template != ProjectTemplate.Empty)
        {
            yield return ("main.oscn", Scene());
        }

        if (Git)
        {
            yield return (".gitignore", "# Orblit specific ignores\n.orblit/\n/android/\n");
            yield return (".gitattributes", "# Normalize EOL for all files that Git considers text files.\n* text=auto eol=lf\n");
        }
    }

    string RootType => Template switch
    {
        ProjectTemplate.Spatial => "Entity3D",
        ProjectTemplate.Planar => "Entity2D",
        ProjectTemplate.Empty => "Entity",
        _ => throw new InvalidOperationException(Template.ToString()),
    };

    string? ScriptPath => Script switch
    {
        FirstScript.Csharp => "res://Main.cs",
        FirstScript.Graph => "res://main.ograph",
        FirstScript.None => null,
        _ => throw new InvalidOperationException(Script.ToString()),
    };

    string Settings()
    {
        var text = new StringBuilder("config_version=5\n\n[application]\n\n");
        text.Append(CultureInfo.InvariantCulture, $"config/name=\"{Name.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"\n");
        var tags = Tags();
        if (tags.Count > 0)
        {
            text.Append(CultureInfo.InvariantCulture, $"config/tags=PackedStringArray({string.Join(", ", tags.Select(tag => $"\"{tag}\""))})\n");
        }

        if (Template != ProjectTemplate.Empty)
        {
            text.Append("run/main_scene=\"res://main.oscn\"\n");
        }

        text.Append("config/features=PackedStringArray(\"1.4\", \"C#\")\n");
        if (Script == FirstScript.Csharp)
        {
            text.Append(CultureInfo.InvariantCulture, $"\n[dotnet]\n\nproject/assembly_name=\"{Assembly}\"\n");
        }

        return text.ToString();
    }

    // The launcher's cards show these as chips.
    List<string> Tags()
    {
        string[] tags =
        [
            Template switch { ProjectTemplate.Spatial => "3D", ProjectTemplate.Planar => "2D", _ => "" },
            Script switch { FirstScript.Csharp => "C#", FirstScript.Graph => "Graph", _ => "" },
        ];
        return [.. tags.Where(tag => tag.Length > 0)];
    }

    string Scene()
    {
        var text = new StringBuilder("[orb_scene format=3]\n\n");
        if (ScriptPath is { } script)
        {
            text.Append(CultureInfo.InvariantCulture, $"[ext_resource type=\"Script\" path=\"{script}\" id=\"1\"]\n\n");
        }

        text.Append(Template == ProjectTemplate.Spatial ? SpatialResources : PlanarResources);
        text.Append(CultureInfo.InvariantCulture, $"[node name=\"Main\" type=\"{RootType}\"]\n");
        if (ScriptPath is not null)
        {
            text.Append("script = ExtResource(\"1\")\n");
        }

        text.Append(Template == ProjectTemplate.Spatial ? SpatialNodes : PlanarNodes);
        return text.ToString();
    }

    const string SpatialResources = """
        [sub_resource type="PlaneMesh" id="PlaneMesh_ground"]
        size = Vector2(20, 20)


        """;

    // The camera sits 4 up and 8 back looking at the origin. The sun leans 60 degrees down.
    const string SpatialNodes = """

        [node name="Camera" type="Camera3D" parent="."]
        transform = Transform3D(1, 0, 0, 0, 0.894427, 0.447214, 0, -0.447214, 0.894427, 0, 4, 8)

        [node name="Sun" type="DirectionalLight3D" parent="."]
        transform = Transform3D(1, 0, 0, 0, 0.5, 0.866025, 0, -0.866025, 0.5, 0, 6, 0)

        [node name="Ground" type="MeshInstance3D" parent="."]
        mesh = SubResource("PlaneMesh_ground")

        """;

    // The sprite is a 64 pixel square in the logo's two oranges, so the project needs no image file.
    const string PlanarResources = """
        [sub_resource type="Gradient" id="Gradient_sprite"]
        colors = PackedColorArray(1, 0.760784, 0.101961, 1, 1, 0.415686, 0, 1)

        [sub_resource type="GradientTexture2D" id="GradientTexture2D_sprite"]
        gradient = SubResource("Gradient_sprite")
        width = 64
        height = 64


        """;

    const string PlanarNodes = """

        [node name="Camera" type="Camera2D" parent="."]

        [node name="Sprite" type="Sprite2D" parent="."]
        texture = SubResource("GradientTexture2D_sprite")

        """;
}
