using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Layout;
using Orblit.Editor.App.Theme;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App.Shell;

/// <summary>
/// One row of the scene tree: the class icon, the name with the filter's match marked, its kind in plain words,
/// the class name when the pane shows it, a lock and a group mark when the entity has those flags, and, for a planar or spatial entity,
/// an eye. A hidden entity is dimmed and shows a closed eye. The name can be edited in the row.
/// </summary>
internal sealed class OutlineRow : Grid
{
    // A row is 23 pixels high, so the eye button is tighter than a toolbar one. Its icon ends 9 pixels
    // in from the right edge of the dock: 2 pixels of padding and 6 of margin, and the tree item adds 1.
    const double EyePadding = 2;
    const double EyeMargin = 6;

    // A hidden row is dimmed, not grayed out, so the name stays readable.
    const double HiddenOpacity = 0.5;

    readonly OutlineNode _entity;
    readonly StackPanel _label;

    public OutlineRow(OutlineNode entity, Action<OutlineNode> eyeClicked, string filter, bool showClassName)
    {
        _entity = entity;
        ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto");
        HorizontalAlignment = HorizontalAlignment.Stretch;
        Margin = new Thickness(-EditorTheme.RowOverhang, 0, 0, 0);
        var icon = Parts.RowIcon(EditorIcons.ForClass(entity.ClassName));
        var name = MarkedName(entity.Name, filter.Trim());
        var label = _label = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        label.Children.Add(name);
        if (PlainNames.Of(entity.ClassName) is { } kind)
        {
            label.Children.Add(new TextBlock { Text = kind.Name, FontSize = 11, Foreground = EditorBrushes.TextDim, VerticalAlignment = VerticalAlignment.Center });
        }

        if (showClassName)
        {
            label.Children.Add(new TextBlock { Text = entity.ClassName, FontFamily = EditorTheme.Mono, FontSize = 10.5, Foreground = EditorBrushes.Unit, VerticalAlignment = VerticalAlignment.Center });
        }

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
            var eye = Parts.IconButton(entity.Hidden ? "GuiVisibilityHidden" : "GuiVisibilityVisible", "");
            var says = entity.Hidden ? "Shows it again, in the view and the game." : "Hides it in the view and the game. It stays in the scene.";
            new HoverCard(entity.Hidden ? "Show" : "Hide", says).On(eye);
            eye.Click += (_, _) => eyeClicked(entity);
            eye.Padding = new Thickness(EyePadding);
            eye.BorderThickness = new Thickness(0);
            eye.Margin = new Thickness(0, 0, EyeMargin, 0);
            SetColumn(eye, 2);
            Children.Add(eye);
        }
    }

    /// <summary>The box the name is being edited in, or null when the row is not renaming.</summary>
    internal TextBox? Editing { get; private set; }

    /// <summary>
    /// Swaps the name for a box holding it, with the keys that keep or cancel beside it. Enter or leaving the box
    /// keeps the new name, Escape keeps the old one. An unchanged name is not passed on.
    /// </summary>
    public void Rename(Action<string> keep)
    {
        var box = Editing = new TextBox
        {
            Text = _entity.Name,
            Height = 22,
            MinHeight = 0,
            Padding = new Thickness(6, 0),
            FontFamily = EditorTheme.Mono,
            FontSize = 12,
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        var keys = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(6, 0), Children = { Parts.KeyHint("keep", "↵"), Parts.KeyHint("cancel", "esc") } };
        DockPanel.SetDock(keys, Dock.Right);
        var editor = new DockPanel { Children = { keys, box } };
        var done = false;
        void Finish(bool keepIt)
        {
            if (done)
            {
                return;
            }

            done = true;
            Children.Remove(editor);
            Editing = null;
            _label.IsVisible = true;
            if (keepIt && box.Text is { Length: > 0 } text && text != _entity.Name)
            {
                keep(text);
            }
        }

        box.KeyDown += (_, e) =>
        {
            if (e.Key is Key.Enter or Key.Escape)
            {
                e.Handled = true;
                Finish(e.Key == Key.Enter);
            }
        };
        box.LostFocus += (_, _) => Finish(true);
        _label.IsVisible = false;
        SetColumn(editor, 1);
        SetColumnSpan(editor, 2);
        Children.Add(editor);
        box.Focus();
        box.SelectAll();
    }

    // The part of the name the filter matched takes the soft accent, so the reader sees why the row is shown.
    static TextBlock MarkedName(string name, string filter)
    {
        var text = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        var at = filter == "" ? -1 : name.IndexOf(filter, StringComparison.OrdinalIgnoreCase);
        if (at < 0)
        {
            text.Text = name;
            return text;
        }

        text.Inlines = new InlineCollection
        {
            new Run(name[..at]),
            new Run(name.Substring(at, filter.Length)) { Background = EditorBrushes.AccentSoft, Foreground = EditorBrushes.Accent },
            new Run(name[(at + filter.Length)..]),
        };
        return text;
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
