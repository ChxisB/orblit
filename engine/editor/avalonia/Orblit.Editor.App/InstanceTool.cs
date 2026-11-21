using Avalonia.Controls;
using Orblit.Editor.App.Shell;
using Orblit.Editor.App.Theme;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App;

/// <summary>
/// The Instance a scene dialog. It lists the project's scene files and puts the chosen one under the selected
/// entity, or under the root with none selected. The open scene is listed but off, with the reason.
/// </summary>
internal sealed class InstanceTool
{
    readonly ChoiceList _scenes = new("Search scenes");
    IEditorScene? _scene;
    OutlineNode? _root;
    OutlineNode? _selected;

    public InstanceTool()
    {
        Dialog = new Dialog("Instance a scene", "", _scenes) { CardWidth = 320, MainText = "Instance" };
        _scenes.PickedChanged += () => Dialog.MainButton.IsEnabled = _scenes.Picked is not null;
        OpenButton.Click += (_, _) => Open();
        Dialog.MainButton.Click += (_, _) => Run();
        Show(null, null, null);
    }

    /// <summary>Raised after the scene took the instance.</summary>
    public event Action<EntityEdit>? Changed;

    /// <summary>Raised with the reason when the scene refused the instance.</summary>
    public event Action<string>? Failed;

    /// <summary>The link of the Scene dock. It opens the dialog.</summary>
    internal Button OpenButton { get; } = Parts.IconButton("Instance", "Instance a scene as a child of the selected entity");

    internal Dialog Dialog { get; }

    internal ChoiceList SceneList => _scenes;

    /// <summary>Takes the scene and the selection. It reads no files, because a selection changes often.</summary>
    public void Show(IEditorScene? scene, OutlineNode? root, OutlineNode? selected)
    {
        _scene = scene;
        _root = root;
        _selected = selected;
        OpenButton.IsEnabled = root is not null;
    }

    // shortcut: only the open scene is off, so A inside B inside A is not caught. Check the chain when the
    // bridge can read the scenes a file instances. The line is the folder, as the bridge cannot read a root's class.
    void Open()
    {
        Dialog.Why.Text = $"As a child of {(_selected ?? _root)!.Name}";
        _scenes.Show([.. _scene!.Scenes().Where(path => !path.EndsWith('/')).Select(path => new Choice("SceneFile", Path.GetFileName(path))
        {
            Tint = EditorBrushes.SceneIcon,
            Off = path == _scene.SceneFile,
            Line = path == _scene.SceneFile ? "This scene. It cannot hold itself." : path[..(path.LastIndexOf('/') + 1)],
        })]);
        Dialog.Open();
        _scenes.SearchBox.Focus();
    }

    void Run()
    {
        try
        {
            var parent = (_selected ?? _root)!;
            // A choice that is on has its folder as its line, so the two make the path.
            var path = _scenes.Picked!.Line + _scenes.Picked.Name;
            var made = _scene!.Instance(parent.Handle, path);
            Dialog.Close();
            Changed?.Invoke(new EntityEdit(made, $"Instanced {Path.GetFileNameWithoutExtension(path)} under {parent.Name}."));
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or IOException)
        {
            Failed?.Invoke(error.Message);
        }
    }
}
