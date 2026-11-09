using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Orblit.Editor.App.Theme;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App.Shell;

/// <summary>
/// One row of the scene tree: the class icon, the name with a lock and a group mark when the entity has those
/// flags, and, for a planar or spatial entity, an eye. A hidden entity is dimmed and shows a closed eye.
/// </summary>
internal sealed class OutlineRow : Grid
{
    // A row is 23 pixels high, so the eye button is tighter than a toolbar one. Its icon ends 9 pixels
    // in from the right edge of the dock: 2 pixels of padding and 6 of margin, and the tree item adds 1.
    const double EyePadding = 2;
    const double EyeMargin = 6;

    // A hidden row is dimmed, not greyed out, so the name stays readable.
    const double HiddenOpacity = 0.5;

    public OutlineRow(OutlineNode entity, Action<OutlineNode> eyeClicked)
    {
        ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto");
        HorizontalAlignment = HorizontalAlignment.Stretch;
        Margin = new Thickness(-EditorTheme.RowOverhang, 0, 0, 0);
        var icon = Parts.RowIcon(EditorIcons.ForClass(entity.ClassName));
        var name = new TextBlock { Text = entity.Name, VerticalAlignment = VerticalAlignment.Center };
        var label = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        label.Children.Add(name);
        AddMark(label, entity.Locked, "Lock");
        AddMark(label, entity.Grouped, "Group");
        SetColumn(label, 1);
        if (entity.Hidden)
        {
            icon.Opacity = HiddenOpacity;
            name.Opacity = HiddenOpacity;
        }
        var highlight = new RowHighlight();
        SetColumnSpan(highlight, 3);
        Children.Add(highlight);
        Children.Add(icon);
        Children.Add(label);
        if (OutlineNode.IsPlaced(entity.ClassName))
        {
            var eye = Parts.IconButton(entity.Hidden ? "GuiVisibilityHidden" : "GuiVisibilityVisible", "Toggle visibility");
            eye.Click += (_, _) => eyeClicked(entity);
            eye.Padding = new Thickness(EyePadding);
            eye.BorderThickness = new Thickness(0);
            eye.Margin = new Thickness(0, 0, EyeMargin, 0);
            SetColumn(eye, 2);
            Children.Add(eye);
        }
    }

    // A mark is an icon, not a button: the toolbar is where a flag changes.
    static void AddMark(StackPanel label, bool shown, string icon)
    {
        if (shown)
        {
            label.Children.Add(new EditorIcon(icon));
        }
    }
}
