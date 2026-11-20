using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Shell;

/// <summary>
/// The New Graph Script dialog. It asks for the name of the file, the class the script extends and its folder.
/// A name that is a graph script already turns Create off and says so under the field.
/// </summary>
internal sealed class CreateGraphForm : StackPanel
{
    const string DefaultName = "new_graph";
    const string DefaultBase = "Entity";
    const string DefaultFolder = "res://";
    const string Suffix = ".ograph";

    // shortcut: a short fixed list of bases. Replace it with a class browser when the editor has one.
    static readonly string[] Bases = ["Entity", "Entity2D", "Entity3D", "Marker3D", "MeshInstance3D", "Camera3D"];

    readonly TextBox _name = new()
    {
        PlaceholderText = "Name",
        InnerRightContent = new TextBlock
        {
            Text = Suffix,
            FontFamily = EditorTheme.Mono,
            FontSize = 11,
            Foreground = EditorBrushes.TextDim,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0),
        },
        Classes = { EditorTheme.Field },
    };
    readonly ComboBox _extends = new() { ItemsSource = Bases, ItemTemplate = new FuncDataTemplate<string>((name, _) => BaseRow(name)), Classes = { EditorTheme.Field } };
    readonly TextBox _folder = new()
    {
        PlaceholderText = "Folder",
        FontFamily = EditorTheme.Mono,
        InnerLeftContent = Parts.FieldIcon("FolderLine", EditorBrushes.TextDim),
        Classes = { EditorTheme.Field },
    };
    readonly TextBlock _fault = Parts.FaultLine();
    IReadOnlyCollection<string> _scripts = [];

    public CreateGraphForm()
    {
        Spacing = 10;
        Children.Add(Parts.Labelled("Name", _name, _fault));
        Children.Add(Parts.Labelled("Extends", _extends));
        Children.Add(Parts.Labelled("Folder", _folder));
        Dialog = new Dialog("New Graph Script", "A script made of steps.", this) { MainText = "Create", CardWidth = 380 };
        // TextChanged waits for a render, so the property is the one that fires at once.
        _name.PropertyChanged += (_, e) => Check(e.Property);
        _folder.PropertyChanged += (_, e) => Check(e.Property);
        // Open picks a base, and the list has no empty row.
        Dialog.MainButton.Click += (_, _) => Requested?.Invoke(PathOf(), (string)_extends.SelectedItem!);
    }

    /// <summary>Raised with the path of the new file and the class to extend when Create is pressed.</summary>
    public event Action<string, string>? Requested;

    internal Dialog Dialog { get; }

    internal TextBox NameBox => _name;

    internal ComboBox ExtendsBox => _extends;

    internal TextBox FolderBox => _folder;

    internal Button CreateButton => Dialog.MainButton;

    /// <summary>The line under the name that says why the script cannot be made, or empty.</summary>
    internal string Fault => _fault.IsVisible ? _fault.Text ?? "" : "";

    /// <summary>Opens the dialog with the first values and the name selected. A new path must not match one of the scripts.</summary>
    public void Open(IReadOnlyCollection<string> scripts)
    {
        _scripts = scripts;
        _name.Text = DefaultName;
        _extends.SelectedItem = DefaultBase;
        _folder.Text = DefaultFolder;
        Check();
        Dialog.Open();
        _name.Focus();
        _name.SelectAll();
    }

    /// <summary>Shows why the script was not made under the name. An empty message hides the line.</summary>
    public void Say(string message)
    {
        _fault.Text = message;
        _fault.IsVisible = message != "";
    }

    void Check(AvaloniaProperty property)
    {
        if (property == TextBox.TextProperty)
        {
            Check();
        }
    }

    // Create waits for a name and a folder, and for a path that is no graph script yet.
    void Check()
    {
        var name = _name.Text?.Trim() ?? "";
        var taken = _scripts.Contains(PathOf());
        _name.Classes.Set(EditorTheme.Bad, taken);
        Say(taken ? $"{name}{Suffix} already exists. Pick another name." : "");
        Dialog.MainButton.IsEnabled = name != "" && !string.IsNullOrWhiteSpace(_folder.Text) && !taken;
    }

    // A folder without its closing slash gets one, so res://enemies and res://enemies/ give one path.
    string PathOf()
    {
        var folder = _folder.Text?.Trim() ?? "";
        return $"{(folder.EndsWith('/') ? folder : folder + "/")}{_name.Text?.Trim()}{Suffix}";
    }

    // A 3D base is marked in the 3D colour and a 2D base in the 2D colour, as the roots of New Scene are.
    static StackPanel BaseRow(string name)
    {
        var tint = name switch
        {
            _ when name.EndsWith("3D", StringComparison.Ordinal) => EditorBrushes.Spatial,
            _ when name.EndsWith("2D", StringComparison.Ordinal) => EditorBrushes.Planar,
            _ => EditorBrushes.TextSoft,
        };
        return new StackPanel { Orientation = Orientation.Horizontal, Children = { Parts.FieldIcon("Ring", tint), new TextBlock { Text = name, VerticalAlignment = VerticalAlignment.Center } } };
    }
}
