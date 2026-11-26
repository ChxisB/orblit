using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Shell;

/// <summary>The tab of one open graph script: its file name, which lights when it is the one on show, and a close button.</summary>
internal sealed class GraphTab : Border, IOverflowTool
{
    // Wide enough for most file names. A longer one is cut, and the tab's hover card gives its whole path.
    const double Widest = 200;

    readonly TextBlock _label = new() { VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
    readonly string _name;
    readonly Button _choose;
    readonly Button _close = Parts.Tight(Parts.IconButton("GuiClose", "Close the graph script"));

    public GraphTab(string script)
    {
        Script = script;
        _name = Path.GetFileName(script);
        _choose = new Button { Content = _label, Padding = new Thickness(Gaps.Item, Gaps.Tight), MinHeight = 0, BorderThickness = new Thickness(0), Background = null };
        _choose.Click += (_, _) => Chosen?.Invoke();
        _close.Click += (_, _) => CloseRequested?.Invoke();
        ToolTip.SetTip(this, script);
        MaxWidth = Widest;
        CornerRadius = EditorTheme.Corners;
        BorderThickness = new Thickness(1);
        _close.Margin = new Thickness(Gaps.Joined, 0, 0, 0);
        DockPanel.SetDock(_close, Dock.Right);
        Child = new DockPanel { Children = { _close, _choose } };
        Show(false, false);
    }

    /// <summary>Raised when the name is pressed.</summary>
    public event Action? Chosen;

    /// <summary>Raised when the close button is pressed.</summary>
    public event Action? CloseRequested;

    /// <summary>The res:// path of the file the tab stands for.</summary>
    public string Script { get; }

    internal TextBlock Label => _label;

    internal Button ChooseButton => _choose;

    internal Button CloseButton => _close;

    string IOverflowTool.MenuWords => _label.Text!;

    Button IOverflowTool.MenuButton => _choose;

    /// <summary>Lights the tab when it is on show, and marks the name when the file has edits.</summary>
    public void Show(bool selected, bool edited)
    {
        _label.Text = edited ? $"{_name} *" : _name;
        _label.Foreground = selected ? EditorBrushes.Text : EditorBrushes.TextDim;
        Background = selected ? EditorBrushes.Raised : null;
        BorderBrush = selected ? EditorBrushes.LineStrong : null;
    }
}
