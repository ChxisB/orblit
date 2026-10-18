import '../component.dart';
import '../values.dart';

/// How a sound fades between [SoundSourceComponent.near] and
/// [SoundSourceComponent.far].
enum SoundCurve { linear, inverse }

/// An entity that makes a sound, heard from where the entity is.
///
/// A looping source plays for as long as the entity is in the scene and
/// shown. One that does not loop plays once when it appears, and again when
/// the game asks. The sound is full volume within [near] metres and silent
/// from [far], and a source with no [clip] is silent, so a source can be
/// placed before its sound is chosen.
///
/// A file whose range is not a range, with [far] no further than [near] or
/// [near] not above nothing, reads as the default range instead, since a
/// sound with no falloff is better than a scene that will not open.
class SoundSourceComponent extends SceneComponent {
  SoundSourceComponent({
    this.clip,
    this.bus = 'master',
    this.loop = true,
    this.volume = 1,
    this.near = 1,
    this.far = 50,
    this.curve = SoundCurve.inverse,
  }) : assert(near > 0 && far > near, 'need 0 < near < far');

  static SoundSourceComponent fromJson(Map<String, Object?> json) {
    final near = Values.number(json, 'near', 1);
    final far = Values.number(json, 'far', 50);
    final ranged = near > 0 && far > near;
    final clip = Values.text(json, 'clip');
    final bus = Values.text(json, 'bus');
    return SoundSourceComponent(
      // A name that is empty is no name: a clip of '' would be asked of the
      // asset store, and a bus of '' is one no mixer has.
      clip: clip == null || clip.isEmpty ? null : clip,
      bus: bus == null || bus.isEmpty ? 'master' : bus,
      loop: Values.flag(json, 'loop', fallback: true),
      volume: Values.number(json, 'volume', 1),
      near: ranged ? near : 1,
      far: ranged ? far : 50,
      curve:
          Values.named(SoundCurve.values, json['curve']) ?? SoundCurve.inverse,
    );
  }

  /// The same source with some of it changed. A clip cannot be cleared here,
  /// only set; build a new source to leave it out.
  SoundSourceComponent copyWith({
    String? clip,
    String? bus,
    bool? loop,
    double? volume,
    double? near,
    double? far,
    SoundCurve? curve,
  }) => SoundSourceComponent(
    clip: clip ?? this.clip,
    bus: bus ?? this.bus,
    loop: loop ?? this.loop,
    volume: volume ?? this.volume,
    near: near ?? this.near,
    far: far ?? this.far,
    curve: curve ?? this.curve,
  );

  /// Project-relative, or null for no sound yet.
  final String? clip;

  /// The mixer bus it plays on. A name the game's mixer does not have is the
  /// game's to refuse.
  final String bus;
  final bool loop;

  /// A linear gain, before the bus and the distance.
  final double volume;

  /// Metres.
  final double near;
  final double far;
  final SoundCurve curve;

  @override
  String get type => SceneComponents.soundSource;

  @override
  Map<String, Object?> toJson() => Values.pruned({
    'clip': clip,
    'bus': bus,
    'loop': loop,
    'volume': volume,
    'near': near,
    'far': far,
    'curve': curve.name,
  });
}

/// This entity is where sound is heard from, facing down its own -Z.
///
/// Without one anywhere in the scene the sound is heard from the camera. With
/// one, from the first entity that has it, which is how a game hears from its
/// player rather than from a camera that sits behind them.
class SoundListenerComponent extends SceneComponent {
  const SoundListenerComponent();

  static SoundListenerComponent fromJson(Map<String, Object?> _) =>
      const SoundListenerComponent();

  @override
  String get type => SceneComponents.soundListener;

  @override
  Map<String, Object?> toJson() => const {};
}

/// What this entity is made of, as far as a sound is concerned: wood, stone,
/// glass. Two surfaces meeting pick the clip a touch makes.
///
/// Not a [MaterialComponent], which is what the renderer draws, and not the
/// friction and bounce of a body. A crate can look like one thing, grip like
/// another and ring like a third.
class SoundSurfaceComponent extends SceneComponent {
  const SoundSurfaceComponent({this.name = ''});

  static SoundSurfaceComponent fromJson(Map<String, Object?> json) =>
      SoundSurfaceComponent(name: Values.text(json, 'name') ?? '');

  final String name;

  @override
  String get type => SceneComponents.soundSurface;

  @override
  Map<String, Object?> toJson() => {'name': name};
}
