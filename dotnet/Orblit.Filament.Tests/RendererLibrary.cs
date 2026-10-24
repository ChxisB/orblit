using System.Runtime.CompilerServices;
using Orblit.Interop;

namespace Orblit.Filament.Tests;

/// <summary>
/// The renderer needs a Filament release to build, so a checkout without one
/// has no library. The tests that draw say so and skip, and the gate that runs
/// them sets <c>ORBLIT_FILAMENT_SDK</c>.
/// </summary>
public static class RendererLibrary
{
    public const string SkipReason =
        "The renderer library is not built. Set ORBLIT_FILAMENT_SDK and run tool/build_native.sh.";

    public static bool Present { get; } =
        NativeLoader.SearchPaths(NativeMethods.Library, AppContext.BaseDirectory).Any(File.Exists);

    public const string AppleSkipReason =
        "Feeds take pixel buffers on macOS only, and the renderer library must be built there.";

    // A feed is a CoreVideo pixel buffer, which only Apple has.
    public static bool ApplePresent { get; } = OperatingSystem.IsMacOS() && Present;
}

/// <summary>A test that needs the renderer library, skipped where it is not built.</summary>
public sealed class RendererFactAttribute : FactAttribute
{
    public RendererFactAttribute([CallerFilePath] string? sourceFilePath = null, [CallerLineNumber] int sourceLineNumber = -1)
        : base(sourceFilePath, sourceLineNumber)
    {
        if (!RendererLibrary.Present)
        {
            Skip = RendererLibrary.SkipReason;
        }
    }
}
