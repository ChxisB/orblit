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
/// The New scene dialog. It asks what the scene starts with and the name of its file, and shows where the file
/// goes. Something else… searches every class for the top thing. A name that is a scene already turns Create off
/// and says so under the field.
/// </summary>
internal sealed class NewSceneTool : StackPanel
{
    readonly (RadioButton Card, string ClassName)[] _roots;
    readonly TextBox _name = new() { PlaceholderText = "Name", Classes = { EditorTheme.Field } };
    readonly TextBlock _saved = new() { FontSize = 11, Foreground = EditorBrushes.TextDim };
    readonly TextBlock _fault = Parts.FaultLine();
    readonly ChoiceList _others = new("Search classes") { IsVisible = false, Margin = new Thickness(0, Gaps.Item, 0, 0) };
    IReadOnlyCollection<string> _scenes = [];
    string _folder = FileTree.Root;
    string _path = "";

    public NewSceneTool()
    {
        _roots =
        [
            (Card(new RootCard(), ("3D scene", "Entity3D"), "For a world with depth and a camera.", "Cube", EditorBrushes.Spatial), "Entity3D"),
            (Card(new RootCard(), ("2D scene", "Entity2D"), "For flat games, menus and buttons.", "Square", EditorBrushes.Planar), "Entity2D"),
            (Card(new RootCard(), ("Empty", "Entity"), "A plain point. Pick a kind later.", "Ring", EditorBrushes.TextSoft), "Entity"),
        ];
        OtherCard = Card(new RootCard(), ("Something else…", null), "Pick any kind as the top thing", "Lens", EditorBrushes.TextDim);
        OtherCard.IsCheckedChanged += (_, _) =>
        {
            _others.IsVisible = OtherCard.IsChecked == true;
            Check();
        };
        _others.PickedChanged += Check;
        var cards = new UniformGrid { Columns = 2, RowSpacing = Gaps.Item, ColumnSpacing = Gaps.Item };
        cards.Children.AddRange([.. _roots.Select(root => root.Card), OtherCard]);
        Children.Add(Parts.Labeled("Start with", cards));
        Children.Add(_others);
        var name = Parts.Labeled("Name", _name, _fault, _saved);
        name.Margin = new Thickness(0, Gaps.Group, 0, 0);
        Children.Add(name);

        Dialog = new Dialog("New scene", "A scene is a level, a menu or a part you reuse.", this);
        // The key that runs Create sits on a dark cap, so the orange under it stays bright.
        var key = new Border
        {
            Padding = new Thickness(Gaps.Tight, Gaps.Joined),
            CornerRadius = new CornerRadius(4),
            Background = EditorBrushes.Shade,
            Opacity = 0.7,
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock { Text = "↵", FontFamily = EditorTheme.Mono, FontSize = 10.5 },
        };
        Dialog.MainButton.Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Gaps.Item, Children = { new TextBlock { Text = "Create", VerticalAlignment = VerticalAlignment.Center }, key } };
        // Create waits for a root, so a class is there when it is pressed.
        Dialog.MainButton.Click += (_, _) => Requested?.Invoke(_path, RootClass()!);
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

    /// <summary>The line under the name that says where the scene is saved.</summary>
    internal TextBlock SavedLine => _saved;

    internal Button CreateButton => Dialog.MainButton;

    /// <summary>The line under the name that says why the scene cannot be made, or empty.</summary>
    internal string Fault => _fault.IsVisible ? _fault.Text ?? "" : "";

    /// <summary>
    /// Opens the dialog with no name and the keys in the name box, on the 2D root in the 2D workspace and the 3D
    /// root elsewhere. The scene is saved in <paramref name="folder"/>, a res:// path that ends in a slash. A new
    /// name must not match one of the scenes, and Something else… offers the classes.
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

    // The class of the checked card, or of the class lit under Something else…. Before Open checks a card there is none.
    string? RootClass() => OtherCard.IsChecked == true ? _others.Picked?.Name : _roots.FirstOrDefault(root => root.Card.IsChecked == true).ClassName;

    // The path follows the name, and Create waits for a root and for a name that is no scene yet. Its card says
    // which of those it waits for.
    void Check()
    {
        var name = _name.Text?.Trim() ?? "";
        _path = name == "" ? "" : $"{_folder}{name}.oscn";
        _saved.Text = name == "" ? "" : $"Saved as {_path}";
        var taken = _scenes.Contains(_path);
        _name.Classes.Set(EditorTheme.Bad, taken);
        var fault = taken ? $"{name}.oscn already exists. Pick another name." : "";
        Say(fault);
        var why = name == "" ? "Give it a name first." : taken ? fault : RootClass() is null ? "Pick a kind in the list first." : null;
        Dialog.MainButton.IsEnabled = why is null;
        new HoverCard("Create", why ?? "Makes the scene and opens it.", "↵").On(Dialog.MainButton);
    }

    // An icon over a name, with the engine's name beside it in dim mono, and a dim line, on a worded button tall
    // enough to hold them.
    static T Card<T>(T card, (string Label, string? Engine) name, string line, string icon, IBrush tint) where T : Button
    {
        var mark = new EditorIcon(icon) { Width = 20, Height = 20, HorizontalAlignment = HorizontalAlignment.Left };
        TextElement.SetForeground(mark, tint);
        card.Classes.Add(EditorTheme.TextButton);
        card.Classes.Add(EditorTheme.Card);
        card.Content = new StackPanel
        {
            Spacing = Gaps.Item,
            Children =
            {
                mark,
                Parts.Noted(name.Label, line, name.Engine),
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
