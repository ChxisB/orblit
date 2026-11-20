using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Orblit.Editor.App.Shell;
using Orblit.Editor.App.Theme;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App.Graph;

/// <summary>
/// The Add a variable popover: a name, a type, a default and whether the Inspector shows it. The engine checks
/// the name and the default when the panels pass them on, so Add is off only while the name is empty.
/// </summary>
internal sealed class VariableTool : StackPanel
{
    readonly TextBox _name = new() { FontFamily = EditorTheme.Mono, Classes = { EditorTheme.Field } };
    readonly ComboBox _type = new() { ItemsSource = VariantTypes.Names, SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch, Classes = { EditorTheme.Field } };
    readonly TextBox _default = new() { PlaceholderText = "null", FontFamily = EditorTheme.Mono, Classes = { EditorTheme.Field } };
    readonly ToggleSwitch _shown = new() { OnContent = "Show in the Inspector", OffContent = "Show in the Inspector", FontSize = 12, Foreground = EditorBrushes.TextSoft };
    readonly Button _cancel = new() { Content = "Cancel", Classes = { EditorTheme.TextButton, EditorTheme.Ghost }, Margin = new Thickness(0, 0, 8, 0) };
    readonly Button _add = new() { Content = "Add", Classes = { EditorTheme.TextButton, EditorTheme.Primary } };

    public VariableTool()
    {
        Width = 300;
        Head = new CardHead("Add a variable", "");
        DockPanel.SetDock(_add, Dock.Right);
        DockPanel.SetDock(_cancel, Dock.Right);
        Children.Add(Head);
        Children.Add(new Border
        {
            Padding = new Thickness(14, 12),
            Child = new StackPanel { Spacing = 10, Children = { Parts.Labelled("Name", _name), Parts.Labelled("Type", _type), Parts.Labelled("Default", _default), _shown } },
        });
        Children.Add(new Border
        {
            Padding = new Thickness(12, 10),
            BorderBrush = EditorBrushes.Line,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Child = new DockPanel { LastChildFill = false, Children = { _add, _cancel } },
        });
        // TextChanged waits for a render, so the property is the one that fires at once.
        _name.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty)
            {
                _add.IsEnabled = Typed.Length > 0;
            }
        };
        _add.Click += (_, _) => Requested?.Invoke(Typed, Math.Max(_type.SelectedIndex, 0), (_default.Text ?? "").Trim(), _shown.IsChecked == true);
        Head.CloseButton.Click += (_, _) => Cancelled?.Invoke();
        _cancel.Click += (_, _) => Cancelled?.Invoke();
    }

    /// <summary>Raised with the name, the type number, the default as Variant text or empty, and whether the Inspector shows it.</summary>
    public event Action<string, int, string, bool>? Requested;

    /// <summary>Raised by Cancel and by the cross.</summary>
    public event Action? Cancelled;

    internal CardHead Head { get; }

    internal TextBox NameBox => _name;

    internal ComboBox TypeBox => _type;

    internal TextBox DefaultBox => _default;

    internal ToggleSwitch Shown => _shown;

    internal Button AddButton => _add;

    string Typed => (_name.Text ?? "").Trim();

    /// <summary>Starts again with this name, a value of any type, no default and hidden from the Inspector.</summary>
    public void Reset(string name)
    {
        _name.Text = name;
        _type.SelectedIndex = 0;
        _default.Text = "";
        _shown.IsChecked = false;
    }
}
