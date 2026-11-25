using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Layout;
using Orblit.Editor.App.Theme;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App.Shell;

/// <summary>
/// The strip over the viewport: one tab for the open scene, a plus, and Hide panels at the right end. The tab is
/// raised on a fine edge with its icon in the accent, like a chosen dock tab. A right-click on the tab
/// makes, plays, closes and finds the scene.
/// </summary>
internal sealed class SceneTabStrip : Grid
{
    readonly Border _tab = new()
    {
        Height = 26,
        CornerRadius = EditorTheme.Corners,
        Padding = new Thickness(10, 0, 0, 0),
        Background = EditorBrushes.Raised,
        BorderBrush = EditorBrushes.LineStrong,
        BorderThickness = new Thickness(1),
        VerticalAlignment = VerticalAlignment.Center,
        IsVisible = false,
    };
    readonly ContentControl _icon = new();
    readonly TextBlock _name = new() { VerticalAlignment = VerticalAlignment.Center };
    readonly Button _close = Parts.Tight(Parts.IconButton("GuiClose", "Close the scene"));
    readonly Button _add = Parts.IconButton("Add", "Make a new scene");
    readonly Button _panels = new() { Height = 26, MinHeight = 0, Padding = new Thickness(8, 0), BorderThickness = new Thickness(0) };
    readonly TextBlock _panelsWord;
    readonly HoverCard _hide;
    readonly HoverCard _show;
    readonly MenuItem _find;

    public SceneTabStrip(Button runScene, KeyModifiers command)
    {
        ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*,Auto");
        Margin = new Thickness(6, 0);
        TextElement.SetForeground(_icon, EditorBrushes.Accent);
        TextElement.SetForeground(_close, EditorBrushes.TextDim);
        var key = Parts.KeyText(new KeyGesture(Key.OemPipe, command));
        _hide = new HoverCard(HidePanels, "The view fills the window. Panels wait one key away.", key);
        _show = new HoverCard(ShowPanels, "Brings the panels back as they were.", key);
        var keyed = Parts.Keyed(HidePanels, key);
        _panelsWord = (TextBlock)keyed.Children[0];
        _panels.Content = keyed;
        _hide.On(_panels);
        _tab.Child = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 7,
            Children = { _icon, _name, _close },
        };
        _add.Margin = new Thickness(4, 0, 0, 0);
        _add.VerticalAlignment = VerticalAlignment.Center;
        _panels.VerticalAlignment = VerticalAlignment.Center;
        Place(_tab, 0);
        Place(_add, 1);
        Place(_panels, 3);
        _find = Parts.PressItem(ShowInFiles, ShowInFilesButton);
        _tab.ContextMenu = new ContextMenu
        {
            Items =
            {
                Parts.PressItem("New scene…", _add, new KeyGesture(Key.N, command)),
                Parts.PressItem("Play this scene", runScene, new KeyGesture(Key.R, command)),
                Parts.PressItem("Close", _close, new KeyGesture(Key.W, command), "Saves your changes first"),
                new Separator(),
                _find,
            },
        };
    }

    internal Border Tab => _tab;

    internal ContentControl Icon => _icon;

    internal TextBlock Label => _name;

    internal Button CloseButton => _close;

    internal Button AddButton => _add;

    /// <summary>Shows the open scene's file in the Files panel. The window does the finding.</summary>
    internal Button ShowInFilesButton { get; } = new();

    internal const string ShowInFiles = "Show in Files";

    internal const string HidePanels = "Hide panels";

    internal const string ShowPanels = "Show panels";

    internal Button PanelsButton => _panels;

    /// <summary>Names the panels button for what a press does: Show panels while every side is folded, else Hide panels.</summary>
    internal void ShowPanelsHidden(bool hidden)
    {
        _panelsWord.Text = hidden ? ShowPanels : HidePanels;
        (hidden ? _show : _hide).On(_panels);
    }

    /// <summary>
    /// Shows the open scene as a tab with its root's icon and this label, or no tab for none. Show in Files is on
    /// while the scene has a res:// <paramref name="file"/>.
    /// </summary>
    public void Show(OutlineNode? root, string label, string file)
    {
        _tab.IsVisible = _close.IsEnabled = root is not null;
        ShowInFilesButton.IsEnabled = file != "";
        _find.Header = Parts.Noted(ShowInFiles, file == "" ? "Save the scene first. Then it has a file." : $"Finds {Path.GetFileName(file)} in the Files panel");
        if (root is null)
        {
            return;
        }

        // The icon draws to its own size, so a stretch would squash it.
        _icon.Content = new EditorIcon(EditorIcons.ForClass(root.ClassName)) { VerticalAlignment = VerticalAlignment.Center };
        _name.Text = label;
    }

    void Place(Control child, int column)
    {
        SetColumn(child, column);
        Children.Add(child);
    }
}
