using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App;

/// <summary>Moves the selected entity up or down among its siblings, or to the end of another parent.</summary>
internal sealed class MoveTools : StackPanel
{
    readonly Button _up = new() { Content = "Up" };
    readonly Button _down = new() { Content = "Down" };
    readonly ComboBox _parent = new() { MinWidth = 160, PlaceholderText = "New parent" };
    readonly Button _moveUnder = new() { Content = "Move under" };
    List<(OutlineNode Entity, string Path)> _choices = [];
    IEditorScene? _scene;
    OutlineNode? _selected;

    public MoveTools()
    {
        Orientation = Orientation.Horizontal;
        Spacing = 8;
        Margin = new Thickness(8, 0, 8, 8);
        Children.Add(_up);
        Children.Add(_down);
        Children.Add(_parent);
        Children.Add(_moveUnder);
        _up.Click += (_, _) => Run(() => Step(-1, "up"));
        _down.Click += (_, _) => Run(() => Step(1, "down"));
        _parent.SelectionChanged += (_, _) => _moveUnder.IsEnabled = _parent.SelectedIndex >= 0;
        _moveUnder.Click += (_, _) => Run(MoveUnder);
        Show(null, null, null);
    }

    /// <summary>Raised after the scene took a move.</summary>
    public event Action<EntityEdit>? Changed;

    /// <summary>Raised with the reason when the scene refused a move.</summary>
    public event Action<string>? Failed;

    internal Button UpButton => _up;

    internal Button DownButton => _down;

    internal ComboBox ParentBox => _parent;

    internal Button MoveUnderButton => _moveUnder;

    public void Show(IEditorScene? scene, OutlineNode? root, OutlineNode? selected)
    {
        _scene = scene;
        _selected = selected;
        var parent = root is null || selected is null ? null : ParentOf(root, selected);
        var place = parent?.Children.ToList().IndexOf(selected!) ?? -1;
        _up.IsEnabled = place > 0;
        _down.IsEnabled = parent is not null && place < parent.Children.Count - 1;
        _choices = parent is null ? [] : Choices(root!, selected!, parent);
        _parent.ItemsSource = _choices.Select(choice => choice.Path).ToList();
        _parent.SelectedIndex = -1;
        _moveUnder.IsEnabled = false;
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

    EntityEdit Step(int places, string word)
    {
        _scene!.MoveWithinParent(_selected!.Handle, places);
        return new EntityEdit(_selected.Handle, $"Moved {_selected.Name} {word}.");
    }

    EntityEdit MoveUnder()
    {
        var target = _choices[_parent.SelectedIndex];
        _scene!.Reparent(_selected!.Handle, target.Entity.Handle);
        return new EntityEdit(_selected.Handle, $"Moved {_selected.Name} under {target.Path}.");
    }

    // Every entity but the selected one, what is under it and where it already is. The path is the names
    // from the scene root down, for example Field/Lamp.
    static List<(OutlineNode Entity, string Path)> Choices(OutlineNode root, OutlineNode selected, OutlineNode parent) =>
        All(root, root.Name).Where(choice => choice.Entity != parent && selected.Find(choice.Entity.Handle) is null).ToList();

    static IEnumerable<(OutlineNode Entity, string Path)> All(OutlineNode node, string path) =>
        node.Children.SelectMany(child => All(child, $"{path}/{child.Name}")).Prepend((node, path));

    static OutlineNode? ParentOf(OutlineNode node, OutlineNode child) =>
        node.Children.Contains(child) ? node : node.Children.Select(next => ParentOf(next, child)).FirstOrDefault(found => found is not null);
}
