using System.Globalization;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Orblit.Editor.App.Theme;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App;

/// <summary>
/// One engine property in the inspector, with an editor of its kind: a switch, a list of an enum's choices, a number
/// box with its unit, a text box, a vector or a colour swatch with its hex. Any other kind shows read only.
/// A box raises <see cref="Entered"/> when it holds a value of its kind that differs from the one shown. Any other
/// text is put back. A drag across a number's label steps it. <see cref="Mixed"/> shows that the picked entities
/// differ, and a vector shows that per axis as NaN.
/// </summary>
internal sealed class PropertyRow : Border
{
    // Variant types and property hints, as the engine numbers them.
    const int Bool = 1;
    const int Int = 2;
    const int Float = 3;
    const int Text = 4;
    const int Vector2Type = 5;
    const int Vector3Type = 9;
    const int ColorType = 20;
    const int Object = 24;
    const int HintNone = 0;
    const int HintRange = 1;
    const int HintEnum = 2;
    const int HintColorNoAlpha = 21;

    const double LabelWidth = 96;
    const double LabelGap = 8;
    const double RowHeight = 30;
    const double FieldHeight = 28;

    // Words the engine's names spell in lower case that read as capitals.
    static readonly string[] s_capitals = ["fov", "id", "lod", "ui", "uv"];

    readonly EntityProperty _property;
    Action<object?> _show = _ => { };
    Action _commit = () => { };
    object? _shown;
    bool _showing;

    /// <summary>The value to show when the picked entities hold different values.</summary>
    internal static readonly object Mixed = new();

    public PropertyRow(EntityProperty property)
    {
        _property = property;
        Label = LabelOf(property);
        Editor = property switch
        {
            { Type: Bool } => Switch(),
            { Type: Int, Hint: HintEnum } => Choices(),
            { Type: Int or Float, Hint: HintNone or HintRange } => Number(),
            { Type: Text } => TextBox(),
            { Type: Vector2Type or Vector3Type } => Vector(),
            { Type: ColorType } => Swatch(),
            _ => ReadOnly(),
        };
        var label = new TextBlock { Text = Label, FontSize = 12, Foreground = EditorBrushes.TextSoft, Background = Brushes.Transparent, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
        // A vector row has its own label.
        Child = Editor is VectorRow ? Editor : Labeled(label, Editor);
        if (property.Type is Int or Float && Editor is TextBox box)
        {
            Scrub.Attach(label, box, _commit, property.Type == Int);
        }

        Show(property.Value);
    }

    /// <summary>Raised with the value the user entered. The row already shows it.</summary>
    public event Action<object>? Entered;

    internal string PropertyName => _property.Name;

    internal string Label { get; }

    /// <summary>The control that edits the value: a ToggleSwitch, a ComboBox, a TextBox, a VectorRow, a colour box or a TextBlock.</summary>
    internal Control Editor { get; }

    public void Show(object? value)
    {
        _shown = value;
        _showing = true;
        _show(value);
        _showing = false;
    }

    void Enter(object value)
    {
        if (!_showing && !value.Equals(_shown))
        {
            _shown = value;
            Entered?.Invoke(value);
        }
    }

    ToggleSwitch Switch()
    {
        var toggle = new ToggleSwitch { OnContent = null, OffContent = null, MinWidth = 0 };
        toggle.IsCheckedChanged += (_, _) => Enter(toggle.IsChecked == true);
        // A switch over entities that differ is neither on nor off.
        _show = value => toggle.IsChecked = value == Mixed ? null : value is true;
        return toggle;
    }

    // An enum names its choices in order, or gives each a number after a colon.
    ComboBox Choices()
    {
        var choices = _property.HintString.Split(',').Select((choice, at) => choice.Split(':') is [var name, var number] && long.TryParse(number, out var value)
            ? (Name: name.Trim(), Value: value)
            : (Name: choice.Trim(), Value: (long)at)).ToArray();
        var box = new ComboBox { ItemsSource = choices.Select(choice => choice.Name).ToArray(), HorizontalAlignment = HorizontalAlignment.Stretch, Height = FieldHeight, MinHeight = 0 };
        box.SelectionChanged += (_, _) =>
        {
            if (box.SelectedIndex >= 0)
            {
                Enter(choices[box.SelectedIndex].Value);
            }
        };
        _show = value =>
        {
            box.SelectedIndex = Array.FindIndex(choices, choice => value is long shown && choice.Value == shown);
            box.PlaceholderText = value == Mixed ? "Mixed" : null;
        };
        return box;
    }

    // A range's hint ends with its unit. An angle stored in radians shows in degrees.
    Control Number()
    {
        var hints = _property.HintString.Split(',');
        var radians = hints.Contains("radians_as_degrees");
        var unit = hints.Contains("degrees") || radians ? "°" : hints.FirstOrDefault(hint => hint.StartsWith("suffix:", StringComparison.Ordinal))?["suffix:".Length..] ?? "";
        var scale = radians ? 180 / Math.PI : 1;
        return Box(
            value => value switch
            {
                long whole => whole.ToString(CultureInfo.InvariantCulture),
                double real => VectorRow.AtRestText(real * scale),
                _ => "",
            },
            text => _property.Type == Int
                ? long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var whole) ? whole : null
                // NaN and infinity parse but a scene file cannot hold them.
                : double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var real) && double.IsFinite(real) ? real / scale : null,
            unit);
    }

    Control TextBox() => Box(value => value as string ?? "", text => text, "");

    VectorRow Vector()
    {
        var flat = _property.Type == Vector2Type;
        var unit = _property.HintString.StartsWith("suffix:", StringComparison.Ordinal) ? _property.HintString["suffix:".Length..] : "";
        var row = new VectorRow(Label, unit, flat ? 2 : 3);
        row.Entered += value => Enter(flat ? new Vector2(value.X, value.Y) : value);
        _show = value => row.Show(value switch
        {
            Vector2 shown => new Vector3(shown, 0),
            Vector3 shown => shown,
            _ => null,
        });
        return row;
    }

    // The colour is the engine's red, green, blue and alpha from 0 to 1. The hex is #rrggbb, with aa after it
    // when the alpha is not full, as the engine writes it.
    Control Swatch()
    {
        var swatch = new Border { Width = 18, Height = 18, CornerRadius = new CornerRadius(4), BorderBrush = EditorBrushes.LineStrong, BorderThickness = new Thickness(1), Margin = new Thickness(6, 0, 0, 0) };
        var box = Box(
            value => value is Vector4 color ? Hex(color) : "",
            ParseHex,
            "");
        var show = _show;
        _show = value =>
        {
            show(value);
            swatch.Background = value is Vector4 color ? new ImmutableSolidColorBrush(Avalonia.Media.Color.FromArgb(Byte(color.W), Byte(color.X), Byte(color.Y), Byte(color.Z))) : null;
        };
        DockPanel.SetDock(swatch, Dock.Left);
        return new DockPanel { Children = { swatch, box } };
    }

    TextBlock ReadOnly()
    {
        var text = new TextBlock { Foreground = EditorBrushes.TextDim, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        // An object shows the resource type it takes, a number or text its value, and any other kind its type.
        _show = value => text.Text = (_property.Type, value) switch
        {
            _ when value == Mixed => "Mixed",
            (Object, null) => "<empty>",
            (Object, _) => _property.HintString.Split(',')[0],
            (_, long whole) => whole.ToString(CultureInfo.InvariantCulture),
            (_, double real) => VectorRow.AtRestText(real),
            (_, string words) => words,
            _ => VariantTypes.Names[_property.Type],
        };
        return text;
    }

    // A text box that commits on Enter and when it loses the caret. Text that does not parse is put back. Text
    // still as shown commits nothing, so a box passed through keeps a value it shows rounded, and Mixed stays.
    TextBox Box(Func<object?, string> format, Func<string, object?> parse, string unit)
    {
        var box = new TextBox
        {
            Height = FieldHeight,
            MinHeight = 0,
            VerticalContentAlignment = VerticalAlignment.Center,
            FontFamily = _property.Type == Text ? FontFamily.Default : EditorTheme.Mono,
            InnerRightContent = unit.Length == 0 ? null : new TextBlock { Text = unit, Foreground = EditorBrushes.Unit, FontSize = 11, Margin = new Thickness(0, 0, 7, 0), VerticalAlignment = VerticalAlignment.Center },
            Classes = { EditorTheme.Field },
        };
        void Commit()
        {
            if (box.Text != format(_shown) && parse(box.Text ?? "") is { } typed)
            {
                Enter(typed);
            }
            box.Text = format(_shown);
        }
        box.LostFocus += (_, _) => Commit();
        box.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                Commit();
            }
        };
        _show = value =>
        {
            box.Text = format(value);
            box.PlaceholderText = value == Mixed ? "Mixed" : null;
        };
        _commit = Commit;
        return box;
    }

    // Avalonia reads eight digits as alpha first, so the engine's order is parsed here.
    object? ParseHex(string text)
    {
        var hex = text.Trim().TrimStart('#');
        if (hex.Length is not (6 or 8) || !uint.TryParse(hex, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var packed))
        {
            return null;
        }
        var rgba = hex.Length == 6 ? packed << 8 | 0xFF : packed;
        // A colour with no alpha keeps it full.
        rgba |= _property.Hint == HintColorNoAlpha ? 0xFFu : 0;
        return new Vector4(rgba >> 24, (rgba >> 16) & 0xFF, (rgba >> 8) & 0xFF, rgba & 0xFF) / 255;
    }

    static string Hex(Vector4 color) =>
        $"#{Byte(color.X):x2}{Byte(color.Y):x2}{Byte(color.Z):x2}{(Byte(color.W) == 255 ? "" : Byte(color.W).ToString("x2", CultureInfo.InvariantCulture))}";

    static byte Byte(float part) => (byte)Math.Clamp(MathF.Round(part * 255), 0, 255);

    static Grid Labeled(TextBlock label, Control editor)
    {
        Grid.SetColumn(editor, 2);
        editor.VerticalAlignment = VerticalAlignment.Center;
        return new Grid
        {
            Height = RowHeight,
            ColumnDefinitions = new ColumnDefinitions($"{LabelWidth},{LabelGap},*"),
            Children = { label, editor },
        };
    }

    // The engine's name in words, without its group's prefix: run_mode in the Run group reads Mode.
    static string LabelOf(EntityProperty property)
    {
        var prefix = property.Group.ToLowerInvariant().Replace(' ', '_') + "_";
        var name = property.Group.Length > 0 && property.Name.Length > prefix.Length && property.Name.StartsWith(prefix, StringComparison.Ordinal) ? property.Name[prefix.Length..] : property.Name;
        return string.Join(' ', name.Split('_', StringSplitOptions.RemoveEmptyEntries)
            .Select(word => s_capitals.Contains(word) ? word.ToUpperInvariant() : char.ToUpperInvariant(word[0]) + word[1..]));
    }
}
