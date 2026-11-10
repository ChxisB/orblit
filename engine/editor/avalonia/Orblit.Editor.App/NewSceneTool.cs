using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace Orblit.Editor.App;

/// <summary>Asks for the name of a new scene file and the class of its root entity.</summary>
internal sealed class NewSceneTool : StackPanel
{
    readonly ComboBox _class = new() { ItemsSource = EntityTools.ClassNames, SelectedIndex = 0 };
    readonly TextBox _name = new() { PlaceholderText = "Scene name", MinWidth = 160 };
    readonly Button _create = new() { Content = "Create", IsEnabled = false };

    public NewSceneTool()
    {
        Orientation = Orientation.Horizontal;
        Spacing = 8;
        Margin = new Thickness(8);
        Children.Add(_class);
        Children.Add(_name);
        Children.Add(_create);
        // TextChanged waits for a render, so the property is the one that fires at once.
        _name.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty)
            {
                _create.IsEnabled = !string.IsNullOrWhiteSpace(_name.Text);
            }
        };
        _create.Click += (_, _) => Requested?.Invoke(_name.Text!.Trim(), (string)_class.SelectedItem!);
    }

    /// <summary>Raised with the trimmed name and the root class when Create is pressed.</summary>
    public event Action<string, string>? Requested;

    internal ComboBox Class => _class;

    internal TextBox NameBox => _name;

    internal Button CreateButton => _create;

    public void Clear() => _name.Text = "";
}
