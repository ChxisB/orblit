using Orblit.Editor.App;

namespace Orblit.Editor.App.Tests;

public sealed class ImportFileTests
{
    internal const string Rock = """
        [remap]

        importer="scene"
        type="PackedScene"
        metadata={
        "imported": true
        }

        [deps]

        source_file="res://rock.glb"

        [params]

        nodes/root_name=""
        meshes/ensure_tangents=true
        meshes/light_baking=1
        animation/fps=30.0
        animation/import=true
        process/channel_remap/red=0
        _subresources={
        "meshes": {}
        }
        """;

    [Fact]
    public void The_options_are_the_params_lines_with_a_group_a_kind_and_a_value()
    {
        var options = new ImportFile(Rock).Options();

        Assert.Equal(["nodes/root_name", "meshes/ensure_tangents", "meshes/light_baking", "animation/fps", "animation/import", "process/channel_remap/red"], options.Select(option => option.Key));
        Assert.Equal(["Nodes", "Meshes", "Meshes", "Animation", "Animation", "Process"], options.Select(option => option.Property.Group));
        Assert.Equal("channel_remap_red", options[^1].Property.Name);
        Assert.Equal<object?>(["", true, 1L, 30.0, true, 0L], options.Select(option => option.Property.Value));
        Assert.Equal([4, 1, 2, 3, 1, 2], options.Select(option => option.Property.Type));
    }

    [Fact]
    public void The_importer_names_the_kind_of_file()
    {
        Assert.Equal("Scene", new ImportFile(Rock).Importer);
        Assert.Equal("", new ImportFile("[params]\na/b=1").Importer);
    }

    [Fact]
    public void A_change_rewrites_one_line_and_keeps_the_rest()
    {
        var file = new ImportFile(Rock).With("meshes/ensure_tangents", false).With("animation/fps", 24.0).With("nodes/root_name", "Rock \"big\"");

        var expected = Rock.Replace("ensure_tangents=true", "ensure_tangents=false", StringComparison.Ordinal)
            .Replace("fps=30.0", "fps=24.0", StringComparison.Ordinal)
            .Replace("root_name=\"\"", "root_name=\"Rock \\\"big\\\"\"", StringComparison.Ordinal);
        Assert.Equal(expected, file.Text);
        Assert.Equal(0.25, new ImportFile(file.With("animation/fps", 0.25).Text).Options().Single(option => option.Key == "animation/fps").Property.Value);
    }

    [Fact]
    public void A_value_that_is_not_one_of_the_shown_kinds_reads_as_text_and_a_key_outside_params_is_not_an_option()
    {
        var options = new ImportFile("[remap]\nimporter=\"texture\"\n[params]\nsizes=[1, 2]\nplain=on").Options();

        Assert.Equal([("sizes", "[1, 2]"), ("plain", "on")], options.Select(option => (option.Key, (string)option.Property.Value!)));
        Assert.All(options, option => Assert.Equal(0, option.Property.Type));
        Assert.Equal("General", options[0].Property.Group);
    }
}
