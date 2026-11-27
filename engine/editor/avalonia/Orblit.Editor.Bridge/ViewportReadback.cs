namespace Orblit.Editor.Bridge;

/// <summary>
/// Copies the root viewport's pixels to host memory. The engine reads the GPU texture back and stalls
/// until it arrives, so this is the floor for showing the engine inside another toolkit. A headless
/// engine draws nothing, so frames come only from a windowed one. Reading from the device needs the
/// render thread, so the engine must not render on a thread of its own. Dispose it before the host.
/// </summary>
public sealed class ViewportReadback : IDisposable
{
    const long FormatRgb8 = 4;
    const long FormatRgba8 = 5;
    const long FormatRgbHalf = 14;
    const long FormatRgbaHalf = 15;

    readonly EngineHost _host;
    readonly nint _root;
    readonly nint _texture;
    readonly nint _server;
    // Zero with the OpenGL renderer, which has no rendering device, and once the target turns out to be HDR.
    nint _device;
    readonly ulong _target;

    public ViewportReadback(EngineHost host)
    {
        _host = host;
        var loop = host.EngineGetMainLoop(host.Singleton("Engine"));
        _root = host.StageGetRoot(loop);
        // The call hands back a counted reference. Dispose releases it.
        _texture = host.ViewportGetTexture(_root);
        _server = host.Singleton("RenderingSystem");
        _device = host.RenderingSystemGetRenderingDevice(_server);
        _target = host.RenderingSystemViewportGetTexture(_server, host.ViewportGetViewportRid(_root));
        // The engine draws only while one of its windows can be seen, and the editor covers its
        // window. A registered output keeps it drawing.
        host.DisplaySystemRegisterAdditionalOutput(host.Singleton("DisplaySystem"), _root);
    }

    public void Resize(int width, int height) => _host.WindowSetSize(_root, new Int2(width, height));

    /// <summary>
    /// Copies one frame into the buffer as red, green, blue and a spare byte that is not alpha. Grows
    /// the buffer when needed. Returns false before the first frame.
    /// </summary>
    public bool TryRead(out int width, out int height, ref byte[] rgba) =>
        (_device != 0 && TryReadDevice(out width, out height, ref rgba)) || TryReadImage(out width, out height, ref rgba);

    // The engine's image of an opaque viewport is RGB8, converted from the device's RGBA8 on every
    // call. Copying the device's bytes skips that and the conversion back.
    bool TryReadDevice(out int width, out int height, ref byte[] rgba)
    {
        width = height = 0;
        var texture = _host.RenderingSystemTextureGetRdTexture(_server, _target, false);
        var format = texture == 0 ? 0 : _host.RenderingDeviceTextureGetFormat(_device, texture);
        if (format == 0)
        {
            return false;
        }
        width = (int)_host.RdTextureFormatGetWidth(format);
        height = (int)_host.RdTextureFormatGetHeight(format);
        _host.Release(format);
        using var data = _host.RenderingDeviceTextureGetData(_device, texture, 0);
        // An HDR target holds eight bytes a pixel. The engine's image reads it from then on, so a frame
        // does not wait for the GPU twice.
        var length = data.Length;
        if (length != width * height * 4)
        {
            _device = 0;
            return false;
        }
        Fit(ref rgba, length);
        data.Bytes.CopyTo(rgba);
        return true;
    }

    bool TryReadImage(out int width, out int height, ref byte[] rgba)
    {
        width = height = 0;
        var image = _host.Texture2DGetImage(_texture);
        if (image == 0)
        {
            return false;
        }
        try
        {
            var format = ToEightBits(image);
            if (format != FormatRgb8 && format != FormatRgba8)
            {
                throw new InvalidOperationException($"Unexpected image format {format}.");
            }
            width = (int)_host.ImageGetWidth(image);
            height = (int)_host.ImageGetHeight(image);
            using var data = _host.ImageGetData(image);
            ToRgba(data.Bytes, format == FormatRgba8, width * height, ref rgba);
            return true;
        }
        finally
        {
            _host.Release(image);
        }
    }

    // An HDR viewport's image is linear half floats, which the engine's own screenshots convert the same way.
    // shortcut: two conversions a frame; read the half floats from the device if an HDR project needs the speed.
    long ToEightBits(nint image)
    {
        var format = _host.ImageGetFormat(image);
        if (format != FormatRgbHalf && format != FormatRgbaHalf)
        {
            return format;
        }
        _host.ImageLinearToSrgb(image);
        _host.ImageConvert(image, FormatRgba8);
        return FormatRgba8;
    }

    static void ToRgba(ReadOnlySpan<byte> source, bool hasAlpha, int pixels, ref byte[] rgba)
    {
        Fit(ref rgba, pixels * 4);
        if (hasAlpha)
        {
            source[..(pixels * 4)].CopyTo(rgba);
            return;
        }
        for (var i = 0; i < pixels; i++)
        {
            rgba[i * 4] = source[i * 3];
            rgba[i * 4 + 1] = source[i * 3 + 1];
            rgba[i * 4 + 2] = source[i * 3 + 2];
            rgba[i * 4 + 3] = 255;
        }
    }

    static void Fit(ref byte[] rgba, int length)
    {
        if (rgba.Length < length)
        {
            rgba = new byte[length];
        }
    }

    public void Dispose() => _host.Release(_texture);
}
