using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Orblit.Editor.App.Shell;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App;

/// <summary>
/// The New Scene dialog. It asks what the root of the scene is and the name of its file, and shows where the
/// file goes. Other... searches every class for the root. A name that is a scene already turns Create off and says
/// so under the field.
/// </summary>
internal sealed class NewSceneTool : StackPanel
{
    readonly (RadioButton Card, string ClassName)[] _roots;
    readonly TextBox _name = new() { PlaceholderText = "Name", Classes = { EditorTheme.Field } };
    readonly TextBox _path = new()
    {
        PlaceholderText = "Path",
        IsReadOnly = true,
        FontFamily = EditorTheme.Mono,
        InnerLeftContent = Parts.FieldIcon("FolderLine", EditorBrushes.TextDim),
        Classes = { EditorTheme.Field },
    };
    readonly TextBlock _fault = Parts.FaultLine();
    readonly ChoiceList _others = new("Search classes") { IsVisible = false, Margin = new Thickness(0, 10, 0, 0) };
    IReadOnlyCollection<string> _scenes = [];
    string _folder = FileTree.Root;

    public NewSceneTool()
    {
        _roots =
        [
            (Card(new RootCard(), "3D scene", "Root Entity3D", "Cube", EditorBrushes.Spatial), "Entity3D"),
            (Card(new RootCard(), "2D scene", "Root Entity2D", "Square", EditorBrushes.Planar), "Entity2D"),
            (Card(new RootCard(), "Empty", "Root Entity", "Ring", EditorBrushes.TextSoft), "Entity"),
        ];
        OtherCard = Card(new RootCard(), "Other...", "Any class", "Lens", EditorBrushes.TextDim);
        OtherCard.IsCheckedChanged += (_, _) =>
        {
            _others.IsVisible = OtherCard.IsChecked == true;
            Check();
        };
        _others.PickedChanged += Check;
        var cards = new UniformGrid { Columns = 2, RowSpacing = 8, ColumnSpacing = 8 };
        cards.Children.AddRange([.. _roots.Select(root => root.Card), OtherCard]);
        Children.Add(cards);
        Children.Add(_others);
        var name = Parts.Labeled("Name", _name, _fault);
        name.Margin = new Thickness(0, 14, 0, 0);
        Children.Add(name);
        var path = Parts.Labeled("Saved to", _path);
        path.Margin = new Thickness(0, 10, 0, 0);
        Children.Add(path);

        Dialog = new Dialog("New Scene", "Pick what the scene is for. You can change its root later.", this);
        // The key that runs Create sits on a dark cap, so the orange under it stays bright.
        var key = new Border
        {
            Padding = new Thickness(5, 3),
            CornerRadius = new CornerRadius(4),
            Background = EditorBrushes.Shade,
            Opacity = 0.7,
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock { Text = "↵", FontFamily = EditorTheme.Mono, FontSize = 10.5 },
        };
        Dialog.MainButton.Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { new TextBlock { Text = "Create", VerticalAlignment = VerticalAlignment.Center }, key } };
        // Create waits for a root, so a class is there when it is pressed.
        Dialog.MainButton.Click += (_, _) => Requested?.Invoke(_path.Text ?? "", RootClass()!);
        // TextChanged waits for a render, so the property is the one that fires at once.
        _name.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty)
            {
                Check();
            }
        };
    }

    /// <summary>Raised with the path of the new scene file and the class of its root when Create is pressed.</summary>
    public event Action<string, string>? Requested;

    internal Dialog Dialog { get; }

    internal IEnumerable<RadioButton> Roots => _roots.Select(root => root.Card);

    internal RadioButton OtherCard { get; }

    internal ChoiceList OtherClasses => _others;

    internal TextBox NameBox => _name;

    internal TextBox PathBox => _path;

    internal Button CreateButton => Dialog.MainButton;

    /// <summary>The line under the name that says why the scene cannot be made, or empty.</summary>
    internal string Fault => _fault.IsVisible ? _fault.Text ?? "" : "";

    /// <summary>
    /// Opens the dialog with no name and the keys in the name box, on the 2D root in the 2D workspace and the 3D
    /// root elsewhere. The scene is saved in <paramref name="folder"/>, a res:// path that ends in a slash. A new
    /// name must not match one of the scenes, and Other... offers the classes.
    /// </summary>
    public void Open(IReadOnlyCollection<string> scenes, bool planar, IReadOnlyList<string> classes, string folder)
    {
        _scenes = scenes;
        _folder = folder;
        _others.Show([.. classes.Select(name => new Choice(EditorIcons.ForClass(name), name) { Tint = EditorBrushes.TreeIcon })]);
        _roots[planar ? 1 : 0].Card.IsChecked = true;
        _name.Text = "";
        Check();
        Dialog.Open();
        _name.Focus();
    }

    /// <summary>Shows why the scene was not made under the name. An empty message hides the line.</summary>
    public void Say(string message)
    {
        _fault.Text = message;
        _fault.IsVisible = message != "";
    }

    // The class of the checked card, or of the class lit under Other.... Before Open checks a card there is none.
    string? RootClass() => OtherCard.IsChecked == true ? _others.Picked?.Name : _roots.FirstOrDefault(root => root.Card.IsChecked == true).ClassName;

    // The path follows the name, and Create waits for a root and for a name that is no scene yet.
    void Check()
    {
        var name = _name.Text?.Trim() ?? "";
        _path.Text = name == "" ? "" : $"{_folder}{name}.oscn";
        var taken = _scenes.Contains(_path.Text);
        _name.Classes.Set(EditorTheme.Bad, taken);
        Say(taken ? $"{name}.oscn already exists. Pick another name." : "");
        Dialog.MainButton.IsEnabled = name != "" && !taken && RootClass() is not null;
    }

    // An icon over a bold name and a dim line, on a worded button tall enough to hold them.
    static T Card<T>(T card, string label, string line, string icon, IBrush tint) where T : Button
    {
        var mark = new EditorIcon(icon) { Width = 20, Height = 20, HorizontalAlignment = HorizontalAlignment.Left };
        TextElement.SetForeground(mark, tint);
        card.Classes.Add(EditorTheme.TextButton);
        card.Classes.Add(EditorTheme.Card);
        card.Content = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                mark,
                new TextBlock { Text = label, FontSize = 12, FontWeight = FontWeight.SemiBold },
                new TextBlock { Text = line, FontSize = 11, Foreground = EditorBrushes.TextDim },
            },
        };
        return card;
    }

    // A radio choice drawn as a worded button, so the cards share one look and a check moves between them.
    sealed class RootCard : RadioButton
    {
        protected override Type StyleKeyOverride => typeof(Button);
    }
}
