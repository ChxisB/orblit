namespace Orblit.Editor.Bridge;

/// <summary>
/// Copies the root viewport's pixels to host memory. The engine reads the GPU texture back and stalls
/// until it arrives, so this is the floor for showing the engine inside another toolkit. A headless
/// engine draws nothing, so frames come only from a windowed one. Dispose it before the host.
/// </summary>
public sealed class ViewportReadback : IDisposable
{
    const long FormatRgb8 = 4;
    const long FormatRgba8 = 5;

    readonly EngineHost _host;
    readonly nint _root;
    readonly nint _texture;

    public ViewportReadback(EngineHost host)
    {
        _host = host;
        var loop = host.EngineGetMainLoop(host.Singleton("Engine"));
        _root = host.StageGetRoot(loop);
        // The call hands back a counted reference. Dispose releases it.
        _texture = host.ViewportGetTexture(_root);
    }

    public void Resize(int width, int height) => _host.WindowSetSize(_root, new Int2(width, height));

    /// <summary>Copies one frame as RGBA8 into the buffer, growing it when needed. Returns false before the first frame.</summary>
    public bool TryRead(out int width, out int height, ref byte[] rgba)
    {
        width = height = 0;
        var image = _host.Texture2DGetImage(_texture);
        if (image == 0)
        {
            return false;
        }
        try
        {
            var format = _host.ImageGetFormat(image);
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

    static void ToRgba(ReadOnlySpan<byte> source, bool hasAlpha, int pixels, ref byte[] rgba)
    {
        if (rgba.Length < pixels * 4)
        {
            rgba = new byte[pixels * 4];
        }
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

    public void Dispose() => _host.Release(_texture);
}
