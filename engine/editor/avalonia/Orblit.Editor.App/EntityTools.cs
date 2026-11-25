using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Orblit.Editor.App.Shell;
using Orblit.Editor.App.Theme;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App;

/// <summary>What a tool changed. Select is the entity to show selected afterwards, or zero for none.</summary>
internal sealed record EntityEdit(nint Select, string Message);

/// <summary>
/// Adds an entity inside the selected one, or over it as its new parent, through the Add a thing dialog, and
/// copies, renames and removes the selected entity. Each command is a button, so the dock, its menus and a key press the same one.
/// </summary>
internal sealed class EntityTools
{
    internal const string AddTitle = "Add a thing";
    internal const string ParentTitle = "Add a parent";

    // The kinds most scenes start with lead the list, in this order. Every other kind waits in a folded group.
    internal static readonly string[] MostUsed = ["Entity3D", "MeshInstance3D", "Camera3D", "DirectionalLight3D", "Marker3D", "Entity2D", "ColorRect"];

    readonly ComboBox _inside = new()
    {
        MinWidth = 150,
        MinHeight = 0,
        VerticalAlignment = VerticalAlignment.Center,
        ItemTemplate = new FuncDataTemplate<OutlineNode>((node, _) => new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Children = { Parts.RowIcon(EditorIcons.ForClass(node.ClassName)), new TextBlock { Text = node.Name, VerticalAlignment = VerticalAlignment.Center } },
        }),
    };

    readonly ChoiceList _classes;
    readonly KindPreview _preview = new();
    IEditorScene? _scene;
    OutlineNode? _root;
    OutlineNode? _selected;
    Func<EntityEdit> _adding;

    public EntityTools()
    {
        var inside = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(10, 0, 0, 0), Children = { new TextBlock { Text = "Inside", Foreground = EditorBrushes.TextSoft, VerticalAlignment = VerticalAlignment.Center }, _inside } };
        var where = new HoverCard("Inside", "The new thing goes inside this one. Pick another to change it.");
        where.SaysWhyOff(_inside, "A new parent goes where the picked thing is.");
        where.On(_inside);
        _classes = new ChoiceList("Find a thing to add, like light or camera") { Beside = inside, Aside = _preview };
        AddDialog = new Dialog(AddTitle, "", _classes)
        {
            CardWidth = 720,
            Hint = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Children = { Parts.KeyHint("move", "↑", "↓"), Parts.KeyHint("add", "↵") } },
        };
        var add = new HoverCard("Add", "Puts the lit thing in the scene.", "↵");
        add.SaysWhyOff(AddDialog.MainButton, "Pick a thing to add first.");
        add.On(AddDialog.MainButton);
        _classes.PickedChanged += ShowPicked;
        _adding = Add;
        AddButton.Click += (_, _) => OpenAddChild();
        NewParentButton.Click += (_, _) => OpenAddParent();
        AddDialog.MainButton.Click += (_, _) =>
        {
            Run(_adding);
            // The new entity has its class's name, so its row opens for a better one. Escape keeps the class name.
            if (!AddDialog.IsVisible)
            {
                RenameButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
        };
        DuplicateButton.Click += (_, _) => Run(Duplicate);
        RemoveButton.Click += (_, _) => Run(Remove);
        Show(null, null, null);
    }

    /// <summary>Raised after the scene took a change.</summary>
    public event Action<EntityEdit>? Changed;

    /// <summary>Raised with the reason when the scene refused a change.</summary>
    public event Action<string>? Failed;

    /// <summary>The plus of the Scene dock. It opens the Add a thing dialog.</summary>
    internal Button AddButton { get; } = Parts.IconButton("Add", "Add an entity");

    /// <summary>Opens the Add a thing dialog to put the new entity in the selected one's place, over it.</summary>
    internal Button NewParentButton { get; } = new();

    internal Dialog AddDialog { get; }

    internal ChoiceList ClassList => _classes;

    /// <summary>Where the new thing goes. A new parent goes where the selected entity is, so it is off then.</summary>
    internal ComboBox Inside => _inside;

    internal KindPreview Preview => _preview;

    internal Button DuplicateButton { get; } = new();

    /// <summary>Starts a rename of the selected entity. The Scene dock edits the name in its row and calls <see cref="Rename"/>.</summary>
    internal Button RenameButton { get; } = new();

    internal Button RemoveButton { get; } = new();

    public void Show(IEditorScene? scene, OutlineNode? root, OutlineNode? selected)
    {
        _scene = scene;
        _root = root;
        _selected = selected;
        AddButton.IsEnabled = root is not null;
        DuplicateButton.IsEnabled = selected is not null && selected != root;
        NewParentButton.IsEnabled = selected is not null && selected != root;
        RenameButton.IsEnabled = selected is not null;
        RemoveButton.IsEnabled = selected is not null && selected != root;
    }

    /// <summary>Gives the selected entity this name.</summary>
    public void Rename(string name) => Run(() =>
    {
        _scene!.Rename(_selected!.Handle, name);
        return new EntityEdit(_selected.Handle, $"Renamed {_selected.Name}.");
    });

    /// <summary>The class a choice adds. A kind with a plain name keeps its class as the engine's name.</summary>
    internal static string ClassOf(Choice choice) => choice.Engine ?? choice.Name;

    void OpenAddChild()
    {
        AddDialog.Title = AddTitle;
        AddDialog.EngineName = "Add Child Entity";
        AddDialog.Why.Text = "Pick what to add. It goes inside the thing you had picked.";
        OpenAdd(Add, _selected ?? _root!);
        _inside.IsEnabled = true;
    }

    void OpenAddParent()
    {
        AddDialog.Title = ParentTitle;
        AddDialog.EngineName = "Reparent to New Entity";
        AddDialog.Why.Text = $"Pick what to add. It takes the place of {_selected!.Name}, and {_selected.Name} goes inside it.";
        OpenAdd(AddParent, _root!.PathTo(_selected.Handle)[^2]);
        _inside.IsEnabled = false;
    }

    // The keys go to the search, so the arrows and Enter work at once.
    void OpenAdd(Func<EntityEdit> adding, OutlineNode inside)
    {
        _adding = adding;
        _inside.ItemsSource = Flatten(_root!).ToList();
        _inside.SelectedItem = inside;
        var classes = _scene!.AddableClasses();
        var most = new ChoiceGroup("Most used");
        var rest = classes.Except(MostUsed).ToList();
        var all = new ChoiceGroup("All kinds, A to Z") { Folded = true, Note = $"{rest.Count} more, for experts" };
        _classes.Show([
            .. MostUsed.Where(classes.Contains).Select(name => KindChoice(name, most)),
            .. rest.Select(name => KindChoice(name, all)).OrderBy(choice => choice.Name, StringComparer.OrdinalIgnoreCase),
        ]);
        AddDialog.Open();
        _classes.SearchBox.Focus();
    }

    static IEnumerable<OutlineNode> Flatten(OutlineNode node) => node.Children.SelectMany(Flatten).Prepend(node);

    static Choice KindChoice(string className, ChoiceGroup group) => PlainNames.Of(className) is { } row
        ? new Choice(EditorIcons.ForClass(className), row.Name) { Line = row.Line, Engine = row.Name == className ? null : className, Group = group, Tint = EditorBrushes.TreeIcon }
        : new Choice(EditorIcons.ForClass(className), className) { Group = group, Tint = EditorBrushes.TreeIcon };

    // The main button names the lit thing, so it says what Enter will add.
    void ShowPicked()
    {
        var picked = _classes.Picked;
        AddDialog.MainButton.IsEnabled = picked is not null;
        AddDialog.MainButton.Content = Parts.Keyed(picked is null ? "Add" : $"Add {picked.Name}", "↵");
        _preview.Show(picked is null ? null : ClassOf(picked), picked?.Name ?? "");
    }

    void Run(Func<EntityEdit> change)
    {
        try
        {
            Changed?.Invoke(change());
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException)
        {
            Failed?.Invoke(error.Message);
        }
    }

    // The engine makes the name unique among the new entity's siblings.
    EntityEdit Add()
    {
        var className = ClassOf(_classes.Picked!);
        var made = _scene!.AddChild(((OutlineNode)_inside.SelectedItem!).Handle, className, className);
        AddDialog.Close();
        return new EntityEdit(made, $"Added {className}.");
    }

    // The new entity starts last among the selected one's siblings, moves up to its place and takes it under.
    // The selected entity keeps where it is drawn, because the new parent sits where the old one does.
    EntityEdit AddParent()
    {
        var className = ClassOf(_classes.Picked!);
        var parent = _root!.PathTo(_selected!.Handle)[^2];
        var at = parent.Children.ToList().FindIndex(child => child.Handle == _selected.Handle);
        var made = _scene!.AddChild(parent.Handle, className, className);
        _scene.MoveWithinParent(made, at - parent.Children.Count);
        _scene.Reparent(_selected.Handle, made, keepGlobalTransform: true);
        AddDialog.Close();
        return new EntityEdit(made, $"Added {className} over {_selected.Name}.");
    }

    EntityEdit Duplicate()
    {
        var copy = _scene!.Duplicate(_selected!.Handle);
        return new EntityEdit(copy, $"Duplicated {_selected.Name}.");
    }

    EntityEdit Remove()
    {
        _scene!.Detach(_selected!.Handle);
        return new EntityEdit(0, $"Removed {_selected.Name}.");
    }
}
