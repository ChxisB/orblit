using Avalonia.Controls;
using Avalonia.Interactivity;
using Orblit.Editor.App.Shell;
using Orblit.Editor.App.Theme;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App;

/// <summary>What a tool changed. Select is the entity to show selected afterwards, or zero for none.</summary>
internal sealed record EntityEdit(nint Select, string Message);

/// <summary>
/// Adds an entity under the selected one, or over it as its new parent, through the Add an entity dialog, and
/// copies, renames and removes the selected entity. Each command is a button, so the dock, its menus and a key press the same one.
/// </summary>
internal sealed class EntityTools
{
    // The classes most scenes start with come first, each with a line on what it is for.
    internal static readonly (string ClassName, string Line)[] Common =
    [
        ("Entity", "The base of every entity."),
        ("Entity3D", "A point in 3D space."),
        ("MeshInstance3D", "Draws a mesh."),
        ("Camera3D", "Looks at the world."),
        ("DirectionalLight3D", "Light from far away, like the sun."),
        ("Marker3D", "A point to place things at."),
        ("Entity2D", "A point in 2D space."),
        ("ColorRect", "A filled rectangle for UI."),
    ];

    readonly ChoiceList _classes = new("Search classes");
    IEditorScene? _scene;
    OutlineNode? _root;
    OutlineNode? _selected;
    Func<EntityEdit> _adding;

    public EntityTools()
    {
        AddDialog = new Dialog("Add an entity", "", _classes) { CardWidth = 320, MainText = "Add", Hint = "↑ ↓ to move, ↵ to add" };
        AddDialog.CancelButton.IsVisible = false;
        _classes.PickedChanged += () => AddDialog.MainButton.IsEnabled = _classes.Picked is not null;
        _adding = Add;
        AddButton.Click += (_, _) => OpenAdd(Add, $"As a child of {(_selected ?? _root)!.Name}");
        NewParentButton.Click += (_, _) => OpenAdd(AddParent, $"As the parent of {_selected!.Name}");
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

    /// <summary>The plus of the Scene dock. It opens the Add an entity dialog.</summary>
    internal Button AddButton { get; } = Parts.IconButton("Add", "Add an entity");

    /// <summary>Opens the Add an entity dialog to put the new entity in the selected one's place, over it.</summary>
    internal Button NewParentButton { get; } = new();

    internal Dialog AddDialog { get; }

    internal ChoiceList ClassList => _classes;

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

    // The keys go to the search, so the arrows and Enter work at once.
    void OpenAdd(Func<EntityEdit> adding, string why)
    {
        _adding = adding;
        AddDialog.Why.Text = why;
        var classes = _scene!.AddableClasses();
        var lined = Common.Where(entry => classes.Contains(entry.ClassName));
        var rest = classes.Except(Common.Select(entry => entry.ClassName)).Select(name => (ClassName: name, Line: ""));
        _classes.Show([.. lined.Concat(rest).Select(entry => new Choice(EditorIcons.ForClass(entry.ClassName), entry.ClassName) { Line = entry.Line, Tint = EditorBrushes.TreeIcon })]);
        AddDialog.Open();
        _classes.SearchBox.Focus();
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
        var className = _classes.Picked!.Name;
        var made = _scene!.AddChild((_selected ?? _root)!.Handle, className, className);
        AddDialog.Close();
        return new EntityEdit(made, $"Added {className}.");
    }

    // The new entity starts last among the selected one's siblings, moves up to its place and takes it under.
    // The selected entity keeps where it is drawn, because the new parent sits where the old one does.
    EntityEdit AddParent()
    {
        var className = _classes.Picked!.Name;
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
