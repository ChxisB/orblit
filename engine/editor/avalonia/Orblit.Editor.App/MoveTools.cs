using Avalonia;
using Avalonia.Controls;
using Orblit.Editor.App.Shell;
using Orblit.Editor.App.Theme;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App;

/// <summary>
/// Moves the selected entity up or down among its siblings, or through the Move under dialog to the end of
/// another parent. Each command is a button, so the dock, its menus and a key press the same one.
/// </summary>
internal sealed class MoveTools
{
    readonly ChoiceList _parents = new("Filter entities");
    readonly ToggleSwitch _keepGlobal = new()
    {
        IsChecked = true,
        OnContent = "Keep the global transform",
        OffContent = "Keep the global transform",
        FontSize = 12,
        Foreground = EditorBrushes.TextSoft,
        Margin = new Thickness(0, 10, 0, 0),
    };
    List<(Choice Choice, OutlineNode Entity, string Path)> _targets = [];
    IEditorScene? _scene;
    OutlineNode? _root;
    OutlineNode? _selected;

    public MoveTools()
    {
        Dialog = new Dialog("", "", new StackPanel { Children = { _parents, _keepGlobal } }) { CardWidth = 300, MainText = "Move" };
        _parents.PickedChanged += () => Dialog.MainButton.IsEnabled = _parents.Picked is not null;
        _keepGlobal.IsCheckedChanged += (_, _) => SayWhy();
        UpButton.Click += (_, _) => Run(() => Step(-1, "up"));
        DownButton.Click += (_, _) => Run(() => Step(1, "down"));
        MoveUnderButton.Click += (_, _) => OpenMoveUnder();
        Dialog.MainButton.Click += (_, _) => Run(MoveUnder);
        Show(null, null, null);
    }

    /// <summary>Raised after the scene took a move.</summary>
    public event Action<EntityEdit>? Changed;

    /// <summary>Raised with the reason when the scene refused a move.</summary>
    public event Action<string>? Failed;

    internal Button UpButton { get; } = new();

    internal Button DownButton { get; } = new();

    /// <summary>Opens the Move under dialog for the selected entity.</summary>
    internal Button MoveUnderButton { get; } = new();

    internal Dialog Dialog { get; }

    internal ChoiceList ParentList => _parents;

    internal ToggleSwitch KeepGlobal => _keepGlobal;

    public void Show(IEditorScene? scene, OutlineNode? root, OutlineNode? selected)
    {
        _scene = scene;
        _root = root;
        _selected = selected;
        var parent = root is null || selected is null ? null : ParentOf(root, selected);
        var place = parent?.Children.ToList().IndexOf(selected!) ?? -1;
        UpButton.IsEnabled = place > 0;
        DownButton.IsEnabled = parent is not null && place < parent.Children.Count - 1;
        MoveUnderButton.IsEnabled = parent is not null;
    }

    static OutlineNode? ParentOf(OutlineNode node, OutlineNode child) =>
        node.Children.Contains(child) ? node : node.Children.Select(next => ParentOf(next, child)).FirstOrDefault(found => found is not null);

    // The tree without the selected entity and what is under it. The parent it has now is listed but off.
    void OpenMoveUnder()
    {
        var parent = ParentOf(_root!, _selected!);
        _targets = [.. Tree(_root!, _root!.Name, 0)
            .Where(target => _selected!.Find(target.Entity.Handle) is null)
            .Select(target => (new Choice(EditorIcons.ForClass(target.Entity.ClassName), target.Entity.Name)
            {
                Tint = EditorBrushes.TreeIcon,
                Depth = target.Depth,
                Off = target.Entity == parent,
            }, target.Entity, target.Path))];
        Dialog.Title = $"Move {_selected!.Name} under";
        _keepGlobal.IsChecked = true;
        SayWhy();
        _parents.Show([.. _targets.Select(target => target.Choice)]);
        Dialog.Open();
        _parents.SearchBox.Focus();
    }

    void SayWhy() => Dialog.Why.Text = _keepGlobal.IsChecked == true
        ? $"{_selected?.Name} keeps its place in the world."
        : $"{_selected?.Name} keeps its local transform under the new parent.";

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

    // Two entities can share a name and a depth, so the lit choice is found by reference, not by value.
    EntityEdit MoveUnder()
    {
        var picked = _parents.Picked!;
        var target = _targets.Single(target => ReferenceEquals(target.Choice, picked));
        _scene!.Reparent(_selected!.Handle, target.Entity.Handle, _keepGlobal.IsChecked == true);
        Dialog.Close();
        return new EntityEdit(_selected.Handle, $"Moved {_selected.Name} under {target.Path}.");
    }

    // Every entity from the root down, parents first. The path is the names from the root, for example Field/Lamp.
    static IEnumerable<(OutlineNode Entity, string Path, int Depth)> Tree(OutlineNode node, string path, int depth) =>
        node.Children.SelectMany(child => Tree(child, $"{path}/{child.Name}", depth + 1)).Prepend((node, path, depth));
}
