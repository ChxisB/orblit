using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Orblit.Editor.App.Shell;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App;

/// <summary>
/// The Add a custom value popover. It asks for a name, a kind and a value, and raises the value read as that kind.
/// An empty value is the empty value of the kind. While Add is off, its card says what to fix first. Enter adds.
/// </summary>
internal sealed class MetadataTool : StackPanel
{
    const string NameRule = "Letters, numbers and _ only.";

    // The order is the order of the kinds in the row. A list of cases, so it is data.
    static readonly (string Name, string Engine, string Line, object Empty, string Hint, string Fault, Func<string, object?> Read)[] s_kinds =
    [
        ("Text", "String", "Words, like a name or a code.", "", "Empty", "", text => text),
        ("Whole number", "int", "A number with no point, like 3 or -12.", 0L, "0", "Type a whole number.", text => long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : null),
        ("Number", "float", "A number that can have a point, like 2.5.", 0.0, "0.0", "Type a number.", text => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) && double.IsFinite(n) ? n : null),
        ("Yes or no", "bool", "true or false.", false, "false", "Type true or false.", text => bool.TryParse(text, out var b) ? b : null),
    ];

    readonly TextBox _name = new() { PlaceholderText = "door_code", FontFamily = EditorTheme.Mono, Classes = { EditorTheme.Field } };
    readonly TextBlock _nameRule = new() { Text = NameRule, FontSize = 11, Foreground = EditorBrushes.TextDim };
    readonly Segments _kind = new([.. s_kinds.Select(kind => kind.Name)]);
    readonly TextBox _value = new() { Classes = { EditorTheme.Field } };
    readonly TextBlock _fault = Parts.FaultLine();
    readonly Button _cancel = new() { Content = "Cancel", Classes = { EditorTheme.TextButton, EditorTheme.Ghost }, Margin = new Thickness(0, 0, 8, 0) };
    readonly Button _add = new() { Content = Parts.Keyed("Add", "↵"), Classes = { EditorTheme.TextButton, EditorTheme.Primary } };

    public MetadataTool()
    {
        Width = 320;
        Head = new CardHead("Add a custom value", "") { EngineName = "Metadata" };
        foreach (var (choice, kind) in _kind.Choices.Zip(s_kinds))
        {
            new HoverCard(kind.Name, kind.Line, engine: kind.Engine).On(choice);
        }
        DockPanel.SetDock(_add, Dock.Right);
        DockPanel.SetDock(_cancel, Dock.Right);
        Children.Add(Head);
        Children.Add(new Border
        {
            Padding = new Thickness(14, 12),
            Child = new StackPanel { Spacing = 10, Children = { Parts.Labeled("Name", _name, _nameRule), Parts.Labeled("Type", _kind), Parts.Labeled("Value", _value, _fault) } },
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
        // A popover is no dialog, so Enter is caught here. The box keeps a key it uses, such as a new line.
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter && _add.IsEnabled)
            {
                e.Handled = true;
                _add.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
        };
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

    /// <summary>Names the thing the value is stored on.</summary>
    public void ShowOwner(string entity) =>
        Head.Why.Text = $"Store your own data on {entity}, like a score or a door code. Scripts can read it.";

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

    // A name breaks the rule when it holds anything but letters, numbers and _, or starts with a number.
    void Check()
    {
        var kind = s_kinds[_kind.SelectedIndex];
        var bad = Value() is null;
        var badName = Typed.Length > 0 && !(Typed.All(c => char.IsAsciiLetterOrDigit(c) || c == '_') && !char.IsAsciiDigit(Typed[0]));
        _value.PlaceholderText = kind.Hint;
        _value.Classes.Set(EditorTheme.Bad, bad);
        _name.Classes.Set(EditorTheme.Bad, badName);
        _nameRule.Foreground = badName ? EditorBrushes.FaultText : EditorBrushes.TextDim;
        _fault.Text = kind.Fault;
        _fault.IsVisible = bad;
        var why = Typed.Length == 0 ? "Give it a name first." : badName ? NameRule : bad ? kind.Fault : null;
        _add.IsEnabled = why is null;
        new HoverCard("Add", why ?? "Stores the value on this thing.", "↵").On(_add);
    }

    // Null when the kind cannot read the text.
    object? Value()
    {
        var kind = s_kinds[_kind.SelectedIndex];
        var text = _value.Text ?? "";
        return text.Trim() == "" ? kind.Empty : kind.Read(text);
    }
}
