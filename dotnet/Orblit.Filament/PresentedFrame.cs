using System.Runtime.InteropServices;

namespace Orblit.Filament;

/// <summary>
/// A frame a <see cref="SurfaceKind.Platform"/> renderer presented: a CoreVideo pixel buffer backed
/// by an IOSurface, which a host can hand to its compositor without reading a pixel. Apple only.
/// The buffer is held until this is disposed. The renderer rings four buffers, so a host should
/// hold fewer than four at a time.
/// </summary>
public sealed partial class PresentedFrame : IDisposable
{
    private const string CoreVideo = "/System/Library/Frameworks/CoreVideo.framework/CoreVideo";
    private const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
    private const string IOSurface = "/System/Library/Frameworks/IOSurface.framework/IOSurface";

    private nint _buffer;

    internal PresentedFrame(nint buffer)
    {
        _buffer = buffer;
        Width = (int)CVPixelBufferGetWidth(buffer);
        Height = (int)CVPixelBufferGetHeight(buffer);
        Surface = CVPixelBufferGetIOSurface(buffer);
        SurfaceId = Surface == nint.Zero ? 0 : IOSurfaceGetID(Surface);
    }

    public int Width { get; }

    public int Height { get; }

    /// <summary>The <c>IOSurfaceRef</c> under the buffer, or zero if the buffer has none. It lives as long as this does.</summary>
    public nint Surface { get; }

    /// <summary>The system's id for <see cref="Surface"/>. The same surface keeps its id, so a host can cache what it made from it.</summary>
    public uint SurfaceId { get; }

    public void Dispose()
    {
        if (_buffer == nint.Zero)
        {
            return;
        }

        CFRelease(_buffer);
        _buffer = nint.Zero;
    }

    [LibraryImport(CoreVideo)]
    private static partial nuint CVPixelBufferGetWidth(nint buffer);

    [LibraryImport(CoreVideo)]
    private static partial nuint CVPixelBufferGetHeight(nint buffer);

    [LibraryImport(CoreVideo)]
    private static partial nint CVPixelBufferGetIOSurface(nint buffer);

    [LibraryImport(IOSurface)]
    private static partial uint IOSurfaceGetID(nint surface);

    [LibraryImport(CoreFoundation)]
    private static partial void CFRelease(nint value);
}
