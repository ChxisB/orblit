import 'package:vector_math/vector_math_64.dart';

import '../component.dart';
import '../values.dart';

/// The shape a body is simulated as.
///
/// Deliberately few, and each one exactly: a box, a ball, a capsule and the
/// ground. A shape the solver cannot hold is not offered here as something it
/// will approximate, because a body that behaves like a box while the file
/// says cylinder is a bug somebody tunes around rather than reports.
enum BodyShape {
  /// A box [BodyComponent.size] across.
  box,

  /// A ball of [BodyComponent.radius].
  sphere,

  /// A cylinder with a hemisphere on each end, [BodyComponent.height] tall
  /// from tip to tip and [BodyComponent.radius] round, standing along the
  /// entity's own up. What a character or a limb is made of: it has no corner
  /// to catch on the seam between two floor tiles.
  capsule,

  /// Endless ground. Everything below the entity's own up, through the point
  /// the body is centred on, is solid. It never moves, whatever [BodyMotion]
  /// says, because a half-space has no middle to spin about.
  plane,
}

/// How a body moves.
enum BodyMotion {
  /// Never moves. The floor, the walls.
  fixed,

  /// Goes exactly where it is sent and pushes whatever is in the way without
  /// being pushed back. A lift, a door, a moving platform.
  driven,

  /// Falls, is pushed, and pushes back.
  free,
}

/// Something the physics simulates.
///
/// Not the same thing as the boundary a mesh carries. A boundary says where an
/// object begins and ends for picking it and for asking what a ray hits in the
/// scene's geometry; a body says what the simulation does with it — whether it
/// falls, how heavy it is, what it bounces off. A crate usually has both, a
/// decoration has a boundary and no body, and a trigger in empty space has a
/// body and nothing to draw.
///
/// Sizes are in the entity's own units, so a body scales with its entity: a
/// crate scaled to two is a crate with a body twice the size, and fitting a
/// body to a mesh is copying the mesh's own dimensions rather than working
/// out what they come to in the world. A ball takes the largest of its
/// entity's three scales, and a capsule its larger sideways scale for the
/// radius and its upright one for the height, because neither can be
/// stretched into anything but a bigger version of itself.
///
/// Every field is written every time, including the ones this shape does not
/// use, for the reason [LightComponent] gives: a box switched to a ball and
/// back should come back the size it was.
class BodyComponent extends SceneComponent {
  BodyComponent({
    this.shape = BodyShape.box,
    Vector3? size,
    this.radius = 0.5,
    this.height = 2,
    Vector3? centre,
    this.motion = BodyMotion.free,
    this.mass = 1,
    this.friction = 0.5,
    this.restitution = 0,
    this.linearDamping = 0.05,
    this.angularDamping = 0.05,
    this.layers = 1,
    this.cares = everyLayer,
    this.startsAsleep = false,
  }) : size = size ?? Vector3.all(1),
       centre = centre ?? Vector3.zero();

  static BodyComponent fromJson(Map<String, Object?> json) => BodyComponent(
    shape: Values.named(BodyShape.values, json['shape']) ?? BodyShape.box,
    size: Values.vector(json['size'], fallback: 1),
    radius: Values.number(json, 'radius', 0.5),
    height: Values.number(json, 'height', 2),
    centre: Values.vector(json['centre']),
    motion: Values.named(BodyMotion.values, json['motion']) ?? BodyMotion.free,
    mass: Values.number(json, 'mass', 1),
    friction: Values.number(json, 'friction', 0.5),
    restitution: Values.number(json, 'restitution', 0),
    linearDamping: Values.number(json, 'linearDamping', 0.05),
    angularDamping: Values.number(json, 'angularDamping', 0.05),
    layers: Values.bits(json, 'layers', 1),
    cares: Values.bits(json, 'cares', everyLayer),
    startsAsleep: Values.flag(json, 'asleep', fallback: false),
  );

  /// All thirty-two layers.
  static const int everyLayer = 0xFFFFFFFF;

  final BodyShape shape;

  /// How big a box is, edge to edge — not from the middle — because a one
  /// metre crate should say one.
  final Vector3 size;

  /// How round a ball or a capsule is, in metres.
  final double radius;

  /// How tall a capsule is from tip to tip, hemispheres included, because that
  /// is how tall a character is. A capsule no taller than twice its radius is
  /// all ends and no middle, and is simulated as the ball that makes it.
  final double height;

  /// Where the shape sits relative to the entity, for the times the entity's
  /// origin is not the middle of what it is — a character stands on its feet,
  /// and its capsule belongs round its waist.
  final Vector3 centre;

  final BodyMotion motion;

  /// Kilograms. Only a free body is moved by what it weighs.
  final double mass;

  /// How much it grips, from nothing (ice) up; half is ordinary.
  final double friction;

  /// How much it bounces, from nothing (a sandbag) to fully (a perfect ball).
  final double restitution;

  /// How quickly it slows down and stops spinning on its own, as a fraction a
  /// second. What stands in for air.
  final double linearDamping;
  final double angularDamping;

  /// Which layers it is in, one bit each.
  final int layers;

  /// Which layers it wants to meet. A pair meets when either cares about the
  /// other, not only when both do, so a bullet that cares about walls hits a
  /// wall that cares about nothing — the same rule every query in Orblit uses.
  final int cares;

  /// Whether it begins at rest, waiting to be touched. A tower of crates that
  /// starts asleep costs nothing until somebody knocks it.
  final bool startsAsleep;

  @override
  String get type => SceneComponents.body;

  @override
  Map<String, Object?> toJson() => {
    'shape': shape.name,
    'size': Values.vectorToJson(size),
    'radius': radius,
    'height': height,
    'centre': Values.vectorToJson(centre),
    'motion': motion.name,
    'mass': mass,
    'friction': friction,
    'restitution': restitution,
    'linearDamping': linearDamping,
    'angularDamping': angularDamping,
    'layers': layers,
    'cares': cares,
    'asleep': startsAsleep,
  };
}
