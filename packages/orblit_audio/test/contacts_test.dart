import 'package:orblit_audio/orblit_audio.dart';
import 'package:test/test.dart';

void main() {
  late RecordedAudio backend;
  late Audio audio;

  setUp(() {
    backend = RecordedAudio();
    audio = Audio(backend);
  });

  ContactSounds make({int limit = 16}) => ContactSounds(
    audio,
    rules: const [
      ContactRule('wood', 'stone', 'wood_on_stone'),
      ContactRule('glass', null, 'glass_any'),
      ContactRule('wood', null, 'wood_any'),
    ],
    quiet: 1,
    loud: 5,
    limit: limit,
  );

  String clipOf(int index) => (backend.calls[index] as AudioStarted).clip;
  double gainOf(int index) => (backend.calls[index] as AudioStarted).level.gain;

  test('a pair rule matches in either order', () {
    final sounds = make();

    sounds.hit('wood', 'stone', strength: 3);
    sounds.hit('stone', 'wood', strength: 3);

    expect([clipOf(0), clipOf(1)], ['wood_on_stone', 'wood_on_stone']);
  });

  test('a rule for two surfaces beats a rule for one', () {
    make().hit('wood', 'stone', strength: 3);

    expect(clipOf(0), 'wood_on_stone');
  });

  test('a rule for one surface covers the rest', () {
    final sounds = make();

    sounds.hit('wood', 'grass', strength: 3);
    sounds.hit('grass', 'glass', strength: 3);

    expect([clipOf(0), clipOf(1)], ['wood_any', 'glass_any']);
  });

  test('volume climbs from quiet to loud and stops there', () {
    final sounds = make();

    sounds.hit('wood', 'grass', strength: 3);
    sounds.hit('wood', 'grass', strength: 50);

    expect(gainOf(0), closeTo(0.5, 1e-9));
    expect(gainOf(1), 1);
  });

  test('a touch below quiet, or a pair nothing covers, is silent', () {
    final sounds = make();

    sounds.hit('wood', 'grass', strength: 0.9);
    sounds.hit('rubber', 'grass', strength: 9);

    expect(backend.calls, isEmpty);
  });

  test('no more than the limit play at once, and a freed slot is reused', () {
    final sounds = make(limit: 2);
    for (var i = 0; i < 5; i++) {
      sounds.hit('wood', 'grass', strength: 3);
    }
    expect(backend.calls, hasLength(2));

    backend.finish(0);
    audio.advance(0);
    sounds.hit('wood', 'grass', strength: 3);

    expect(backend.calls, hasLength(3));
  });

  test('the levels have to make sense', () {
    expect(
      () => ContactSounds(audio, rules: const [], quiet: 5, loud: 5),
      throwsArgumentError,
    );
  });
}
