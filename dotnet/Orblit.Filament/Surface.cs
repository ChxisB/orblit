using System.Runtime.InteropServices;

namespace Orblit.Filament;

/// <summary>Where the frames of a <see cref="Renderer"/> go.</summary>
public enum SurfaceKind
{
    /// <summary>Nowhere anybody sees. Frames are read back with <see cref="Renderer.RequestCapture"/>.</summary>
    Headless = 0,

    /// <summary>A native window, in <see cref="Surface.Window"/>: whatever Filament takes on that platform.</summary>
    Window = 1,

    /// <summary>The platform's texture-sharing surface, where it has one. Apple only.</summary>
    Platform = 2,
}

/// <summary>A surface and the native window it needs, if any. The layout is the C struct's.</summary>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct Surface(SurfaceKind Kind, nint Window)
{
    public static Surface Headless => default;
}
