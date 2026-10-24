using System.Runtime.InteropServices;

namespace Orblit.Filament.Tests;

/// <summary>
/// Makes CoreVideo pixel buffers for the tests of a feed. Apple only.
/// </summary>
internal static partial class PixelBuffers
{
    private const string CoreVideo = "/System/Library/Frameworks/CoreVideo.framework/CoreVideo";
    private const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

    // The four characters 'BGRA'.
    private const uint Bgra32 = 0x42475241;

    /// <summary>A buffer that the renderer can show with no copy.</summary>
    internal static nint CreateSharedWithGpu(uint width, uint height) => Create(width, height, backedByIoSurface: true);

    /// <summary>A buffer in the right format that is not backed by an IOSurface.</summary>
    internal static nint CreateInMemoryOnly(uint width, uint height) => Create(width, height, backedByIoSurface: false);

    internal static void Release(nint buffer) => CFRelease(buffer);

    private static nint Create(uint width, uint height, bool backedByIoSurface)
    {
        var attributes = backedByIoSurface ? IoSurfaceAttributes() : 0;
        try
        {
            var status = CVPixelBufferCreate(0, width, height, Bgra32, attributes, out var buffer);
            Assert.Equal(0, status);
            return buffer;
        }
        finally
        {
            if (attributes != 0)
            {
                CFRelease(attributes);
            }
        }
    }

    // CoreVideo makes a buffer IOSurface-backed when its attributes hold the
    // IOSurface properties key, even with nothing under it.
    private static nint IoSurfaceAttributes()
    {
        var coreFoundation = NativeLibrary.Load(CoreFoundation);
        var keyCallbacks = NativeLibrary.GetExport(coreFoundation, "kCFTypeDictionaryKeyCallBacks");
        var valueCallbacks = NativeLibrary.GetExport(coreFoundation, "kCFTypeDictionaryValueCallBacks");
        var key = Marshal.ReadIntPtr(NativeLibrary.GetExport(NativeLibrary.Load(CoreVideo), "kCVPixelBufferIOSurfacePropertiesKey"));

        var empty = CFDictionaryCreate(0, default, default, 0, keyCallbacks, valueCallbacks);
        try
        {
            return CFDictionaryCreate(0, [key], [empty], 1, keyCallbacks, valueCallbacks);
        }
        finally
        {
            CFRelease(empty);
        }
    }

    // F2: the system's own signature.
    [LibraryImport(CoreVideo)]
    private static partial int CVPixelBufferCreate(
        nint allocator,
        nuint width,
        nuint height,
        uint pixelFormat,
        nint attributes,
        out nint buffer);

    // F2: the system's own signature.
    [LibraryImport(CoreFoundation)]
    private static partial nint CFDictionaryCreate(
        nint allocator,
        ReadOnlySpan<nint> keys,
        ReadOnlySpan<nint> values,
        nint count,
        nint keyCallbacks,
        nint valueCallbacks);

    [LibraryImport(CoreFoundation)]
    private static partial void CFRelease(nint value);
}
