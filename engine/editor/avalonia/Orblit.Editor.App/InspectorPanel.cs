using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Orblit.Editor.App.Shell;
using Orblit.Editor.App.Theme;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App;

/// <summary>
/// The selected entity's classes, most derived first, each under its bar with its own rows and groups. Only an
/// Entity3D has the Transform rows, which edit rotation in degrees through the transform calls. Every other row
/// edits through the engine's typed get and set. With nothing selected the list is empty. A filter hides the rows
/// whose label lacks the text, and every bar and group that then holds no row. A closed group with a match shows
/// only its header. The properties, the Script row and the metadata rows read the scene when the selection
/// changes and after an edit here, and not on each drag step.
/// </summary>
internal sealed class InspectorPanel : StackPanel
{
    // The engine's Transform group holds these, and the vector rows edit them through the transform calls.
    static readonly string[] s_vectorNames = ["position", "rotation", "scale"];
    static readonly Thickness s_groupIndent = new(12, -2, 0, 0);

    readonly VectorRow _position = new("Position", "m");
    readonly VectorRow _rotation = new("Rotation", "°");
    readonly VectorRow _scale = new("Scale");
    readonly StackPanel _transformRows = new() { Margin = s_groupIndent };
    readonly PropertyGroup _transform;
    readonly StackPanel _sections = new();
    readonly List<PropertyRow> _rows = [];
    // Whether each group was open, by title, so a group keeps its fold from one entity to the next.
    readonly Dictionary<string, bool> _folds = [];
    readonly ScriptRow _script = new();
    readonly MetadataRows _metadataRows = new();
    readonly MetadataTool _metadataTool = new();
    readonly Flyout _metadataFlyout = new() { FlyoutPresenterClasses = { EditorTheme.Flush } };
    readonly Button _metadata = Dashed("Add", "Add Metadata");
    readonly ScriptTool _scriptTool = new();
    readonly Flyout _attachFlyout = new() { FlyoutPresenterClasses = { EditorTheme.Flush } };
    readonly Button _attach = Dashed("ScriptCreate", "Attach a script");
    readonly TextBlock _count = new() { FontSize = 11, Foreground = EditorBrushes.TextDim, Margin = new Thickness(6, 8, 6, 14) };
    readonly Control[] _entityParts;
    IEditorScene? _scene;
    OutlineNode? _node;
    IReadOnlyList<string> _chain = [];
    // The classes and property names the sections were built for. Values alone change in place.
    string _built = "";
    string _filter = "";

    public InspectorPanel()
    {
        Margin = new Thickness(6);
        _transform = new PropertyGroup("Transform", _transformRows);
        Add(_position, (entity, value) => _scene!.MoveTo(entity, value));
        Add(_rotation, (entity, value) => _scene!.RotateTo(entity, value));
        Add(_scale, (entity, value) => _scene!.ScaleTo(entity, value));
        _entityParts = [_script, _attach, _metadataRows, _metadata];
        Children.AddRange([_sections, .. _entityParts, _count]);
        WireExtras();
        Show(null, null);
    }

    /// <summary>Raised after the scene took a new position, rotation, scale, property, metadata value or script.</summary>
    public event Action? Edited;

    /// <summary>Raised with the reason when the scene refused a property, a metadata value or a script.</summary>
    public event Action<string>? Failed;

    /// <summary>Raised with the res:// path of the attached script when Edit is chosen in the script menu.</summary>
    public event Action<string>? Editing;

    internal VectorRow Position => _position;

    internal VectorRow Rotation => _rotation;

    internal VectorRow Scale => _scale;

    internal Button ScriptField => _script.Field;

    internal Button MetadataButton => _metadata;

    internal Button AttachButton => _attach;

    internal ScriptTool ScriptTool => _scriptTool;

    internal Flyout AttachFlyout => _attachFlyout;

    internal MetadataRows MetadataRows => _metadataRows;

    internal MetadataTool MetadataTool => _metadataTool;

    internal Flyout MetadataFlyout => _metadataFlyout;

    internal MenuFlyout ScriptMenu => _script.Menu;

    internal TextBlock FilterCount => _count;

    /// <summary>The selected entity's class and the classes it inherits, most derived first.</summary>
    internal IReadOnlyList<string> Chain => _chain;

    internal IReadOnlyList<PropertyRow> Rows => _rows;

    bool IsSelected => _scene is not null && _node is not null;

    bool IsEditable => IsSelected && _scene!.IsEntity3D(_node!.Handle);

    public void Show(IEditorScene? scene, OutlineNode? node)
    {
        var selectionChanged = !ReferenceEquals(scene, _scene) || !ReferenceEquals(node, _node);
        _scene = scene;
        _node = node;
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

    /// <summary>Opens or closes every group.</summary>
    public void FoldAll(bool open)
    {
        foreach (var group in Groups())
        {
            group.Fold(open);
        }
    }

    IEnumerable<PropertyGroup> Groups() => _sections.Children.OfType<Panel>().SelectMany(section => section.Children.OfType<PropertyGroup>());

    // The vector rows count only while their group is in a section.
    IEnumerable<(Control Row, string Label)> Labeled() =>
        [.. (_transform.Parent is null ? [] : new[] { _position, _rotation, _scale }).Select(row => ((Control)row, row.Label)), .. _rows.Select(row => ((Control)row, row.Label))];

    // A filter keeps the rows that match, and the bar and group above them. The script and metadata parts show
    // only when there is no filter.
    void ShowRows()
    {
        var any = _node is not null;
        var all = any && _filter.Length == 0;
        var labeled = Labeled().ToArray();
        // The vector rows stay hidden while their group is in no section.
        _position.IsVisible = _rotation.IsVisible = _scale.IsVisible = false;
        foreach (var (row, label) in labeled)
        {
            row.IsVisible = label.Contains(_filter, StringComparison.OrdinalIgnoreCase);
        }
        foreach (var section in _sections.Children.OfType<Panel>())
        {
            foreach (var group in section.Children.OfType<PropertyGroup>())
            {
                group.IsVisible = ((Panel)group.Body).Children.Any(row => row.IsVisible);
            }
            section.Children[0].IsVisible = all || section.Children.Skip(1).Any(part => part.IsVisible);
        }
        foreach (var part in _entityParts)
        {
            part.IsVisible = all;
        }

        var matched = labeled.Count(row => row.Row.IsVisible);
        _count.IsVisible = any && !all;
        _count.Text = matched == 0 ? "No properties match." : $"{matched} of {labeled.Length} properties match.";
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
        ShowProperties(entity);
        _metadataRows.Show(entity == 0 ? [] : _scene!.MetadataOf(entity));
        _script.Show(entity == 0 ? null : _scene!.ScriptOf(entity));
        if (entity != 0)
        {
            _metadataTool.ShowOwner(_node!.Name, _scene!.SceneFile);
        }
    }

    // The sections are built again only for other classes or property names, so a row keeps its box and caret
    // when only its value changed.
    void ShowProperties(nint entity)
    {
        _chain = entity == 0 ? [] : _scene!.ClassChainOf(entity);
        var properties = entity == 0 ? [] : _scene!.PropertiesOf(entity);
        var built = string.Join(',', _chain) + ';' + string.Join(',', properties.Select(property => property.Name));
        if (built != _built)
        {
            _built = built;
            Build(properties);
            return;
        }
        var values = properties.DistinctBy(property => property.Name).ToDictionary(property => property.Name, property => property.Value);
        foreach (var row in _rows)
        {
            row.Show(values[row.PropertyName]);
        }
    }

    // One section per class: its bar, its rows with no group, then its groups in the engine's order. The
    // Entity3D section starts with the Transform group, which takes the engine's other Transform rows.
    void Build(IReadOnlyList<EntityProperty> properties)
    {
        foreach (var group in Groups().Where(group => group != _transform))
        {
            _folds[group.Title] = group.IsOpen;
        }
        foreach (var section in _sections.Children.OfType<Panel>())
        {
            section.Children.Clear();
        }
        _sections.Children.Clear();
        _transformRows.Children.RemoveRange(s_vectorNames.Length, _transformRows.Children.Count - s_vectorNames.Length);
        _rows.Clear();
        foreach (var className in _chain)
        {
            var own = properties.Where(property => property.Class == className).ToArray();
            var transform = className == "Entity3D" && IsEditable;
            var section = new StackPanel { Children = { new ClassBar(className) } };
            if (transform)
            {
                section.Children.Add(_transform);
                _transformRows.Children.AddRange(own.Where(property => property.Group == "Transform" && !s_vectorNames.Contains(property.Name)).Select(Row));
            }
            section.Children.AddRange(own.Where(property => property.Group.Length == 0).Select(Row));
            foreach (var group in own.Where(property => property.Group.Length > 0 && !(transform && property.Group == "Transform")).GroupBy(property => property.Group))
            {
                var body = new StackPanel { Margin = s_groupIndent };
                body.Children.AddRange(group.Select(Row));
                var folder = new PropertyGroup(group.Key, body);
                folder.Fold(_folds.GetValueOrDefault(group.Key));
                section.Children.Add(folder);
            }
            _sections.Children.Add(section);
        }
    }

    PropertyRow Row(EntityProperty property)
    {
        var row = new PropertyRow(property);
        row.Entered += value => TryChange(entity => _scene!.SetProperty(entity, property.Name, value));
        _rows.Add(row);
        return row;
    }

    void WireExtras()
    {
        _script.Choices = () => _scene is null ? [] : [.. _scene.Scripts(), .. _scene.GraphScripts()];
        _script.Chosen += path => TryChange(entity => _scene!.AttachScript(entity, path));
        _script.Cleared += () => TryChange(entity => _scene!.ClearScript(entity));
        _script.Loading += () => _attachFlyout.ShowAt(_script.Field);
        _script.Editing += path => Editing?.Invoke(path);
        _metadataRows.Entered += (name, value) => TryChange(entity => _scene!.SetMetadata(entity, name, value));
        _metadataRows.Removed += name => TryChange(entity => _scene!.RemoveMetadata(entity, name));
        _metadataFlyout.Content = _metadataTool;
        _metadata.Flyout = _metadataFlyout;
        _metadataTool.Cancelled += _metadataFlyout.Hide;
        // The board draws Attach a script a little taller and quieter than Add Metadata.
        _attach.Height = 32;
        _attach.Margin = new Thickness(0, 10, 0, 0);
        _attach.Foreground = EditorBrushes.TextDim;
        _attachFlyout.Content = _scriptTool;
        _attach.Flyout = _attachFlyout;
        _attachFlyout.Opening += (_, _) =>
        {
            if (IsSelected)
            {
                _scriptTool.Show(_node!.Name, _scene!.Scripts(), _scene.GraphScripts());
            }
        };
        _scriptTool.Cancelled += _attachFlyout.Hide;
        _scriptTool.Requested += path =>
        {
            if (TryChange(entity => _scene!.AttachScript(entity, path)))
            {
                _attachFlyout.Hide();
            }
        };
        // The popover shows in a popup, so the button in the window says it.
        _scriptTool.NewButton.Click += (_, _) => _attach.RaiseEvent(new UnbuiltEventArgs("New script is not built yet."));
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

    // A ghost button on a dashed edge, because it adds to the entity and holds no value. Border draws no dashes,
    // so a rectangle under the content does.
    static Button Dashed(string icon, string text) => new()
    {
        Height = 30,
        MinHeight = 0,
        MinWidth = 0,
        Padding = new Thickness(0),
        BorderThickness = new Thickness(0),
        Margin = new Thickness(0, 13, 0, 0),
        HorizontalAlignment = HorizontalAlignment.Stretch,
        HorizontalContentAlignment = HorizontalAlignment.Stretch,
        VerticalContentAlignment = VerticalAlignment.Stretch,
        Foreground = EditorBrushes.TextSoft,
        CornerRadius = EditorTheme.Corners,
        Content = new Panel
        {
            Children =
            {
                new Rectangle { Stroke = EditorBrushes.LineStrong, StrokeThickness = 1, StrokeDashArray = [4, 3], RadiusX = 6, RadiusY = 6 },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 6,
                    Margin = new Thickness(12, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    Children = { new EditorIcon(icon) { VerticalAlignment = VerticalAlignment.Center }, new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center } },
                },
            },
        },
    };
}
