import 'package:vector_math/vector_math_64.dart';

import '../component.dart';
import '../values.dart';

/// The shape a body is simulated as.
///
/// Deliberately few, and each one exactly: a box, a ball, a capsule, a
/// cylinder, a convex hull and the ground. A shape the solver cannot hold is
/// not offered here as something it will approximate, because a body that
/// behaves like a box while the file says cone is a bug somebody tunes around
/// rather than reports.
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

  /// A cylinder with flat ends, [BodyComponent.height] tall from end to end and
  /// [BodyComponent.radius] round, standing along the entity's own up. A
  /// barrel, a wheel, a pillar.
  cylinder,

  /// The smallest convex solid round the points in [BodyComponent.hull]. A rock,
  /// a wedge, a crate with its corners knocked off. Convex means no dents: a
  /// bowl is a solid lump to the solver, so a body that must be hollow is
  /// several.
  hull,

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

/// One of the six ways a free body can be held still, in the world's axes
/// rather than the body's own: a body locked from moving along `z` stays in
/// its plane however it is turned.
enum BodyLock { moveX, moveY, moveZ, turnX, turnY, turnZ }

/// A surface by what it is made of: how much it grips and how much it bounces.
///
/// A preset is a shortcut for setting two numbers, so a body does not store
/// one. It is a preset while its friction and restitution are exactly its
/// own, and it stops being one the moment either is dragged.
final class BodyMaterial {
  const BodyMaterial(
    this.name, {
    required this.friction,
    required this.restitution,
  });

  final String name;
  final double friction;
  final double restitution;

  /// The presets, slipperiest first.
  static const List<BodyMaterial> presets = [
    BodyMaterial('Ice', friction: 0.05, restitution: 0.05),
    BodyMaterial('Metal', friction: 0.4, restitution: 0.15),
    BodyMaterial('Wood', friction: 0.5, restitution: 0.2),
    BodyMaterial('Stone', friction: 0.7, restitution: 0.1),
    BodyMaterial('Sandbag', friction: 0.9, restitution: 0),
    BodyMaterial('Rubber', friction: 0.95, restitution: 0.8),
  ];

  /// The preset [body] is made of, or null when its numbers are its own.
  static BodyMaterial? of(BodyComponent body) {
    for (final preset in presets) {
      if (preset.friction == body.friction &&
          preset.restitution == body.restitution) {
        return preset;
      }
    }
    return null;
  }
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
/// entity's three scales, and a capsule or a cylinder its larger sideways scale
/// for the radius and its upright one for the height, because neither can be
/// stretched into anything but a bigger version of itself. A hull can be
/// stretched, and each of its points moves by the entity's scale along each
/// axis.
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
    List<double> hull = const [],
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
    this.trigger = false,
    this.stay = false,
    Vector3? surface,
    Set<BodyLock> locks = const {},
    this.gravityScale = 1,
    this.maxSpeed = 0,
    this.maxSpin = 0,
    Vector3? centreOfMass,
    Vector3? inertia,
  }) : size = size ?? Vector3.all(1),
       hull = List.unmodifiable(hull),
       centre = centre ?? Vector3.zero(),
       surface = surface ?? Vector3.zero(),
       locks = Set.unmodifiable(locks),
       centreOfMass = centreOfMass ?? Vector3.zero(),
       inertia = inertia ?? Vector3.zero();

  static BodyComponent fromJson(Map<String, Object?> json) => BodyComponent(
    shape: Values.named(BodyShape.values, json['shape']) ?? BodyShape.box,
    size: Values.vector(json['size'], fallback: 1),
    radius: Values.number(json, 'radius', 0.5),
    height: Values.number(json, 'height', 2),
    hull: Values.numbers(json['hull']),
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
    trigger: Values.flag(json, 'trigger', fallback: false),
    stay: Values.flag(json, 'stay', fallback: false),
    surface: Values.vector(json['surface']),
    locks: {
      for (final name in Values.texts(json['locks']))
        if (Values.named(BodyLock.values, name) case final lock?) lock,
    },
    gravityScale: Values.number(json, 'gravityScale', 1),
    maxSpeed: Values.number(json, 'maxSpeed', 0),
    maxSpin: Values.number(json, 'maxSpin', 0),
    centreOfMass: Values.vector(json['centreOfMass']),
    inertia: Values.vector(json['inertia']),
  );

  /// All thirty-two layers.
  static const int everyLayer = 0xFFFFFFFF;

  /// The same body with some of it changed.
  ///
  /// A component is replaced rather than edited, so this is how one field of
  /// a body changes: an inspector's mass slider makes a new body a frame. The
  /// vectors are copied, so the two never share a size.
  BodyComponent copyWith({
    BodyShape? shape,
    Vector3? size,
    double? radius,
    double? height,
    List<double>? hull,
    Vector3? centre,
    BodyMotion? motion,
    double? mass,
    double? friction,
    double? restitution,
    double? linearDamping,
    double? angularDamping,
    int? layers,
    int? cares,
    bool? startsAsleep,
    bool? trigger,
    bool? stay,
    Vector3? surface,
    Set<BodyLock>? locks,
    double? gravityScale,
    double? maxSpeed,
    double? maxSpin,
    Vector3? centreOfMass,
    Vector3? inertia,
  }) => BodyComponent(
    shape: shape ?? this.shape,
    size: (size ?? this.size).clone(),
    radius: radius ?? this.radius,
    height: height ?? this.height,
    hull: hull ?? this.hull,
    centre: (centre ?? this.centre).clone(),
    motion: motion ?? this.motion,
    mass: mass ?? this.mass,
    friction: friction ?? this.friction,
    restitution: restitution ?? this.restitution,
    linearDamping: linearDamping ?? this.linearDamping,
    angularDamping: angularDamping ?? this.angularDamping,
    layers: layers ?? this.layers,
    cares: cares ?? this.cares,
    startsAsleep: startsAsleep ?? this.startsAsleep,
    trigger: trigger ?? this.trigger,
    stay: stay ?? this.stay,
    surface: (surface ?? this.surface).clone(),
    locks: locks ?? this.locks,
    gravityScale: gravityScale ?? this.gravityScale,
    maxSpeed: maxSpeed ?? this.maxSpeed,
    maxSpin: maxSpin ?? this.maxSpin,
    centreOfMass: (centreOfMass ?? this.centreOfMass).clone(),
    inertia: (inertia ?? this.inertia).clone(),
  );

  /// The same body made of [material]: its friction and its bounce, and
  /// nothing else about it.
  BodyComponent madeOf(BodyMaterial material) =>
      copyWith(friction: material.friction, restitution: material.restitution);

  final BodyShape shape;

  /// How big a box is, edge to edge — not from the middle — because a one
  /// metre crate should say one.
  final Vector3 size;

  /// How round a ball, a capsule or a cylinder is, in metres.
  final double radius;

  /// How tall a capsule is from tip to tip, hemispheres included, because that
  /// is how tall a character is. A capsule no taller than twice its radius is
  /// all ends and no middle, and is simulated as the ball that makes it. A
  /// cylinder's is from one flat end to the other.
  final double height;

  /// The corners of a [BodyShape.hull], three numbers each: `x0, y0, z0, x1,
  /// ...`. In the entity's own units and measured from [centre], so a hull
  /// scales with its entity as a box does.
  ///
  /// The body is the smallest convex solid round them, so a point inside it
  /// changes nothing, and a hull cut from a mesh fills the mesh's dents. Fewer
  /// than four points, or points that all lie in one plane, make a hull with
  /// no inside, and no body is simulated for it. Every other shape ignores it.
  final List<double> hull;

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

  /// Whether it is a place rather than a thing: nothing collides with it and it
  /// pushes nothing, and it reports the bodies that come into it and leave it.
  /// A pickup, a door's sensor, the region a [ZoneComponent] takes effect over.
  /// Solid queries and characters do not see it.
  ///
  /// Only a fixed or driven body can be one. A free body has to be moved by the
  /// solver, so the flag is ignored for it.
  final bool trigger;

  /// Whether it hears every step that a contact goes on, or that a body is
  /// still inside it, and not only when one begins and ends. Most bodies do
  /// not want a message a step, so it is asked for.
  final bool stay;

  /// How fast its surface moves, in world metres per second, while the body
  /// stays where it is: a conveyor belt. What stands on it is carried along.
  /// Only the part along the face it touches counts.
  final Vector3 surface;

  /// The ways it may not move, in the world's axes. A locked move drops that
  /// part of every velocity, push and contact, and a locked turn does the same
  /// to spin. Only a free body is held: a fixed or driven one is placed, not
  /// pushed.
  final Set<BodyLock> locks;

  /// How much of the world's gravity, and of a [ZoneComponent]'s, it feels.
  /// One is ordinary, zero floats and a negative number rises.
  final double gravityScale;

  /// The fastest it may go in metres per second, and the fastest it may spin
  /// in radians per second. Zero is no limit.
  final double maxSpeed;
  final double maxSpin;

  /// Where its weight is, from the middle of its shape, in the entity's own
  /// units so it scales with the entity. It turns about this point, and a push
  /// through it does not spin it. Zero is the middle. Not [centre], which is
  /// where the shape sits on the entity.
  final Vector3 centreOfMass;

  /// How hard it is to turn about each of its own axes through the centre of
  /// mass, in kilogram square metres. All three above zero and it is used as
  /// given. Otherwise the shape's is used.
  final Vector3 inertia;

  @override
  String get type => SceneComponents.body;

  @override
  Map<String, Object?> toJson() => {
    'shape': shape.name,
    'size': Values.vectorToJson(size),
    'radius': radius,
    'height': height,
    'hull': hull,
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
    'trigger': trigger,
    'stay': stay,
    'surface': Values.vectorToJson(surface),
    // In the enum's order, so two bodies with the same locks write the same
    // list whichever order the locks were added in.
    'locks': [
      for (final lock in BodyLock.values)
        if (locks.contains(lock)) lock.name,
    ],
    'gravityScale': gravityScale,
    'maxSpeed': maxSpeed,
    'maxSpin': maxSpin,
    'centreOfMass': Values.vectorToJson(centreOfMass),
    'inertia': Values.vectorToJson(inertia),
  };
}
