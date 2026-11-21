namespace Orblit.Editor.App.Tests;

public sealed class ExportArgumentsTests
{
    [Fact]
    public void An_export_runs_the_engine_headless_on_the_project_with_the_preset_the_path_and_a_log()
    {
        var arguments = EditorEngine.ExportArguments("/work/My Game", "macOS", "/work/My Game/build/Game.app", "/tmp/export.log");

        Assert.Equal(["--headless", "--path", "/work/My Game", "--export-release", "macOS", "/work/My Game/build/Game.app", "--log-file", "/tmp/export.log"], arguments);
    }

    [Fact]
    public void The_reason_is_the_error_before_the_failed_line_with_its_lines_up_to_the_line_that_names_the_source()
    {
        string[] log =
        [
            "Orblit Engine v1.0.stable.csharp",
            "ERROR: Required virtual method MultiplayerPeerExtension::_get_transfer_mode must be overridden before calling.",
            "   at: _orblit_virtual_print_required_error (core/object/orblit_virtual.gen.h:23)",
            "[   0% ] first_scan_filesystem | Started Project initialization (5 steps)",
            "ERROR: Cannot export project with preset \"macOS\" due to configuration errors:",
            "No export template found at the expected path:",
            "/data/export_templates/1.0.stable.csharp/macos.zip",
            "",
            "   at: _fs_changed (editor/editor_node.cpp:1443)",
            "ERROR: Project export for preset \"macOS\" failed.",
            "   at: _fs_changed (editor/editor_node.cpp:1459)",
            "ERROR: Parameter \"singleton\" is null.",
        ];

        Assert.Equal(
            "Cannot export project with preset \"macOS\" due to configuration errors: No export template found at the expected path: /data/export_templates/1.0.stable.csharp/macos.zip",
            EditorEngine.ExportReason(log));
    }

    [Fact]
    public void Without_a_failed_line_the_reason_is_the_first_error()
    {
        Assert.Equal("Invalid export preset name: Web.", EditorEngine.ExportReason(["Orblit Engine v1.0.stable", "ERROR: Invalid export preset name: Web.", "ERROR: Parameter \"singleton\" is null."]));
    }

    [Fact]
    public void A_log_with_no_error_gives_no_reason()
    {
        Assert.Null(EditorEngine.ExportReason(["Orblit Engine v1.0.stable.csharp", "WARNING: a warning"]));
    }
}
