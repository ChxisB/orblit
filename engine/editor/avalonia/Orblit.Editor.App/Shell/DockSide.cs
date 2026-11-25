using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Shell;

/// <summary>
/// One side of the window's body: its docks, or a narrow rail with a button for each dock when the side is folded.
/// A workspace that needs the width, such as Script, folds the sides. A press on a rail button opens the side again
/// until another workspace is chosen.
/// </summary>
internal sealed class DockSide : Panel
{
    internal const double RailWidth = 44;

    readonly Control _docks;
    readonly Func<IEnumerable<DockTabs>> _onSide;
    readonly StackPanel _buttons = new() { Spacing = 4, HorizontalAlignment = HorizontalAlignment.Center };
    GridLength _width;

    /// <param name="docks">What the side shows when it is open.</param>
    /// <param name="onSide">The docks on the side now, because a dock can move to the other side.</param>
    public DockSide(Control docks, Func<IEnumerable<DockTabs>> onSide)
    {
        _docks = docks;
        _onSide = onSide;
        Rail = new Border { Classes = { EditorTheme.Island }, Padding = new Thickness(0, 8), IsVisible = false, Child = _buttons };
        Children.Add(_docks);
        Children.Add(Rail);
    }

    /// <summary>The splitter between this side and the middle. It hides while the side is folded.</summary>
    internal GridSplitter Splitter { get; } = new() { Background = Brushes.Transparent, ResizeDirection = GridResizeDirection.Columns };

    internal Border Rail { get; }

    internal bool IsFolded => Rail.IsVisible;

    /// <summary>The rail's buttons, one for each dock on the side, in order.</summary>
    internal IEnumerable<Button> RailButtons => _buttons.Children.OfType<Button>();

    /// <summary>Folds the side to its rail, or opens it at the width it had.</summary>
    public void Fold(bool fold)
    {
        if (fold == IsFolded)
        {
            return;
        }

        // The window puts the side in a column of the body grid.
        var column = ((Grid)Parent!).ColumnDefinitions[Grid.GetColumn(this)];
        if (fold)
        {
            _width = column.Width;
            column.Width = new GridLength(RailWidth);
            _buttons.Children.Clear();
            _buttons.Children.AddRange(_onSide().Where(dock => dock.IsVisible).Select(RailButton));
        }
        else
        {
            column.Width = _width;
        }

        _docks.IsVisible = !fold;
        Rail.IsVisible = fold;
        Splitter.IsVisible = !fold;
    }

    Button RailButton(DockTabs dock)
    {
        var button = new HoverCard(dock.Title, "Folded to make room. Click to open this side.").On(Parts.IconButton(dock.Icon, ""));
        button.Click += (_, _) => Fold(false);
        return button;
    }
}
