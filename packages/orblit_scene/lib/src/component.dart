import 'components/body.dart';
import 'components/data.dart';
import 'components/drawing.dart';
import 'components/flat.dart';
import 'components/staging.dart';
import 'components/transform.dart';

/// One fact about an entity.
///
/// A scene used to say what an object *was* — a mesh, a light, a camera — and
/// every property of every kind lived on one class, so a light carried a mesh
/// path it would never use and a camera carried a shadow flag. It also meant
/// that a thing which was two of them at once could not be said at all: a lamp
/// is a mesh and a light, and the old shape made somebody build it out of two
/// objects and keep them in step by hand.
///
/// A component says one thing, an entity has as many as it needs, and the two
/// questions a renderer asks — what is here, and what do I do with it — are
/// answered by looking at what the entity has rather than at what it claims to
/// be.
abstract class SceneComponent {
  const SceneComponent();

  /// The name this is written under, and the key it lives at on an entity.
  String get type;

  /// The component as it goes into the file.
  ///
  /// Also what a diff compares, which is why it is the only view that has to
  /// be exhaustive: a field left out of here is a field an edit to which
  /// cannot be undone.
  Map<String, Object?> toJson();
}

/// A component this version of Orblit has never heard of.
///
/// Kept exactly as it arrived, and written back exactly as it arrived. The
/// case this exists for is two people on one project with different versions
/// of the editor, or a project that has a component some tool of its own puts
/// there: dropping what we do not recognise would mean the newer half of a
/// team silently deletes the older half's work every time they open a file.
///
/// It is not a licence to read newer *files* — a file whose format version is
/// ahead of ours is still refused, because a version bump says the keys we do
/// recognise may have changed meaning. This is for a known format carrying an
/// unknown component, which is the ordinary case.
class UnknownComponent extends SceneComponent {
  const UnknownComponent(this.type, this._json);

  @override
  final String type;

  final Map<String, Object?> _json;

  @override
  Map<String, Object?> toJson() => _json;
}

/// Reads a component of one type out of its JSON.
typedef ComponentReader = SceneComponent Function(Map<String, Object?> json);

/// Which components exist, how to read them, and the order they are written.
abstract final class SceneComponents {
  static const String transform = 'transform';
  static const String mesh = 'mesh';
  static const String material = 'material';
  static const String body = 'body';
  static const String light = 'light';
  static const String camera = 'camera';
  static const String splats = 'splats';
  static const String sprite = 'sprite';
  static const String tilemap = 'tilemap';
  static const String parallax = 'parallax';
  static const String weather = 'weather';
  static const String canvas = 'canvas';
  static const String data = 'data';
  static const String prefab = 'prefab';

  /// The order components are written in, so two saves of one scene produce
  /// the same bytes and a diff of a scene file is a diff of what changed.
  ///
  /// Roughly outside-in: where a thing is, then what it draws and what it is
  /// simulated as, then what it lights or watches with, then the flat layers,
  /// then what it carries.
  /// Anything not on this list is written after it, in the order it was read,
  /// which keeps an unknown component's position stable too.
  static const List<String> order = [
    transform,
    mesh,
    material,
    body,
    light,
    camera,
    splats,
    sprite,
    tilemap,
    parallax,
    weather,
    canvas,
    data,
    prefab,
  ];

  static const Map<String, ComponentReader> readers = {
    transform: TransformComponent.fromJson,
    mesh: MeshComponent.fromJson,
    material: MaterialComponent.fromJson,
    body: BodyComponent.fromJson,
    light: LightComponent.fromJson,
    camera: CameraComponent.fromJson,
    splats: SplatsComponent.fromJson,
    sprite: SpriteComponent.fromJson,
    tilemap: TilemapComponent.fromJson,
    parallax: ParallaxComponent.fromJson,
    weather: WeatherComponent.fromJson,
    canvas: CanvasComponent.fromJson,
    data: DataComponent.fromJson,
    prefab: PrefabComponent.fromJson,
  };

  /// One component, typed when we know the name and kept whole when we do not.
  static SceneComponent read(String type, Map<String, Object?> json) {
    final reader = readers[type];
    return reader == null ? UnknownComponent(type, json) : reader(json);
  }
}
