using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Orblit.Editor.App.Shell;
using Orblit.Editor.App.Theme;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App;

/// <summary>
/// The selected entity's classes and their groups. Only an Entity3D has a transform to edit. Rotation is in
/// degrees. With nothing selected the list is empty. A filter hides the rows whose label lacks the text, and
/// every bar and group that then holds no row. The groups beside Transform have no rows yet. The Script row and the
/// metadata rows read the scene when the selection changes, and not on each drag step.
/// </summary>
internal sealed class InspectorPanel : StackPanel
{
    static readonly string[] s_entityGroups = ["Process", "Physics Interpolation", "Auto Translate", "Editor Description"];

    readonly ClassBar _classBar = new();
    readonly ClassBar _entity3DBar = new("Entity3D");
    readonly ClassBar _entityBar = new("Entity");
    readonly VectorRow _position = new("Position", "m");
    readonly VectorRow _rotation = new("Rotation", "°");
    readonly VectorRow _scale = new("Scale");
    readonly StackPanel _transformRows = new() { Margin = new Thickness(12, -2, 0, 0) };
    readonly PropertyGroup _transform;
    readonly PropertyGroup _visibility = new("Visibility");
    readonly ScriptRow _script = new();
    readonly MetadataRows _metadataRows = new();
    readonly MetadataTool _metadataTool = new();
    readonly Flyout _metadataFlyout = new();
    readonly Button _metadata = Metadata();
    readonly Control[] _entityParts;
    IEditorScene? _scene;
    OutlineNode? _node;
    string _filter = "";

    public InspectorPanel()
    {
        Margin = new Thickness(6);
        _transform = new PropertyGroup("Transform", _transformRows);
        Add(_position, (entity, value) => _scene!.MoveTo(entity, value));
        Add(_rotation, (entity, value) => _scene!.RotateTo(entity, value));
        Add(_scale, (entity, value) => _scene!.ScaleTo(entity, value));
        _entityParts = [_entityBar, .. s_entityGroups.Select(title => new PropertyGroup(title)), _script, _metadataRows, _metadata];
        Children.AddRange([_classBar, _entity3DBar, _transform, _visibility, .. _entityParts]);
        WireExtras();
        Show(null, null);
    }

    /// <summary>Raised after the scene took a new position, rotation, scale, metadata value or script.</summary>
    public event Action? Edited;

    /// <summary>Raised with the reason when the scene refused a metadata value or a script.</summary>
    public event Action<string>? Failed;

    internal VectorRow Position => _position;

    internal VectorRow Rotation => _rotation;

    internal VectorRow Scale => _scale;

    internal Button ScriptField => _script.Field;

    internal Button MetadataButton => _metadata;

    internal MetadataRows MetadataRows => _metadataRows;

    internal MetadataTool MetadataTool => _metadataTool;

    internal Flyout MetadataFlyout => _metadataFlyout;

    internal MenuFlyout ScriptMenu => _script.Menu;

    bool IsSelected => _scene is not null && _node is not null;

    bool IsEditable => IsSelected && _scene!.IsEntity3D(_node!.Handle);

    public void Show(IEditorScene? scene, OutlineNode? node)
    {
        var selectionChanged = !ReferenceEquals(scene, _scene) || !ReferenceEquals(node, _node);
        _scene = scene;
        _node = node;
        _classBar.Show(node?.ClassName ?? "");
        Refresh();
        if (selectionChanged)
        {
            ShowExtras();
        }
        ShowRows();
    }

    /// <summary>Shows only the rows whose label holds this text, ignoring case.</summary>
    public void SetFilter(string text)
    {
        _filter = text.Trim();
        ShowRows();
    }

    // A filter keeps the rows that match, and the bar and group above them. The parts with no rows show only
    // when there is no filter.
    void ShowRows()
    {
        var any = _node is not null;
        var all = any && _filter.Length == 0;
        foreach (var row in new[] { _position, _rotation, _scale })
        {
            row.IsVisible = any && row.Label.Contains(_filter, StringComparison.OrdinalIgnoreCase);
        }

        _transform.IsVisible = IsEditable && _transformRows.Children.Any(row => row.IsVisible);
        _visibility.IsVisible = all && IsEditable;
        _entity3DBar.IsVisible = _transform.IsVisible || _visibility.IsVisible;
        _classBar.IsVisible = all && _node!.ClassName is not ("Entity3D" or "Entity");
        foreach (var part in _entityParts)
        {
            part.IsVisible = all;
        }
    }

    // The scene may answer with other numbers than the ones typed, such as a rotation turned into range.
    void Refresh()
    {
        if (!IsEditable)
        {
            _position.Show(null);
            _rotation.Show(null);
            _scale.Show(null);
            return;
        }
        var entity = _node!.Handle;
        _position.Show(_scene!.PositionOf(entity));
        _rotation.Show(_scene.RotationOf(entity));
        _scale.Show(_scene.ScaleOf(entity));
    }

    void ShowExtras()
    {
        var entity = _scene is not null && _node is not null ? _node.Handle : 0;
        _metadataRows.Show(entity == 0 ? [] : _scene!.MetadataOf(entity));
        _script.Show(entity == 0 ? null : _scene!.ScriptOf(entity));
    }

    void WireExtras()
    {
        _script.Choices = () => _scene?.Scripts() ?? [];
        _script.Chosen += path => TryChange(entity => _scene!.AttachScript(entity, path));
        _script.Cleared += () => TryChange(entity => _scene!.ClearScript(entity));
        _metadataRows.Entered += (name, value) => TryChange(entity => _scene!.SetMetadata(entity, name, value));
        _metadataRows.Removed += name => TryChange(entity => _scene!.RemoveMetadata(entity, name));
        _metadataFlyout.Content = _metadataTool;
        _metadata.Flyout = _metadataFlyout;
        _metadataTool.Requested += (name, value) =>
        {
            if (TryChange(entity => _scene!.SetMetadata(entity, name, value)))
            {
                _metadataFlyout.Hide();
                _metadataTool.Clear();
            }
        };
    }

    // Runs one edit on the selected entity and answers whether the scene took it. Either way the rows show what the
    // scene holds, so a refused value is put back.
    bool TryChange(Action<nint> write)
    {
        if (!IsSelected)
        {
            return false;
        }
        try
        {
            write(_node!.Handle);
            Edited?.Invoke();
            return true;
        }
        catch (Exception error) when (error is ArgumentException or IOException)
        {
            Failed?.Invoke(error.Message);
            return false;
        }
        finally
        {
            ShowExtras();
        }
    }

    void Add(VectorRow row, Action<nint, Vector3> write)
    {
        row.Entered += value =>
        {
            if (IsEditable)
            {
                write(_node!.Handle, value);
                Edited?.Invoke();
            }
            Refresh();
        };
        _transformRows.Children.Add(row);
    }

    static Button Metadata() => new()
    {
        Width = 130,
        Height = 30,
        MinHeight = 0,
        MinWidth = 0,
        Padding = new Thickness(0),
        BorderThickness = new Thickness(0),
        Margin = new Thickness(0, 13, 0, 0),
        HorizontalAlignment = HorizontalAlignment.Center,
        HorizontalContentAlignment = HorizontalAlignment.Center,
        VerticalContentAlignment = VerticalAlignment.Center,
        Background = EditorBrushes.ActiveTool,
        CornerRadius = EditorTheme.Corners,
        Content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children = { new EditorIcon("Add") { VerticalAlignment = VerticalAlignment.Center }, new TextBlock { Text = "Add Metadata", VerticalAlignment = VerticalAlignment.Center } },
        },
    };
}
