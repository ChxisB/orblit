using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Shell;

/// <summary>Asks for the class a new graph script extends and the path of its file.</summary>
internal sealed class CreateGraphForm : StackPanel
{
    const string DefaultBase = "Entity";
    const string DefaultPath = "res://new_graph.ograph";
    const double FieldWidth = 280;

    readonly TextBox _inherits = new() { Text = DefaultBase, MinWidth = FieldWidth };
    readonly TextBox _path = new() { Text = DefaultPath, MinWidth = FieldWidth };
    readonly TextBlock _notice = new() { IsVisible = false, TextWrapping = Avalonia.Media.TextWrapping.Wrap, MaxWidth = FieldWidth, Foreground = EditorBrushes.TabInactive };
    readonly Button _create = new() { Content = "Create" };
    readonly Button _cancel = new() { Content = "Cancel" };

    public CreateGraphForm()
    {
        Spacing = 6;
        Margin = new Thickness(8);
        Children.Add(new TextBlock { Text = "Inherits" });
        Children.Add(_inherits);
        Children.Add(new TextBlock { Text = "Path" });
        Children.Add(_path);
        Children.Add(_notice);
        Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
            Children = { _cancel, _create },
        });
        // TextChanged waits for a render, so the property is the one that fires at once.
        _inherits.PropertyChanged += (_, e) => EnableCreate(e.Property);
        _path.PropertyChanged += (_, e) => EnableCreate(e.Property);
        _create.Click += (_, _) => Requested?.Invoke(_path.Text!.Trim(), _inherits.Text!.Trim());
        _cancel.Click += (_, _) => Cancelled?.Invoke();
    }

    /// <summary>Raised with the trimmed path and the class to extend when Create is pressed.</summary>
    public event Action<string, string>? Requested;

    public event Action? Cancelled;

    internal TextBox InheritsBox => _inherits;

    internal TextBox PathBox => _path;

    internal Button CreateButton => _create;

    internal Button CancelButton => _cancel;

    /// <summary>The line under the fields that says why a request failed, or empty.</summary>
    internal string Notice => _notice.IsVisible ? _notice.Text ?? "" : "";

    /// <summary>Puts back the first values and takes the notice away.</summary>
    public void Reset()
    {
        _inherits.Text = DefaultBase;
        _path.Text = DefaultPath;
        Say("");
    }

    /// <summary>Shows a line under the fields. An empty one hides it.</summary>
    public void Say(string message)
    {
        _notice.Text = message;
        _notice.IsVisible = message != "";
    }

    void EnableCreate(AvaloniaProperty property)
    {
        if (property == TextBox.TextProperty)
        {
            _create.IsEnabled = !string.IsNullOrWhiteSpace(_inherits.Text) && !string.IsNullOrWhiteSpace(_path.Text);
        }
    }
}
