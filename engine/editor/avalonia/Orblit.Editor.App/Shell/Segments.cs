using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Shell;

/// <summary>A segmented choice: a row of words on the field tone, with the chosen one lifted.</summary>
internal sealed class Segments : Border
{
    readonly Choice[] _choices;
    int _selected;

    public Segments(params string[] names)
    {
        _choices = [.. names.Select(name => new Choice { Content = name, Classes = { EditorTheme.TextButton, EditorTheme.Ghost, EditorTheme.Small, EditorTheme.Segment } })];
        _choices[0].IsChecked = true;
        // The new choice says it is checked before the group turns the old one off, so the index is kept here.
        foreach (var (choice, at) in _choices.Select((choice, at) => (choice, at)))
        {
            choice.IsCheckedChanged += (_, _) =>
            {
                if (choice.IsChecked == true)
                {
                    _selected = at;
                    Changed?.Invoke();
                }
            };
        }
        Padding = new Thickness(2);
        Background = EditorBrushes.Field;
        BorderBrush = EditorBrushes.Line;
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(8);
        HorizontalAlignment = HorizontalAlignment.Left;
        // Radio buttons with one parent and no group name turn each other off.
        var row = new WrapPanel { ItemSpacing = 2, LineSpacing = 2 };
        row.Children.AddRange(_choices);
        Child = row;
    }

    /// <summary>Raised when another word is chosen.</summary>
    public event Action? Changed;

    public int SelectedIndex
    {
        get => _selected;
        set => _choices[value].IsChecked = true;
    }

    internal IReadOnlyList<RadioButton> Choices => _choices;

    // A radio choice drawn as a worded button, so a check moves between words that share one look.
    sealed class Choice : RadioButton
    {
        protected override Type StyleKeyOverride => typeof(Button);
    }
}
