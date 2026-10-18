import 'package:matcher/matcher.dart';
import 'package:orblit_audio/orblit_audio.dart';
import 'package:orblit_scene/orblit_scene.dart';
import 'package:vector_math/vector_math_64.dart';

/// A matcher on one thing [read] finds in the value it is given.
final class _Reads<T, V> extends CustomMatcher {
  _Reads(String what, String name, this._read, Object matcher)
    : super(what, name, matcher);

  final V Function(T actual) _read;

  @override
  Object? featureValueOf(Object? actual) => _read(actual as T);
}

/// An entity with a component of type [T].
Matcher hasComponent<T extends SceneComponent>() =>
    _Reads<SceneEntity, Iterable<SceneComponent>>(
      'an entity with a $T',
      'components',
      (entity) => entity.components.values,
      contains(isA<T>()),
    );

/// An entity whose own position, not counting its parents, is within [within]
/// of [point]. Fails for an entity with no transform.
Matcher isPositionedNear(Vector3 point, {double within = 0.001}) =>
    _Reads<SceneEntity, double>(
      'an entity within $within of $point',
      'distance',
      (entity) {
        final transform = entity[SceneComponents.transform];
        if (transform is! TransformComponent) {
          throw StateError('${entity.name} has no transform.');
        }
        return transform.position.distanceTo(point);
      },
      lessThanOrEqualTo(within),
    );

/// Recorded sound that started [clip] at some point, finished or not.
Matcher hasPlayed(String clip) => _Reads<RecordedAudio, Iterable<String>>(
  'sound that started "$clip"',
  'clips started',
  (audio) => audio.calls.whereType<AudioStarted>().map((call) => call.clip),
  contains(clip),
);

/// Recorded sound with a voice of [clip] playing now.
Matcher isSounding(String clip) => _Reads<RecordedAudio, Iterable<String>>(
  'sound with "$clip" playing',
  'clips playing',
  (audio) => audio.calls
      .whereType<AudioStarted>()
      .where((call) => audio.isPlaying(call.voice))
      .map((call) => call.clip),
  contains(clip),
);
