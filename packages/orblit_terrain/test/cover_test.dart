import 'dart:typed_data';

import 'package:orblit_terrain/orblit_terrain.dart';
import 'package:test/test.dart';

void main() {
  group('Cover', () {
    test('each field sits in its own bits', () {
      expect(Cover.of(base: 31).word, 0xF8000000);
      expect(Cover.of(overlay: 31).word, 0x07C00000);
      expect(Cover.of(blend: 1).word, 0x003FC000);
      expect(Cover.of(angle: 15).word, 0x00003C00);
      expect(Cover.of(scale: -20).word, 0x00000380);
      expect(Cover.of(hole: true).word, 0x4);
      expect(Cover.of(navigation: true).word, 0x2);
      expect(Cover.of(automatic: true).word, 0x1);
    });

    test('reads back what it was made of', () {
      final cover = Cover.of(
        base: 7,
        overlay: 19,
        blend: 0.5,
        angle: 5,
        scale: 40,
        hole: true,
        automatic: true,
      );
      expect(cover.base, 7);
      expect(cover.overlay, 19);
      expect(cover.blendStep, 128);
      expect(cover.blend, closeTo(0.5, 1 / 255));
      expect(cover.angle, 5);
      expect(cover.scale, 40);
      expect(cover.hole, isTrue);
      expect(cover.navigation, isFalse);
      expect(cover.automatic, isTrue);
    });

    test('scale codes run up to +80 then back from −60', () {
      for (final (code, percent) in Cover.scales.indexed) {
        final cover = Cover.of(scale: percent);
        expect(cover.scaleCode, code);
        expect(cover.scale, percent);
      }
      expect(Cover.of(scale: 20).uvMultiplier, closeTo(0.8, 1e-12));
      expect(Cover.of(scale: -20).uvMultiplier, closeTo(1.2, 1e-12));
      expect(Cover.of(scale: 33).scale, 40, reason: 'snaps to the nearest');
      expect(Cover.of(scale: 500).scale, 80, reason: 'clamped');
    });

    test('an angle is sixteenths of a turn', () {
      expect(Cover.of(angle: 4).radians, closeTo(3.14159265 / 2, 1e-6));
    });

    test('out-of-range parts wrap into their own bits', () {
      final cover = Cover.of(base: 33, overlay: 32, angle: 17);
      expect(cover.base, 1);
      expect(cover.overlay, 0);
      expect(cover.angle, 1);
      expect(Cover.of(blend: 3).blendStep, 255);
      expect(Cover.of(blend: -1).blendStep, 0);
    });

    test('a change leaves the other fields alone', () {
      final full = Cover(0xFFFFFFFF);
      expect(full.withBase(0).word, 0x07FFFFFF);
      expect(full.withOverlay(0).word, 0xF83FFFFF);
      expect(full.withBlend(0).word, 0xFFC03FFF);
      expect(full.withAngle(0).word, 0xFFFFC3FF);
      expect(full.withScale(0).word, 0xFFFFFC7F);
      expect(full.withHole(false).word, 0xFFFFFFFB);
      expect(full.withNavigation(false).word, 0xFFFFFFFD);
      expect(full.withAutomatic(false).word, 0xFFFFFFFE);
      expect(Cover.plain.withBase(31).withHole(true).word, 0xF8000004);
    });

    test('stays a thirty-two-bit word through a Uint32List', () {
      final cover = Cover.of(base: 31, overlay: 31, blend: 1, angle: 15);
      expect(cover.word, lessThan(1 << 32));
      expect(cover.word, greaterThanOrEqualTo(0));
      final map = Uint32List(1)..[0] = cover.word;
      expect(Cover(map[0]), cover);
    });

    test('new ground is automatic', () {
      expect(Cover.auto.automatic, isTrue);
      expect(Cover.auto.base, 0);
      expect(Cover.plain.automatic, isFalse);
    });
  });

  group('GroundColour', () {
    test('none is white and leaves roughness alone', () {
      const none = GroundColour.none;
      expect((none.red, none.green, none.blue), (255, 255, 255));
      expect(none.roughnessByte, 128);
      expect(none.roughness, closeTo(0, 0.005));
      expect(GroundColour.of(), none);
    });

    test('roughness runs −1 to +1 across the byte', () {
      expect(GroundColour.of(roughness: -1).roughnessByte, 0);
      expect(GroundColour.of(roughness: 1).roughnessByte, 255);
      expect(GroundColour.of(roughness: 9).roughnessByte, 255);
      expect(GroundColour.of(roughness: 1).roughness, 1);
    });

    test('changes one part at a time', () {
      final colour = GroundColour.of(red: 10, green: 20, blue: 30);
      expect(colour.withRoughness(-1).red, 10);
      expect(colour.withColour(1, 2, 3).roughnessByte, 128);
      expect(GroundColour.bytes(1, 2, 3, 4).rgba, 0x01020304);
    });
  });
}
