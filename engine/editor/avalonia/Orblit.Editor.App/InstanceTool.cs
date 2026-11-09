using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App;

/// <summary>Instances a scene file of the project under the selected entity, or under the root with none selected.</summary>
internal sealed class InstanceTool : StackPanel
{
    readonly ComboBox _scenes = new() { MinWidth = 220, PlaceholderText = "Scene" };
    readonly Button _instance = new() { Content = "Instance", IsEnabled = false };
    IEditorScene? _scene;
    OutlineNode? _root;
    OutlineNode? _selected;

    public InstanceTool()
    {
        Orientation = Orientation.Horizontal;
        Spacing = 8;
        Margin = new Thickness(8);
        Children.Add(_scenes);
        Children.Add(_instance);
        _scenes.SelectionChanged += (_, _) => EnableInstance();
        _instance.Click += (_, _) => Run();
    }

    /// <summary>Raised after the scene took the instance.</summary>
    public event Action<EntityEdit>? Changed;

    /// <summary>Raised with the reason when the scene refused the instance.</summary>
    public event Action<string>? Failed;

    internal ComboBox Scene => _scenes;

    internal Button InstanceButton => _instance;

    /// <summary>Takes the scene and the selection. It reads no files, because a selection changes often.</summary>
    public void Show(IEditorScene? scene, OutlineNode? root, OutlineNode? selected)
    {
        _scene = scene;
        _root = root;
        _selected = selected;
        EnableInstance();
    }

    // shortcut: only the open scene is left out, so A inside B inside A is not caught. Check the chain
    // when the bridge can read the scenes a file instances.
    /// <summary>Lists the project's scene files other than the open one, with nothing chosen.</summary>
    public void LoadScenes()
    {
        _scenes.ItemsSource = _scene?.Scenes().Where(path => !path.EndsWith('/') && path != _scene.SceneFile).ToList() ?? [];
        _scenes.SelectedIndex = -1;
    }

    void EnableInstance() => _instance.IsEnabled = _root is not null && _scenes.SelectedIndex >= 0;

    void Run()
    {
        try
        {
            var parent = (_selected ?? _root)!;
            var path = (string)_scenes.SelectedItem!;
            var made = _scene!.Instance(parent.Handle, path);
            Changed?.Invoke(new EntityEdit(made, $"Instanced {Path.GetFileNameWithoutExtension(path)} under {parent.Name}."));
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or IOException)
        {
            Failed?.Invoke(error.Message);
        }
    }
}
