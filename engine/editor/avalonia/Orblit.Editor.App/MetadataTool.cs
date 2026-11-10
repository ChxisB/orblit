using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace Orblit.Editor.App;

/// <summary>Asks for a name and a kind and raises the empty value of that kind.</summary>
internal sealed class MetadataTool : StackPanel
{
    // The order is the order of the kinds in the box.
    static readonly object[] s_empty = ["", 0L, 0.0, false];

    readonly TextBox _name = new() { MinWidth = 160, PlaceholderText = "Name" };
    readonly ComboBox _kind = new() { ItemsSource = new[] { "String", "Integer", "Float", "Boolean" }, SelectedIndex = 0 };
    readonly Button _add = new() { Content = "Add", IsEnabled = false };

    public MetadataTool()
    {
        Orientation = Orientation.Horizontal;
        Spacing = 8;
        Margin = new Thickness(8);
        Children.Add(_name);
        Children.Add(_kind);
        Children.Add(_add);
        _name.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty)
            {
                _add.IsEnabled = Typed.Length > 0;
            }
        };
        _add.Click += (_, _) => Requested?.Invoke(Typed, s_empty[_kind.SelectedIndex]);
    }

    /// <summary>Raised with the name and the empty value of the chosen kind.</summary>
    public event Action<string, object>? Requested;

    internal TextBox NameBox => _name;

    internal ComboBox Kind => _kind;

    internal Button AddButton => _add;

    string Typed => (_name.Text ?? "").Trim();

    public void Clear() => _name.Text = "";
}
