using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Shell;

/// <summary>The tab of one open graph script: its file name, which lights when it is the one on show, and a close button.</summary>
internal sealed class GraphTab : Border
{
    readonly TextBlock _label = new() { VerticalAlignment = VerticalAlignment.Center };
    readonly string _name;
    readonly Button _choose;
    readonly Button _close = Parts.Tight(Parts.IconButton("GuiClose", "Close the graph script"));

    public GraphTab(string script)
    {
        Script = script;
        _name = Path.GetFileName(script);
        _choose = new Button { Content = _label, Padding = new Thickness(8, 4), MinHeight = 0, BorderThickness = new Thickness(0), Background = null };
        _choose.Click += (_, _) => Chosen?.Invoke();
        _close.Click += (_, _) => CloseRequested?.Invoke();
        ToolTip.SetTip(this, script);
        Child = new StackPanel { Orientation = Orientation.Horizontal, Children = { _choose, _close } };
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

    /// <summary>Lights the tab when it is on show, and marks the name when the file has edits.</summary>
    public void Show(bool selected, bool edited)
    {
        _label.Text = edited ? $"{_name} *" : _name;
        _label.Foreground = selected ? EditorBrushes.Accent : EditorBrushes.TabInactive;
        Background = selected ? EditorBrushes.Panel : null;
    }
}
