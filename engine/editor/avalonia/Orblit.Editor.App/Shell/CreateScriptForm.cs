using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Shell;

/// <summary>The words, the engine's name and the file suffix of one kind of script that the New script dialog makes.</summary>
internal sealed record ScriptKind(string Title, string Engine, string Lead, string DefaultName, string Suffix)
{
    public static readonly ScriptKind Graph = new("New graph script", "New Graph Script", "A script made of steps. You pick what it runs on.", "new_graph", ".ograph");

    public static readonly ScriptKind Csharp = new("New C# script", "New C# Script", "Code that tells a thing what to do.", "NewScript", ".cs");
}

/// <summary>
/// The New script dialog for one kind of script. It asks for the name of the file, the kind of thing the script
/// works on and its folder, and shows where the file goes. A name that is a script of the kind already turns Create
/// off and says so under the field. While Create is off, its card says what to do first.
/// </summary>
internal sealed class CreateScriptForm : StackPanel
{
    const string DefaultBase = "Entity";
    const string DefaultFolder = "res://";
    const string WorksOnLine = "The kind of thing it can be added to. Entity works on anything.";

    readonly ScriptKind _kind;
    readonly TextBox _name = new()
    {
        PlaceholderText = "Name",
        InnerRightContent = new TextBlock
        {
            FontFamily = EditorTheme.Mono,
            FontSize = 11,
            Foreground = EditorBrushes.TextDim,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0),
        },
        Classes = { EditorTheme.Field },
    };
    readonly ComboBox _extends = new() { ItemTemplate = new FuncDataTemplate<string>((name, _) => BaseRow(name)), Classes = { EditorTheme.Field } };
    readonly TextBox _folder = new()
    {
        PlaceholderText = "Folder",
        FontFamily = EditorTheme.Mono,
        InnerLeftContent = Parts.FieldIcon("FolderLine", EditorBrushes.TextDim),
        Classes = { EditorTheme.Field },
    };
    readonly TextBlock _fault = Parts.FaultLine();
    readonly TextBlock _saved = new() { FontSize = 11, Foreground = EditorBrushes.TextDim };
    IReadOnlyCollection<string> _scripts = [];

    public CreateScriptForm(ScriptKind kind)
    {
        _kind = kind;
        ((TextBlock)_name.InnerRightContent!).Text = kind.Suffix;
        Spacing = 10;
        Children.Add(Parts.Labeled("Name", _name, _fault, _saved));
        Children.Add(Parts.Labeled("Works on", _extends, new TextBlock { Text = WorksOnLine, FontSize = 11, Foreground = EditorBrushes.TextDim, TextWrapping = TextWrapping.Wrap }));
        Children.Add(Parts.Labeled("Folder", _folder));
        Dialog = new Dialog(kind.Title, kind.Lead, this) { EngineName = kind.Engine, CardWidth = 380 };
        Dialog.MainButton.Content = Parts.Keyed("Create", "↵");
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

    /// <summary>The line under the name that says where the script is saved.</summary>
    internal TextBlock SavedLine => _saved;

    /// <summary>The line under the name that says why the script cannot be made, or empty.</summary>
    internal string Fault => _fault.IsVisible ? _fault.Text ?? "" : "";

    /// <summary>
    /// Opens the dialog with the first values and the name selected. A new path must not match one of the scripts, and
    /// the script extends one of the classes.
    /// </summary>
    public void Open(IReadOnlyCollection<string> scripts, IReadOnlyList<string> classes)
    {
        _scripts = scripts;
        _extends.ItemsSource = classes;
        _name.Text = _kind.DefaultName;
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

    // Create waits for a name and a folder, and for a path that is no script yet. Its card says which it waits for.
    void Check()
    {
        var name = _name.Text?.Trim() ?? "";
        var blank = string.IsNullOrWhiteSpace(_folder.Text);
        var taken = _scripts.Contains(PathOf());
        _name.Classes.Set(EditorTheme.Bad, taken);
        var fault = taken ? $"{name}{_kind.Suffix} already exists. Pick another name." : "";
        Say(fault);
        _saved.Text = name == "" || blank ? "" : $"Saved as {PathOf()}";
        var why = name == "" ? "Give it a name first." : blank ? "Give it a folder first." : taken ? fault : null;
        Dialog.MainButton.IsEnabled = why is null;
        new HoverCard("Create", why ?? "Makes the script and opens it.", "↵").On(Dialog.MainButton);
    }

    // A folder without its closing slash gets one, so res://enemies and res://enemies/ give one path.
    string PathOf()
    {
        var folder = _folder.Text?.Trim() ?? "";
        return $"{(folder.EndsWith('/') ? folder : folder + "/")}{_name.Text?.Trim()}{_kind.Suffix}";
    }

    // A 3D base is marked in the 3D color and a 2D base in the 2D color, as the roots of New scene are.
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
