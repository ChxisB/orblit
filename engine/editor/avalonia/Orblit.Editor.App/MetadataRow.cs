using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Orblit.Editor.App.Theme;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App;

/// <summary>
/// One metadata value: its name, a box or a check box of the value's kind, and a button that removes it. A box
/// raises <see cref="Entered"/> when it holds a value of its kind that differs from the one shown. Any other text
/// is put back.
/// </summary>
internal sealed class MetadataRow : Grid
{
    const double RowHeight = 28;

    object _shown;

    public MetadataRow(MetaEntry entry)
    {
        Key = entry.Name;
        _shown = entry.Value;
        ColumnDefinitions = new ColumnDefinitions("*,*,Auto");
        Height = RowHeight;
        Margin = new Thickness(0, 4, 0, 0);
        RemoveButton = new Button
        {
            Height = RowHeight,
            Width = RowHeight,
            MinHeight = 0,
            MinWidth = 0,
            Padding = new Thickness(0),
            BorderThickness = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Content = new EditorIcon("Remove"),
        };
        RemoveButton.Click += (_, _) => Removed?.Invoke();
        Control editor = entry.Value is bool flag ? CheckBoxFor(flag) : BoxFor(entry.Value);
        SetColumn(editor, 1);
        SetColumn(RemoveButton, 2);
        Children.Add(new TextBlock { Text = entry.Name, Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
        Children.Add(editor);
        Children.Add(RemoveButton);
    }

    /// <summary>Raised with the value the user entered. The row already shows it.</summary>
    public event Action<object>? Entered;

    public event Action? Removed;

    internal string Key { get; }

    /// <summary>The box of a string or a number. Null for a bool.</summary>
    internal TextBox? Box { get; private set; }

    /// <summary>The check box of a bool. Null for any other kind.</summary>
    internal CheckBox? Check { get; private set; }

    internal Button RemoveButton { get; }

    CheckBox CheckBoxFor(bool value)
    {
        Check = new CheckBox { IsChecked = value, Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        Check.IsCheckedChanged += (_, _) => Entered?.Invoke(Check.IsChecked == true);
        return Check;
    }

    TextBox BoxFor(object value)
    {
        Box = new TextBox
        {
            Height = RowHeight,
            MinHeight = 0,
            Padding = new Thickness(8, 0, 0, 0),
            VerticalContentAlignment = VerticalAlignment.Center,
            Background = EditorBrushes.Field,
            BorderThickness = new Thickness(0),
            CornerRadius = EditorTheme.Corners,
            Text = Format(value),
        };
        Box.LostFocus += (_, _) => Commit();
        Box.KeyDown += (_, e) =>
        {
            if (e.Key == Avalonia.Input.Key.Enter)
            {
                Commit();
            }
        };
        return Box;
    }

    void Commit()
    {
        if (Parse(Box!.Text ?? "") is { } typed && !typed.Equals(_shown))
        {
            _shown = typed;
            Entered?.Invoke(typed);
            return;
        }
        Box.Text = Format(_shown);
    }

    // The kind stays the one the value had, so a whole number typed into a float is a float. NaN and infinity parse
    // but a scene file cannot hold them.
    object? Parse(string text) => _shown switch
    {
        string => text,
        long => long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var whole) ? whole : null,
        _ => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var real) && double.IsFinite(real) ? real : null,
    };

    static string Format(object value) => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
}
