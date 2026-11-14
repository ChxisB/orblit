using System.Globalization;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Styling;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App;

/// <summary>
/// A label over three fields for one vector, each with a tag for its axis, the value and the unit. It raises
/// <see cref="Entered"/> when the boxes hold a finite value that differs from the one shown. Any other text is
/// put back.
/// </summary>
internal sealed class VectorRow : StackPanel
{
    // The label is 30 high and the fields 28, 4 apart, with a pixel under them. The tag is 18 wide, the value
    // starts 6 after it, and the unit ends 7 short of the field's edge.
    const double LabelHeight = 30;
    const double FieldHeight = 28;
    const double FieldGap = 4;
    const double LabelIndent = 8;
    const double TagWidth = 18;
    const double ValueIndent = 6;
    const double UnitGap = 7;

    // The axis colour at 12 percent, so the tag reads as a tint and the letter on it in full colour.
    const byte TagAlpha = 0x1F;

    const string Field = "num";

    static readonly (string Letter, Color Color)[] s_letters =
        [("X", EditorColors.AxisX), ("Y", EditorColors.AxisY), ("Z", EditorColors.AxisZ)];

    readonly TextBox[] _axes = [AxisBox(), AxisBox(), AxisBox()];
    Vector3? _shown;

    public VectorRow(string label, string unit = "")
    {
        Label = label;
        // Styles, not local values, so focus can light the edge of the field that holds the caret.
        Styles.Add(new Style(x => x.OfType<Border>().Class(Field)) { Setters = { new Setter(Border.BorderBrushProperty, EditorBrushes.Line) } });
        Styles.Add(new Style(x => x.OfType<Border>().Class(Field).Class(":focus-within")) { Setters = { new Setter(Border.BorderBrushProperty, EditorBrushes.Accent) } });
        // The field draws the edge, so the box drops the 2 pixel ring the theme gives a focused text box.
        Styles.Add(new Style(x => x.OfType<TextBox>().Class(":focus").Template().OfType<Border>().Name("PART_BorderElement")) { Setters = { new Setter(Border.BorderThicknessProperty, new Thickness(0)) } });
        Children.Add(new Border
        {
            Height = LabelHeight,
            Child = new TextBlock { Text = label, Margin = new Thickness(LabelIndent, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center },
        });
        var cells = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*"), ColumnSpacing = FieldGap, Margin = new Thickness(0, 0, 0, 1) };
        for (var i = 0; i < _axes.Length; i++)
        {
            var box = _axes[i];
            box.LostFocus += (_, _) => Commit();
            box.KeyDown += (_, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    Commit();
                }
            };
            var cell = Cell(i, unit);
            Grid.SetColumn(cell, i);
            cells.Children.Add(cell);
        }
        Children.Add(cells);
        Show(null);
    }

    /// <summary>Raised with the value the user typed. The row already shows it. The owner may show another.</summary>
    public event Action<Vector3>? Entered;

    internal string Label { get; }

    internal IReadOnlyList<TextBox> Axes => _axes;

    /// <summary>Shows a value in the boxes. Null empties and disables them.</summary>
    public void Show(Vector3? value)
    {
        _shown = value;
        for (var i = 0; i < _axes.Length; i++)
        {
            _axes[i].IsEnabled = value is not null;
            _axes[i].Text = value is { } shown ? shown[i].ToString(CultureInfo.InvariantCulture) : "";
        }
    }

    // The value box fills what the tag and the unit leave, so a long value scrolls inside it and the field keeps its width.
    Border Cell(int index, string unit)
    {
        var (letter, color) = s_letters[index];
        var tag = new Border
        {
            Width = TagWidth,
            Background = new ImmutableSolidColorBrush(Color.FromArgb(TagAlpha, color.R, color.G, color.B)),
            Child = new TextBlock
            {
                Text = letter,
                Foreground = new ImmutableSolidColorBrush(color),
                FontSize = 10.5,
                FontWeight = FontWeight.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };
        DockPanel.SetDock(tag, Dock.Left);
        var suffix = new TextBlock
        {
            Text = unit,
            Foreground = EditorBrushes.Unit,
            FontSize = 11,
            Margin = new Thickness(0, 0, UnitGap, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        DockPanel.SetDock(suffix, Dock.Right);
        return new Border
        {
            Classes = { Field },
            Background = EditorBrushes.Field,
            BorderThickness = new Thickness(1),
            CornerRadius = EditorTheme.Corners,
            Height = FieldHeight,
            ClipToBounds = true,
            Child = new DockPanel { Children = { tag, suffix, _axes[index] } },
        };
    }

    void Commit()
    {
        if (_shown is not { } shown)
        {
            return;
        }
        if (TryRead(out var typed) && typed != shown)
        {
            _shown = typed;
            Entered?.Invoke(typed);
            return;
        }
        Show(shown);
    }

    // NaN and infinity parse as numbers but would corrupt a transform.
    bool TryRead(out Vector3 value)
    {
        var parts = new float[_axes.Length];
        for (var i = 0; i < parts.Length; i++)
        {
            if (!float.TryParse(_axes[i].Text, NumberStyles.Float, CultureInfo.InvariantCulture, out parts[i]) || !float.IsFinite(parts[i]))
            {
                value = default;
                return false;
            }
        }
        value = new Vector3(parts[0], parts[1], parts[2]);
        return true;
    }

    // The field draws the edge and the fill, so the box has neither.
    static TextBox AxisBox() => new()
    {
        MinHeight = 0,
        MinWidth = 0,
        Padding = new Thickness(ValueIndent, 0),
        BorderThickness = new Thickness(0),
        Background = Brushes.Transparent,
        VerticalContentAlignment = VerticalAlignment.Center,
    };
}
