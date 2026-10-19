using System;
using System.IO;
using System.Runtime.InteropServices;

namespace SurfaceSpike;

[StructLayout(LayoutKind.Sequential)]
internal struct SurfaceDesc
{
    public int Kind;
    public IntPtr Window;
}

[StructLayout(LayoutKind.Sequential)]
internal struct Stats
{
    public double GpuMilliseconds;
    public double CpuMilliseconds;
    public uint BatchedObjects;
    public uint BatchGroups;
    public uint PassCount;
}

// The renderer's C ABI, as orblit_renderer.h declares it. Throwaway.
internal static unsafe class Native
{
    public const int SurfaceHeadless = 0;
    public const int SurfaceWindow = 1;
    public const int SurfacePlatform = 2;

    const string Renderer = "orblit_renderer";
    const string View = "spike_view";

    // SPIKE_NATIVE is native/build; SPIKE_VARIANT picks apple or portable.
    public static void Resolve()
    {
        var root = Environment.GetEnvironmentVariable("SPIKE_NATIVE")
            ?? throw new InvalidOperationException("SPIKE_NATIVE is not set");
        var variant = Environment.GetEnvironmentVariable("SPIKE_VARIANT") ?? "apple";
        NativeLibrary.SetDllImportResolver(typeof(Native).Assembly, (name, _, _) => name switch
        {
            Renderer => NativeLibrary.Load(Path.Combine(root, variant, "liborblit_renderer.dylib")),
            View => NativeLibrary.Load(Path.Combine(root, "libspike_view.dylib")),
            _ => IntPtr.Zero,
        });
    }

    [DllImport(Renderer)]
    public static extern IntPtr orblit_renderer_create(int backend, SurfaceDesc* surface, uint width, uint height);

    [DllImport(Renderer)]
    public static extern void orblit_renderer_destroy(IntPtr renderer);

    [DllImport(Renderer)]
    public static extern int orblit_renderer_resize(IntPtr renderer, uint width, uint height);

    [DllImport(Renderer)]
    public static extern int orblit_renderer_draw(IntPtr renderer, double seconds);

    [DllImport(Renderer)]
    public static extern ulong orblit_renderer_rendered_frames(IntPtr renderer);

    [DllImport(Renderer)]
    public static extern int orblit_renderer_apply_objects(
        IntPtr renderer, uint count, long* keys, float* transforms, nuint transformFloats,
        float* colours, nuint colourFloats, int* meshes, int* flags, int* materials,
        int* morphCounts, float* morphWeights, nuint morphWeightFloats, byte** paths, uint pathCount);

    [DllImport(Renderer)]
    public static extern int orblit_renderer_apply_lights(
        IntPtr renderer, uint count, long* keys, int* kinds, int* flags, float* parameters, nuint floats);

    [DllImport(Renderer)]
    public static extern int orblit_renderer_set_sky_colour(IntPtr renderer, float* colour, float ambient, int showBody);

    [DllImport(Renderer)]
    public static extern int orblit_renderer_set_camera(
        IntPtr renderer, float* position, float* target, float fieldOfView, int orthographic, float viewHeight, double at);

    [DllImport(Renderer)]
    public static extern int orblit_renderer_set_exposure(IntPtr renderer, float aperture, float shutter, float sensitivity);

    [DllImport(Renderer)]
    public static extern int orblit_renderer_request_capture(IntPtr renderer);

    [DllImport(Renderer)]
    public static extern nuint orblit_renderer_read_capture(IntPtr renderer, byte* rgba, nuint capacity, uint* width, uint* height);

    [DllImport(Renderer)]
    public static extern int orblit_renderer_stats(IntPtr renderer, Stats* stats);

    [DllImport(Renderer)]
    public static extern IntPtr orblit_renderer_copy_presented(IntPtr renderer);

    [DllImport(View)]
    public static extern IntPtr spike_make_metal_view();

    [DllImport(View)]
    public static extern IntPtr spike_view_layer(IntPtr view);

    [DllImport(View)]
    public static extern int spike_view_pixels(IntPtr view, uint* width, uint* height);

    [DllImport(View)]
    public static extern IntPtr spike_make_shared_event();

    [DllImport(View)]
    public static extern void spike_event_signal(IntPtr eventHandle, ulong value);

    [DllImport(View)]
    public static extern ulong spike_event_value(IntPtr eventHandle);

    const string CoreVideo = "/System/Library/Frameworks/CoreVideo.framework/CoreVideo";
    const string IOSurface = "/System/Library/Frameworks/IOSurface.framework/IOSurface";
    const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

    [DllImport(CoreVideo)]
    public static extern IntPtr CVPixelBufferGetIOSurface(IntPtr buffer);

    [DllImport(CoreVideo)]
    public static extern nuint CVPixelBufferGetWidth(IntPtr buffer);

    [DllImport(CoreVideo)]
    public static extern nuint CVPixelBufferGetHeight(IntPtr buffer);

    [DllImport(IOSurface)]
    public static extern uint IOSurfaceGetID(IntPtr surface);

    [DllImport(CoreFoundation)]
    public static extern void CFRelease(IntPtr handle);
}
