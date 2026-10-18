import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_filament/orblit_filament.dart';
import 'package:orblit_stage/orblit_stage.dart';
import 'package:orblit_ui/orblit_ui.dart';
import 'package:vector_math/vector_math_64.dart';

const _style = TextStyle(
  fontFamily: 'Ahem',
  fontSize: 10,
  color: Color(0xFFFFFFFF),
);

String nameOf(MethodCall call) => (call.arguments as Map)['name'] as String;

Nameplate plate(int id, String text, Vector3 at) =>
    Nameplate(id: id, text: text, position: at);

/// The plate with [id] among what was built, as the object and its material.
(OrblitObject, OrblitMaterial) find(
  ({List<OrblitObject> objects, List<OrblitMaterial> materials}) built,
  Nameplates plates,
  int id,
) {
  final key = plates.keyBase + id;
  return (
    built.objects.singleWhere((o) => o.key == key),
    built.materials.singleWhere((m) => m.key == key),
  );
}

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  late List<MethodCall> calls;
  setUp(() {
    calls = [];
    TestDefaultBinaryMessengerBinding.instance.defaultBinaryMessenger
        .setMockMethodCallHandler(
          const MethodChannel('orblit_filament/resources'),
          (call) async {
            calls.add(call);
            return call.method == 'release' ? true : null;
          },
        );
  });

  final front = OrblitCamera(
    position: Vector3(0, 0, 10),
    target: Vector3.zero(),
  );

  Nameplates make({int maxLabels = 256}) => Nameplates(
    atlas: LabelAtlas(style: _style, width: 256),
    fadeStart: 10,
    fadeEnd: 30,
    maxLabels: maxLabels,
  );

  group('a nameplate faces the camera', () {
    testWidgets('its face is square to the view', (tester) async {
      final plates = make();
      await tester.runAsync(() => plates.sync(['Ada']));

      final built = plates.build(front, [plate(1, 'Ada', Vector3(1, 2, 0))]);

      final (object, _) = find(built, plates, 1);
      final face = object.transform.getColumn(2).xyz..normalize();
      final right = object.transform.getColumn(0).xyz..normalize();
      expect(face.x, closeTo(0, 1e-9));
      expect(face.z, closeTo(1, 1e-9));
      expect(right.x, closeTo(1, 1e-9));
      expect(object.transform.getTranslation(), Vector3(1, 2, 0));
    });

    testWidgets('it follows the camera round', (tester) async {
      final plates = make();
      await tester.runAsync(() => plates.sync(['Ada']));
      final side = OrblitCamera(
        position: Vector3(10, 0, 0),
        target: Vector3.zero(),
      );

      final built = plates.build(side, [plate(1, 'Ada', Vector3.zero())]);

      final (object, _) = find(built, plates, 1);
      final face = object.transform.getColumn(2).xyz..normalize();
      expect(face.x, closeTo(1, 1e-9));
      expect(face.z, closeTo(0, 1e-9));
    });

    testWidgets('looking straight down still gives a usable turn', (
      tester,
    ) async {
      final plates = make();
      await tester.runAsync(() => plates.sync(['Ada']));
      final down = OrblitCamera(
        position: Vector3(0, 10, 0),
        target: Vector3.zero(),
      );

      final built = plates.build(down, [plate(1, 'Ada', Vector3.zero())]);

      final (object, _) = find(built, plates, 1);
      expect(object.transform.storage.every((v) => v.isFinite), isTrue);
      final face = object.transform.getColumn(2).xyz..normalize();
      expect(face.y, closeTo(1, 1e-9));
    });
  });

  group('distance', () {
    testWidgets('a plate fades out between the two ranges and is then gone', (
      tester,
    ) async {
      final plates = make();
      await tester.runAsync(() => plates.sync(['Ada']));

      // The camera is ten metres from the origin, so z = 0 is at ten and
      // moving the plate back from it adds to the distance.
      final built = plates.build(front, [
        plate(1, 'Ada', Vector3(0, 0, 5)),
        plate(2, 'Ada', Vector3(0, 0, -10)),
        plate(3, 'Ada', Vector3(0, 0, -21)),
      ]);

      expect(find(built, plates, 1).$2.baseColour.w, 1);
      expect(find(built, plates, 2).$2.baseColour.w, closeTo(0.5, 1e-9));
      expect(
        built.objects.map((o) => o.key),
        isNot(contains(plates.keyBase + 3)),
      );
    });

    testWidgets('past the cap the nearest are kept', (tester) async {
      final plates = make(maxLabels: 2);
      await tester.runAsync(() => plates.sync(['Ada']));

      final built = plates.build(front, [
        plate(1, 'Ada', Vector3(0, 0, -5)),
        plate(2, 'Ada', Vector3(0, 0, 5)),
        plate(3, 'Ada', Vector3(0, 0, 0)),
      ]);

      expect(built.objects.map((o) => o.key - plates.keyBase).toSet(), {2, 3});
    });
  });

  group('the shared picture', () {
    testWidgets('the same text shares one rectangle', (tester) async {
      final plates = make();
      await tester.runAsync(() => plates.sync(['Ada', 'Brannock']));

      final built = plates.build(front, [
        plate(1, 'Ada', Vector3(0, 0, 0)),
        plate(2, 'Ada', Vector3(2, 0, 0)),
        plate(3, 'Brannock', Vector3(4, 0, 0)),
      ]);

      final one = find(built, plates, 1).$2;
      final two = find(built, plates, 2).$2;
      final three = find(built, plates, 3).$2;
      expect(one.tiling, two.tiling);
      expect(one.offset, two.offset);
      expect(one.offset, isNot(three.offset));
      expect(one.baseColourMap!.path, three.baseColourMap!.path);
    });

    testWidgets('a rectangle becomes the right part of the picture', (
      tester,
    ) async {
      final plates = make();
      await tester.runAsync(() => plates.sync(['Ada']));
      final rect = plates.atlas.rectOf('Ada')!;
      final size = plates.atlas.size;

      final built = plates.build(front, [plate(1, 'Ada', Vector3.zero())]);

      final material = find(built, plates, 1).$2;
      expect(material.tiling.x, closeTo(rect.width / size.width, 1e-9));
      expect(material.tiling.y, closeTo(-rect.height / size.height, 1e-9));
      expect(material.offset.x, closeTo(rect.left / size.width, 1e-9));
      expect(material.offset.y, closeTo(rect.bottom / size.height, 1e-9));
    });

    testWidgets('a plate is as big as its text at the pixel size', (
      tester,
    ) async {
      final plates = make();
      await tester.runAsync(() => plates.sync(['Ada']));
      final rect = plates.atlas.rectOf('Ada')!;

      final built = plates.build(front, [plate(1, 'Ada', Vector3.zero())]);

      // The cube spans -1 to 1, so its width is twice the x axis' length.
      final (object, _) = find(built, plates, 1);
      final across = object.transform.getColumn(0).xyz.length * 2;
      expect(across, closeTo(rect.width * plates.metresPerPixel, 1e-9));
    });

    testWidgets('a string the picture lacks is left out until it is added', (
      tester,
    ) async {
      final plates = make();
      await tester.runAsync(() => plates.sync(['Ada']));
      final stranger = plate(2, 'Zed', Vector3.zero());

      expect(plates.build(front, [stranger]).objects, isEmpty);

      await tester.runAsync(() => plates.sync(['Zed']));
      expect(plates.build(front, [stranger]).objects, hasLength(1));
    });
  });

  group('the picture reaches the renderer', () {
    testWidgets('a new string is a new name and the old is released', (
      tester,
    ) async {
      final plates = make();
      await tester.runAsync(() => plates.sync(['Ada']));
      final first = nameOf(calls.single);
      expect(first, startsWith('orblit:resource/panel/nameplates/'));

      await tester.runAsync(() => plates.sync(['Ada', 'Brannock']));

      final provided = calls.where((c) => c.method == 'provide').toList();
      expect(provided, hasLength(2));
      expect(nameOf(provided.last), isNot(first));
      expect(nameOf(calls.singleWhere((c) => c.method == 'release')), first);
    });

    testWidgets('dispose releases the picture', (tester) async {
      final plates = make();
      await tester.runAsync(() => plates.sync(['Ada']));
      final name = nameOf(calls.single);

      await tester.runAsync(plates.dispose);

      expect(nameOf(calls.last), name);
      expect(calls.last.method, 'release');
      expect(
        plates.build(front, [plate(1, 'Ada', Vector3.zero())]).objects,
        isEmpty,
      );
    });

    testWidgets('strings already in the picture send nothing', (tester) async {
      final plates = make();
      await tester.runAsync(() => plates.sync(['Ada']));
      calls.clear();

      await tester.runAsync(() => plates.sync(['Ada']));

      expect(calls, isEmpty);
    });
  });
}
