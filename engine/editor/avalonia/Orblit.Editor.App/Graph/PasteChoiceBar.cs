using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Orblit.Editor.App.Theme;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App.Graph;

/// <summary>
/// The question a paste asks when the steps carry a name the script already has with another shape. It shows
/// while the clipboard waits for the answer, and the three buttons are the answers.
/// </summary>
internal sealed class PasteChoiceBar : Border
{
    readonly TextBlock _text = new() { TextWrapping = Avalonia.Media.TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center, FontSize = 12 };

    public PasteChoiceBar(GraphClipboard clipboard)
    {
        IsVisible = false;
        Background = EditorBrushes.Panel;
        BorderBrush = EditorBrushes.Accent;
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(4);
        Padding = new Thickness(10, 4);
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Top;
        Margin = new Thickness(12);
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        row.Children.Add(_text);
        row.Children.Add(PanelFields.Small("paste:keep_both", "Keep both", () => clipboard.Resolve(GraphPasteChoice.KeepBoth)));
        row.Children.Add(PanelFields.Small("paste:reuse", "Reuse", () => clipboard.Resolve(GraphPasteChoice.Reuse)));
        row.Children.Add(PanelFields.Small("paste:cancel", "Cancel", clipboard.Cancel));
        Child = row;
        clipboard.Conflicted += Ask;
        clipboard.ChoiceClosed += () => IsVisible = false;
    }

    /// <summary>The question on show, or empty when there is none.</summary>
    internal string Message => IsVisible ? _text.Text ?? "" : "";

    void Ask(IReadOnlyList<GraphPasteClash> clashes)
    {
        var names = string.Join(", ", clashes.Select(clash => $"{clash.Kind} {clash.Name}"));
        _text.Text = $"This script has {names} with another type. Keep both renames the pasted ones. Reuse points the steps at the script's.";
        IsVisible = true;
    }
}
