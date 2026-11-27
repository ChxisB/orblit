using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Shell;

/// <summary>
/// A tab of a group of panels. It can move to another group, and takes its panel and its menu item with it. In a group
/// too narrow for it, the strip's More menu holds it, and its menu item chooses it.
/// </summary>
internal sealed class DockTab : Border, IOverflowTool
{
    internal const double TabHeight = 26;

    readonly TextBlock _title;
    readonly Button _choose = new();

    internal DockTab(DockPage page)
    {
        Page = page;
        _title = new TextBlock { Text = page.Title, FontWeight = FontWeight.Medium, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        Height = TabHeight;
        CornerRadius = EditorTheme.Corners;
        Padding = new Thickness(Gaps.Item, 0);
        Child = _title;
        _choose.Click += (_, _) => Owner.Select(this);
        PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                Owner.Select(this);
            }
        };
        Item = Parts.ActionItem(page.Title, () => Owner.Toggle(this), note: EditorMenu.PanelNote(page.Title));
        Item.ToggleType = MenuItemToggleType.CheckBox;
        Item.Bind(MenuItem.IsCheckedProperty, this.GetObservable(IsVisibleProperty));
    }

    internal DockPage Page { get; }

    /// <summary>The group that holds the tab. A group sets it when it takes the tab, before the tab shows.</summary>
    internal DockTabs Owner { get; set; } = null!;

    /// <summary>The tab's item in its group's menu, which shows and hides it.</summary>
    internal MenuItem Item { get; }

    string IOverflowTool.MenuWords => Page.Title;

    Button IOverflowTool.MenuButton => _choose;

    internal void Light(bool chosen)
    {
        Background = chosen ? EditorBrushes.Line : Brushes.Transparent;
        _title.Foreground = chosen ? EditorBrushes.Text : EditorBrushes.TextDim;
    }
}
