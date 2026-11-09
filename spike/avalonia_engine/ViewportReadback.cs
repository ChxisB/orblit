namespace AvaloniaEngine;

/// <summary>
/// Path 3, the floor: copy the root viewport's pixels to host memory every frame.
/// The engine reads the GPU texture back and stalls until it arrives.
/// </summary>
public sealed unsafe class ViewportReadback
{
    const long GetMainLoop = 1016888095;
    const long GetRoot = 1757182445;
    const long GetTexture = 1746695840;
    const long GetImage = 4190603485;
    const long GetWidth = 3905245786;
    const long GetHeight = 3905245786;
    const long GetFormat = 3847873762;
    const long GetData = 2362200018;
    const long Unreference = 2240911060;
    const long FormatRgb8 = 4;
    const long FormatRgba8 = 5;

    readonly Engine _engine;
    readonly nint _texture;

    public ViewportReadback(Engine engine)
    {
        _engine = engine;
        nint loop = 0;
        engine.Call("Engine", "get_main_loop", GetMainLoop, engine.Singleton("Engine"), null, &loop);
        nint root = 0;
        engine.Call("SceneTree", "get_root", GetRoot, loop, null, &root);
        // The call hands back a counted reference. Close releases it before the engine shuts down.
        nint texture = 0;
        engine.Call("Viewport", "get_texture", GetTexture, root, null, &texture);
        _texture = texture;
    }

    public void Close() => Release(_texture);

    /// <summary>Copies one frame as RGBA8. Returns false when the engine has no image yet.</summary>
    public bool TryRead(out int width, out int height, ref byte[] rgba)
    {
        width = height = 0;
        nint image = 0;
        _engine.Call("Texture2D", "get_image", GetImage, _texture, null, &image);
        if (image == 0)
        {
            return false;
        }

        long w = 0, h = 0, format = 0;
        _engine.Call("Image", "get_width", GetWidth, image, null, &w);
        _engine.Call("Image", "get_height", GetHeight, image, null, &h);
        _engine.Call("Image", "get_format", GetFormat, image, null, &format);
        if (format != FormatRgb8 && format != FormatRgba8)
        {
            Release(image);
            throw new InvalidOperationException($"Unexpected image format {format}");
        }

        // A PackedByteArray is two pointers. The engine assigns into it, so it starts zeroed.
        var data = stackalloc nint[2];
        data[0] = 0;
        data[1] = 0;
        _engine.Call("Image", "get_data", GetData, image, null, data);

        width = (int)w;
        height = (int)h;
        var pixels = width * height;
        if (rgba.Length < pixels * 4)
        {
            rgba = new byte[pixels * 4];
        }

        var source = Engine.ByteAt(data, 0);
        fixed (byte* destination = rgba)
        {
            if (format == FormatRgba8)
            {
                Buffer.MemoryCopy(source, destination, rgba.Length, (long)pixels * 4);
            }
            else
            {
                for (var i = 0; i < pixels; i++)
                {
                    destination[i * 4] = source[i * 3];
                    destination[i * 4 + 1] = source[i * 3 + 1];
                    destination[i * 4 + 2] = source[i * 3 + 2];
                    destination[i * 4 + 3] = 255;
                }
            }
        }

        Engine.DestroyByteArray(data);
        Release(image);
        return true;
    }

    void Release(nint refCounted)
    {
        byte freed = 0;
        _engine.Call("RefCounted", "unreference", Unreference, refCounted, null, &freed);
        if (freed != 0)
        {
            Engine.DestroyObject(refCounted);
        }
    }
}
