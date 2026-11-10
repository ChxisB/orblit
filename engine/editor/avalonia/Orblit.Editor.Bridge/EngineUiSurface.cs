using Orblit.Ui;

namespace Orblit.Editor.Bridge;

/// <summary>
/// Shows the frames of a <see cref="UiLayer"/> in the engine: one rectangle over the whole window, drawn from
/// an image texture. The rectangle sits under the stage root and lets mouse events through, so the layer
/// decides what a click lands on. Dispose it before the host.
/// </summary>
public sealed class EngineUiSurface : IUiSurface
{
    // Image.Format.
    const long FormatRgba8 = 5;
    // CanvasItemMaterial.BlendMode, Control.MouseFilter, Control.LayoutPreset.
    const long PremultipliedAlpha = 4;
    const long IgnoreMouse = 2;
    const long FullRect = 15;

    readonly EngineHost _host;
    readonly nint _image;
    readonly nint _texture;
    readonly nint _rect;
    bool _disposed;

    public EngineUiSurface(EngineHost host)
    {
        _host = host;
        _image = host.Construct("Image");
        _texture = host.Construct("ImageTexture");
        // The layer's pixels are premultiplied, so the default blend would darken every soft edge.
        var material = host.Construct("CanvasItemMaterial");
        host.CanvasItemMaterialSetBlendMode(material, PremultipliedAlpha);
        _rect = EditorWorld.Add(host, EditorWorld.StageRoot(host), "TextureRect", "UiSurface");
        host.ControlSetMouseFilter(_rect, IgnoreMouse);
        host.ControlSetAnchorsAndOffsetsPreset(_rect, FullRect, 0, 0);
        host.CanvasItemSetMaterial(_rect, material);
        host.TextureRectSetTexture(_rect, _texture);
        EditorWorld.Release(host, material);
    }

    // shortcut: set_image makes a new texture on every upload, switch to ImageTexture.update while the size holds when frames come every tick.
    public void Upload(ReadOnlySpan<byte> pixels, int width, int height)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (width <= 0 || height <= 0 || pixels.Length != (long)width * height * 4)
        {
            throw new ArgumentException($"{pixels.Length} bytes are not a {width}x{height} frame of four bytes a pixel.");
        }
        _host.ImageSetData(_image, width, height, false, FormatRgba8, pixels);
        _host.ImageTextureSetImage(_texture, _image);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _host.EntityQueueDestroy(_rect);
        EditorWorld.Release(_host, _image, _texture);
    }
}
