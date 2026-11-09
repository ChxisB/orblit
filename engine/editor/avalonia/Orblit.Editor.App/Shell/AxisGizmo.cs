using System.Globalization;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App.Shell;

/// <summary>
/// The three axes drawn from the camera's angles: a lettered dot at the positive end of each and a plain dot
/// at the negative end, back to front. The camera starts at <see cref="OrbitView.Start"/> and takes the
/// same turns as this control, and a frame keeps the angles, so the two agree without sharing state.
/// </summary>
internal sealed class AxisGizmo : Control
{
    internal const double Size = 100;

    // The distance from the middle to the centre of an end dot that faces straight across.
    internal const double Reach = 29;

    const double Radius = 8.5;

    // The letter is a darker shade of the dot it sits on.
    const double LetterShade = 0.45;

    static readonly (string Name, Vector3 Direction, Color Fill, Color Edge)[] Axes =
    [
        ("X", Vector3.UnitX, Color.Parse("#f63252"), Color.Parse("#f63252")),
        ("Y", Vector3.UnitY, Color.Parse("#87d703"), Color.Parse("#87d703")),
        ("Z", Vector3.UnitZ, Color.Parse("#288cf5"), Color.Parse("#288cf5")),
        ("-X", -Vector3.UnitX, Color.Parse("#a72d40"), Color.Parse("#d74e65")),
        ("-Y", -Vector3.UnitY, Color.Parse("#649810"), Color.Parse("#87bd30")),
        ("-Z", -Vector3.UnitZ, Color.Parse("#3e76b1"), Color.Parse("#5c92ca")),
    ];

    OrbitView _view = OrbitView.Start;

    /// <summary>Where an end dot's centre lands in the control, and how far it is toward the viewer.</summary>
    internal readonly record struct End(string Name, Point At, float Depth);

    public AxisGizmo()
    {
        Width = Size;
        Height = Size;
        IsHitTestVisible = false;
    }

    internal OrbitView View => _view;

    public void Orbit(float yaw, float pitch)
    {
        _view = _view.Orbited(yaw, pitch);
        InvalidateVisual();
    }

    /// <summary>The six ends for a view, in the order of the table: X, Y, Z, then the negatives.</summary>
    internal static End[] Ends(OrbitView view)
    {
        var (sinYaw, cosYaw) = MathF.SinCos(view.Yaw);
        var (sinPitch, cosPitch) = MathF.SinCos(view.Pitch);
        var right = new Vector3(cosYaw, 0, -sinYaw);
        var up = new Vector3(-sinPitch * sinYaw, cosPitch, -sinPitch * cosYaw);
        var back = new Vector3(cosPitch * sinYaw, sinPitch, cosPitch * cosYaw);
        return [.. Axes.Select(axis => new End(
            axis.Name,
            new Point(Size / 2 + Vector3.Dot(axis.Direction, right) * Reach, Size / 2 - Vector3.Dot(axis.Direction, up) * Reach),
            Vector3.Dot(axis.Direction, back)))];
    }

    public override void Render(DrawingContext context)
    {
        var middle = new Point(Size / 2, Size / 2);
        var typeface = new Typeface(TextElement.GetFontFamily(this), weight: FontWeight.Bold);
        foreach (var (end, axis) in Ends(_view).Zip(Axes).OrderBy(pair => pair.First.Depth))
        {
            var positive = axis.Name.Length == 1;
            var fill = new ImmutableSolidColorBrush(axis.Fill);
            if (positive)
            {
                context.DrawLine(new Pen(fill, 2), middle, end.At);
            }

            context.DrawEllipse(fill, positive ? null : new Pen(new ImmutableSolidColorBrush(axis.Edge), 1), end.At, Radius, Radius);
            if (positive)
            {
                var letter = new FormattedText(axis.Name, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, 12, new ImmutableSolidColorBrush(Shade(axis.Fill)));
                context.DrawText(letter, new Point(end.At.X - letter.Width / 2, end.At.Y - letter.Height / 2));
            }
        }
    }

    static Color Shade(Color color) => Color.FromRgb((byte)(color.R * LetterShade), (byte)(color.G * LetterShade), (byte)(color.B * LetterShade));
}
