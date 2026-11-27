using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Shell;

/// <summary>
/// One side column of a workspace. A button in its header folds it to a rail at the window's edge, and the rail's button
/// brings it back. The parent is a <see cref="Grid"/>, whose column it narrows.
/// </summary>
internal sealed class FoldingColumn : Panel
{
    readonly Control _body;
    readonly Border _rail;
    // The grid column's width and limits while it is folded.
    (GridLength Width, double Least, double Most) _open;

    /// <param name="title">Names the column on its rail button and in the saved layout.</param>
    /// <param name="icon">The rail button's icon.</param>
    /// <param name="side">The window's edge the column is on.</param>
    /// <param name="body">Builds the column, and puts the collapse button it is given in a header.</param>
    public FoldingColumn(string title, string icon, Dock side, Func<Button, Control> body)
    {
        Title = title;
        CollapseButton = Parts.Dress(new Button(), side == Dock.Left ? "Back" : "Forward", new HoverCard("Collapse to the side", "Folds this column to a button at the window's edge."));
        RailButton = Parts.Dress(new Button { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top }, icon, new HoverCard(title, "Click to bring this column back."));
        CollapseButton.Click += (_, _) => Folded = true;
        RailButton.Click += (_, _) => Folded = false;
        _body = body(CollapseButton);
        _rail = new Border { Classes = { EditorTheme.Island }, Padding = new Thickness(0, Gaps.Item), IsVisible = false, Child = RailButton };
        Children.AddRange([_body, _rail]);
    }

    /// <summary>Raised when the column folds or comes back.</summary>
    public event Action? Changed;

    internal string Title { get; }

    internal Button CollapseButton { get; }

    internal Button RailButton { get; }

    internal bool Folded
    {
        get => _rail.IsVisible;
        set
        {
            if (value == Folded)
            {
                return;
            }

            var column = ((Grid)Parent!).ColumnDefinitions[Grid.GetColumn(this)];
            if (value)
            {
                _open = (column.Width, column.MinWidth, column.MaxWidth);
            }

            (column.Width, column.MinWidth, column.MaxWidth) = value ? (new GridLength(DockSide.RailWidth), DockSide.RailWidth, DockSide.RailWidth) : _open;
            _body.IsVisible = !value;
            _rail.IsVisible = value;
            Changed?.Invoke();
        }
    }
}
