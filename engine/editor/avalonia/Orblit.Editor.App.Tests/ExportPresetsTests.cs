namespace Orblit.Editor.App.Tests;

public sealed class ExportPresetsTests : IDisposable
{
    readonly string _project = Path.Combine(Path.GetTempPath(), $"orblit_presets_{Guid.NewGuid():N}");

    public ExportPresetsTests() => Directory.CreateDirectory(_project);

    public void Dispose() => Directory.Delete(_project, true);

    string Read(string file) => File.ReadAllText(Path.Combine(_project, file));

    void Write(string file, string text) => File.WriteAllText(Path.Combine(_project, file), text);

    [Fact]
    public void A_project_with_no_presets_file_has_no_presets()
    {
        Assert.Empty(ExportPresets.Read(_project));
    }

    [Fact]
    public void Each_preset_section_gives_its_name_its_platform_and_its_full_export_path()
    {
        Write("export_presets.cfg", """
            [preset.0]

            name="Desktop"
            platform="Linux"
            export_path="build/game.x86_64"

            [preset.0.options]

            binary_format/architecture="x86_64"

            [preset.1]

            name="Mac"
            platform="macOS"
            export_path="/builds/Game.app"

            [preset.1.options]

            application/bundle_identifier="com.example.game"

            """);

        Assert.Equal(
            [new ExportPreset("Desktop", "Linux", Path.Combine(_project, "build", "game.x86_64")), new ExportPreset("Mac", "macOS", Path.GetFullPath("/builds/Game.app"))],
            ExportPresets.Read(_project));
    }

    [Fact]
    public void A_preset_for_a_mac_names_its_bundle_and_turns_on_the_textures_a_mac_build_needs()
    {
        Write("project.orblit", "config_version=5\n\n[application]\n\nconfig/name=\"Cs Game!\"\n");

        ExportPresets.Add(_project, ("macOS", ".app", "universal"));

        Assert.Equal([new ExportPreset("macOS", "macOS", Path.Combine(_project, "build", "Cs Game.app"))], ExportPresets.Read(_project));
        Assert.Contains("[preset.0.options]\n\nbinary_format/architecture=\"universal\"\napplication/bundle_identifier=\"com.example.csgame\"\n", Read("export_presets.cfg"));
        Assert.EndsWith("\n[rendering]\n\ntextures/vram_compression/import_etc2_astc=true\n", Read("project.orblit"));
    }

    [Fact]
    public void A_second_preset_takes_the_next_number_and_a_linux_one_leaves_the_settings_alone()
    {
        const string settings = "config_version=5\n";
        Write("project.orblit", settings);
        Write("export_presets.cfg", "[preset.0]\n\nname=\"Mac\"\nplatform=\"macOS\"\nexport_path=\"\"\n\n[preset.0.options]\n\nbinary_format/architecture=\"universal\"\n");

        ExportPresets.Add(_project, ("Linux", ".x86_64", "x86_64"));

        Assert.Equal(new ExportPreset("Linux", "Linux", Path.Combine(_project, "build", "Game.x86_64")), ExportPresets.Read(_project)[1]);
        var added = Read("export_presets.cfg").Split("[preset.1]")[1];
        Assert.Contains("[preset.1.options]\n\nbinary_format/architecture=\"x86_64\"\n", added);
        Assert.DoesNotContain("bundle_identifier", added);
        Assert.Equal(settings, Read("project.orblit"));
    }

    [Fact]
    public void A_preset_with_no_export_path_keeps_it_empty()
    {
        Write("export_presets.cfg", "[preset.0]\n\nname=\"Mac\"\nplatform=\"macOS\"\nexport_path=\"\"\n");

        Assert.Equal("", ExportPresets.Read(_project)[0].Path);
    }
}
