import 'package:orblit_filament/orblit_filament.dart';
import 'package:orblit_motion/orblit_motion.dart';
import 'package:orblit_scene/orblit_scene.dart';
import 'package:vector_math/vector_math_64.dart';

import 'document_view.dart';

/// The cutscenes a game can play over a staged scene, one at a time.
///
/// A cutscene is started by its name, from game code or from a mark. While
/// it runs, each [advance] moves what it keys and has the view look through
/// its shots. When it is over the view looks through the scene's own camera
/// again. A cutscene that holds leaves the scene where it put it, and one
/// that releases puts back everything it moved.
final class OrblitCutscenes {
  OrblitCutscenes(this.view, Iterable<CutsceneDocument> cutscenes)
    : _byName = {} {
    for (final cutscene in cutscenes) {
      if (_byName.containsKey(cutscene.name)) {
        throw ArgumentError.value(
          cutscene.name,
          'cutscenes',
          'Two cutscenes have the same name, so one could never be started.',
        );
      }
      _byName[cutscene.name] = cutscene;
    }
  }

  final OrblitDocumentView view;

  final Map<String, CutsceneDocument> _byName;

  _Running? _running;

  /// Whether there is a cutscene called [name] to start.
  bool has(String name) => _byName.containsKey(name);

  /// The name of the cutscene running, or null when none is.
  String? get playing => _running?.cutscene.name;

  /// Starts the cutscene called [name] from its beginning, ending whichever
  /// one was running first.
  void start(String name) {
    final cutscene = _byName[name];
    if (cutscene == null) {
      throw ArgumentError.value(name, 'name', 'There is no such cutscene.');
    }
    stop();
    final running = _Running(
      cutscene,
      Director(cutscene.sequence, whenDone: cutscene.motion.whenDone)..play(),
    );
    _running = running;
    _show(running);
  }

  /// Starts the cutscene named by the first of [marks] that names one.
  ///
  /// A mark named after a cutscene is how a clip, or another cutscene,
  /// starts one without code.
  void startFrom(Iterable<Mark> marks) {
    for (final mark in marks) {
      if (!has(mark.name)) continue;
      start(mark.name);
      return;
    }
  }

  /// Ends the cutscene running now, as though it had reached its end.
  void stop() {
    if (_running case final running?) _end(running);
  }

  /// Plays on by [seconds], and says what happened on the way.
  OrblitCutsceneStep advance(double seconds) {
    final running = _running;
    if (running == null) return const OrblitCutsceneStep();
    final step = running.director.advance(seconds);
    _show(running);
    if (!running.director.finished) {
      return OrblitCutsceneStep(marks: step.marks, sounds: step.frame.sounds);
    }
    _end(running);
    return OrblitCutsceneStep(marks: step.marks, ended: true);
  }

  /// Puts the scene where the cutscene says it is now, and the view behind
  /// its shots.
  ///
  /// Keys first, since a camera may be one of the things a cutscene moves.
  void _show(_Running running) {
    final at = running.director.at;
    final ops = sceneOpsFor(
      running.cutscene.motion.sampleAt(at),
      view.document,
      ClipScope.wholeScene,
    );
    for (final op in ops) {
      running.before.putIfAbsent((op.id, op.type, op.field), () => op.from);
    }
    view.apply(SceneDiff(ops));
    view.through = blendCameras([
      for (final shot in running.cutscene.shotsAt(at))
        if (view.cameraOf(shot.camera) case final camera?)
          (camera, shot.weight),
    ]);
  }

  void _end(_Running running) {
    _running = null;
    view.through = null;
    if (running.director.whenDone == WhenDone.release) {
      view.apply(SceneDiff(_putBack(running.before)));
    }
  }

  /// The changes that put back every field a cutscene moved to what it was
  /// before the cutscene first moved it.
  List<SetField> _putBack(Map<(String, String, String), Object?> before) => [
    for (final MapEntry(key: (id, type, field), value: was) in before.entries)
      if (view.document[id]?[type] case final component?)
        if (component.toJson()[field] case final now
            when !Values.same(now, was))
          SetField(id, type, field, from: now, to: was),
  ];
}

/// What happened in one [OrblitCutscenes.advance].
final class OrblitCutsceneStep {
  const OrblitCutsceneStep({
    this.marks = const [],
    this.sounds = const [],
    this.ended = false,
  });

  /// The marks passed, in order, for the game to act on.
  final List<Mark> marks;

  /// The sounds that should be playing now, and how far into each.
  final List<SoundAt> sounds;

  /// Whether the cutscene ended in this step.
  final bool ended;
}

/// The view from between [cameras], each with how much it has to say.
///
/// Where they stand and which way they look are averaged by weight, and so
/// is the field of view. The rest is the heaviest camera's, since an
/// exposure halfway between two is not something a shot is framed with.
/// Null when no camera has any say.
OrblitCamera? blendCameras(Iterable<(OrblitCamera, double)> cameras) {
  final weighted = [
    for (final (camera, weight) in cameras)
      if (weight > 0) (camera, weight),
  ];
  if (weighted.isEmpty) return null;
  if (weighted.length == 1) return weighted.single.$1;

  var (heaviest, most) = weighted.first;
  var total = 0.0;
  var fieldOfView = 0.0;
  final position = Vector3.zero();
  final facing = Vector3.zero();
  for (final (camera, weight) in weighted) {
    total += weight;
    fieldOfView += camera.fieldOfView * weight;
    position.addScaled(camera.position, weight);
    facing.addScaled(_facing(camera), weight);
    if (weight > most) (heaviest, most) = (camera, weight);
  }
  position.scale(1 / total);
  // Two cameras looking exactly opposite ways add up to looking nowhere, so
  // look the heaviest one's way instead.
  final forward = facing.length2 < 1e-12
      ? _facing(heaviest)
      : facing.normalized();
  return heaviest.copyWith(
    position: position,
    target: position + forward,
    fieldOfView: fieldOfView / total,
  );
}

Vector3 _facing(OrblitCamera camera) =>
    (camera.target - camera.position).normalized();

final class _Running {
  _Running(this.cutscene, this.director);

  final CutsceneDocument cutscene;
  final Director director;

  /// Each field the cutscene has moved, as it was before the first move.
  final Map<(String, String, String), Object?> before = {};
}
