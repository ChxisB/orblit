using System.Runtime.CompilerServices;
using Orblit.Interop;

namespace Orblit.Filament.Avalonia.Tests;

/// <summary>What is built on this machine. A checkout without a Filament release has no renderer library.</summary>
internal static class Libraries
{
    internal const string RendererSkip =
        "The renderer library is not built. Set ORBLIT_FILAMENT_SDK and run tool/build_native.sh.";

    internal const string AppleSkip = "The Apple helper library is built on macOS only.";

    internal static bool Renderer { get; } = Present("orblit_filament");

    internal static bool Apple { get; } = OperatingSystem.IsMacOS() && Present("orblit_filament_apple");

    private static bool Present(string library) =>
        NativeLoader.SearchPaths(library, AppContext.BaseDirectory).Any(File.Exists);
}

/// <summary>A test that draws, skipped where the renderer library is not built.</summary>
public sealed class RendererFactAttribute : FactAttribute
{
    public RendererFactAttribute([CallerFilePath] string? sourceFilePath = null, [CallerLineNumber] int sourceLineNumber = -1)
        : base(sourceFilePath, sourceLineNumber)
    {
        if (!Libraries.Renderer)
        {
            Skip = Libraries.RendererSkip;
        }
    }
}

/// <summary>A test that needs the Apple helper library, skipped everywhere else.</summary>
public sealed class AppleFactAttribute : FactAttribute
{
    public AppleFactAttribute([CallerFilePath] string? sourceFilePath = null, [CallerLineNumber] int sourceLineNumber = -1)
        : base(sourceFilePath, sourceLineNumber)
    {
        if (!Libraries.Apple)
        {
            Skip = Libraries.AppleSkip;
        }
    }
}
