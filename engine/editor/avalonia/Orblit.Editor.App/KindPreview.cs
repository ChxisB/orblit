using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using Orblit.Editor.App.Shell;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App;

/// <summary>
/// The side of the Add a thing dialog that tells about the lit kind: its icon, its plain name, the engine's name,
/// what it does and what it is good for. With nothing lit, it says what to do.
/// </summary>
internal sealed class KindPreview : Border
{
    internal const string NothingLit = "Pick a thing to see what it does.";

    // The engine has no summary of a class the editor can read, so a class with no plain name says only this.
    internal const string NoRow = "One of the engine's own kinds, for experts.";

    readonly Border _picture = new()
    {
        Height = 110,
        CornerRadius = new CornerRadius(10),
        Background = EditorBrushes.Raised,
    };

    readonly TextBlock _engine = new() { FontFamily = EditorTheme.Mono, FontSize = 10.5, Foreground = EditorBrushes.Unit };

    public KindPreview()
    {
        Width = 250;
        Padding = new Thickness(Gaps.Dialog);
        BorderBrush = EditorBrushes.Line;
        BorderThickness = new Thickness(1, 0, 0, 0);
        Child = new StackPanel { Spacing = Gaps.Item, Children = { _picture, Title, _engine, About, GoodFor } };
    }

    internal TextBlock Title { get; } = new() { FontSize = 16, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap };

    internal TextBlock About { get; } = new() { FontSize = 13, Foreground = EditorBrushes.TextSoft, TextWrapping = TextWrapping.Wrap };

    internal TextBlock GoodFor { get; } = new() { FontSize = 13, Foreground = EditorBrushes.TextDim, TextWrapping = TextWrapping.Wrap };

    /// <summary>Tells about the kind of this class, named as the list names it. None says what to do.</summary>
    public void Show(string? className, string name)
    {
        var row = className is null ? null : PlainNames.Of(className);
        _picture.Child = className is null ? null : Icon(className);
        _picture.IsVisible = Title.IsVisible = className is not null;
        Title.Text = name;
        _engine.Text = className;
        _engine.IsVisible = className is not null && className != name;
        About.Text = className is null ? NothingLit : row?.About ?? NoRow;
        GoodFor.Text = row is null ? "" : $"Good for: {row.GoodFor}";
        GoodFor.IsVisible = row is not null;
    }

    static EditorIcon Icon(string className)
    {
        var icon = new EditorIcon(EditorIcons.ForClass(className)) { Width = 32, Height = 32, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        TextElement.SetForeground(icon, EditorBrushes.Accent);
        return icon;
    }
}
