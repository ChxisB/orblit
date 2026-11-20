using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Orblit.Editor.App.Shell;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App;

/// <summary>
/// The Add Metadata popover. It asks for a name, a kind and a value, and raises the value read as that kind. An
/// empty value is the empty value of the kind. A value the kind cannot read turns Add off and says why.
/// </summary>
internal sealed class MetadataTool : StackPanel
{
    // The order is the order of the kinds in the row. A list of cases, so it is data.
    static readonly (string Name, object Empty, string Hint, string Fault, Func<string, object?> Read)[] s_kinds =
    [
        ("String", "", "Empty", "", text => text),
        ("Integer", 0L, "0", "Type a whole number.", text => long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : null),
        ("Float", 0.0, "0.0", "Type a number.", text => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) && double.IsFinite(n) ? n : null),
        ("Boolean", false, "false", "Type true or false.", text => bool.TryParse(text, out var b) ? b : null),
    ];

    readonly TextBox _name = new() { PlaceholderText = "Name", FontFamily = EditorTheme.Mono, Classes = { EditorTheme.Field } };
    readonly Segments _kind = new([.. s_kinds.Select(kind => kind.Name)]);
    readonly TextBox _value = new() { Classes = { EditorTheme.Field } };
    readonly TextBlock _fault = Parts.FaultLine();
    readonly Button _cancel = new() { Content = "Cancel", Classes = { EditorTheme.TextButton, EditorTheme.Ghost }, Margin = new Thickness(0, 0, 8, 0) };
    readonly Button _add = new() { Content = "Add", Classes = { EditorTheme.TextButton, EditorTheme.Primary } };

    public MetadataTool()
    {
        Width = 320;
        Head = new CardHead("Add Metadata", "");
        DockPanel.SetDock(_add, Dock.Right);
        DockPanel.SetDock(_cancel, Dock.Right);
        Children.Add(Head);
        Children.Add(new Border
        {
            Padding = new Thickness(14, 12),
            Child = new StackPanel { Spacing = 10, Children = { Parts.Labelled("Name", _name), Parts.Labelled("Type", _kind), Parts.Labelled("Value", _value, _fault) } },
        });
        Children.Add(new Border
        {
            Padding = new Thickness(12, 10),
            BorderBrush = EditorBrushes.Line,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Child = new DockPanel { LastChildFill = false, Children = { _add, _cancel } },
        });
        // TextChanged waits for a render, so the property is the one that fires at once.
        _name.PropertyChanged += (_, e) => Check(e);
        _value.PropertyChanged += (_, e) => Check(e);
        _kind.Changed += Check;
        _add.Click += (_, _) =>
        {
            if (Value() is { } value)
            {
                Requested?.Invoke(Typed, value);
            }
        };
        Head.CloseButton.Click += (_, _) => Cancelled?.Invoke();
        _cancel.Click += (_, _) => Cancelled?.Invoke();
        Check();
    }

    /// <summary>Raised with the name and the value read as the chosen kind.</summary>
    public event Action<string, object>? Requested;

    /// <summary>Raised by Cancel and by the cross.</summary>
    public event Action? Cancelled;

    internal CardHead Head { get; }

    internal TextBox NameBox => _name;

    internal Segments Kind => _kind;

    internal TextBox ValueBox => _value;

    internal TextBlock Fault => _fault;

    internal Button CancelButton => _cancel;

    internal Button AddButton => _add;

    string Typed => (_name.Text ?? "").Trim();

    /// <summary>Names the entity the value is saved with, and the scene file it is saved in.</summary>
    public void ShowOwner(string entity, string sceneFile) =>
        Head.Why.Text = sceneFile == "" ? $"Saved with {entity} in this scene." : $"Saved with {entity} in {sceneFile[(sceneFile.LastIndexOf('/') + 1)..]}.";

    public void Clear()
    {
        _name.Text = "";
        _value.Text = "";
    }

    void Check(AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == TextBox.TextProperty)
        {
            Check();
        }
    }

    void Check()
    {
        var kind = s_kinds[_kind.SelectedIndex];
        var bad = Value() is null;
        _value.PlaceholderText = kind.Hint;
        _value.Classes.Set(EditorTheme.Bad, bad);
        _fault.Text = kind.Fault;
        _fault.IsVisible = bad;
        _add.IsEnabled = Typed.Length > 0 && !bad;
    }

    // Null when the kind cannot read the text.
    object? Value()
    {
        var kind = s_kinds[_kind.SelectedIndex];
        var text = _value.Text ?? "";
        return text.Trim() == "" ? kind.Empty : kind.Read(text);
    }
}
