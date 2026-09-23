import 'package:orblit_sequence/orblit_sequence.dart';
import 'package:test/test.dart';
import 'package:vector_math/vector_math_64.dart';

void main() {
  group('a curve', () {
    test('passes through its keys', () {
      final channel = Channel<double>([
        const Key(0, 0.0, hold: Hold.curve),
        const Key(1, 4.0, hold: Hold.curve),
        const Key(3, 2.0),
      ], doubleMixer);
      expect(channel.at(0), 0);
      expect(channel.at(1), closeTo(4, 1e-12));
      expect(channel.at(3), 2);
    });

    test('with named slopes is the cubic they describe', () {
      // Leaving 0 rising at 3 a second and arriving at 1 flat: the Hermite
      // weights for the start slope and the end value, and nothing else.
      final channel = Channel<double>([
        const Key(0, 0.0, hold: Hold.curve, slopeOut: 3),
        const Key(1, 1.0, slopeIn: 0),
      ], doubleMixer);
      for (final t in [0.25, 0.5, 0.75]) {
        final expected =
            3 * (t * t * t - 2 * t * t + t) + (-2 * t * t * t + 3 * t * t);
        expect(channel.at(t), closeTo(expected, 1e-12));
      }
    });

    test('flows through a middle key instead of stopping at it', () {
      final channel = Channel<double>([
        const Key(0, 0.0, hold: Hold.curve),
        const Key(1, 1.0, hold: Hold.curve),
        const Key(2, 2.0),
      ], doubleMixer);
      expect(channel.slopeAt(1), closeTo(1, 1e-12));
      // Either side of the middle key the value is still moving, where a
      // smooth hold would have come to rest there.
      final before = channel.at(0.99);
      final after = channel.at(1.01);
      expect(after - before, closeTo(0.02, 1e-3));
    });

    test('leaves a peak flat, so it never overshoots it', () {
      final channel = Channel<double>([
        const Key(0, 0.0, hold: Hold.curve),
        const Key(1, 5.0, hold: Hold.curve),
        const Key(2, 0.0),
      ], doubleMixer);
      expect(channel.slopeAt(1), 0);
      for (var t = 0.0; t <= 2; t += 0.05) {
        expect(channel.at(t), lessThanOrEqualTo(5 + 1e-9));
      }
    });

    test('starts and finishes at rest', () {
      final channel = Channel<double>([
        const Key(0, 0.0, hold: Hold.curve),
        const Key(1, 1.0),
      ], doubleMixer);
      expect(channel.slopeAt(0), 0);
      expect(channel.slopeAt(1), 0);
      // At rest at both ends, the curve is a smoothstep.
      expect(channel.at(0.3), closeTo(3 * 0.09 - 2 * 0.027, 1e-12));
    });

    test('flattens one axis of a vector without stopping the others', () {
      final channel = Channel<Vector3>([
        Key(0, Vector3(0, 0, 0), hold: Hold.curve),
        Key(1, Vector3(1, 3, 0), hold: Hold.curve),
        Key(2, Vector3(2, 0, 0)),
      ], vector3Mixer);
      final slope = channel.slopeAt(1);
      expect(slope.x, closeTo(1, 1e-12), reason: 'still running forwards');
      expect(slope.y, 0, reason: 'the top of the jump');
    });

    test('keeps a rotation a rotation', () {
      final a = Quaternion.axisAngle(Vector3(0, 1, 0), 0);
      final b = Quaternion.axisAngle(Vector3(0, 1, 0), 1);
      final c = Quaternion.axisAngle(Vector3(0, 1, 0), 2);
      final channel = Channel<Quaternion>([
        Key(0, a, hold: Hold.curve),
        Key(1, b, hold: Hold.curve),
        Key(2, c),
      ], quaternionMixer);
      for (var t = 0.0; t <= 2; t += 0.1) {
        expect(channel.at(t).length, closeTo(1, 1e-9));
      }
      expect(channel.at(1).w, closeTo(b.w, 1e-9));
    });

    test('goes the short way round a rotation written the other way', () {
      final a = Quaternion.axisAngle(Vector3(0, 1, 0), 0.1);
      final b = Quaternion.axisAngle(Vector3(0, 1, 0), -0.1);
      final negated = Quaternion(-b.x, -b.y, -b.z, -b.w);
      final channel = Channel<Quaternion>([
        Key(0, a, hold: Hold.curve),
        Key(1, negated),
      ], quaternionMixer);
      final middle = channel.at(0.5);
      // Halfway between a small turn one way and a small turn the other is no
      // turn at all, not a half turn.
      expect(middle.w.abs(), closeTo(1, 1e-6));
    });

    test('moves a flag the way a line does', () {
      final channel = Channel<bool>([
        const Key(0, false, hold: Hold.curve),
        const Key(1, true),
      ], boolMixer);
      expect(channel.at(0.4), isFalse);
      expect(channel.at(0.6), isTrue);
      expect(() => channel.slopeAt(0), throwsStateError);
    });
  });
}
