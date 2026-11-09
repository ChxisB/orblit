using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App;

/// <summary>What a tool changed. Select is the entity to show selected afterwards, or zero for none.</summary>
internal sealed record EntityEdit(nint Select, string Message);

/// <summary>Adds an entity under the selected one, copies the selected entity, renames it and removes it.</summary>
internal sealed class EntityTools : StackPanel
{
    // shortcut: a short fixed list. Replace it with a class browser when the editor has one.
    internal static readonly string[] ClassNames = ["Entity", "Entity3D", "Marker3D", "MeshInstance3D", "Camera3D"];

    readonly ComboBox _class = new() { ItemsSource = ClassNames, SelectedIndex = 0 };
    readonly TextBox _name = new() { PlaceholderText = "Name", MinWidth = 160 };
    readonly Button _add = new() { Content = "Add" };
    readonly Button _duplicate = new() { Content = "Duplicate" };
    readonly Button _rename = new() { Content = "Rename" };
    readonly Button _remove = new() { Content = "Remove" };
    IEditorScene? _scene;
    OutlineNode? _root;
    OutlineNode? _selected;

    public EntityTools()
    {
        Orientation = Orientation.Horizontal;
        Spacing = 8;
        Margin = new Thickness(8, 0, 8, 8);
        Children.Add(_class);
        Children.Add(_name);
        Children.Add(_add);
        Children.Add(_duplicate);
        Children.Add(_rename);
        Children.Add(_remove);
        _add.Click += (_, _) => Run(Add);
        _duplicate.Click += (_, _) => Run(Duplicate);
        _rename.Click += (_, _) => Run(Rename);
        _remove.Click += (_, _) => Run(Remove);
        Show(null, null, null);
    }

    /// <summary>Raised after the scene took a change.</summary>
    public event Action<EntityEdit>? Changed;

    /// <summary>Raised with the reason when the scene refused a change.</summary>
    public event Action<string>? Failed;

    internal ComboBox Class => _class;

    internal TextBox NameBox => _name;

    internal Button AddButton => _add;

    internal Button DuplicateButton => _duplicate;

    internal Button RenameButton => _rename;

    internal Button RemoveButton => _remove;

    public void Show(IEditorScene? scene, OutlineNode? root, OutlineNode? selected)
    {
        _scene = scene;
        _root = root;
        _selected = selected;
        _add.IsEnabled = root is not null;
        _duplicate.IsEnabled = selected is not null && selected != root;
        _rename.IsEnabled = selected is not null;
        _remove.IsEnabled = selected is not null && selected != root;
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

    EntityEdit Add()
    {
        var className = (string)_class.SelectedItem!;
        var name = string.IsNullOrWhiteSpace(_name.Text) ? className : _name.Text.Trim();
        var made = _scene!.AddChild((_selected ?? _root)!.Handle, className, name);
        return new EntityEdit(made, $"Added {name}.");
    }

    EntityEdit Duplicate()
    {
        var copy = _scene!.Duplicate(_selected!.Handle);
        return new EntityEdit(copy, $"Duplicated {_selected.Name}.");
    }

    EntityEdit Rename()
    {
        _scene!.Rename(_selected!.Handle, _name.Text ?? "");
        return new EntityEdit(_selected.Handle, $"Renamed {_selected.Name}.");
    }

    EntityEdit Remove()
    {
        _scene!.Remove(_selected!.Handle);
        return new EntityEdit(0, $"Removed {_selected.Name}.");
    }
}
