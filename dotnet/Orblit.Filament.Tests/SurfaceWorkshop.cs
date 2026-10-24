using System.Runtime.CompilerServices;
using Orblit.Asset;

namespace Orblit.Filament.Tests;

/// <summary>
/// Where the cooking tool for surfaces is. The draw tests cook the surfaces they draw with, so a
/// machine without Filament's <c>matc</c> skips them and says why.
/// </summary>
internal static class Matc
{
    internal const string SkipReason =
        "matc was not found. Set ORBLIT_MATC, or set ORBLIT_FILAMENT_SDK to a Filament release. "
        + "The surfaces are cooked for Metal, so this runs on a Mac.";

    internal static string? Location { get; } = Find();

    private static string? Find()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return null;
        }

        var named = Environment.GetEnvironmentVariable("ORBLIT_MATC");
        var release = Environment.GetEnvironmentVariable("ORBLIT_FILAMENT_SDK");
        var found = string.IsNullOrEmpty(named) && !string.IsNullOrEmpty(release) ? Path.Combine(release, "bin", "matc") : named;
        return found is not null && File.Exists(found) ? found : null;
    }
}

/// <summary>A test that draws with a surface it cooks, skipped where the renderer or matc is missing.</summary>
public sealed class MatcFactAttribute : FactAttribute
{
    public MatcFactAttribute([CallerFilePath] string? sourceFilePath = null, [CallerLineNumber] int sourceLineNumber = -1)
        : base(sourceFilePath, sourceLineNumber)
    {
        if (!RendererLibrary.Present)
        {
            Skip = RendererLibrary.SkipReason;
        }
        else if (Matc.Location is null)
        {
            Skip = Matc.SkipReason;
        }
    }
}

/// <summary>
/// The cooked surfaces the tests draw with, made once for a class. Two come from materials written
/// for Filament, and two from surface sources that go through the same cook a project's own do.
/// The files a test writes live in a folder that goes with the fixture.
/// </summary>
public sealed class SurfaceWorkshop : IAsyncLifetime
{
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("orblit_surfaces_");

    /// <summary>A tinted, scaled, textured unlit material.</summary>
    internal byte[] Good { get; private set; } = [];

    /// <summary>The same material at half the brightness.</summary>
    internal byte[] Dim { get; private set; } = [];

    /// <summary>The built-in unlit surface, written as a surface source.</summary>
    internal byte[] Unlit { get; private set; } = [];

    /// <summary>A tint that breathes with time.</summary>
    internal byte[] Pulse { get; private set; } = [];

    public async ValueTask InitializeAsync()
    {
        if (Matc.Location is not { } matc)
        {
            return;
        }

        var tool = new NativeTool("matc", matc);
        Good = await CookMaterial(tool, "custom_surface.mat");
        Dim = await CookMaterial(tool, "custom_surface_dim.mat");
        var importer = new MaterialImporter(tool);
        Unlit = await CookSurface(importer, "unlit.oshader");
        Pulse = await CookSurface(importer, "pulse.oshader");
    }

    public ValueTask DisposeAsync()
    {
        _directory.Delete(recursive: true);
        return ValueTask.CompletedTask;
    }

    /// <summary>The path of a file holding <paramref name="bytes"/>, which a test may overwrite as an editor does on save.</summary>
    internal string Place(string name, byte[] bytes)
    {
        var path = Path.Combine(_directory.FullName, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private static string Source(string name) => Path.Combine(AppContext.BaseDirectory, "surfaces", name);

    private async Task<byte[]> CookMaterial(NativeTool tool, string name)
    {
        var output = Path.Combine(_directory.FullName, name + ".filamat");
        await tool.RunAsync(["-a", "metal", "-p", "all", "-o", output, Source(name)], AssetId.Parse("surfaces/" + name));
        return await File.ReadAllBytesAsync(output);
    }

    private static async Task<byte[]> CookSurface(MaterialImporter importer, string name)
    {
        var id = AssetId.Parse("surfaces/" + name);
        var bytes = await File.ReadAllBytesAsync(Source(name));
        var result = await importer.ImportAsync(
            new ImportRequest
            {
                Id = id,
                Bytes = bytes,
                Settings = new Dictionary<string, object?>(),
                Target = CookTargets.Macos,
                Source = new MemoryAssetSource(new Dictionary<AssetId, byte[]> { [id] = bytes }),
            });
        return result.Outputs["material.filamat"];
    }
}
