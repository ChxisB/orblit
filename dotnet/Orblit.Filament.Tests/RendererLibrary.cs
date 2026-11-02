using System.Runtime.CompilerServices;
using Orblit.Interop;

namespace Orblit.Filament.Tests;

/// <summary>
/// The renderer needs a Filament release to build, so a checkout without one
/// has no library. The tests that draw say so and skip, and the gate that runs
/// them sets <c>ORBLIT_FILAMENT_SDK</c>. A leg that builds the renderer also sets
/// <c>ORBLIT_REQUIRE_RENDERER=1</c>, and for that leg a missing library is a failure.
/// </summary>
public static class RendererLibrary
{
    public const string RequireVariable = "ORBLIT_REQUIRE_RENDERER";

    public const string SkipReason =
        "The renderer library is not built. Set ORBLIT_FILAMENT_SDK and run tool/build_native.sh.";

    public static bool Required { get; } = Environment.GetEnvironmentVariable(RequireVariable) == "1";

    internal static IReadOnlyList<string> Searched { get; } =
        NativeLoader.SearchPaths(NativeMethods.Library, AppContext.BaseDirectory);

    public static bool Present { get; } = Searched.Any(File.Exists);

    /// <summary>
    /// What to tell a leg that needs the library and has none, or null when nothing is wrong.
    /// Naming the paths saves a look through the build output for where it went.
    /// </summary>
    internal static string? UnmetRequirement(bool required, IReadOnlyList<string> searched) =>
        required && !searched.Any(File.Exists)
            ? $"{RequireVariable} is set, but the renderer library was not found. Looked in: {string.Join(", ", searched)}. Run tool/build_native.sh."
            : null;

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
