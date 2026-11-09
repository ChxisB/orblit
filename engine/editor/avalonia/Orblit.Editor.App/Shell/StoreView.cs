using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Shell;

/// <summary>
/// The Asset Store workspace: the kits the store's index lists, one row each. It reads the index when Load is
/// pressed. It names kits and does not install them.
/// </summary>
internal sealed class StoreView : Border
{
    readonly Func<CancellationToken, Task<string>> _read;
    readonly Button _load = new() { Content = "Load", Padding = new Thickness(12, 4), MinHeight = 0 };
    readonly TextBlock _status = new() { Text = "Press Load to read the store.", Foreground = EditorBrushes.TabInactive, VerticalAlignment = VerticalAlignment.Center };
    readonly ListBox _list = new() { Background = null, BorderThickness = new Thickness(0) };
    bool _loading;

    // shortcut: the index is read when Load is pressed and not on first show, so a tab click sends no request. Read it on first show once the store lists kits.
    public StoreView(Func<CancellationToken, Task<string>> read)
    {
        _read = read;
        Background = EditorBrushes.Panel;
        _load.Click += async (_, _) => await LoadAsync();
        var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Margin = new Thickness(12, 8) };
        header.Children.Add(new TextBlock { Text = "Asset Store", FontWeight = FontWeight.Bold, VerticalAlignment = VerticalAlignment.Center });
        header.Children.Add(_load);
        header.Children.Add(_status);
        var panel = new DockPanel();
        DockPanel.SetDock(header, Dock.Top);
        panel.Children.Add(header);
        panel.Children.Add(_list);
        Child = panel;
    }

    internal Button LoadButton => _load;

    internal TextBlock Status => _status;

    internal ListBox List => _list;

    /// <summary>
    /// Reads the index and shows its kits in place of the rows before. A failure is said in the status and the
    /// rows before stay. A press while a read is running does nothing.
    /// </summary>
    public async Task LoadAsync()
    {
        if (_loading)
        {
            return;
        }

        _loading = true;
        _load.IsEnabled = false;
        _status.Text = "Loading the store.";
        try
        {
            var kits = KitIndex.Parse(await _read(CancellationToken.None));
            _list.ItemsSource = kits.Select(Row).ToList();
            _status.Text = kits.Count == 1 ? "1 kit." : $"{kits.Count} kits.";
        }
        catch (Exception error) when (error is IOException or FormatException)
        {
            _status.Text = error.Message;
        }
        finally
        {
            _loading = false;
            _load.IsEnabled = true;
        }
    }

    static StackPanel Row(KitEntry kit)
    {
        var title = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        title.Children.Add(new TextBlock { Text = kit.Name, FontWeight = FontWeight.Bold });
        title.Children.Add(new TextBlock { Text = $"{kit.Version} by {kit.Publisher}", Foreground = EditorBrushes.TabInactive });
        var row = new StackPanel { Margin = new Thickness(4, 4) };
        row.Children.Add(title);
        if (kit.Summary.Length > 0)
        {
            row.Children.Add(new TextBlock { Text = kit.Summary, TextWrapping = TextWrapping.Wrap });
        }

        return row;
    }
}
