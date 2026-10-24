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
}
