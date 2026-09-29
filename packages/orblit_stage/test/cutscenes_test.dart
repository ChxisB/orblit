import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_filament/orblit_filament.dart';
import 'package:orblit_motion/orblit_motion.dart';
import 'package:orblit_scene/orblit_scene.dart';
import 'package:orblit_stage/orblit_stage.dart';
import 'package:vector_math/vector_math_64.dart';

SceneEntity camera(
  String id, {
  required Vector3 at,
  Vector3? turned,
  double fieldOfView = 50,
  bool visible = true,
}) => SceneEntity(
  id: id,
  name: id,
  visible: visible,
  components: {
    SceneComponents.transform: TransformComponent(
      position: at,
      rotation: turned,
    ),
    SceneComponents.camera: CameraComponent(fieldOfView: fieldOfView),
  },
);

/// The game's camera, two more for a cutscene to cut between, and a cube.
SceneDocument stage() => SceneDocument(
  entities: [
    camera('game', at: Vector3(0, 0, 10)),
    camera('front', at: Vector3(0, 0, 5), fieldOfView: 40, visible: false),
    camera(
      'side',
      at: Vector3(5, 0, 0),
      turned: Vector3(0, 90, 0),
      fieldOfView: 60,
      visible: false,
    ),
    SceneEntity(
      id: 'cube',
      name: 'cube',
      components: {
        SceneComponents.transform: TransformComponent(),
        SceneComponents.mesh: const MeshComponent(),
      },
    ),
  ],
);

/// The cube slides four metres, seen from the front and then the side, with
/// the two blended for a second between.
CutsceneDocument intro({WhenDone whenDone = WhenDone.hold}) => CutsceneDocument(
  motion: ClipDocument(
    name: 'Intro',
    duration: 4,
    whenDone: whenDone,
    channels: [
      ClipChannel<Vector3>(
        target: 'cube',
        property: 'transform.position',
        kind: ChannelKind.vector,
        keys: [
          Key(0, Vector3.zero(), hold: Hold.linear),
          Key(4, Vector3(4, 0, 0)),
        ],
      ),
    ],
    marks: const [Mark(3, 'boom')],
  ),
  shots: [
    CutsceneShot(camera: 'front', start: 0, duration: 3),
    CutsceneShot(camera: 'side', start: 2, duration: 2),
  ],
  sounds: [CutsceneSound(sound: 'swell.ogg', start: 0, duration: 4)],
);

Vector3 cubeAt(OrblitDocumentView view) =>
    (view.document['cube']![SceneComponents.transform]! as TransformComponent)
        .position;

Vector3 facingOf(OrblitCamera camera) =>
    (camera.target - camera.position).normalized();

Matcher near(Vector3 wanted) => predicate<Vector3>(
  (got) => got.distanceTo(wanted) < 1e-6,
  'within 1e-6 of $wanted',
);

void main() {
  group('a view', () {
    test("gives a camera entity's view, shown or not", () {
      final view = OrblitDocumentView(stage());
      final side = view.cameraOf('side')!;
      expect(side.position, near(Vector3(5, 0, 0)));
      expect(facingOf(side), near(Vector3(-1, 0, 0)));
      expect(side.fieldOfView, 60);
      expect(view.cameraOf('cube'), isNull);
      expect(view.cameraOf('nobody'), isNull);
    });

    test('looks through another camera until given its own back', () {
      final view = OrblitDocumentView(stage());
      expect(view.scene.camera.position, near(Vector3(0, 0, 10)));
      view.through = view.cameraOf('front');
      expect(view.scene.camera.position, near(Vector3(0, 0, 5)));
      view.through = null;
      expect(view.scene.camera.position, near(Vector3(0, 0, 10)));
    });
  });

  group('blending cameras', () {
    final front = OrblitCamera(
      position: Vector3(0, 0, 5),
      target: Vector3.zero(),
      fieldOfView: 40,
    );
    final side = OrblitCamera(
      position: Vector3(5, 0, 0),
      target: Vector3.zero(),
      fieldOfView: 60,
      aperture: 2,
    );

    test('with none, or none with a say, is nothing', () {
      expect(blendCameras(const []), isNull);
      expect(blendCameras([(front, 0)]), isNull);
    });

    test('with one is that one', () {
      expect(blendCameras([(front, 0.3), (side, 0)]), same(front));
    });

    test('stands between them and looks between them', () {
      final between = blendCameras([(front, 0.5), (side, 0.5)])!;
      expect(between.position, near(Vector3(2.5, 0, 2.5)));
      expect(facingOf(between), near(Vector3(-1, 0, -1).normalized()));
      expect(between.fieldOfView, closeTo(50, 1e-9));
    });

    test('takes the rest of the lens from the heaviest', () {
      expect(blendCameras([(front, 0.25), (side, 0.75)])!.aperture, 2);
      expect(blendCameras([(front, 0.75), (side, 0.25)])!.aperture, 16);
    });

    test("looks the first one's way when two look exactly opposite", () {
      final back = OrblitCamera(
        position: Vector3(0, 0, -5),
        target: Vector3.zero(),
      );
      expect(
        facingOf(blendCameras([(front, 0.5), (back, 0.5)])!),
        near(Vector3(0, 0, -1)),
      );
    });
  });

  group('a cutscene', () {
    test('looks through its first shot and keys the scene when started', () {
      final view = OrblitDocumentView(stage());
      final cutscenes = OrblitCutscenes(view, [intro()])..start('Intro');
      expect(cutscenes.playing, 'Intro');
      expect(view.scene.camera.position, near(Vector3(0, 0, 5)));
      expect(cubeAt(view), near(Vector3.zero()));
    });

    test('moves the cube, blends its shots, and hands the view back', () {
      final view = OrblitDocumentView(stage());
      final cutscenes = OrblitCutscenes(view, [intro()])..start('Intro');

      final first = cutscenes.advance(1);
      expect(cubeAt(view), near(Vector3(1, 0, 0)));
      expect(view.scene.camera.position, near(Vector3(0, 0, 5)));
      expect(first.sounds.single.sound, 'swell.ogg');
      expect(first.ended, isFalse);

      cutscenes.advance(1.5);
      expect(view.scene.camera.position, near(Vector3(2.5, 0, 2.5)));

      final last = cutscenes.advance(2);
      expect(last.marks.map((mark) => mark.name), ['boom']);
      expect(last.ended, isTrue);
      expect(cutscenes.playing, isNull);
      expect(view.through, isNull);
      expect(view.scene.camera.position, near(Vector3(0, 0, 10)));
    });

    test('that holds leaves the scene where it put it', () {
      final view = OrblitDocumentView(stage());
      OrblitCutscenes(view, [intro()])
        ..start('Intro')
        ..advance(5);
      expect(cubeAt(view), near(Vector3(4, 0, 0)));
    });

    test('that releases puts back what it moved', () {
      final view = OrblitDocumentView(stage());
      OrblitCutscenes(view, [intro(whenDone: WhenDone.release)])
        ..start('Intro')
        ..advance(5);
      expect(cubeAt(view), near(Vector3.zero()));
    });

    test('stopped ends where it is and hands the view back', () {
      final view = OrblitDocumentView(stage());
      final cutscenes = OrblitCutscenes(view, [intro()])
        ..start('Intro')
        ..advance(2)
        ..stop();
      expect(cutscenes.playing, isNull);
      expect(cubeAt(view), near(Vector3(2, 0, 0)));
      expect(view.scene.camera.position, near(Vector3(0, 0, 10)));
      expect(cutscenes.advance(1).ended, isFalse);
    });

    test('starts from a mark named after it', () {
      final cutscenes = OrblitCutscenes(OrblitDocumentView(stage()), [intro()])
        ..startFrom(const [Mark(1, 'footstep')]);
      expect(cutscenes.playing, isNull);
      cutscenes.startFrom(const [Mark(1, 'footstep'), Mark(1, 'Intro')]);
      expect(cutscenes.playing, 'Intro');
    });

    test('is refused when there is no such cutscene', () {
      final cutscenes = OrblitCutscenes(OrblitDocumentView(stage()), [intro()]);
      expect(() => cutscenes.start('Outro'), throwsArgumentError);
      expect(cutscenes.has('Intro'), isTrue);
      expect(cutscenes.has('Outro'), isFalse);
    });

    test('needs names that differ, or one could never be started', () {
      expect(
        () => OrblitCutscenes(OrblitDocumentView(stage()), [intro(), intro()]),
        throwsArgumentError,
      );
    });
  });
}
