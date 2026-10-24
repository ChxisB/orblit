using System.Runtime.InteropServices;
using Orblit.Interop;

namespace Orblit.Filament;

/// <summary>
/// The C ABI of <c>orblit_renderer.h</c>, declared and nothing else. Handles
/// are <see cref="nint"/>.
/// </summary>
internal static partial class NativeMethods
{
    internal const string Library = "orblit_filament";

    static NativeMethods() => NativeLoader.Register(typeof(NativeMethods).Assembly, Library);

    /// <summary>Which graphics API the renderer draws with.</summary>
    internal enum Backend
    {
        Default = 0,
        Metal = 1,
        Vulkan = 2,
        OpenGl = 3,
        WebGpu = 4,
    }

    [LibraryImport(Library, EntryPoint = "orblit_renderer_create")]
    internal static partial nint Create(Backend backend, nint surface, uint width, uint height);

    [LibraryImport(Library, EntryPoint = "orblit_renderer_destroy")]
    internal static partial void Destroy(nint renderer);

    [LibraryImport(Library, EntryPoint = "orblit_renderer_draw")]
    internal static partial int Draw(nint renderer, double seconds);

    [LibraryImport(Library, EntryPoint = "orblit_renderer_rendered_frames")]
    internal static partial ulong RenderedFrames(nint renderer);

    // F2: the ABI's own signature.
    [LibraryImport(Library, EntryPoint = "orblit_renderer_set_camera")]
    internal static partial int SetCamera(
        nint renderer,
        ReadOnlySpan<float> position,
        ReadOnlySpan<float> target,
        float fieldOfView,
        int orthographic,
        float viewHeight,
        double at);

    [LibraryImport(Library, EntryPoint = "orblit_renderer_request_capture")]
    internal static partial int RequestCapture(nint renderer);

    // F2: the ABI's own signature.
    [LibraryImport(Library, EntryPoint = "orblit_renderer_read_capture")]
    internal static partial nuint ReadCapture(
        nint renderer,
        Span<byte> rgba,
        nuint capacity,
        out uint width,
        out uint height);
}
