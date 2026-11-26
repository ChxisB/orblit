using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Shell;

/// <summary>
/// A foldable group of inspector rows: an arrow and a title over a body.
/// </summary>
internal sealed class PropertyGroup : StackPanel
{
    const double HeaderHeight = 24.5;

    readonly Control _body;
    readonly EditorIcon _right = new("GuiTreeArrowRight");
    readonly EditorIcon _down = new("GuiTreeArrowDown");

    public PropertyGroup(string title, Control body)
    {
        Title = title;
        _body = body;
        Header = new Button
        {
            Height = HeaderHeight,
            MinHeight = 0,
            MinWidth = 0,
            Padding = new Thickness(0),
            BorderThickness = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            VerticalContentAlignment = VerticalAlignment.Center,
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = Gaps.Joined,
                Children =
                {
                    new Grid { Children = { _right, _down } },
                    new TextBlock { Text = title, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center },
                },
            },
        };
        Header.Click += (_, _) => Fold(!IsOpen);
        Children.AddRange([Header, body]);
        Fold(true);
    }

    internal string Title { get; }

    internal Button Header { get; }

    internal Control Body => _body;

    internal bool IsOpen => _body.IsVisible;

    internal void Fold(bool open)
    {
        _body.IsVisible = open;
        _down.IsVisible = open;
        _right.IsVisible = !open;
    }
}
