import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_profile/orblit_profile.dart';
import 'package:orblit_scene/orblit_scene.dart';
import 'package:orblit_stage/orblit_stage.dart';
import 'package:vector_math/vector_math_64.dart';

// The two phases the document view times: building the scene it hands the
// renderer, and applying a diff to what it holds.

SceneEntity crate(Vector3 at) => SceneEntity(
  id: 'crate',
  name: 'crate',
  components: {
    SceneComponents.transform: TransformComponent(position: at),
    SceneComponents.mesh: const MeshComponent(),
  },
);

void main() {
  test('times scene build and diff, and nothing else', () {
    final profiler = Profiler()..frame();
    final before = SceneDocument(entities: [crate(Vector3.zero())]);
    final view = OrblitDocumentView(before, profiler: profiler);

    view.apply(
      SceneDiff.between(
        before,
        SceneDocument(entities: [crate(Vector3(0, 1, 0))]),
      ),
    );
    view.scene;
    profiler.frame();

    final spans = profiler.snapshot()['spans']! as Map<String, Object?>;
    expect(spans.keys, unorderedEquals(['diff', 'scene build']));
  });

  test('is silent by default', () {
    final view = OrblitDocumentView(SceneDocument(entities: []));

    expect(view.profiler.snapshot(), isEmpty);
  });
}
