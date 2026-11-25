using Avalonia.Controls;
using Orblit.Editor.App.Shell;
using Orblit.Editor.App.Theme;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App;

/// <summary>
/// The Put a scene inside dialog. It lists the project's scene files, each with 2D or 3D, and puts the chosen one
/// under the selected entity, or under the root with none selected. The open scene is listed but off, with the
/// reason.
/// </summary>
internal sealed class InstanceTool
{
    internal const string WhatACopyIs = "It shows up as one thing. Change the original, and every copy changes with it.";

    readonly ChoiceList _scenes = new("Find a scene");
    // Two scenes in different folders can share a name and a kind, so each choice maps to its path by reference.
    readonly Dictionary<Choice, string> _paths = new(ReferenceEqualityComparer.Instance);
    IEditorScene? _scene;
    OutlineNode? _root;
    OutlineNode? _selected;

    public InstanceTool()
    {
        Dialog = new Dialog("", WhatACopyIs, _scenes) { CardWidth = 420, EngineName = "Instance Scene" };
        Dialog.MainButton.Content = Parts.Keyed("Put it in", "↵");
        var card = new HoverCard("Put it in", "Puts a copy of the lit scene inside.", "↵");
        card.SaysWhyOff(Dialog.MainButton, "Pick a scene first.");
        card.On(Dialog.MainButton);
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
    internal Button OpenButton { get; } = Parts.IconButton("Instance", "Put a scene inside the picked thing");

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
    // bridge can read the scenes a file instances.
    void Open()
    {
        Dialog.Title = $"Put a scene inside {(_selected ?? _root)!.Name}";
        _paths.Clear();
        foreach (var path in _scene!.Scenes().Where(path => !path.EndsWith('/')))
        {
            _paths[SceneChoice(path)] = path;
        }

        _scenes.Show([.. _paths.Keys]);
        Dialog.Open();
        _scenes.SearchBox.Focus();
    }

    // A scene outside the top folder adds its folder to the line, so two of the same name tell apart.
    Choice SceneChoice(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        var folder = path[..(path.LastIndexOf('/') + 1)].Replace("res://", "");
        return new Choice("SceneFile", name)
        {
            Tint = EditorBrushes.SceneIcon,
            Off = path == _scene!.SceneFile,
            Line = path == _scene.SceneFile
                ? $"You are in {name}. A scene can't hold itself."
                : string.Join(" · ", new[] { _scene.SceneKindOf(path), folder }.Where(part => part != "")),
        };
    }

    /// <summary>Puts a copy of the scene at the res:// path inside the selected thing, or inside the top thing when none is picked.</summary>
    public void Use(string path)
    {
        if (_root is null)
        {
            Failed?.Invoke("Open a scene first.");
            return;
        }

        Put(path);
    }

    void Run() => Put(_paths[_scenes.Picked!]);

    void Put(string path)
    {
        try
        {
            var parent = (_selected ?? _root)!;
            var made = _scene!.Instance(parent.Handle, path);
            Dialog.Close();
            Changed?.Invoke(new EntityEdit(made, $"Put {Path.GetFileNameWithoutExtension(path)} inside {parent.Name}."));
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or IOException)
        {
            Failed?.Invoke(error.Message);
        }
    }
}
