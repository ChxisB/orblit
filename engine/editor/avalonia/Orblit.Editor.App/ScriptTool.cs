using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Orblit.Editor.App.Shell;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App;

/// <summary>
/// The Attach a script popover. It lists the project's C# scripts or its graph scripts, and Attach raises the
/// one chosen. A row shows the script's path, because the editor does not know the class a script extends.
/// </summary>
internal sealed class ScriptTool : StackPanel
{
    // The order is the order of the kinds in the row.
    static readonly string[] s_none = ["No C# scripts in the project.", "No graph scripts in the project."];

    readonly Segments _kind = new("C#", "Graph");
    readonly ListBox _list = new() { MaxHeight = 180, Classes = { EditorTheme.Field } };
    readonly TextBlock _none = new() { FontSize = 12, Foreground = EditorBrushes.TextDim, Margin = new Thickness(2, 4) };
    readonly Button _cancel = new() { Content = "Cancel", Classes = { EditorTheme.TextButton, EditorTheme.Ghost }, Margin = new Thickness(0, 0, 8, 0) };
    readonly Button _attach = new() { Content = "Attach", Classes = { EditorTheme.TextButton, EditorTheme.Primary }, IsEnabled = false };
    IReadOnlyList<string>[] _paths = [[], []];

    public ScriptTool()
    {
        Width = 320;
        Head = new CardHead("Attach a script", "");
        DockPanel.SetDock(_attach, Dock.Right);
        DockPanel.SetDock(_cancel, Dock.Right);
        Children.Add(Head);
        Children.Add(new Border
        {
            Padding = new Thickness(14, 12),
            Child = new StackPanel { Spacing = 10, Children = { _kind, _list, _none } },
        });
        Children.Add(new Border
        {
            Padding = new Thickness(12, 10),
            BorderBrush = EditorBrushes.Line,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Child = new DockPanel { LastChildFill = false, Children = { _attach, _cancel } },
        });
        _kind.Changed += ShowList;
        _list.SelectionChanged += (_, _) => _attach.IsEnabled = _list.SelectedItem is not null;
        _attach.Click += (_, _) =>
        {
            if (_list.SelectedItem is string path)
            {
                Requested?.Invoke(path);
            }
        };
        Head.CloseButton.Click += (_, _) => Cancelled?.Invoke();
        _cancel.Click += (_, _) => Cancelled?.Invoke();
    }

    /// <summary>Raised with the res:// path of the script to attach.</summary>
    public event Action<string>? Requested;

    /// <summary>Raised by Cancel and by the cross.</summary>
    public event Action? Cancelled;

    internal CardHead Head { get; }

    internal Segments Kind => _kind;

    internal ListBox List => _list;

    internal TextBlock None => _none;


    internal Button CancelButton => _cancel;

    internal Button AttachButton => _attach;

    /// <summary>Names the entity that runs the script and lists the scripts of each kind. Nothing is chosen.</summary>
    public void Show(string entity, IReadOnlyList<string> scripts, IReadOnlyList<string> graphs)
    {
        Head.Why.Text = $"{entity} runs it.";
        _paths = [scripts, graphs];
        ShowList();
    }

    void ShowList()
    {
        var paths = _paths[_kind.SelectedIndex];
        _list.ItemsSource = paths;
        _list.SelectedIndex = -1;
        _list.IsVisible = paths.Count > 0;
        _none.Text = s_none[_kind.SelectedIndex];
        _none.IsVisible = paths.Count == 0;
    }
}
