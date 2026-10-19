using System;

namespace SurfaceSpike;

// One renderer with the headless host's scene. All three paths use it, so the
// only thing that differs between them is how a frame reaches the screen.
internal sealed unsafe class Engine : IDisposable
{
    public IntPtr Handle { get; private set; }

    public Engine(int surfaceKind, IntPtr window, uint width, uint height)
    {
        var desc = new SurfaceDesc { Kind = surfaceKind, Window = window };
        Handle = Native.orblit_renderer_create(0, &desc, width, height);
        if (Handle == IntPtr.Zero)
        {
            throw new InvalidOperationException("the renderer would not start");
        }
        Publish();
    }

    public void Resize(uint width, uint height) => Check(Native.orblit_renderer_resize(Handle, width, height));

    public void Draw(double seconds)
    {
        Orbit(seconds);
        Check(Native.orblit_renderer_draw(Handle, seconds));
    }

    public Stats Measure()
    {
        Stats stats;
        Check(Native.orblit_renderer_stats(Handle, &stats));
        return stats;
    }

    public void Dispose()
    {
        if (Handle == IntPtr.Zero) return;
        Native.orblit_renderer_destroy(Handle);
        Handle = IntPtr.Zero;
    }

    static void Check(int result)
    {
        if (result != 0) throw new InvalidOperationException($"renderer call returned {result}");
    }

    // A column-major box scaled about its middle and moved.
    static void Place(float* m, float x, float y, float z, float sx, float sy, float sz)
    {
        new Span<float>(m, 16).Clear();
        m[0] = sx; m[5] = sy; m[10] = sz;
        m[12] = x; m[13] = y; m[14] = z; m[15] = 1f;
    }

    void Publish()
    {
        float* sky = stackalloc float[] { 0.30f, 0.45f, 0.70f };
        Check(Native.orblit_renderer_set_sky_colour(Handle, sky, 24000f, 1));

        const int lightFloats = 22;
        float* sun = stackalloc float[lightFloats]
        {
            1.0f, 0.95f, 0.88f, 100000f, 0, 0, 0, -0.45f, -0.8f, -0.55f, 0,
            0, 0, 0.53f, 0.1f, 10f, 80f, 0, 0, 0, 0, 0,
        };
        long lightKey = 1;
        int lightKind = 0;
        int lightFlags = 1;
        Check(Native.orblit_renderer_apply_lights(Handle, 1, &lightKey, &lightKind, &lightFlags, sun, lightFloats));

        const int count = 6;
        long* keys = stackalloc long[count];
        float* transforms = stackalloc float[count * 16];
        float* colours = stackalloc float[count * 3];
        int* meshes = stackalloc int[count];
        int* flags = stackalloc int[count];
        int* materials = stackalloc int[count];
        int* morphCounts = stackalloc int[count];
        float[,] blocks =
        {
            { -1.8f, 0.0f, 0.2f, 0.5f }, { -0.6f, 0.25f, -1.0f, 0.75f }, { 0.7f, -0.1f, 0.6f, 0.4f },
            { 1.8f, 0.4f, -0.5f, 0.9f }, { 0.0f, -0.25f, 1.9f, 0.25f },
        };
        float[,] tints =
        {
            { 0.55f, 0.55f, 0.5f }, { 0.85f, 0.28f, 0.18f }, { 0.20f, 0.55f, 0.85f },
            { 0.95f, 0.75f, 0.2f }, { 0.35f, 0.75f, 0.35f }, { 0.75f, 0.35f, 0.8f },
        };
        for (var i = 0; i < count; i++)
        {
            keys[i] = 100 + i;
            meshes[i] = -1;
            flags[i] = 1 | 2 | 4;
            materials[i] = -1;
            morphCounts[i] = 0;
            for (var c = 0; c < 3; c++) colours[i * 3 + c] = tints[i, c];
        }
        Place(transforms, 0f, -0.55f, 0f, 6f, 0.05f, 6f);
        for (var i = 0; i < 5; i++)
        {
            Place(transforms + (i + 1) * 16, blocks[i, 0], blocks[i, 1] - 0.5f + blocks[i, 3], blocks[i, 2],
                blocks[i, 3], blocks[i, 3], blocks[i, 3]);
        }
        Check(Native.orblit_renderer_apply_objects(Handle, count, keys, transforms, count * 16,
            colours, count * 3, meshes, flags, materials, morphCounts, null, 0, null, 0));
        Check(Native.orblit_renderer_set_exposure(Handle, 16f, 1f / 125f, 100f));
        Orbit(0);
    }

    // A slow turn about the scene, so a frame differs from the one before it.
    void Orbit(double seconds)
    {
        var angle = seconds * 0.5;
        float* eye = stackalloc float[] { (float)(Math.Sin(angle) * 8.5), 3.6f, (float)(Math.Cos(angle) * 8.5) };
        float* look = stackalloc float[] { 0f, 0f, 0f };
        Check(Native.orblit_renderer_set_camera(Handle, eye, look, 42f, 0, 10f, seconds));
    }
}
