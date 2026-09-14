import 'package:flutter_test/flutter_test.dart';
import 'package:orbis_filament/orbis_filament.dart';
import 'package:vector_math/vector_math_64.dart';

/// Batching is one flag on the wire and nothing else, and these are the ways
/// that one flag could go missing between the author and the renderer.
///
/// What batching does to a frame is measured on the device, where the frames
/// are; what is checked here is that the scene says what the author asked
/// for, and says nothing else differently — because a batched scene that also
/// packed its objects differently would be two changes, and the measurement
/// would not be able to tell which one moved the picture.
void main() {
  OrbisScene sceneOf({bool? batching}) {
    final objects = [
      for (var i = 0; i < 6; i++)
        OrbisObject(
          key: 100 + i,
          transform: Matrix4.translationValues(i.toDouble(), 0, 0),
          colour: Vector3(0.5, 0.4, 0.3),
        ),
    ];
    final camera = OrbisCamera(
      position: Vector3(0, 2, 8),
      target: Vector3.zero(),
    );
    return batching == null
        ? OrbisScene(objects: objects, camera: camera)
        : OrbisScene(objects: objects, camera: camera, batching: batching);
  }

  test('is on unless turned off', () {
    // On by default since the cause of the batched-shadow difference was
    // found: the placeholder cube declared a bounding box that did not
    // contain it, so the *unbatched* side was fitting its shadows from the
    // wrong volume. With that corrected, what is left is a group's box being
    // the union of its members', which is bounded, confined to shadow edges
    // and saturating at eight members to a chunk — see the field's own doc
    // comment for the measurements this rests on. A scene that says nothing
    // gets the merged draws.
    expect(sceneOf().batching, isTrue);
    expect(sceneOf().toMessage(1)['batching'], isTrue);
  });

  test('is turned off by saying so, not by saying nothing', () {
    // The half of the switch that is easy to lose when a default is flipped:
    // a scene that wants every renderable culled on its own must still be
    // able to have that, and must be able to say it explicitly rather than
    // by omission.
    expect(sceneOf(batching: false).batching, isFalse);
    expect(sceneOf(batching: false).toMessage(1)['batching'], isFalse);
  });

  test('reaches the message when asked for', () {
    expect(sceneOf(batching: true).toMessage(1)['batching'], isTrue);
  });

  test('changes nothing else about the message', () {
    // The objects travel exactly as they did. Batching is decided on the far
    // side by comparing what arrives, not by packing anything differently.
    final off = sceneOf(batching: false).toMessage(1);
    final on = sceneOf(batching: true).toMessage(1);
    expect(off.keys.toSet(), on.keys.toSet());
    for (final key in off.keys) {
      if (key == 'batching') continue;
      expect(on[key], off[key], reason: '$key differs with batching on');
    }
  });

  test('survives copyWith, and can be changed by it', () {
    final on = sceneOf(batching: true);
    // Another field changed: batching is kept rather than quietly reset,
    // which is the way a flag added to a copyWith is usually lost.
    expect(on.copyWith(sky: OrbisSky()).batching, isTrue);
    expect(on.copyWith(batching: false).batching, isFalse);
    expect(sceneOf(batching: false).copyWith(batching: true).batching, isTrue);
    // The direction that a flipped default makes easy to break: `off` is no
    // longer the same value as "not stated", so a copyWith that resolved the
    // flag with `batching ?? true` rather than `batching ?? this.batching`
    // would turn a deliberately unbatched scene back on the first time
    // anything else about it was changed.
    final off = sceneOf(batching: false);
    expect(off.copyWith(sky: OrbisSky()).batching, isFalse);
    expect(off.copyWith(sky: OrbisSky()).toMessage(1)['batching'], isFalse);
  });
}
