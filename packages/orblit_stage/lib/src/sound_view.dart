import 'package:orblit_audio/orblit_audio.dart';
import 'package:orblit_motion/orblit_motion.dart';
import 'package:orblit_scene/orblit_scene.dart';
import 'package:vector_math/vector_math_64.dart';

import 'cutscenes.dart';
import 'document_view.dart';

/// The sound of a staged scene.
///
/// A [SoundSourceComponent] is heard from where its entity is, for as long as
/// the entity is shown. The sound is heard from the first entity with a
/// [SoundListenerComponent], or from the view's camera when there is none. A
/// cutscene's sound cues play through the same call, from the step that
/// [OrblitCutscenes.advance] returned.
///
/// Call [advance] and then [Audio.advance] once a frame. The first puts the
/// listener and the emitters where the scene has them, and the second tells
/// the backend what that sounds like.
final class OrblitSound {
  OrblitSound(this.view, this.audio, {this.cueBus});

  final OrblitDocumentView view;
  final Audio audio;

  /// The bus a cutscene's sounds play on, or the master bus.
  final Bus? cueBus;

  late final CueBoard _cues = CueBoard(
    audio,
    bus: cueBus,
    emitterFor: _emitterFor,
    release: _release,
  );

  final Map<String, _Source> _sources = {};
  Map<String, SoundSourceComponent> _wanted = {};
  SceneDocument? _synced;
  String? _listenerId;
  bool _heard = false;

  static const double _release = 0.05;

  /// What the entity [id] is made of, as far as sound goes, or null when it
  /// has no surface to name.
  String? surfaceOf(String id) {
    final surface = view.document[id]?[SceneComponents.soundSurface];
    if (surface is! SoundSurfaceComponent || surface.name.isEmpty) return null;
    return surface.name;
  }

  /// Plays the source on [id] again from the start. A source that is not in
  /// the scene, or not shown, makes no sound.
  void play(String id) {
    if (_changed) {
      _scan();
      _reconcile();
    }
    final source = _sources[id];
    if (source == null) return;
    source.voice?.stop(fadeSeconds: _release);
    source.voice = _begin(id, source);
  }

  /// Moves the listener and the sources to where the scene has them, adds
  /// and drops sources as the scene changed, and plays the cues in [step].
  void advance(double seconds, [OrblitCutsceneStep? step]) {
    // The listener goes first, since a voice that starts is given the level
    // it has for where the listener is.
    final changed = _changed;
    if (changed) _scan();
    _hear(seconds);
    if (changed) _reconcile();
    for (final MapEntry(:key, :value) in _sources.entries) {
      _glide(value.emitter, view.worldOf(key).getTranslation(), seconds);
    }
    _hear(seconds);
    _cues.update([
      for (final sound in step?.sounds ?? const <SoundAt>[])
        Cue(sound.sound, sound.at, sound.gain, binding: sound.binding),
    ]);
  }

  /// Fades out every voice this made, for the end of a scene. A scene that
  /// changes after this starts its sources again.
  void stop() {
    for (final source in _sources.values) {
      source.voice?.stop(fadeSeconds: _release);
    }
    _sources.clear();
    _cues.stop();
  }

  bool get _changed => !identical(view.document, _synced);

  /// Reads which sources should be sounding, by entity, and which entity is
  /// the listener.
  void _scan() {
    final document = view.document;
    _synced = document;
    _listenerId = null;
    _wanted = {};
    for (final entity in document.entities) {
      if (!view.isShown(entity.id)) continue;
      if (entity[SceneComponents.soundListener] != null) {
        _listenerId ??= entity.id;
      }
      final source = entity[SceneComponents.soundSource];
      if (source is SoundSourceComponent && source.clip != null) {
        _wanted[entity.id] = source;
      }
    }
  }

  /// Starts the sources that are wanted and not sounding, follows the ones
  /// that stay and drops the ones that are not wanted.
  void _reconcile() {
    for (final id in _sources.keys.toList()) {
      final source = _sources[id]!;
      final next = _wanted[id];
      if (next != null && _shape(next) == _shape(source.component)) {
        source.component = next;
        source.voice?.volume = next.volume;
      } else {
        _sources.remove(id);
        source.voice?.stop(fadeSeconds: _release);
      }
    }
    for (final MapEntry(:key, :value) in _wanted.entries) {
      if (_sources.containsKey(key)) continue;
      final source = _Source(value, _emitterOf(key, value));
      source.voice = _begin(key, source);
      _sources[key] = source;
    }
  }

  Emitter _emitterOf(String id, SoundSourceComponent source) => Emitter(
    position: view.worldOf(id).getTranslation(),
    falloff: Falloff(
      near: source.near,
      far: source.far,
      curve: switch (source.curve) {
        SoundCurve.linear => FalloffCurve.linear,
        SoundCurve.inverse => FalloffCurve.inverse,
      },
    ),
  );

  /// What a source's voice cannot change once it is playing.
  static (String?, String, bool, double, double, SoundCurve) _shape(
    SoundSourceComponent source,
  ) => (
    source.clip,
    source.bus,
    source.loop,
    source.near,
    source.far,
    source.curve,
  );

  Voice _begin(String id, _Source source) {
    final component = source.component;
    final Bus bus;
    try {
      bus = audio.mixer.bus(component.bus);
    } on ArgumentError {
      throw ArgumentError.value(
        component.bus,
        'bus',
        'The sound source "$id" plays on a bus the mixer does not have.',
      );
    }
    return audio.play(
      component.clip!, // _sync only keeps a source that has a clip.
      bus: bus,
      loop: component.loop,
      volume: component.volume,
      at: source.emitter,
    );
  }

  void _hear(double seconds) {
    final listener = audio.listener;
    final Vector3 at;
    final Vector3 forward;
    final Vector3 up;
    if (_listenerId case final id?) {
      final world = view.worldOf(id);
      at = world.getTranslation();
      forward = world.rotated3(Vector3(0, 0, -1)).normalized();
      up = world.rotated3(Vector3(0, 1, 0)).normalized();
    } else {
      final camera = view.camera;
      at = camera.position;
      forward = (camera.target - camera.position).normalized();
      // shortcut: the camera's up is taken as world up, so a camera looking
      // straight up or down hears no pan, give OrblitCamera an up if one does.
      up = Vector3(0, 1, 0);
    }
    if (_heard) {
      _glide(listener, at, seconds);
    } else {
      listener.teleport(at);
      _heard = true;
    }
    listener.forward = forward;
    listener.up = up;
  }

  /// A move faster than Doppler can follow is a cut, which has no speed.
  void _glide(Motion motion, Vector3 to, double seconds) {
    if (motion.position.distanceTo(to) > speedOfSound / 2 * seconds) {
      motion.teleport(to);
    } else {
      motion.moveTo(to, seconds);
    }
  }

  /// Where a cue bound to an entity is heard from, fixed where the entity
  /// stands when the cue starts, or nowhere for a binding that is no entity.
  // shortcut: the emitter does not follow the entity, make it one of
  // _sources if a cue's sound needs to travel with what it comes from.
  Emitter? _emitterFor(String binding) => view.document.contains(binding)
      ? Emitter(position: view.worldOf(binding).getTranslation())
      : null;
}

final class _Source {
  _Source(this.component, this.emitter);

  SoundSourceComponent component;
  final Emitter emitter;
  Voice? voice;
}
