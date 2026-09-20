import 'dart:math' as math;
import 'dart:typed_data';

import 'package:orblit_light/orblit_light.dart' as artist;
import 'package:orblit_scene/orblit_scene.dart';
import 'package:orblit_weather/orblit_weather.dart' show CelestialBody;
import 'package:vector_math/vector_math_64.dart';

import 'package:orblit_filament/orblit_filament.dart';

import 'material_view.dart';

/// A scene document, kept as something the renderer can draw.
///
/// The document says what a scene *is*; this says what to do with it. Keeping
/// the two apart is what lets a runtime, an importer and a command-line tool
/// read a scene without taking a renderer with them — and it is why this class
/// lives here rather than in `orblit_scene`.
///
/// It is stateful on purpose. An [OrblitScene] is stated whole every frame,
/// which would mean rebuilding four thousand objects to move one of them, so
/// what is built is kept and a [SceneDiff] rebuilds only what it touched.
/// [scene] is therefore cheap to read every frame and [apply] is the expensive
/// call, which is the right way round: a frame happens sixty times a second
/// and an edit does not.
class OrblitDocumentView {
  OrblitDocumentView(
    this._document, {
    this.projectRoot,
    MaterialLibrary? materials,
    String? look,
  }) : _library = materials ?? MaterialLibrary(),
       _look = look {
    _rebuildAll();
  }

  /// Where project-relative asset paths are rooted.
  ///
  /// Null leaves them as they are, which is what a scene whose assets are
  /// provided as bytes wants: the renderer looks in its resource store before
  /// it looks on disk, and a path rewritten to somewhere on this machine would
  /// miss it.
  final String? projectRoot;

  /// The project's materials, already read, with their parents and groups
  /// still to be spent. Resolving happens in here and is cached by the
  /// library, so a scene of four hundred objects wearing eleven materials
  /// walks eleven chains.
  MaterialLibrary _library;

  /// The look the scene is being shown in, or null for what each object wears
  /// by default.
  ///
  /// Not a scene setting, because it is not a fact about the scene: the same
  /// document shown in "summer" and in "winter" is the same document. It is
  /// how a viewer is asking to see it, which is why it is stated here and can
  /// be changed without editing anything.
  String? _look;

  /// The materials in use, by what decided them: the material's own path and
  /// the look it was resolved under.
  ///
  /// Keyed that way rather than by entity so that a hundred crates wearing one
  /// material are one material. The renderer builds a compiled instance per
  /// key it is handed, so keying by entity would be a hundred instances of the
  /// same thing and a hundred draws that cannot be batched.
  final Map<String, String> _materialOf = {};
  final Map<String, Set<String>> _materialUsers = {};

  SceneDocument _document;

  SceneDocument get document => _document;

  /// Keys the renderer knows things by, one per entity and per role.
  ///
  /// Handed out once and never reused, because the renderer keeps what it has
  /// built for as long as a key keeps being mentioned. Drawn from a single
  /// counter across every role so that a lamp which is both a mesh and a light
  /// cannot hand the same number to two different things — the two lists may
  /// or may not share a namespace, and this costs nothing and does not care.
  final Map<String, int> _keys = {};
  int _nextKey = 1;

  final Map<String, Matrix4> _world = {};
  final Map<String, OrblitObject> _objects = {};
  final Map<String, OrblitLight> _lights = {};
  final Map<String, OrblitSplats> _splats = {};
  final Map<String, OrblitSprites> _sprites = {};
  final Map<String, OrblitMaterial> _materials = {};

  OrblitCamera _camera = _defaultCamera();
  OrblitSky _sky = OrblitSky();
  OrblitFog _fog = OrblitFog.none;
  OrblitPrecipitation _precipitation = OrblitPrecipitation.none;

  /// The document as the renderer takes it.
  ///
  /// Assembled in document order every time rather than cached, because the
  /// lists are the cheap part — the objects in them are what cost something to
  /// build, and those are what is kept.
  OrblitScene get scene => OrblitScene(
    camera: _camera,
    objects: [
      for (final entity in _document.entities)
        if (_objects[entity.id] case final object?) object,
    ],
    lights: [
      for (final entity in _document.entities)
        if (_lights[entity.id] case final light?) light,
    ],
    splats: [
      for (final entity in _document.entities)
        if (_splats[entity.id] case final cloud?) cloud,
    ],
    sprites: [
      for (final entity in _document.entities)
        if (_sprites[entity.id] case final layer?) layer,
    ],
    materials: _materials.values.toList(growable: false),
    sky: _sky,
    fog: _fog,
    precipitation: _precipitation,
  );

  /// Moves the view on by a diff, rebuilding only what it touched.
  ///
  /// "Touched" means the entities the operations name *and everything under
  /// them*, because two of the things an entity gets from the document are
  /// inherited: where it is in the world is every transform above it
  /// multiplied together, and whether it is shown is only true if everything
  /// above it is shown too. Moving a group and rebuilding only the group is
  /// the bug where the crate stays behind.
  ///
  /// Worked out as the closure over both documents rather than as a rule per
  /// kind of operation. The rule-per-operation version was written first and
  /// was wrong twice over: it missed a transform *arriving* on an entity that
  /// had none, and it missed that hiding a group hides its contents. The
  /// closure cannot miss either, and costs a walk of the entity list.
  void apply(SceneDiff diff) {
    if (diff.isEmpty) return;

    final touched = <String>{};
    var settings = false;

    for (final op in diff.operations) {
      switch (op) {
        case AddEntity(:final id):
          touched.add(id);
        case RemoveEntity(:final id):
          touched.add(id);
        case Reparent(:final id):
          touched.add(id);
        case SetVisible(:final id):
          touched.add(id);
        case SetComponent(:final id):
          touched.add(id);
        case SetField(:final id):
          touched.add(id);
        // The order the entities are in decides what draws over what, and an
        // entity moving in it changes nothing about the entity itself. The
        // lists are read in document order, so there is nothing to rebuild.
        case Reorder():
          break;
        // A name is the editor's business. Nothing drawn depends on one.
        case SetEntityName():
          break;
        case SetSetting():
          settings = true;
      }
    }

    final before = _document;
    _document = diff.applyTo(_document);

    if (touched.isEmpty) {
      if (settings) _buildSettings();
      return;
    }

    // Both documents: an entity that has just been taken out of a group still
    // has descendants in the document it was taken out of, and those are
    // exactly the ones whose place in the world has just changed.
    final affected = <String>{
      ..._withDescendants(touched, before),
      ..._withDescendants(touched, _document),
    };

    for (final id in affected) {
      _world.remove(id);
      _forget(id);
    }
    for (final id in affected) {
      final entity = _document[id];
      if (entity != null) _build(entity);
    }

    _buildCamera();
    // The air is read off whichever entity carries the weather, so an edit to
    // that entity moves the sky as surely as an edit to the scene's settings.
    if (settings || affected.any(_carriesWeather)) _buildSettings();
  }

  bool _carriesWeather(String id) =>
      _document[id]?.has(SceneComponents.weather) ?? false;

  /// [ids], plus everything hanging from any of them in [document].
  static Set<String> _withDescendants(Set<String> ids, SceneDocument document) {
    final found = <String>{...ids};
    // As many passes as the tree is deep: a child may sit before its parent in
    // the list, so one pass can miss a grandchild.
    var growing = true;
    while (growing) {
      growing = false;
      for (final entity in document.entities) {
        final parent = entity.parent;
        if (parent == null || found.contains(entity.id)) continue;
        if (found.contains(parent)) {
          found.add(entity.id);
          growing = true;
        }
      }
    }
    return found;
  }

  /// Swaps in a different document, moving only what differs.
  void replace(SceneDocument next) => apply(SceneDiff.between(_document, next));

  void _rebuildAll() {
    _world.clear();
    _objects.clear();
    _lights.clear();
    _splats.clear();
    _sprites.clear();
    _materials.clear();
    _materialOf.clear();
    _materialUsers.clear();
    for (final entity in _document.entities) {
      _build(entity);
    }
    _buildCamera();
    _buildSettings();
  }

  void _forget(String id) {
    _objects.remove(id);
    _lights.remove(id);
    _splats.remove(id);
    _sprites.remove(id);
    _dropMaterialUse(id);
  }

  /// Lets go of whatever material [id] was wearing, and of the material itself
  /// once nothing wears it.
  ///
  /// Kept alive by use rather than by entity because a material is shared: the
  /// crate that was deleted does not take the other ninety-nine crates'
  /// material with it.
  void _dropMaterialUse(String id) {
    final signature = _materialOf.remove(id);
    if (signature == null) return;
    final users = _materialUsers[signature];
    if (users == null) return;
    users.remove(id);
    if (users.isNotEmpty) return;
    _materialUsers.remove(signature);
    _materials.remove(signature);
  }

  /// The material key [entity] wears, building the material the first time
  /// anything wears it.
  ///
  /// Returns null when the entity names no material, which leaves the object
  /// with whatever its mesh brought.
  int? _wear(SceneEntity entity) {
    _dropMaterialUse(entity.id);

    final component = entity[SceneComponents.material];
    if (component is! MaterialComponent) return null;
    final asset = component.under(_look);
    if (asset == null || asset.isEmpty) return null;

    // Two ways of naming a material, and the file extension tells them apart.
    // An `.omat` is a material; anything else is an image, and naming one is
    // shorthand for "a plain surface wearing this picture" — the way this
    // component was read before materials had files of their own, and still
    // the quickest way to put a texture on a box.
    final signature = asset.endsWith(materialExtension)
        ? asset
        : 'image:$asset';
    _materialOf[entity.id] = signature;
    _materialUsers.putIfAbsent(signature, () => <String>{}).add(entity.id);

    final built = _materials[signature];
    if (built != null) return built.key;

    final key = _keyFor(signature, 'material');
    final OrblitMaterial material;
    if (asset.endsWith(materialExtension)) {
      material = materialFrom(
        _library.resolve(asset),
        key: key,
        locate: _resolve,
      );
    } else {
      final found = _resolve(asset);
      if (found == null) return null;
      material = OrblitMaterial(key: key, baseColourMap: OrblitTexture(found));
    }
    _materials[signature] = material;
    return key;
  }

  /// Shows the scene in a named look, or in none.
  ///
  /// Rebuilds rather than diffing, because a look is a scene-wide swap: the
  /// cheapest correct answer to "which of these four hundred objects has
  /// something different to wear" is to ask all four hundred once.
  set look(String? look) {
    if (look == _look) return;
    _look = look;
    _rebuildAll();
  }

  String? get look => _look;

  /// Every look anything in this scene has something of its own for.
  ///
  /// What a viewer offers, gathered from the objects rather than declared once
  /// at the top — the same place `KHR_materials_variants` keeps the mappings,
  /// so a scene that came in through glTF and one authored here list the same
  /// names.
  Set<String> get looks => {
    for (final entity in _document.entities)
      if (entity[SceneComponents.material] case final MaterialComponent worn)
        ...worn.lookNames,
  };

  /// Replaces the materials this scene draws with.
  ///
  /// For a project whose material files have changed on disk: the library
  /// caches what it resolved, so handing in a new one is how a scene is told
  /// to look again.
  set materials(MaterialLibrary library) {
    _library = library;
    _rebuildAll();
  }

  /// A key for one role of one entity, the same one every time.
  int _keyFor(String id, String role) =>
      _keys.putIfAbsent('$role:$id', () => _nextKey++);

  /// Where an entity ends up, with everything above it applied.
  ///
  /// Walked upwards and cached, so a deep tree costs one multiply per level
  /// the first time and nothing after it. Decode has already cut any loop, so
  /// this cannot fail to terminate on a document that came from a file; one
  /// built in memory is guarded anyway, because a hang is a much worse way to
  /// find out than a wrong transform.
  Matrix4 _worldOf(String id) {
    final known = _world[id];
    if (known != null) return known;

    final entity = _document[id];
    if (entity == null) return Matrix4.identity();

    final local = _localOf(entity);
    final parent = entity.parent;
    final world = parent == null || parent == id || !_document.contains(parent)
        ? local
        : _worldOf(parent).multiplied(local);

    _world[id] = world;
    return world;
  }

  Matrix4 _localOf(SceneEntity entity) {
    final transform = entity[SceneComponents.transform];
    if (transform is! TransformComponent) return Matrix4.identity();
    return Matrix4.identity()
      ..setTranslation(transform.position)
      ..multiply(_rotation(transform.rotation))
      ..multiply(Matrix4.diagonal3(transform.scale));
  }

  /// Degrees around X, Y and Z, as the file states them and as somebody typed
  /// them into an inspector.
  ///
  /// Composed Z, then Y, then X, which is the order the editor has always used
  /// and therefore the only order that draws a saved scene the way it was
  /// saved. Euler angles do not commute, so this is not a detail: getting it
  /// wrong turns every object that is rotated about more than one axis, and
  /// turns it by an amount that looks plausible.
  static Matrix4 _rotation(Vector3 degrees) =>
      Matrix4.rotationZ(radians(degrees.z))
        ..multiply(Matrix4.rotationY(radians(degrees.y)))
        ..multiply(Matrix4.rotationX(radians(degrees.x)));

  /// Whether this and everything above it is shown.
  bool _shown(SceneEntity entity) {
    var current = entity;
    final walked = <String>{current.id};
    while (true) {
      if (!current.visible) return false;
      final parent = current.parent;
      if (parent == null || !walked.add(parent)) return true;
      final above = _document[parent];
      if (above == null) return true;
      current = above;
    }
  }

  void _build(SceneEntity entity) {
    final world = _worldOf(entity.id);
    final shown = _shown(entity);

    if (entity[SceneComponents.mesh] case final MeshComponent mesh) {
      final worn = _wear(entity);

      _objects[entity.id] = OrblitObject(
        key: _keyFor(entity.id, 'object'),
        transform: world,
        colour: mesh.colour.linear,
        mesh: _resolve(mesh.asset),
        material: worn,
        castShadows: mesh.castShadows,
        receiveShadows: mesh.receiveShadows,
        visible: shown,
      );
    }

    if (entity[SceneComponents.light] case final LightComponent light) {
      // A hidden light is left out rather than sent dark. Filament shades one
      // directional light and a budget of punctual ones, and a light nobody
      // can see should not be the one that fills the budget.
      if (shown) _lights[entity.id] = _lightFrom(entity, light, world);
    }

    if (entity[SceneComponents.splats] case final SplatsComponent splats) {
      final path = _resolve(splats.asset);
      if (path != null && shown) {
        _splats[entity.id] = OrblitSplats(
          key: _keyFor(entity.id, 'splats'),
          path: path,
          transform: world,
          harmonics: splats.harmonics?.clamp(0, 3) ?? 2,
          limit: splats.budget,
        );
      }
    }

    if (entity[SceneComponents.sprite] case final SpriteComponent sprite) {
      if (shown) _sprites[entity.id] = _spriteFrom(entity, sprite, world);
    }

    // Tilemaps, parallax backdrops, canvases and data files are read from the
    // document, kept, written back and diffed like everything else — and not
    // drawn here yet. Said out loud rather than left as an absence, because
    // an entity that quietly does nothing is indistinguishable from a bug.
    //
    // Each is waiting on something that is a phase of its own rather than on
    // work here. A tilemap names a map file and nothing reads one yet; a
    // parallax is layers of pictures, which wants the texture pipeline; a
    // canvas is an interface, which is orblit_ui's to draw. None of the three
    // is a line of code away, and guessing at any of them would mean a scene
    // that looks right here and wrong once the real one arrives.
  }

  /// The point of view, from the first camera entity there is.
  ///
  /// Scanned rather than set while the entities are built, because a camera
  /// that has just been deleted has nothing to set anything: built per-entity,
  /// the view would keep pointing from where a camera no longer is. A scene
  /// with two cameras takes the first for the same reason the weather does —
  /// stable beats clever when somebody is midway through an edit.
  void _buildCamera() {
    for (final entity in _document.entities) {
      final camera = entity[SceneComponents.camera];
      if (camera is! CameraComponent || !entity.visible) continue;
      final world = _worldOf(entity.id);
      _camera = OrblitCamera(
        position: world.getTranslation(),
        // Down the local -Z axis, which is where a camera looks.
        target: world.getTranslation() + _facing(world),
        fieldOfView: camera.fieldOfView,
      );
      return;
    }
    _camera = _defaultCamera();
  }

  /// Where to stand when the scene names nowhere.
  ///
  /// A scene with no camera in it is a scene somebody has not finished, and
  /// showing them nothing is a worse answer than showing them the scene from
  /// a sensible distance.
  static OrblitCamera _defaultCamera() =>
      OrblitCamera(position: Vector3(6, 4, 8), target: Vector3.zero());

  /// Down the local -Z axis, which is where a light and a camera both point.
  ///
  /// The same convention for the two of them, so a light parented to a camera
  /// rig turns with it rather than against it.
  static Vector3 _facing(Matrix4 world) =>
      world.getRotation().transformed(Vector3(0, 0, -1))..normalize();

  OrblitLight _lightFrom(
    SceneEntity entity,
    LightComponent light,
    Matrix4 world,
  ) {
    final described = artist.Light(
      type: light.kind,
      color: light.colour.linear,
      power: light.power,
      radius: light.sourceRadius,
      sunAngle: light.sunAngle,
      spotSize: light.spotSize,
      spotBlend: light.spotBlend,
      castShadows: light.castShadows,
      // A round source has no width, and an area light needs one. Twice the
      // radius is the square that stands in for it — the falloff and the
      // total are right, and the penumbra is at least a believable width.
      sizeX: light.sourceRadius * 2,
      sizeY: light.sourceRadius * 2,
    );
    final ready = described.toRenderer();

    // What tells a sun from a moon at a glance, once both are white discs of
    // the same width: a sun is wrapped in glare and a moon is not.
    final isMoon = light.body == CelestialBody.moon;

    return OrblitLight(
      key: _keyFor(entity.id, 'light'),
      kind: switch (ready.kind) {
        artist.RendererLightKind.directional => OrblitLightKind.directional,
        artist.RendererLightKind.point => OrblitLightKind.point,
        artist.RendererLightKind.spot => OrblitLightKind.spot,
        artist.RendererLightKind.area => OrblitLightKind.area,
      },
      colour: ready.color,
      intensity: ready.intensity,
      position: world.getTranslation(),
      direction: _facing(world),
      // A sun's influence is infinite, which is not a number a renderer can be
      // given. It ignores the falloff of a directional light anyway, so zero
      // here means "not asked" rather than "no reach".
      falloffRadius: ready.falloffRadius.isFinite ? ready.falloffRadius : 0,
      innerConeAngle: ready.innerConeAngle,
      outerConeAngle: ready.outerConeAngle,
      sunAngularRadius: ready.sunAngularRadius,
      sourceRadius: ready.sourceRadius,
      haloSize: isMoon ? 3 : 12,
      haloFalloff: isMoon ? 240 : 70,
      castShadows: ready.castShadows,
      // Only an area light has a size, and `orblit_light` leaves both at zero
      // for the kinds that do not. Passing that zero through would give the
      // renderer a panel with no area to integrate, which is a light that
      // emits nothing — so the renderer's own default stands in instead.
      width: ready.width > 0 ? ready.width : 1,
      height: ready.height > 0 ? ready.height : 1,
    );
  }

  /// One sprite, as a layer of one.
  ///
  /// A layer per entity rather than one layer holding every sprite in the
  /// scene, because a layer is what carries a texture: sprites drawn from two
  /// different pictures cannot share one however close together they are. The
  /// renderer batches by texture itself, so the cost of saying it this way is
  /// a list entry.
  OrblitSprites _spriteFrom(
    SceneEntity entity,
    SpriteComponent sprite,
    Matrix4 world,
  ) {
    final packed = Float32List(OrblitSprites.stride);
    OrblitSprite(
      x: 0,
      y: 0,
      width: sprite.width,
      height: sprite.height,
      depth: sprite.depth,
      pivotX: sprite.pivotX,
      pivotY: sprite.pivotY,
      red: sprite.colour.red,
      green: sprite.colour.green,
      blue: sprite.colour.blue,
      alpha: sprite.opacity,
    ).writeInto(packed, 0);

    return OrblitSprites(
      key: _keyFor(entity.id, 'sprite'),
      sprites: packed,
      image: switch (_resolve(sprite.texture ?? sprite.atlas)) {
        final path? => OrblitTexture(path),
        _ => null,
      },
      transform: world,
      blend: sprite.additive ? OrblitSpriteBlend.add : OrblitSpriteBlend.alpha,
    );
  }

  /// The sky, the air and whatever is falling out of it.
  ///
  /// Read from the first weather entity there is. A scene with two of them is
  /// a scene somebody is midway through editing, and taking the first is
  /// stable — taking the brightest, or merging them, would make the picture
  /// change depending on which one they touched last.
  void _buildSettings() {
    final settings = _document.settings;

    WeatherComponent? weather;
    for (final entity in _document.entities) {
      final found = entity[SceneComponents.weather];
      if (found is WeatherComponent && entity.visible) {
        weather = found;
        break;
      }
    }

    final air = weather?.air;
    final greyed = air == null
        ? settings.sky.linear
        : settings.sky.linear * (1 - (air.greying * 0.8).clamp(0.0, 1.0));

    _sky = OrblitSky(
      colour: greyed,
      // A covered sky is one enormous diffuser: less of the light arrives from
      // one direction and more of it from everywhere.
      ambient: settings.ambient * (air?.scattered ?? 1),
    );

    _fog = air == null || air.fogDensity <= 0
        ? OrblitFog.none
        : OrblitFog(
            colour: air.fogColour.linear,
            density: air.fogDensity,
            height: air.fogHeight,
            heightFalloff: air.fogFalloff,
            structure: air.mist,
            featureSize: air.mistSize <= 0 ? 0.02 : 1 / air.mistSize,
          );

    final falling = math.max(air?.rain ?? 0, air?.snow ?? 0);
    _precipitation = falling <= 0
        ? OrblitPrecipitation.none
        : OrblitPrecipitation(
            amount: falling,
            // Snow drifts down; rain does not.
            fall: (air?.snow ?? 0) > (air?.rain ?? 0) ? 1.2 : 9,
            wind: _windVector(
              weather?.windDirection ?? 135,
              air?.windSpeed ?? 0,
            ),
          );
  }

  /// Where the wind is going, as a vector across the ground.
  ///
  /// The bearing says where it comes *from*, clockwise from north, which is
  /// how a forecast states one and the opposite of where the rain lands.
  static Vector2 _windVector(double bearing, double speed) {
    final towards = radians(bearing + 180);
    return Vector2(math.sin(towards), math.cos(towards)) * speed;
  }

  /// A project-relative path as one the renderer can open.
  String? _resolve(String? asset) {
    if (asset == null || asset.isEmpty) return null;
    final root = projectRoot;
    if (root == null || asset.startsWith('/')) return asset;
    return root.endsWith('/') ? '$root$asset' : '$root/$asset';
  }
}
