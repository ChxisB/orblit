import 'package:orblit_light/orblit_light.dart' show LightType, Tint;
import 'package:orblit_weather/orblit_weather.dart'
    show CelestialBody, CloudKind, WeatherCondition, WeatherState;

import '../component.dart';
import '../values.dart';

/// A light, stated the way a fixture is.
///
/// Power in watts for anything with a bulb in it, and watts per square metre
/// for a sun, because a sun is not a bulb a long way off — it is a sheet of
/// light arriving parallel, and the number on it is an irradiance. Keeping the
/// two in the same field under the same name is what version one of this
/// format did, and every scene written then had to be converted.
class LightComponent extends SceneComponent {
  const LightComponent({
    this.kind = LightType.sun,
    this.power = 1000,
    this.colour = const Tint.hex(0xD9634F),
    this.spotSize = 45,
    this.spotBlend = 0.15,
    this.sourceRadius = 0.1,
    this.sunAngle = 0.526,
    this.body = CelestialBody.sun,
    this.castShadows = true,
  });

  static LightComponent fromJson(Map<String, Object?> json) => LightComponent(
    kind: Values.named(LightType.values, json['kind']) ?? LightType.sun,
    power: Values.number(json, 'power', 1000),
    colour: Values.tint(json['colour']),
    spotSize: Values.number(json, 'spotSize', 45),
    spotBlend: Values.number(json, 'spotBlend', 0.15),
    sourceRadius: Values.number(json, 'sourceRadius', 0.1),
    sunAngle: Values.number(json, 'sunAngle', 0.526),
    body: Values.named(CelestialBody.values, json['body']) ?? CelestialBody.sun,
    castShadows: Values.flag(json, 'castShadows', fallback: true),
  );

  final LightType kind;

  /// Watts, or watts per square metre when this is a sun.
  final double power;

  final Tint colour;

  /// The full cone angle of a spot, in degrees, and how much of its edge is
  /// soft.
  final double spotSize;
  final double spotBlend;

  /// How big the thing emitting is, in metres. What decides how soft a shadow
  /// edge is: a bare filament gives a hard one and a softbox does not.
  final double sourceRadius;

  /// How wide a sun is from the ground, in degrees. Half a degree is ours.
  final double sunAngle;

  final CelestialBody body;

  final bool castShadows;

  @override
  String get type => SceneComponents.light;

  /// Every field, every time, including the ones this kind of light does not
  /// use.
  ///
  /// The alternative — writing only what applies — loses a spot's cone angle
  /// the moment somebody flips it to a point light and back, which is exactly
  /// the kind of quiet data loss that makes people stop trusting a tool.
  @override
  Map<String, Object?> toJson() => {
    'kind': kind.name,
    'power': power,
    'colour': Values.tintToJson(colour),
    'spotSize': spotSize,
    'spotBlend': spotBlend,
    'sourceRadius': sourceRadius,
    'sunAngle': sunAngle,
    'body': body.name,
    'castShadows': castShadows,
  };
}

/// A point of view, and the lens at it.
///
/// The angle is across the shorter axis of the frame, which is the convention
/// the renderer measures by — stating it the other way makes a scene reframe
/// itself when somebody turns a phone sideways.
class CameraComponent extends SceneComponent {
  const CameraComponent({
    this.fieldOfView = 50,
    this.near = 0.1,
    this.far = 1000,
  });

  static CameraComponent fromJson(Map<String, Object?> json) => CameraComponent(
    fieldOfView: Values.number(json, 'fieldOfView', 50),
    near: Values.number(json, 'near', 0.1),
    far: Values.number(json, 'far', 1000),
  );

  /// Degrees across the shorter axis.
  final double fieldOfView;

  final double near;
  final double far;

  @override
  String get type => SceneComponents.camera;

  @override
  Map<String, Object?> toJson() => {
    'fieldOfView': fieldOfView,
    'near': near,
    'far': far,
  };
}

/// What the air is doing, and what it is on its way to doing.
///
/// An entity rather than a set of fields on the scene, because weather
/// changes: a change has two ends and a set of fields can only hold one. The
/// [condition] is where it is heading, [air] is where it is now, and
/// [transitionSeconds] is how long it takes to get there.
class WeatherComponent extends SceneComponent {
  const WeatherComponent({
    this.condition = WeatherCondition.clear,
    this.cloudKind,
    required this.air,
    this.windDirection = 135,
    this.transitionSeconds = 8,
  });

  /// Weather with nothing set, which is a clear sky.
  factory WeatherComponent.clear() =>
      WeatherComponent(air: WeatherState.of(WeatherCondition.clear));

  static WeatherComponent fromJson(Map<String, Object?> json) {
    final condition =
        Values.named(WeatherCondition.values, json['condition']) ??
        WeatherCondition.clear;
    return WeatherComponent(
      condition: condition,
      cloudKind: Values.named(CloudKind.values, json['cloudKind']),
      air: airFromJson(json['air'], condition),
      windDirection: Values.number(json, 'windDirection', 135),
      transitionSeconds: Values.number(json, 'transition', 8),
    );
  }

  final WeatherCondition condition;

  /// The cloud somebody chose, or null to follow the condition's own.
  ///
  /// Null is not a missing value here — it is "whatever this condition means
  /// by cloud", so a scene that has never been given one keeps following the
  /// mapping when the mapping improves rather than being frozen at whatever
  /// it happened to be the day it was saved.
  final CloudKind? cloudKind;

  final WeatherState air;

  /// Where the wind comes from, in degrees clockwise from north.
  final double windDirection;

  final double transitionSeconds;

  @override
  String get type => SceneComponents.weather;

  @override
  Map<String, Object?> toJson() => Values.pruned({
    'condition': condition.name,
    'cloudKind': cloudKind?.name,
    'windDirection': windDirection,
    'transition': transitionSeconds,
    'air': airToJson(air),
  });

  /// The air as JSON.
  ///
  /// Static and public because the 2 to 3 migration writes one of these out of
  /// a scene's old fog fields, and it should produce exactly what a saved
  /// scene does rather than its own nearly-identical shape.
  static Map<String, Object?> airToJson(WeatherState air) => Values.pruned({
    'cover': air.cloudCover,
    'colour': Values.tintToJson(air.fogColour),
    'density': air.fogDensity,
    'height': air.fogHeight,
    'falloff': air.fogFalloff,
    'mist': air.mist,
    'size': air.mistSize,
    'wind': air.windSpeed,
    // Left out when there is none, so a dry scene's file says nothing
    // about rain.
    'rain': air.rain > 0 ? air.rain : null,
    'snow': air.snow > 0 ? air.snow : null,
    'lightning': air.lightning > 0 ? air.lightning : null,
    'cloudHeight': air.cloudHeight,
  });

  /// And back, falling through to the condition's own values for anything the
  /// file does not say.
  static WeatherState airFromJson(Object? raw, WeatherCondition condition) {
    final preset = WeatherState.of(condition);
    if (raw is! Map<String, Object?>) return preset;

    return WeatherState(
      cloudCover: Values.number(raw, 'cover', preset.cloudCover),
      fogColour: Values.tint(raw['colour'], fallback: preset.fogColour),
      fogDensity: Values.number(raw, 'density', preset.fogDensity),
      fogHeight: Values.number(raw, 'height', preset.fogHeight),
      fogFalloff: Values.number(raw, 'falloff', preset.fogFalloff),
      mist: Values.number(raw, 'mist', preset.mist),
      mistSize: Values.number(raw, 'size', preset.mistSize),
      windSpeed: Values.number(raw, 'wind', preset.windSpeed),
      rain: Values.number(raw, 'rain', 0),
      snow: Values.number(raw, 'snow', 0),
      lightning: Values.number(raw, 'lightning', 0),
      cloudHeight: Values.number(raw, 'cloudHeight', preset.cloudHeight),
    );
  }
}
