using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.Immutable;

namespace Orblit.Editor.App.Theme;

/// <summary>Draws one editor icon at the size of the control. The themed colour is the inherited text foreground.</summary>
internal sealed class EditorIcon : Control
{
    public const double DefaultSize = 16;

    readonly IconDrawing _drawing;

    static EditorIcon()
    {
        AffectsRender<EditorIcon>(TextElement.ForegroundProperty);
    }

    public EditorIcon(string name)
    {
        IconName = name;
        _drawing = EditorIcons.Get(name);
    }

    internal string IconName { get; }

    /// <summary>The icon drawn into a square bitmap, for a window icon. Only icons with fixed colours suit it.</summary>
    public static WindowIcon ToWindowIcon(string name, int side)
    {
        var icon = new EditorIcon(name);
        icon.Measure(new Size(side, side));
        icon.Arrange(new Rect(0, 0, side, side));
        var bitmap = new RenderTargetBitmap(new PixelSize(side, side));
        bitmap.Render(icon);
        return new WindowIcon(bitmap);
    }

    protected override Size MeasureOverride(Size availableSize) => new(DefaultSize, DefaultSize);

    public override void Render(DrawingContext context)
    {
        var themed = TextElement.GetForeground(this) ?? Brushes.Gray;
        using var scale = context.PushTransform(Matrix.CreateScale(Bounds.Width / _drawing.Width, Bounds.Height / _drawing.Height));
        foreach (var shape in _drawing.Shapes)
        {
            using var placed = context.PushTransform(shape.Transform);
            if (shape.ClipGeometry is { } clip)
            {
                using var cut = context.PushGeometryClip(clip);
                Draw(context, shape, themed);
            }
            else
            {
                Draw(context, shape, themed);
            }
        }
    }

    static void Draw(DrawingContext context, IconShape shape, IBrush themed)
    {
        var pen = shape.Stroke == 0 ? null : new Pen(Paint(shape.Stroke, themed), shape.StrokeWidth, lineCap: shape.Cap, lineJoin: shape.Join);
        context.DrawGeometry(shape.Fill == 0 ? null : Paint(shape.Fill, themed), pen, shape.Geometry);
    }

    static IBrush Paint(uint value, IBrush themed) =>
        value == IconShape.Themed ? themed : new ImmutableSolidColorBrush(Color.FromUInt32(value));
}
