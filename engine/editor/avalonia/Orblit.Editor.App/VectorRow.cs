using System.Globalization;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Styling;
using Orblit.Editor.App.Shell;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App;

/// <summary>
/// A label beside two or three fields for one vector, each with a tag for its axis, the value and the unit. A field shows
/// its value rounded at rest and in full while it has the caret. It raises <see cref="Entered"/> when a box holds a
/// finite value that differs from the one shown. Any other text stays, with a red edge and a line that says why,
/// until the next value is shown. A drag across a tag steps its axis. A NaN axis shows Mixed, for entities that
/// differ there, and stays NaN in what the row raises until a number is typed in it.
/// </summary>
internal sealed class VectorRow : Grid
{
    // The label column is 96 wide. The row is 30 high and its fields 28. The tag is 18 wide.
    const double LabelWidth = 96;
    const double LabelGap = Gaps.Item;
    const double RowHeight = 30;
    const double FieldHeight = 28;
    const double FieldGap = Gaps.Tight;
    const double TagWidth = 18;
    const double ValueIndent = Gaps.Tight;
    const double UnitGap = Gaps.Item;

    // The axis colour at 12 percent, so the tag reads as a tint and the letter on it in full colour.
    const byte TagAlpha = 0x1F;

    const string Field = "num";
    const string Bad = "bad";
    const string AtRest = "0.####";

    static readonly (string Letter, Color Color)[] s_letters =
        [("X", EditorColors.AxisX), ("Y", EditorColors.AxisY), ("Z", EditorColors.AxisZ)];

    readonly TextBox[] _axes;
    readonly Border[] _fields;
    readonly TextBlock _fault = Parts.FaultLine();
    Vector3? _shown;

    public VectorRow(string label, string unit = "", int axes = 3)
    {
        Label = label;
        _axes = [.. Enumerable.Range(0, axes).Select(_ => AxisBox())];
        _fields = new Border[axes];
        ColumnDefinitions = new ColumnDefinitions($"{LabelWidth},{LabelGap},*");
        RowDefinitions = new RowDefinitions($"{RowHeight},Auto");
        // Styles, not local values, so focus can light the edge of the field that holds the caret. Bad text comes
        // last, so its red wins over the caret's light.
        Styles.Add(new Style(x => x.OfType<Border>().Class(Field)) { Setters = { new Setter(Border.BorderBrushProperty, EditorBrushes.Line) } });
        Styles.Add(new Style(x => x.OfType<Border>().Class(Field).Class(":focus-within")) { Setters = { new Setter(Border.BorderBrushProperty, EditorBrushes.Accent) } });
        Styles.Add(new Style(x => x.OfType<Border>().Class(Field).Class(Bad)) { Setters = { new Setter(Border.BorderBrushProperty, EditorBrushes.Fault) } });
        // The field draws the edge, so the box drops the 2 pixel ring the theme gives a focused text box.
        Styles.Add(new Style(x => x.OfType<TextBox>().Class(":focus").Template().OfType<Border>().Name("PART_BorderElement")) { Setters = { new Setter(Border.BorderThicknessProperty, new Thickness(0)) } });
        Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 12,
            Foreground = EditorBrushes.TextSoft,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        });
        var cells = new Grid { ColumnDefinitions = new ColumnDefinitions(string.Join(',', Enumerable.Repeat("*", axes))), ColumnSpacing = FieldGap, VerticalAlignment = VerticalAlignment.Center };
        for (var i = 0; i < _axes.Length; i++)
        {
            var axis = i;
            var box = _axes[i];
            box.GotFocus += (_, _) => ShowAxis(axis);
            box.LostFocus += (_, _) => Commit(axis);
            box.KeyDown += (_, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    Commit(axis);
                }
            };
            _fields[i] = Cell(i, unit);
            SetColumn(_fields[i], i);
            cells.Children.Add(_fields[i]);
        }
        SetColumn(cells, 2);
        _fault.Text = "Type a number.";
        _fault.Margin = new Thickness(0, 0, 0, Gaps.Tight);
        SetColumn(_fault, 2);
        SetRow(_fault, 1);
        Children.AddRange([cells, _fault]);
        Show(null);
    }

    /// <summary>Raised with the value the user typed. The row already shows it. The owner may show another.</summary>
    public event Action<Vector3>? Entered;

    internal string Label { get; }

    internal IReadOnlyList<TextBox> Axes => _axes;

    internal IReadOnlyList<Border> Fields => _fields;

    internal TextBlock Fault => _fault;

    /// <summary>Shows a value in the boxes and clears any fault. Null empties and disables them. A row of two shows x and y. A NaN axis shows Mixed.</summary>
    public void Show(Vector3? value)
    {
        _shown = value;
        for (var i = 0; i < _axes.Length; i++)
        {
            _axes[i].IsEnabled = value is not null;
            ShowFault(i, false);
            ShowAxis(i);
        }
    }

    // In full while the box has the caret, so an edit starts from the real value.
    void ShowAxis(int axis)
    {
        var box = _axes[axis];
        var mixed = _shown is { } value && float.IsNaN(value[axis]);
        box.Text = _shown is { } shown && !mixed
            ? box.IsFocused ? shown[axis].ToString(CultureInfo.InvariantCulture) : AtRestText(shown[axis])
            : "";
        box.PlaceholderText = mixed ? "Mixed" : null;
    }

    // The format writes a tiny negative value as -0, which reads as a fault.
    internal static string AtRestText(double value) => Math.Round(value, 4) == 0 ? "0" : value.ToString(AtRest, CultureInfo.InvariantCulture);

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
        Scrub.Attach(tag, _axes[index], () => Commit(index), whole: false);
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

    // One axis at a time, because the other boxes may hold rounded text.
    void Commit(int axis)
    {
        // A Mixed axis left empty keeps each entity's own value.
        if (_shown is not { } shown || (float.IsNaN(shown[axis]) && string.IsNullOrEmpty(_axes[axis].Text)))
        {
            return;
        }
        // NaN and infinity parse as numbers but would corrupt a transform.
        if (!float.TryParse(_axes[axis].Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var typed) || !float.IsFinite(typed))
        {
            ShowFault(axis, true);
            return;
        }
        ShowFault(axis, false);
        if (typed == shown[axis])
        {
            ShowAxis(axis);
            return;
        }
        shown[axis] = typed;
        _shown = shown;
        Entered?.Invoke(shown);
    }

    void ShowFault(int axis, bool bad)
    {
        _fields[axis].Classes.Set(Bad, bad);
        _fault.IsVisible = _fields.Any(field => field.Classes.Contains(Bad));
    }

    // The field draws the edge and the fill, so the box has neither.
    static TextBox AxisBox() => new()
    {
        MinHeight = 0,
        MinWidth = 0,
        Padding = new Thickness(ValueIndent, 0),
        BorderThickness = new Thickness(0),
        Background = Brushes.Transparent,
        FontFamily = EditorTheme.Mono,
        VerticalContentAlignment = VerticalAlignment.Center,
    };
}
