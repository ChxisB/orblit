using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Shell;

/// <summary>
/// The log of the engine and of the games it ran, on an island of its own: a strip with the title, Copy and Clear
/// over the lines. It only shows the lines it is given and asks the window to clear them.
/// </summary>
internal sealed class OutputPanel : Border
{
    // shortcut: a fixed height with no drag handle on its top edge. Add the drag when a panel needs more room.
    const double PanelHeight = 200;

    const double StripHeight = 36;

    readonly ListBox _list = new() { Background = null, BorderThickness = new Thickness(0), FontFamily = EditorTheme.Mono };
    readonly Button _copy = Unbuilt.Mark(Parts.IconButton("ActionCopy", "Copy all"), "Copying the output is not built yet.");
    readonly Button _clear = Parts.IconButton("Clear", "Clear Output");
    bool _scrollPending;

    public OutputPanel()
    {
        Height = PanelHeight;
        Classes.Add(EditorTheme.Island);
        _clear.Click += (_, _) => ClearRequested?.Invoke();
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, VerticalAlignment = VerticalAlignment.Center, Children = { _copy, _clear } };
        DockPanel.SetDock(buttons, Dock.Right);
        Strip = new Border
        {
            Height = StripHeight,
            Padding = new Thickness(12, 0, 6, 0),
            BorderBrush = EditorBrushes.Line,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = new DockPanel
            {
                Children = { buttons, new TextBlock { Text = "Output", FontSize = 12, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center } },
            },
        };
        DockPanel.SetDock(Strip, Dock.Top);
        Child = new DockPanel { Children = { Strip, _list } };
    }

    /// <summary>Raised when the clear button is pressed. The panel has not changed, it follows the lines.</summary>
    public event Action? ClearRequested;

    internal ListBox List => _list;

    internal Border Strip { get; }

    internal Button CopyButton => _copy;

    internal Button ClearButton => _clear;

    /// <summary>Shows these lines and keeps showing them as they change, scrolled to the newest.</summary>
    public void Show(ReadOnlyObservableCollection<string> lines)
    {
        _list.ItemsSource = lines;
        ((INotifyCollectionChanged)lines).CollectionChanged += (_, e) =>
        {
            if (e.Action == NotifyCollectionChangedAction.Add && !_scrollPending)
            {
                // One scroll per batch, after the layout that measures the new rows. Before it the scroll stops at the old end.
                _scrollPending = true;
                Dispatcher.UIThread.Post(() =>
                {
                    _scrollPending = false;
                    _list.ScrollIntoView(lines.Count - 1);
                }, DispatcherPriority.Background);
            }
        };
    }
}
