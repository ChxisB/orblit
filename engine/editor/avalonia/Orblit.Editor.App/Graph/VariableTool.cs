using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Orblit.Editor.App.Shell;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Graph;

/// <summary>
/// The Add a remembered value popover: a name, a type, the value it starts as and whether the Inspector shows it.
/// The engine checks the name and the value when the panels pass them on, so Add is off only while the name is
/// empty, and its card says so. Enter presses it.
/// </summary>
internal sealed class VariableTool : StackPanel
{
    readonly TextBox _name = new() { PlaceholderText = "like health", FontFamily = EditorTheme.Mono, Classes = { EditorTheme.Field } };
    readonly ComboBox _type = PanelFields.TypeList();
    readonly TextBox _default = new() { PlaceholderText = "Nothing", FontFamily = EditorTheme.Mono, Classes = { EditorTheme.Field } };
    readonly ToggleSwitch _shown = new() { OnContent = "Show in the Inspector", OffContent = "Show in the Inspector", FontSize = 12, Foreground = EditorBrushes.TextSoft };
    readonly Button _cancel = new() { Content = "Cancel", Classes = { EditorTheme.TextButton, EditorTheme.Ghost }, Margin = new Thickness(0, 0, Gaps.Item, 0) };
    readonly Button _add = new() { Content = Parts.Keyed("Add", "↵"), Classes = { EditorTheme.TextButton, EditorTheme.Primary } };

    public VariableTool()
    {
        Width = 300;
        Head = new CardHead("Add a remembered value", "A value the graph keeps between steps, like health or opened.") { EngineName = "Add Variable" };
        _type.HorizontalAlignment = HorizontalAlignment.Stretch;
        _type.Classes.Add(EditorTheme.Field);
        var card = new HoverCard("Add", "Adds the value to the graph.", "↵");
        card.On(_add);
        card.SaysWhyOff(_add, "Give it a name first.");
        DockPanel.SetDock(_add, Dock.Right);
        DockPanel.SetDock(_cancel, Dock.Right);
        Children.Add(Head);
        Children.Add(new Border
        {
            Padding = new Thickness(Gaps.Dialog, Gaps.Group),
            Child = new StackPanel
            {
                Spacing = Gaps.Group,
                Children =
                {
                    Parts.Labeled("Name", _name, Dim("Letters, numbers and _ only.")),
                    Parts.Labeled("Type", _type, Dim("Any holds anything. Pick a type, like Yes or no, to catch mistakes early.")),
                    Parts.Labeled("Starts as", _default, Dim("The value when the game starts.")),
                    new StackPanel { Children = { _shown, Dim("Change it for each thing without opening the graph.") } },
                },
            },
        });
        Children.Add(new Border
        {
            Padding = new Thickness(Gaps.Dialog, Gaps.Group),
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
        _add.Click += (_, _) => Add();
        Head.CloseButton.Click += (_, _) => Cancelled?.Invoke();
        _cancel.Click += (_, _) => Cancelled?.Invoke();
        // A popover is no dialog, so Enter is caught here.
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter && _add.IsEnabled)
            {
                e.Handled = true;
                Add();
            }
        };
    }

    /// <summary>Raised with the name, the type number, the value it starts as in Variant text or empty, and whether the Inspector shows it.</summary>
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

    static TextBlock Dim(string line) => new() { Text = line, FontSize = 11, Foreground = EditorBrushes.TextDim, TextWrapping = TextWrapping.Wrap };

    void Add() => Requested?.Invoke(Typed, Math.Max(_type.SelectedIndex, 0), (_default.Text ?? "").Trim(), _shown.IsChecked == true);

    /// <summary>Starts again with this name, a value of any type that starts as nothing, hidden from the Inspector.</summary>
    public void Reset(string name)
    {
        _name.Text = name;
        _type.SelectedIndex = 0;
        _default.Text = "";
        _shown.IsChecked = false;
    }
}
