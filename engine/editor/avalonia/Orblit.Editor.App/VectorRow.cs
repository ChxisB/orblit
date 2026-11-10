using System.Globalization;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App;

/// <summary>
/// A label over a dark field of three cells for one vector, each with its axis letter and the unit. It raises
/// <see cref="Entered"/> when the boxes hold a finite value that differs from the one shown. Any other text is
/// put back.
/// </summary>
internal sealed class VectorRow : StackPanel
{
    // The label is 30 high and the field 28, with a pixel under it. The letter sits 8 pixels into its cell,
    // the value 28 in, and the unit follows the value after a gap.
    const double LabelHeight = 30;
    const double FieldHeight = 28;
    const double LetterIndent = 8;
    const double ValueIndent = 28;
    const double UnitGap = 7;

    static readonly (string Letter, IBrush Brush)[] s_letters =
        [("x", EditorBrushes.AxisX), ("y", EditorBrushes.AxisY), ("z", EditorBrushes.AxisZ)];

    readonly TextBox[] _axes = [AxisBox(), AxisBox(), AxisBox()];
    Vector3? _shown;

    public VectorRow(string label, string unit = "")
    {
        Label = label;
        Children.Add(new Border
        {
            Height = LabelHeight,
            Child = new TextBlock { Text = label, Margin = new Thickness(LetterIndent, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center },
        });
        var cells = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*") };
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
        Children.Add(new Border
        {
            Background = EditorBrushes.Field,
            CornerRadius = EditorTheme.Corners,
            Height = FieldHeight,
            Margin = new Thickness(0, 0, 0, 1),
            Child = cells,
        });
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

    StackPanel Cell(int index, string unit)
    {
        var letter = new TextBlock
        {
            Text = s_letters[index].Letter,
            Foreground = s_letters[index].Brush,
            Width = ValueIndent,
            Padding = new Thickness(LetterIndent, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        var suffix = new TextBlock
        {
            Text = unit,
            Foreground = EditorBrushes.Unit,
            Margin = new Thickness(UnitGap, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        var cell = new StackPanel { Orientation = Orientation.Horizontal };
        cell.Children.Add(letter);
        cell.Children.Add(_axes[index]);
        cell.Children.Add(suffix);
        return cell;
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

    // Padding 0 puts the value at the left edge of its box, so the cell decides where it sits. The box is as
    // wide as its text, so the unit can follow it, and wide enough to click when it is empty.
    static TextBox AxisBox() => new()
    {
        Height = FieldHeight,
        MinHeight = 0,
        MinWidth = 8,
        Padding = new Thickness(0),
        VerticalContentAlignment = VerticalAlignment.Center,
    };
}
