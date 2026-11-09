using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Shell;

/// <summary>
/// A foldable group of inspector rows: an arrow and a title over a body. A group with no body is shown before
/// its rows exist, so it stays closed and a click on it says so.
/// </summary>
internal sealed class PropertyGroup : StackPanel
{
    const double HeaderHeight = 24.5;

    readonly Control? _body;
    readonly EditorIcon _right = new("GuiTreeArrowRight");
    readonly EditorIcon _down = new("GuiTreeArrowDown");

    public PropertyGroup(string title, Control? body = null)
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
                Spacing = 2,
                Children =
                {
                    new Grid { Children = { _right, _down } },
                    new TextBlock { Text = title, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center },
                },
            },
        };
        Header.Click += (_, _) => Click();
        Children.Add(Header);
        if (body is not null)
        {
            Children.Add(body);
        }
        Fold(body is not null);
    }

    internal string Title { get; }

    internal Button Header { get; }

    internal bool IsOpen => _body is { IsVisible: true };

    // A body folds. A group with no body answers that it is not built.
    void Click()
    {
        if (_body is null)
        {
            Header.RaiseEvent(new UnbuiltEventArgs($"{Title} is not built yet."));
        }
        else
        {
            Fold(!IsOpen);
        }
    }

    void Fold(bool open)
    {
        if (_body is not null)
        {
            _body.IsVisible = open;
        }

        _down.IsVisible = open;
        _right.IsVisible = !open;
    }
}
