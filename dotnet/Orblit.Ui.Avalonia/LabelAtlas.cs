using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace Orblit.Ui.Avalonia;

/// <summary>How a <see cref="LabelAtlas"/> draws its strings and how big it may get.</summary>
public sealed record LabelAtlasOptions
{
    /// <summary>The font the strings are drawn in. The strings are white, so the surface that samples the atlas can tint them.</summary>
    public Typeface Typeface { get; init; } = Typeface.Default;

    public double FontSize { get; init; } = 32;

    /// <summary>The picture's width in pixels. A string wider than this, with its padding, is refused.</summary>
    public int Width { get; init; } = 1024;

    public int InitialHeight { get; init; } = 128;

    /// <summary>The tallest the picture may grow. Past it <see cref="LabelAtlas.Add"/> throws.</summary>
    public int MaxHeight { get; init; } = 4096;

    /// <summary>
    /// Empty pixels left around each string, so a filtered sample at its edge does not pick up its
    /// neighbour.
    /// </summary>
    public int Padding { get; init; } = 2;
}

/// <summary>
/// Many short strings drawn once into one picture. A name over a head is the same few words every
/// frame. Drawing each as its own image costs a texture per label, while one picture with every
/// distinct string packed into it costs one texture however many labels use it. Each string keeps a
/// rectangle in that picture, and whatever draws the label samples that rectangle.
/// The picture has a fixed width and grows in height. Growing never moves a string, so a rectangle
/// read earlier stays right in pixels. Only the height changes, so a caller that turns a rectangle
/// into texture coordinates divides by the current <see cref="Size"/>.
/// </summary>
public sealed class LabelAtlas
{
    private readonly LabelAtlasOptions _options;
    private readonly Dictionary<string, (Rect Place, FormattedText Words)> _labels = [];
    private int _height;

    // The shelf being filled: where the next string goes and how tall the tallest string on it is.
    private double _x;
    private double _y;
    private double _shelf;

    public LabelAtlas(LabelAtlasOptions? options = null)
    {
        _options = options ?? new LabelAtlasOptions();
        ArgumentOutOfRangeException.ThrowIfLessThan(_options.InitialHeight, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(_options.InitialHeight, _options.MaxHeight);
        _height = _options.InitialHeight;
        _x = _y = _options.Padding;
    }

    /// <summary>The picture's size in pixels.</summary>
    public Size Size => new(_options.Width, _height);

    /// <summary>Changes each time <see cref="Paint"/> draws a picture that differs from the last.</summary>
    public int Revision { get; private set; }

    /// <summary>Whether a string has been added since <see cref="Paint"/> last drew.</summary>
    public bool IsDirty { get; private set; }

    /// <summary>Where <paramref name="text"/> sits in the picture, in pixels, or null if it was never added.</summary>
    public Rect? RectOf(string text) => _labels.TryGetValue(text, out var label) ? label.Place : null;

    /// <summary>Gives <paramref name="text"/> a place. A string already in the atlas keeps its place.</summary>
    public void Add(string text)
    {
        if (_labels.ContainsKey(text))
        {
            return;
        }

        var words = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, _options.Typeface, _options.FontSize, Brushes.White);
        var wide = Math.Ceiling(words.Width);
        if (wide + (2 * _options.Padding) > _options.Width)
        {
            throw new ArgumentException("The label is wider than the atlas.", nameof(text));
        }

        _labels[text] = (Place(wide, Math.Ceiling(words.Height)), words);
        IsDirty = true;
    }

    /// <summary>Draws every string and returns the picture as PNG bytes.</summary>
    // shortcut: redraws every string on each change, upgrade when an atlas with thousands of strings
    // changes every frame.
    public byte[] Paint()
    {
        if (IsDirty)
        {
            Revision++;
        }

        IsDirty = false;
        using var picture = new RenderTargetBitmap(new PixelSize(_options.Width, _height));
        using (var context = picture.CreateDrawingContext())
        {
            foreach (var (place, words) in _labels.Values)
            {
                context.DrawText(words, place.TopLeft);
            }
        }

        using var png = new MemoryStream();
        picture.Save(png, PngBitmapEncoderOptions.Default);
        return png.ToArray();
    }

    // Finds a spot on the current shelf, or on a new one, and grows the picture if the spot is below
    // its bottom. Nothing changes if it cannot fit.
    private Rect Place(double wide, double high)
    {
        var gap = (double)_options.Padding;
        var wraps = _x + wide + gap > _options.Width;
        var x = wraps ? gap : _x;
        var y = wraps ? _y + _shelf + gap : _y;
        var height = _height;
        while (y + high + gap > height && height < _options.MaxHeight)
        {
            height = Math.Min(height * 2, _options.MaxHeight);
        }

        if (y + high + gap > height)
        {
            throw new InvalidOperationException($"The label atlas is full at {_options.Width}x{_options.MaxHeight}.");
        }

        _height = height;
        _x = x + wide + gap;
        _y = y;
        _shelf = wraps ? high : Math.Max(_shelf, high);
        return new Rect(x, y, wide, high);
    }
}
