using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.VisualTree;

namespace Orblit.Ui;

/// <summary>
/// Draws a control tree off screen and hands each changed frame to a surface. Create it, call
/// it and dispose it on the thread that started the Avalonia platform.
/// </summary>
public sealed class UiLayer : IUiInput, IDisposable
{
    const int BytesPerPixel = 4;

    readonly Window _window;
    readonly Control _content;
    readonly IUiSurface _surface;
    byte[] _shown = [];
    byte[] _next;
    bool _disposed;

    public UiLayer(Control content, PixelSize size, IUiSurface surface)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(surface);
        if (size.Width <= 0 || size.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(size), size, "A layer needs a size above zero.");
        }

        _content = content;
        _surface = surface;
        _next = new byte[size.Width * size.Height * BytesPerPixel];
        _window = new Window
        {
            Content = content,
            Width = size.Width,
            Height = size.Height,
            Background = Brushes.Transparent,
            WindowDecorations = WindowDecorations.None,
            TransparencyLevelHint = [WindowTransparencyLevel.Transparent],
        };
        _window.Show();
        _window.UpdateLayout();
        // The first capture has to happen in the same call that made the window. When it comes later, every glyph is drawn wrong.
        _window.CaptureRenderedFrame()?.Dispose();
    }

    /// <summary>Draws the tree and uploads the frame when its pixels differ from the last upload.</summary>
    public void Render()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        using var frame = _window.CaptureRenderedFrame();
        if (frame is null)
        {
            return;
        }

        Copy(frame);
        // shortcut: compares whole frames, move to dirty rectangles if a UI frame is slow.
        if (!_next.AsSpan().SequenceEqual(_shown))
        {
            _surface.Upload(_next, frame.PixelSize.Width, frame.PixelSize.Height);
            var spare = _shown.Length == 0 ? new byte[_next.Length] : _shown;
            _shown = _next;
            _next = spare;
        }
    }

    public void PointerMoved(Point point) => _window.MouseMove(point);

    public void PointerButton(Point point, MouseButton button, bool pressed)
    {
        if (pressed)
        {
            _window.MouseDown(point, button);
        }
        else
        {
            _window.MouseUp(point, button);
        }
    }

    public void Key(Key key, bool pressed)
    {
        if (pressed)
        {
            _window.KeyPress(key, RawInputModifiers.None, PhysicalKey.None, null);
        }
        else
        {
            _window.KeyRelease(key, RawInputModifiers.None, PhysicalKey.None, null);
        }
    }

    public void Text(string text) => _window.KeyTextInput(text);

    public bool WantsPointer(Point point) =>
        _window.InputHitTest(point) is Visual hit && (hit == _content || _content.IsVisualAncestorOf(hit));

    // shortcut: a click on the scene leaves a focused text box focused, so keys keep going to it. Add a ClearFocus to IUiInput when a game needs that.
    public bool WantsKeys => _window.FocusManager?.GetFocusedElement() is TextBox;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _window.Close();
        _surface.Dispose();
    }

    void Copy(Avalonia.Media.Imaging.WriteableBitmap frame)
    {
        using var pixels = frame.Lock();
        // shortcut: only Rgba8888 frames, which macOS hands over. Add a red and blue swap when a
        // platform hands over Bgra8888.
        if (pixels.Format != PixelFormat.Rgba8888 || pixels.Size.Width * pixels.Size.Height * BytesPerPixel != _next.Length)
        {
            throw new NotSupportedException($"The layer needs {_window.Width}x{_window.Height} Rgba8888 frames, got {pixels.Size} {pixels.Format}.");
        }

        var row = pixels.Size.Width * BytesPerPixel;
        for (var y = 0; y < pixels.Size.Height; y++)
        {
            Marshal.Copy(pixels.Address + y * pixels.RowBytes, _next, y * row, row);
        }
    }
}
