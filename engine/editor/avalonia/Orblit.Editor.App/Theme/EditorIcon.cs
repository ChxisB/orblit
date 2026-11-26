using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.Immutable;

namespace Orblit.Editor.App.Theme;

/// <summary>Draws one editor icon inside the control, in its own proportions. The themed colour is the inherited text foreground.</summary>
internal sealed class EditorIcon : Control
{
    public const double DefaultSize = 16;

    readonly IconDrawing _drawing;

    static EditorIcon()
    {
        AffectsRender<EditorIcon>(TextElement.ForegroundProperty);
        // A stretched icon would grow to its row. An icon is 16 square unless a size is set.
        HorizontalAlignmentProperty.OverrideDefaultValue<EditorIcon>(HorizontalAlignment.Center);
        VerticalAlignmentProperty.OverrideDefaultValue<EditorIcon>(VerticalAlignment.Center);
    }

    public EditorIcon(string name)
    {
        IconName = name;
        _drawing = EditorIcons.Get(name);
    }

    internal string IconName { get; }

    /// <summary>The icon drawn into a square bitmap, for a window icon. Only icons with fixed colors suit it.</summary>
    public static WindowIcon ToWindowIcon(string name, int side)
    {
        var icon = new EditorIcon(name) { Width = side, Height = side };
        icon.Measure(new Size(side, side));
        icon.Arrange(new Rect(0, 0, side, side));
        var bitmap = new RenderTargetBitmap(new PixelSize(side, side));
        bitmap.Render(icon);
        return new WindowIcon(bitmap);
    }

    /// <summary>Where the art lands in a box: as large as it fits without changing shape, in the middle.</summary>
    internal Rect DrawnIn(Size box)
    {
        var scale = Math.Min(box.Width / _drawing.Width, box.Height / _drawing.Height);
        var size = new Size(_drawing.Width * scale, _drawing.Height * scale);
        return new Rect(new Point((box.Width - size.Width) / 2, (box.Height - size.Height) / 2), size);
    }

    protected override Size MeasureOverride(Size availableSize) => new(DefaultSize, DefaultSize);

    public override void Render(DrawingContext context)
    {
        var themed = TextElement.GetForeground(this) ?? Brushes.Gray;
        var drawn = DrawnIn(Bounds.Size);
        var scale = drawn.Width / _drawing.Width;
        using var fitted = context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(drawn.X, drawn.Y));
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
