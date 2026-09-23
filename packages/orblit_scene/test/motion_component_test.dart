import 'package:orblit_scene/orblit_scene.dart';
import 'package:test/test.dart';

void main() {
  group('a motion component', () {
    test('plays nothing when the file says nothing', () {
      final motion = MotionComponent.fromJson(const {});
      expect(motion.clips, isEmpty);
      expect(motion.autoplay, isNull);
    });

    test('survives the round trip through a scene file', () {
      final document = SceneDocument(
        name: 'Scene',
        entities: [
          SceneEntity(
            id: 'fox',
            name: 'Fox',
            components: {
              SceneComponents.transform: TransformComponent(),
              SceneComponents.motion: const MotionComponent(
                clips: ['clips/walk.oclip', 'clips/run.oclip'],
                autoplay: 'clips/walk.oclip',
              ),
            },
          ),
        ],
      );

      final back = SceneDocument.decode(document.encode()).document;
      final motion = back['fox']![SceneComponents.motion]! as MotionComponent;
      expect(motion.clips, ['clips/walk.oclip', 'clips/run.oclip']);
      expect(motion.autoplay, 'clips/walk.oclip');
    });

    test('writes no autoplay when none is set', () {
      expect(const MotionComponent(clips: ['a.oclip']).toJson(), {
        'clips': ['a.oclip'],
      });
    });
  });
}
