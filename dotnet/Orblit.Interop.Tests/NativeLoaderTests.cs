using System.Text.RegularExpressions;

namespace Orblit.Interop.Tests;

public sealed partial class NativeLoaderTests : IDisposable
{
    private readonly string _empty = Directory.CreateTempSubdirectory("orblit-loader").FullName;

    public void Dispose() => Directory.Delete(_empty, recursive: true);

    [GeneratedRegex("^(win|osx|linux)-(x64|arm64)$")]
    private static partial Regex RuntimeIdPattern();

    [Fact]
    public void TheRuntimeIdIsOneTheRuntimesFolderUses() =>
        Assert.Matches(RuntimeIdPattern(), NativeLoader.RuntimeId);

    [Fact]
    public void TheRuntimesFolderIsTriedBeforeTheAppFolder()
    {
        var paths = NativeLoader.SearchPaths("orblit_core", _empty);
        Assert.Equal(2, paths.Count);
        Assert.Equal(Path.Combine(_empty, "runtimes", NativeLoader.RuntimeId, "native", NativeLoader.FileName("orblit_core")), paths[0]);
        Assert.Equal(Path.Combine(_empty, NativeLoader.FileName("orblit_core")), paths[1]);
    }

    [Fact]
    public void LoadsALibraryFromTheRuntimesFolder() =>
        Assert.NotEqual(nint.Zero, NativeLoader.Load("orblit_core", AppContext.BaseDirectory));

    [Fact]
    public void AMissingLibraryNamesEveryPathTried()
    {
        var error = Assert.Throws<DllNotFoundException>(() => NativeLoader.Load("orblit_core", _empty));
        foreach (var path in NativeLoader.SearchPaths("orblit_core", _empty))
        {
            Assert.Contains(path + " (not found)", error.Message);
        }
    }

    [Fact]
    public void AFileThatWillNotLoadIsReportedAsFoundButBroken()
    {
        var path = NativeLoader.SearchPaths("orblit_core", _empty)[1];
        File.WriteAllText(path, "this is not a library");

        var error = Assert.Throws<DllNotFoundException>(() => NativeLoader.Load("orblit_core", _empty));
        Assert.Contains(path + " (found, but it would not load)", error.Message);
    }
}
