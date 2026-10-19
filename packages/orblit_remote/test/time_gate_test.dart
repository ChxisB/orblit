import 'package:orblit_remote/orblit_remote.dart';
import 'package:test/test.dart';

void main() {
  test('a step delta that passes no time is refused', () {
    for (final bad in [0.0, -1.0, double.nan, double.infinity]) {
      expect(() => TimeGate(stepDelta: bad), throwsArgumentError);
    }
  });

  test('passed counts the stepped frames the loop used', () {
    final gate = TimeGate();
    gate.advance(0.5);
    expect(gate.passed, 0);
    gate.step(2).ignore();
    gate.advance(0.5);
    expect(gate.passed, 1);
    gate.pause();
    gate.advance(0.5);
    expect(gate.passed, 1);
  });

  test('a running gate passes the frame delta through', () {
    final gate = TimeGate();
    expect(gate.advance(0.02), 0.02);
    expect(gate.paused, isFalse);
  });

  test('a paused gate passes nothing until it resumes', () {
    final gate = TimeGate()..pause();
    expect(gate.paused, isTrue);
    expect(gate.advance(0.02), 0);
    gate.resume();
    expect(gate.advance(0.02), 0.02);
  });

  test('step passes a fixed delta for each frame, then holds', () async {
    final gate = TimeGate(stepDelta: 0.01);
    var done = false;
    final stepped = gate.step(3).then((_) => done = true);
    expect(gate.paused, isTrue);
    expect(gate.pending, 3);
    expect([gate.advance(0.5), gate.advance(0.5)], [0.01, 0.01]);
    await Future<void>.delayed(Duration.zero);
    expect(done, isFalse);
    expect(gate.advance(0.5), 0.01);
    await stepped;
    expect(done, isTrue);
    expect(gate.pending, 0);
    expect(gate.advance(0.5), 0);
  });

  test('steps asked for twice add up and finish together', () async {
    final gate = TimeGate();
    final first = gate.step(1);
    final second = gate.step(1);
    gate.advance(0.5);
    gate.advance(0.5);
    await Future.wait([first, second]);
    expect(gate.pending, 0);
  });

  test('resume and pause end a step that has frames left', () async {
    final gate = TimeGate();
    final stepped = gate.step(5);
    gate.advance(0.5);
    gate.resume();
    await stepped;
    expect(gate.pending, 0);
    expect(gate.advance(0.5), 0.5);

    final again = gate.step(5);
    gate.pause();
    await again;
    expect(gate.advance(0.5), 0);
  });

  test('a step of no frames is refused', () {
    expect(() => TimeGate().step(0), throwsArgumentError);
    expect(() => TimeGate().step(-1), throwsArgumentError);
  });
}
