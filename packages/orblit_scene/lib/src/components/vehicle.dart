import '../component.dart';
import '../values.dart';

/// A car: the body this entity has, held up by the wheels under it.
///
/// The entity also has a [BodyComponent], which is the chassis: its shape, its
/// mass, its centre of mass and its layers. The wheels are the entity's
/// children that have a [WheelComponent], and only its direct children. A
/// chassis that is scaled is not a vehicle.
///
/// Every field is written every time, including the engine of a car that has
/// no wheel that drives, for the reason [BodyComponent] gives.
class VehicleComponent extends SceneComponent {
  VehicleComponent({
    this.maxSteer = 30,
    this.antiRoll = 0,
    List<double>? torque,
    this.idle = 900,
    List<double>? ratios,
    this.reverse = 3.2,
    this.finalDrive = 3.7,
    this.lock = 0,
    this.ground = 0xFFFFFFFF,
  }) : torque = List.unmodifiable(torque ?? defaultTorque),
       ratios = List.unmodifiable(ratios ?? defaultRatios);

  /// A small petrol engine: revolutions a minute and newton metres, in turn.
  static const defaultTorque = [
    1000.0,
    160.0,
    3000.0,
    220.0,
    5000.0,
    240.0,
    6500.0,
    200.0,
  ];

  /// Five forward gears.
  static const defaultRatios = [3.6, 2.2, 1.5, 1.1, 0.9];

  static VehicleComponent fromJson(Map<String, Object?> json) =>
      VehicleComponent(
        maxSteer: Values.number(json, 'maxSteer', 30),
        antiRoll: Values.number(json, 'antiRoll', 0),
        torque: _someNumbers(json['torque']),
        idle: Values.number(json, 'idle', 900),
        ratios: _someNumbers(json['ratios']),
        reverse: Values.number(json, 'reverse', 3.2),
        finalDrive: Values.number(json, 'finalDrive', 3.7),
        lock: Values.number(json, 'lock', 0),
        ground: Values.bits(json, 'ground', 0xFFFFFFFF),
      );

  VehicleComponent copyWith({
    double? maxSteer,
    double? antiRoll,
    List<double>? torque,
    double? idle,
    List<double>? ratios,
    double? reverse,
    double? finalDrive,
    double? lock,
    int? ground,
  }) => VehicleComponent(
    maxSteer: maxSteer ?? this.maxSteer,
    antiRoll: antiRoll ?? this.antiRoll,
    torque: torque ?? this.torque,
    idle: idle ?? this.idle,
    ratios: ratios ?? this.ratios,
    reverse: reverse ?? this.reverse,
    finalDrive: finalDrive ?? this.finalDrive,
    lock: lock ?? this.lock,
    ground: ground ?? this.ground,
  );

  /// Degrees the middle of the steering axle turns at full lock.
  final double maxSteer;

  /// Newtons a metre of difference in compression across an axle, as a bar
  /// joining its two wheels. Nought is no bar.
  final double antiRoll;

  /// The engine's torque with the pedal down: revolutions a minute, then newton
  /// metres, for each point in order of speed. The last is the redline.
  final List<double> torque;

  /// Revolutions a minute the engine ticks over at.
  final double idle;

  /// The forward gears, first to last, as turns of the engine for a turn of
  /// the gearbox's output. Used only when a wheel drives.
  final List<double> ratios;
  final double reverse;
  final double finalDrive;

  /// Newton metres the differential of an axle may move to the slower wheel.
  /// Nought is open. A very large one is locked.
  final double lock;

  /// The layers a wheel sees as ground, one bit each, so a car does not stand
  /// on a character.
  final int ground;

  @override
  String get type => SceneComponents.vehicle;

  @override
  Map<String, Object?> toJson() => {
    'maxSteer': maxSteer,
    'antiRoll': antiRoll,
    'torque': torque,
    'idle': idle,
    'ratios': ratios,
    'reverse': reverse,
    'finalDrive': finalDrive,
    'lock': lock,
    'ground': ground,
  };

  /// The list a file gives, or null for the default when it gives none. An
  /// empty list is kept: it is a car the spec will refuse, not a car with an
  /// engine nobody wrote.
  static List<double>? _someNumbers(Object? raw) =>
      raw is List ? Values.numbers(raw) : null;
}

/// One wheel of a [VehicleComponent], on an entity that is a direct child of
/// it.
///
/// The entity's position is where the wheel sits when the car is at rest. Its
/// rotation is the vehicle's to write: it steers, and it rolls about its own x
/// axis, so a tyre drawn on a child of this entity turns with it. The wheel's
/// own scale is kept.
class WheelComponent extends SceneComponent {
  const WheelComponent({
    this.radius = 0.35,
    this.steers = false,
    this.drives = false,
    this.brake = 3000,
    this.handbrake = false,
    this.travel = 0.3,
    this.hertz = 1.5,
    this.damping = 0.5,
    this.grip = 1,
  });

  static WheelComponent fromJson(Map<String, Object?> json) => WheelComponent(
    radius: Values.number(json, 'radius', 0.35),
    steers: Values.flag(json, 'steers', fallback: false),
    drives: Values.flag(json, 'drives', fallback: false),
    brake: Values.number(json, 'brake', 3000),
    handbrake: Values.flag(json, 'handbrake', fallback: false),
    travel: Values.number(json, 'travel', 0.3),
    hertz: Values.number(json, 'hertz', 1.5),
    damping: Values.number(json, 'damping', 0.5),
    grip: Values.number(json, 'grip', 1),
  );

  WheelComponent copyWith({
    double? radius,
    bool? steers,
    bool? drives,
    double? brake,
    bool? handbrake,
    double? travel,
    double? hertz,
    double? damping,
    double? grip,
  }) => WheelComponent(
    radius: radius ?? this.radius,
    steers: steers ?? this.steers,
    drives: drives ?? this.drives,
    brake: brake ?? this.brake,
    handbrake: handbrake ?? this.handbrake,
    travel: travel ?? this.travel,
    hertz: hertz ?? this.hertz,
    damping: damping ?? this.damping,
    grip: grip ?? this.grip,
  );

  /// Metres.
  final double radius;

  /// It turns with the steering.
  final bool steers;

  /// The engine turns it.
  final bool drives;

  /// Newton metres of brake at full pressure. Nought is a wheel with no brake.
  final double brake;

  /// The handbrake holds it, with the same brake.
  final bool handbrake;

  /// Metres the wheel can move, from hanging free to the top of its
  /// suspension.
  final double travel;

  /// Cycles a second of the car's weight on this spring, and its damping as a
  /// share of critical.
  final double hertz;
  final double damping;

  /// The most force the rubber gives for each newton of load.
  final double grip;

  @override
  String get type => SceneComponents.wheel;

  @override
  Map<String, Object?> toJson() => {
    'radius': radius,
    'steers': steers,
    'drives': drives,
    'brake': brake,
    'handbrake': handbrake,
    'travel': travel,
    'hertz': hertz,
    'damping': damping,
    'grip': grip,
  };
}
