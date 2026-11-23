using Orblit.Editor.Bridge;

namespace Orblit.Editor.App.Tests;

public sealed class EngineFilesTests : IDisposable
{
    readonly string _folder = Directory.CreateTempSubdirectory("orblit-engine-files-").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    string Touch(string name)
    {
        var path = Path.Combine(_folder, name);
        File.WriteAllText(path, "");
        return path;
    }

    [Theory]
    [InlineData("liborblit.macos.editor.universal.dylib")]
    [InlineData("liborblit.linuxbsd.editor.x86_64.so")]
    [InlineData("orblit.windows.editor.x86_64.dll")]
    public void The_library_beside_the_app_is_found_among_the_apps_own_files(string library)
    {
        Touch("Orblit.Editor.App.dll");
        Touch("Orblit.Editor.Bridge.dll");
        Touch("orblit.windows.editor.x86_64.csharp.exe");
        var expected = Touch(library);

        Assert.Equal(expected, EngineLibrary.InFolder(_folder));
    }

    [Fact]
    public void A_folder_with_no_engine_library_has_none()
    {
        Touch("Orblit.Editor.App.dll");
        Touch("liborblit.macos.editor.universal.txt");

        Assert.Null(EngineLibrary.InFolder(_folder));
    }

    [Theory]
    [InlineData("liborblit.macos.editor.dev.arm64.dylib", "orblit.macos.editor.dev.arm64")]
    [InlineData("liborblit.linuxbsd.editor.x86_64.so", "orblit.linuxbsd.editor.x86_64")]
    [InlineData("orblit.windows.editor.x86_64.dll", "orblit.windows.editor.x86_64.exe")]
    public void The_player_of_a_plain_build_drops_the_lib_prefix_and_takes_the_programs_extension(string library, string player)
    {
        Assert.Equal(Path.Combine(_folder, player), EditorEngine.PlayerPath(Path.Combine(_folder, library)));
    }

    [Theory]
    [InlineData("liborblit.macos.editor.universal.dylib", "orblit.macos.editor.universal.csharp")]
    [InlineData("liborblit.linuxbsd.editor.x86_64.so", "orblit.linuxbsd.editor.x86_64.csharp")]
    [InlineData("orblit.windows.editor.x86_64.dll", "orblit.windows.editor.x86_64.csharp.exe")]
    public void The_player_of_a_csharp_build_is_the_program_tagged_csharp(string library, string player)
    {
        var expected = Touch(player);

        Assert.Equal(expected, EditorEngine.PlayerPath(Path.Combine(_folder, library)));
    }

    [Fact]
    public void The_program_built_last_plays_when_both_programs_are_there()
    {
        var library = Path.Combine(_folder, "liborblit.macos.editor.dev.arm64.dylib");
        var plain = Touch("orblit.macos.editor.dev.arm64");
        var csharp = Touch("orblit.macos.editor.dev.arm64.csharp");

        File.SetLastWriteTimeUtc(csharp, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        File.SetLastWriteTimeUtc(plain, new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc));
        Assert.Equal(plain, EditorEngine.PlayerPath(library));

        File.SetLastWriteTimeUtc(csharp, new DateTime(2026, 1, 3, 0, 0, 0, DateTimeKind.Utc));
        Assert.Equal(csharp, EditorEngine.PlayerPath(library));
    }
}
