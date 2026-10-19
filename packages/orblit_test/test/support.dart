import 'package:matcher/matcher.dart';
import 'package:orblit_scene/orblit_scene.dart';
import 'package:vector_math/vector_math_64.dart';

/// Why [matcher] refuses [value], as a failing test would print it.
String refusal(Matcher matcher, Object? value) {
  final state = <Object?, Object?>{};
  if (matcher.matches(value, state)) throw StateError('It matched.');
  return matcher
      .describeMismatch(value, StringDescription(), state, false)
      .toString();
}

SceneEntity thing(
  String id, {
  String? name,
  Vector3? at,
  Map<String, SceneComponent> components = const {},
}) => SceneEntity(
  id: id,
  name: name ?? id,
  components: {
    if (at != null) SceneComponents.transform: TransformComponent(position: at),
    ...components,
  },
);
