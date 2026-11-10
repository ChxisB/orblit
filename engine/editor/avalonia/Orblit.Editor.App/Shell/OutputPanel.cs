using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Shell;

/// <summary>
/// The log of the engine and of the games it ran: a button column on the left and the lines beside it. It only
/// shows the lines it is given and asks the window to clear them.
/// </summary>
internal sealed class OutputPanel : Border
{
    // shortcut: a fixed height with no drag handle on its top edge. Add the drag when a panel needs more room.
    const double PanelHeight = 200;

    readonly ListBox _list = new() { Background = null, BorderThickness = new Thickness(0) };
    readonly Button _clear = Parts.IconButton("Clear", "Clear Output");
    bool _scrollPending;

    public OutputPanel()
    {
        Height = PanelHeight;
        Background = EditorBrushes.Panel;
        _clear.VerticalAlignment = VerticalAlignment.Top;
        _clear.Margin = new Thickness(6);
        _clear.Click += (_, _) => ClearRequested?.Invoke();
        var panel = new DockPanel();
        DockPanel.SetDock(_clear, Dock.Left);
        panel.Children.Add(_clear);
        panel.Children.Add(_list);
        Child = panel;
    }

    /// <summary>Raised when the clear button is pressed. The panel has not changed, it follows the lines.</summary>
    public event Action? ClearRequested;

    internal ListBox List => _list;

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
