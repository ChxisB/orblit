using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
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
/// changes and after an edit here, and not on each drag step. With several entities picked it shows the classes
/// they all have, a value they differ in shows Mixed, and an edit writes each of them. The script and metadata
/// parts show for one entity only.
/// </summary>
internal sealed class InspectorPanel : StackPanel
{
    // The engine's Transform group holds these, and the vector rows edit them through the transform calls.
    static readonly string[] s_vectorNames = ["position", "rotation", "scale"];
    static readonly Thickness s_groupIndent = new(12, 0, 0, 0);

    readonly VectorRow _position = new("Position", "m");
    readonly VectorRow _rotation = new("Turn", "°");
    readonly VectorRow _scale = new("Size", "×");
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
    readonly Button _metadata = new HoverCard("Add a custom value", "Store your own data on this thing, like a score or a door code.", engine: "Metadata").On(Dashed("Add", "Add a custom value"));
    readonly ScriptTool _scriptTool = new();
    readonly Flyout _attachFlyout = new() { FlyoutPresenterClasses = { EditorTheme.Flush } };
    readonly Button _attach = new HoverCard("Add a script", "Tell this thing what to do while the game runs.", engine: "Attach Script").On(Dashed("ScriptCreate", "Add a script"));
    readonly TextBlock _rootLine = new() { FontSize = 12, Foreground = EditorBrushes.TextDim, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(6, 2, 6, 6) };
    readonly TextBlock _count = new() { FontSize = 11, Foreground = EditorBrushes.TextDim, Margin = new Thickness(6, 8, 6, 14) };
    readonly Control[] _entityParts;
    IEditorScene? _scene;
    IReadOnlyList<OutlineNode> _picked = [];
    // The entity picked last, which the script and metadata parts and the class check of a paste name.
    OutlineNode? _node;
    (string ClassName, (string Name, object Value)[] Values)? _copied;
    IReadOnlyList<string> _chain = [];
    // The classes and property names the sections were built for. Values alone change in place.
    string _built = "";
    string _filter = "";
    bool _isRoot;

    public InspectorPanel()
    {
        Margin = new Thickness(6);
        _transform = new PropertyGroup("Where it is", _transformRows);
        Add(_position, entity => _scene!.PositionOf(entity), (entity, value) => _scene!.MoveTo(entity, value));
        Add(_rotation, entity => _scene!.RotationOf(entity), (entity, value) => _scene!.RotateTo(entity, value));
        Add(_scale, entity => _scene!.ScaleOf(entity), (entity, value) => _scene!.ScaleTo(entity, value));
        _entityParts = [_script, _attach, _metadataRows, _metadata];
        Children.AddRange([_rootLine, _sections, .. _entityParts, _count]);
        WireExtras();
        Show(null, []);
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

    /// <summary>The line over the settings that says the picked thing is the top of the scene.</summary>
    internal TextBlock RootLine => _rootLine;

    /// <summary>Whether Copy all settings kept values that a paste can put on.</summary>
    internal bool HasCopied => _copied is not null;

    /// <summary>The selected entity's class and the classes it inherits that every picked entity has, most derived first.</summary>
    internal IReadOnlyList<string> Chain => _chain;

    internal IReadOnlyList<PropertyRow> Rows => _rows;

    bool IsSelected => _scene is not null && _node is not null;

    bool IsEditable => IsSelected && _picked.All(node => _scene!.IsEntity3D(node.Handle));

    /// <summary>Shows the picked entities, with the selected one last.</summary>
    public void Show(IEditorScene? scene, IReadOnlyList<OutlineNode> picked)
    {
        var selectionChanged = !ReferenceEquals(scene, _scene) || !picked.SequenceEqual(_picked, ReferenceEqualityComparer.Instance);
        _scene = scene;
        _picked = picked;
        _node = picked.Count == 0 ? null : picked[^1];
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

    /// <summary>Keeps the selected entity's values for <see cref="PasteProperties"/>. An object is left out, because the engine can free it before the paste.</summary>
    public void CopyProperties()
    {
        if (IsSelected)
        {
            _copied = (_node!.ClassName, [.. _scene!.PropertiesOf(_node.Handle).Where(property => property.Value is not (null or nint)).Select(property => (property.Name, property.Value!))]);
        }
    }

    /// <summary>Sets the copied values on the selected entity. It must be of the class they came from.</summary>
    public void PasteProperties()
    {
        if (!IsSelected)
        {
            return;
        }
        if (_copied is not { } copied)
        {
            Failed?.Invoke("Copy settings first, then paste them onto a thing of the same kind.");
        }
        else if (_picked.FirstOrDefault(node => node.ClassName != copied.ClassName) is { } other)
        {
            Failed?.Invoke($"The copied settings are a {copied.ClassName}'s, and {other.Name} is a {other.ClassName}.");
        }
        else
        {
            TryChange(entity => Array.ForEach(copied.Values, value => _scene!.SetProperty(entity, value.Name, value.Value)));
        }
    }

    public void MakeResourcesUnique() => TryChange(entity => _scene!.MakeResourcesUnique(entity));

    IEnumerable<PropertyGroup> Groups() => _sections.Children.OfType<Panel>().SelectMany(section => section.Children.OfType<PropertyGroup>());

    // The vector rows count only while their group is in a section. A row matches its engine name too, so a
    // search for rotation finds Turn.
    IEnumerable<(Control Row, string Label)> Labeled() =>
    [
        .. (_transform.Parent is null ? [] : new[] { _position, _rotation, _scale }).Select((row, at) => ((Control)row, $"{row.Label} {s_vectorNames[at]}")),
        .. _rows.Select(row => ((Control)row, $"{row.Label} {row.PropertyName}")),
    ];

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
            part.IsVisible = all && _picked.Count == 1;
        }

        _rootLine.IsVisible = all && _isRoot;
        var matched = labeled.Count(row => row.Row.IsVisible);
        _count.IsVisible = any && !all;
        _count.Text = matched == 0 ? "No settings match." : $"{matched} of {labeled.Length} settings match.";
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
        _position.Show(Shared(_scene!.PositionOf));
        _rotation.Show(Shared(_scene.RotationOf));
        _scale.Show(Shared(_scene.ScaleOf));
    }

    Vector3 Shared(Func<nint, Vector3> read) => _picked.Select(node => read(node.Handle)).Aggregate(Mix);

    void ShowExtras()
    {
        var entity = _scene is not null && _node is not null ? _node.Handle : 0;
        ShowProperties();
        _metadataRows.Show(entity == 0 ? [] : _scene!.MetadataOf(entity));
        _script.Show(entity == 0 ? null : _scene!.ScriptOf(entity));
        _isRoot = entity != 0 && _picked.Count == 1 && _scene!.Outline()?.Handle == entity;
        if (entity != 0)
        {
            _metadataTool.ShowOwner(_node!.Name);
            _rootLine.Text = $"{_node!.Name} is the top thing. Everything else in the scene sits inside it.";
        }
    }

    // The sections are built again only for other classes or property names, so a row keeps its box and caret
    // when only its value changed.
    void ShowProperties()
    {
        var picked = _scene is null ? [] : _picked;
        var chains = picked.Select(node => _scene!.ClassChainOf(node.Handle)).ToArray();
        _chain = chains.Length == 0 ? [] : [.. chains[^1].Where(className => chains.All(chain => chain.Contains(className)))];
        var lists = picked.Select(node => _scene!.PropertiesOf(node.Handle)).ToArray();
        EntityProperty[] properties = lists.Length == 0 ? [] : [.. lists[^1].Where(property => _chain.Contains(property.Class))];
        var built = string.Join(',', _chain) + ';' + string.Join(',', properties.Select(property => property.Name));
        if (built != _built)
        {
            _built = built;
            Build(properties);
        }
        var values = lists.Select(list => list.DistinctBy(property => property.Name).ToDictionary(property => property.Name, property => property.Value)).ToArray();
        foreach (var row in _rows)
        {
            row.Show(Shared([.. values.Select(value => value.GetValueOrDefault(row.PropertyName))]));
        }
    }

    // A vector that differs on an axis is NaN there, so the other axes still show. Any other value that differs
    // is Mixed.
    static object? Shared(object?[] values) => values switch
    {
        _ when values.All(value => Equals(value, values[0])) => values[0],
        _ when values.All(value => value is Vector3) => values.Cast<Vector3>().Aggregate(Mix),
        _ when values.All(value => value is Vector2) => Flat(values.Cast<Vector2>().Select(value => new Vector3(value, 0)).Aggregate(Mix)),
        _ => PropertyRow.Mixed,
    };

    static Vector3 Mix(Vector3 one, Vector3 other) =>
        new(one.X == other.X ? one.X : float.NaN, one.Y == other.Y ? one.Y : float.NaN, one.Z == other.Z ? one.Z : float.NaN);

    // A NaN axis is one left Mixed, so each entity keeps its own value there.
    static Vector3 Merge(Vector3 typed, Vector3 own) =>
        new(float.IsNaN(typed.X) ? own.X : typed.X, float.IsNaN(typed.Y) ? own.Y : typed.Y, float.IsNaN(typed.Z) ? own.Z : typed.Z);

    static Vector2 Flat(Vector3 value) => new(value.X, value.Y);

    object Merge(object typed, nint entity, string name) => typed switch
    {
        Vector3 vector => Merge(vector, (Vector3)Own(entity, name)!),
        Vector2 vector => Flat(Merge(new Vector3(vector, 0), new Vector3((Vector2)Own(entity, name)!, 0))),
        _ => typed,
    };

    object? Own(nint entity, string name) => _scene!.PropertiesOf(entity).First(property => property.Name == name).Value;

    // One section per class: its bar, its rows with no group, then its groups in the engine's order. The
    // Entity3D section starts with the Transform group, which takes the engine's other Transform rows. The
    // picked thing's own class goes by its plain name, and the classes under it say what they give.
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
            var title = className == _chain[0] ? PlainNames.NameOf(className) : PlainNames.FromAny(className);
            var section = new StackPanel { Children = { new ClassBar(className, title) } };
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
        row.Entered += value => TryChange(entity => _scene!.SetProperty(entity, property.Name, Merge(value, entity, property.Name)));
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
        // Add a script sits a little taller and quieter than Add a custom value, so the two read as separate steps.
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
        _scriptTool.Requested += path => Attach(entity => _scene!.AttachScript(entity, path));
        _scriptTool.Making += (path, graph) => Attach(entity =>
        {
            // The new script extends the picked thing's class, so it can use all of its settings.
            if (graph)
            {
                _scene!.CreateGraph(path, _node!.ClassName);
            }
            else
            {
                _scene!.CreateScript(path, _node!.ClassName);
            }
            _scene!.AttachScript(entity, path);
        });
        _metadataTool.Requested += (name, value) =>
        {
            if (TryChange(entity => _scene!.SetMetadata(entity, name, value)))
            {
                _metadataFlyout.Hide();
                _metadataTool.Clear();
            }
        };
    }

    void Attach(Action<nint> write)
    {
        if (TryChange(write))
        {
            _attachFlyout.Hide();
        }
    }

    // Runs one edit on each picked entity and answers whether the scene took it. Either way the rows show what the
    // scene holds, so a refused value is put back.
    bool TryChange(Action<nint> write)
    {
        if (!IsSelected)
        {
            return false;
        }
        var written = 0;
        try
        {
            foreach (var node in _picked)
            {
                write(node.Handle);
                written++;
            }
            Edited?.Invoke();
            return true;
        }
        catch (Exception error) when (error is ArgumentException or IOException)
        {
            // The entities written before the refusal changed, so the edit still counts.
            if (written > 0)
            {
                Edited?.Invoke();
            }
            Failed?.Invoke(error.Message);
            return false;
        }
        finally
        {
            ShowExtras();
        }
    }

    void Add(VectorRow row, Func<nint, Vector3> read, Action<nint, Vector3> write)
    {
        row.Entered += value =>
        {
            if (IsEditable)
            {
                foreach (var node in _picked)
                {
                    write(node.Handle, Merge(value, read(node.Handle)));
                }
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
