import 'package:flutter/painting.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_ui/orblit_ui.dart';

import 'png.dart';

/// The test font draws every glyph as a full square, so a label is as wide as
/// it has letters and every pixel inside it is lit.
const _style = TextStyle(
  fontFamily: 'Ahem',
  fontSize: 10,
  color: Color(0xFFFFFFFF),
);

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  group('a label atlas gives each string a place', () {
    test('two strings get rectangles that do not overlap', () {
      final atlas = LabelAtlas(style: _style, width: 128)
        ..add('Ada')
        ..add('Brannock');

      final a = atlas.rectOf('Ada')!;
      final b = atlas.rectOf('Brannock')!;
      expect(a.size, const Size(30, 10));
      expect(b.size, const Size(80, 10));
      expect(a.overlaps(b), isFalse);
    });

    test('the same string is the same place', () {
      final atlas = LabelAtlas(style: _style)..add('Ada');
      final first = atlas.rectOf('Ada');

      atlas
        ..add('Brannock')
        ..add('Ada');

      expect(atlas.rectOf('Ada'), first);
      expect(atlas.rectOf('never added'), isNull);
    });

    test('rows wrap at the width', () {
      final atlas = LabelAtlas(style: _style, width: 64)
        ..add('aaaa')
        ..add('bbbb');

      expect(atlas.rectOf('bbbb')!.top, greaterThan(atlas.rectOf('aaaa')!.top));
    });

    test('a label wider than the atlas is refused', () {
      final atlas = LabelAtlas(style: _style, width: 32);

      expect(() => atlas.add('far too long'), throwsArgumentError);
    });
  });

  group('a label atlas draws', () {
    testWidgets('the pixels inside a rectangle are the label', (tester) async {
      final atlas = LabelAtlas(style: _style, width: 128)..add('Ada');

      final image = await tester.runAsync(() async {
        return decode(await atlas.paint());
      });

      final a = atlas.rectOf('Ada')!;
      expect(image!.width, 128);
      expect(image.height, atlas.size.height);
      expect(pixel(image, a.left.toInt() + 5, a.top.toInt() + 5), [
        255,
        255,
        255,
        255,
      ]);
      expect(pixel(image, a.right.toInt() + 2, a.top.toInt() + 5)[3], 0);
    });

    testWidgets('a full atlas grows and says so', (tester) async {
      final atlas = LabelAtlas(
        style: _style,
        width: 64,
        initialHeight: 16,
        maxHeight: 128,
      )..add('aaaa');
      await tester.runAsync(atlas.paint);
      final before = (atlas.revision, atlas.size.height);
      final first = atlas.rectOf('aaaa');

      atlas
        ..add('bbbb')
        ..add('cccc')
        ..add('dddd');
      final image = await tester.runAsync(() async {
        return decode(await atlas.paint());
      });

      expect(atlas.size.height, greaterThan(before.$2));
      expect(atlas.revision, greaterThan(before.$1));
      expect(image!.height, atlas.size.height);
      expect(atlas.rectOf('aaaa'), first, reason: 'old places stay put');
    });

    testWidgets('the revision moves only when the picture does', (
      tester,
    ) async {
      final atlas = LabelAtlas(style: _style)..add('Ada');
      expect(atlas.isDirty, isTrue);

      await tester.runAsync(atlas.paint);
      final drawn = atlas.revision;
      expect(atlas.isDirty, isFalse);

      atlas.add('Ada');
      expect(atlas.isDirty, isFalse, reason: 'nothing new to draw');
      await tester.runAsync(atlas.paint);
      expect(atlas.revision, drawn);

      atlas.add('Brannock');
      await tester.runAsync(atlas.paint);
      expect(atlas.revision, drawn + 1);
    });

    test('a full atlas at its limit says so', () {
      final atlas = LabelAtlas(
        style: _style,
        width: 40,
        initialHeight: 16,
        maxHeight: 16,
      )..add('aaa');

      expect(() => atlas.add('bbb'), throwsStateError);
    });

    test(
      'growing stops at the limit and a label that fits there is placed',
      () {
        final atlas = LabelAtlas(
          style: _style,
          width: 40,
          initialHeight: 12,
          maxHeight: 20,
        )..add('aaa');

        expect(atlas.size.height, 20);
      },
    );

    test('a height of nothing is refused', () {
      expect(
        () => LabelAtlas(style: _style, initialHeight: 0),
        throwsRangeError,
      );
    });
  });
}
