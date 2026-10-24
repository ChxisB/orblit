using Orblit.Filament;
using Orblit.Profile;
using Orblit.Scene;
using Orblit.VectorMath;
using Orblit.Weather;

namespace Orblit.Stage;

/// <summary>
/// A scene document as the renderer draws it, kept up to date as the document is edited.
/// </summary>
/// <remarks>
/// <see cref="Apply"/> rebuilds only the entities a diff touched and whatever hangs from them. The result
/// has to be what <see cref="Replace"/> on a fresh view would give, because the editor leans on the two
/// being the same. Keys are handed out once per entity and role and never reused, so the renderer keeps
/// what it built for an entity across every edit to it.
/// </remarks>
public sealed class DocumentView
{
    private const string ObjectRole = "object";
    private const string LightRole = "light";
    private const string SplatsRole = "splats";
    private const string SpriteRole = "sprite";
    private const string MaterialRole = "material";

    private readonly Dictionary<string, int> _keys = [];
    private readonly Dictionary<string, Matrix4> _world = [];
    private readonly Dictionary<string, RenderObject> _objects = [];
    private readonly Dictionary<string, RenderLight> _lights = [];
    private readonly Dictionary<string, SplatCloud> _splats = [];
    private readonly Dictionary<string, SpriteLayer> _sprites = [];
    private readonly OrderedDictionary<string, RenderMaterial> _materials = [];
    private readonly Dictionary<string, string> _materialOf = [];
    private readonly Dictionary<string, HashSet<string>> _materialUsers = [];

    private SceneDocument _document;
    private MaterialLibrary _library;
    private IReadOnlyDictionary<string, CookedSurface> _surfaces = new Dictionary<string, CookedSurface>();
    private string? _look;
    private int _nextKey = 1;
    private RenderCamera _camera = DocumentStaging.DefaultCamera();
    private Sky _sky = new();
    private Fog _fog = Fog.None;
    private Precipitation _precipitation = Precipitation.None;

    public DocumentView(
        SceneDocument document,
        string? projectRoot = null,
        MaterialLibrary? materials = null,
        string? look = null,
        Profiler? profiler = null)
    {
        _document = document;
        _library = materials ?? new MaterialLibrary();
        _look = look;
        ProjectRoot = projectRoot;
        Profiler = profiler ?? Profiler.Off;
        RebuildAll();
    }

    /// <summary>What project-relative paths in the document are relative to, or null to leave them as written.</summary>
    public string? ProjectRoot { get; }

    public Profiler Profiler { get; }

    public SceneDocument Document => _document;

    /// <summary>A camera to draw through in place of the document's own, or null to use it.</summary>
    public RenderCamera? Through { get; set; }

    public RenderCamera Camera => Through ?? _camera;

    /// <summary>The look that materials are worn in, or null for the default one.</summary>
    public string? Look => _look;

    /// <summary>Every look any material in the document has, in document order.</summary>
    public IReadOnlyList<string> Looks => [.. _document.Entities
        .Select(entity => entity[SceneComponents.Material])
        .OfType<MaterialComponent>()
        .SelectMany(worn => worn.LookNames)
        .Distinct()];

    public RenderScene Scene
    {
        get
        {
            var started = Profiler.Begin();
            var staged = Stage();
            Profiler.End("scene build", started);
            return staged;
        }
    }

    public void SetLook(string? look)
    {
        if (look == _look)
        {
            return;
        }

        _look = look;
        RebuildAll();
    }

    public void SetMaterials(MaterialLibrary library)
    {
        _library = library;
        RebuildAll();
    }

    public void SetSurfaces(IReadOnlyDictionary<string, CookedSurface> cooked)
    {
        _surfaces = cooked;
        RebuildAll();
    }

    public void Apply(SceneDiff diff)
    {
        var started = Profiler.Begin();
        ApplyOperations(diff);
        Profiler.End("diff", started);
    }

    /// <summary>Swaps in a different document, moving only what differs.</summary>
    public void Replace(SceneDocument next) => Apply(SceneDiff.Between(_document, next));

    public Matrix4 WorldOf(string id)
    {
        if (_world.TryGetValue(id, out var known))
        {
            return known;
        }

        if (_document[id] is not { } entity)
        {
            return Matrix4.Identity();
        }

        var local = LocalOf(entity);
        var parent = entity.Parent;
        var world = parent is null || parent == id || !_document.Contains(parent)
            ? local
            : WorldOf(parent).Multiplied(local);

        _world[id] = world;
        return world;
    }

    public bool IsShown(string id) => _document[id] is { } entity && Shown(entity);

    /// <summary>The camera an entity carries, or null if it carries none. Whether it is shown does not matter.</summary>
    public RenderCamera? CameraOf(string id) =>
        _document[id]?[SceneComponents.Camera] is CameraComponent camera
            ? DocumentStaging.CameraAt(WorldOf(id), camera)
            : null;

    private static Matrix4 LocalOf(SceneEntity entity)
    {
        if (entity[SceneComponents.Transform] is not TransformComponent transform)
        {
            return Matrix4.Identity();
        }

        var local = Matrix4.Identity();
        local.SetTranslation(transform.Position);
        local.Multiply(RotationOf(transform.Rotation));
        local.Multiply(Matrix4.Diagonal3Values(transform.Scale.X, transform.Scale.Y, transform.Scale.Z));
        return local;
    }

    private static Matrix4 RotationOf(Vector3 degrees)
    {
        var turn = Matrix4.RotationZ(Doubles.Radians(degrees.Z));
        turn.Multiply(Matrix4.RotationY(Doubles.Radians(degrees.Y)));
        turn.Multiply(Matrix4.RotationX(Doubles.Radians(degrees.X)));
        return turn;
    }

    private static List<string> WithDescendants(IEnumerable<string> ids, SceneDocument document)
    {
        var found = new List<string>(ids);
        var seen = new HashSet<string>(found);

        // As many passes as the tree is deep: a child may sit before its parent in the list, so one
        // pass can miss a grandchild.
        var growing = true;
        while (growing)
        {
            growing = false;
            foreach (var entity in document.Entities)
            {
                if (entity.Parent is { } parent && seen.Contains(parent) && seen.Add(entity.Id))
                {
                    found.Add(entity.Id);
                    growing = true;
                }
            }
        }

        return found;
    }

    // The entity an operation changes, or null when nothing drawn depends on it. The order of entities
    // decides what draws over what, and the lists are read in document order, so moving one in it
    // rebuilds nothing. A name is the editor's business.
    private static string? TouchedBy(SceneOp op) => op switch
    {
        AddEntity added => added.Id,
        RemoveEntity removed => removed.Id,
        Reparent moved => moved.Id,
        SetVisible shown => shown.Id,
        SetComponent changed => changed.Id,
        SetField field => field.Id,
        _ => null,
    };

    private RenderScene Stage() => new()
    {
        Camera = Camera,
        Objects = InDocumentOrder(_objects),
        Lights = InDocumentOrder(_lights),
        Splats = InDocumentOrder(_splats),
        Sprites = InDocumentOrder(_sprites),
        Materials = [.. _materials.Values],
        Sky = _sky,
        Fog = _fog,
        Precipitation = _precipitation,
    };

    private List<T> InDocumentOrder<T>(Dictionary<string, T> built)
        where T : class => [.. _document.Entities.Select(entity => built.GetValueOrDefault(entity.Id)).OfType<T>()];

    private void ApplyOperations(SceneDiff diff)
    {
        if (diff.IsEmpty)
        {
            return;
        }

        var touched = diff.Operations.Select(TouchedBy).OfType<string>().Distinct().ToList();
        var settings = diff.Operations.Any(op => op is SetSetting);
        var before = _document;
        _document = diff.ApplyTo(_document);

        if (touched.Count == 0)
        {
            if (settings)
            {
                BuildSettings();
            }

            return;
        }

        // Both documents: an entity just taken out of a group still has descendants in the document it
        // was taken out of, and those are the ones whose place in the world has just changed.
        var affected = WithDescendants(touched, before).Union(WithDescendants(touched, _document)).ToList();
        foreach (var id in affected)
        {
            _world.Remove(id);
            Forget(id);
        }

        foreach (var id in affected)
        {
            if (_document[id] is { } entity)
            {
                Build(entity);
            }
        }

        BuildCamera();

        // The air is read off whichever entity carries the weather, so an edit to that entity moves the
        // sky as surely as an edit to the scene's settings.
        if (settings || affected.Any(CarriesWeather))
        {
            BuildSettings();
        }
    }

    private bool CarriesWeather(string id) => _document[id]?.Has(SceneComponents.Weather) ?? false;

    private void RebuildAll()
    {
        _world.Clear();
        _objects.Clear();
        _lights.Clear();
        _splats.Clear();
        _sprites.Clear();
        _materials.Clear();
        _materialOf.Clear();
        _materialUsers.Clear();
        foreach (var entity in _document.Entities)
        {
            Build(entity);
        }

        BuildCamera();
        BuildSettings();
    }

    private void Forget(string id)
    {
        _objects.Remove(id);
        _lights.Remove(id);
        _splats.Remove(id);
        _sprites.Remove(id);
        DropMaterialUse(id);
    }

    // A material is shared, so it lives as long as anything wears it. The crate that was deleted does
    // not take the other ninety-nine crates' material with it.
    private void DropMaterialUse(string id)
    {
        if (!_materialOf.Remove(id, out var signature) || !_materialUsers.TryGetValue(signature, out var users))
        {
            return;
        }

        users.Remove(id);
        if (users.Count > 0)
        {
            return;
        }

        _materialUsers.Remove(signature);
        _materials.Remove(signature);
    }

    private void Wears(string id, string signature)
    {
        _materialOf[id] = signature;
        if (!_materialUsers.TryGetValue(signature, out var users))
        {
            _materialUsers[signature] = users = [];
        }

        users.Add(id);
    }

    // The key of the material an entity wears, building the material the first time anything wears it.
    // Null when it names none, which leaves the object with whatever its mesh brought.
    private long? Wear(SceneEntity entity)
    {
        DropMaterialUse(entity.Id);

        if (entity[SceneComponents.Material] is not MaterialComponent component)
        {
            return null;
        }

        var asset = component.Under(_look);
        if (string.IsNullOrEmpty(asset))
        {
            return null;
        }

        if (asset.EndsWith(MaterialDocument.ShaderExtension, StringComparison.Ordinal))
        {
            return WearSurface(entity.Id, asset, component.Values);
        }

        // An .omat is a material and anything else is an image. Naming an image is shorthand for a plain
        // surface wearing that picture, and the quickest way to put a texture on a box.
        var signature = asset.EndsWith(MaterialDocument.Extension, StringComparison.Ordinal) ? asset : $"image:{asset}";
        Wears(entity.Id, signature);

        return _materials.TryGetValue(signature, out var built) ? built.Key : BuildMaterial(signature, asset);
    }

    private long? BuildMaterial(string signature, string asset)
    {
        var key = KeyFor(signature, MaterialRole);
        RenderMaterial material;
        if (asset.EndsWith(MaterialDocument.Extension, StringComparison.Ordinal))
        {
            material = MaterialView.From(_library.Resolve(asset), key, Resolve);
        }
        else if (Resolve(asset) is { } found)
        {
            material = new RenderMaterial { Key = key, BaseColourMap = new Texture(found) };
        }
        else
        {
            return null;
        }

        _materials[signature] = material;
        return key;
    }

    private long? WearSurface(string id, string asset, IReadOnlyDictionary<string, object> given)
    {
        if (!_surfaces.TryGetValue(asset, out var cooked))
        {
            return null;
        }

        var signature = given.Count == 0 ? $"surface:{asset}" : $"surface:{asset}@{id}";
        Wears(id, signature);

        if (_materials.TryGetValue(signature, out var built))
        {
            return built.Key;
        }

        if (cooked.SurfaceFor(given, Resolve) is not { } surface)
        {
            return null;
        }

        var key = KeyFor(signature, MaterialRole);
        _materials[signature] = new RenderMaterial { Key = key, Surface = surface };
        return key;
    }

    private int KeyFor(string id, string role)
    {
        var name = $"{role}:{id}";
        if (!_keys.TryGetValue(name, out var key))
        {
            _keys[name] = key = _nextKey++;
        }

        return key;
    }

    // Whether this and everything above it is shown.
    private bool Shown(SceneEntity entity)
    {
        var current = entity;
        var walked = new HashSet<string> { current.Id };
        while (current.Visible)
        {
            if (current.Parent is not { } parent || !walked.Add(parent) || _document[parent] is not { } above)
            {
                return true;
            }

            current = above;
        }

        return false;
    }

    private void Build(SceneEntity entity)
    {
        var world = WorldOf(entity.Id);
        var shown = Shown(entity);
        BuildMesh(entity, world, shown);
        BuildLight(entity, world, shown);
        BuildSplats(entity, world, shown);
        BuildSprite(entity, world, shown);

        // Tilemaps, parallax backdrops, canvases and data files are read, kept, written back and diffed
        // like everything else and not drawn here. An entity that quietly does nothing is hard to tell
        // from a bug, so this says it out loud. Each waits on a phase of its own: nothing reads a map
        // file yet, a parallax wants the texture pipeline and a canvas is an interface for Orblit.Ui.
    }

    private void BuildMesh(SceneEntity entity, Matrix4 world, bool shown)
    {
        if (entity[SceneComponents.Mesh] is not MeshComponent mesh)
        {
            return;
        }

        var worn = Wear(entity);
        _objects[entity.Id] = new RenderObject
        {
            Key = KeyFor(entity.Id, ObjectRole),
            Transform = world,
            Colour = mesh.Colour.Linear,
            Mesh = Resolve(mesh.Asset),
            Material = worn,
            CastShadows = mesh.CastShadows,
            ReceiveShadows = mesh.ReceiveShadows,
            Visible = shown,
        };
    }

    // A hidden light is left out rather than sent dark. The renderer shades one directional light and a
    // budget of punctual ones, and a light nobody can see should not be the one that fills the budget.
    private void BuildLight(SceneEntity entity, Matrix4 world, bool shown)
    {
        if (shown && entity[SceneComponents.Light] is LightComponent light)
        {
            _lights[entity.Id] = DocumentStaging.LightFrom(KeyFor(entity.Id, LightRole), light, world);
        }
    }

    private void BuildSplats(SceneEntity entity, Matrix4 world, bool shown)
    {
        if (entity[SceneComponents.Splats] is SplatsComponent splats && Resolve(splats.Asset) is { } path && shown)
        {
            _splats[entity.Id] = DocumentStaging.SplatsFrom(KeyFor(entity.Id, SplatsRole), path, world, splats);
        }
    }

    private void BuildSprite(SceneEntity entity, Matrix4 world, bool shown)
    {
        if (shown && entity[SceneComponents.Sprite] is SpriteComponent sprite)
        {
            var image = Resolve(sprite.Texture ?? sprite.Atlas);
            _sprites[entity.Id] = DocumentStaging.SpriteFrom(KeyFor(entity.Id, SpriteRole), sprite, world, image);
        }
    }

    private void BuildCamera()
    {
        foreach (var entity in _document.Entities)
        {
            if (entity.Visible && CameraOf(entity.Id) is { } camera)
            {
                _camera = camera;
                return;
            }
        }

        _camera = DocumentStaging.DefaultCamera();
    }

    private void BuildSettings()
    {
        var weather = _document.Entities
            .Where(entity => entity.Visible)
            .Select(entity => entity[SceneComponents.Weather])
            .OfType<WeatherComponent>()
            .FirstOrDefault();

        _sky = DocumentStaging.SkyFrom(_document.Settings, weather?.Air);
        _fog = DocumentStaging.FogFrom(weather?.Air);
        _precipitation = DocumentStaging.PrecipitationFrom(weather);
    }

    // A project-relative path as one the renderer can open.
    private string? Resolve(string? asset)
    {
        if (string.IsNullOrEmpty(asset))
        {
            return null;
        }

        if (ProjectRoot is not { } root || asset.StartsWith('/'))
        {
            return asset;
        }

        return root.EndsWith('/') ? $"{root}{asset}" : $"{root}/{asset}";
    }
}
