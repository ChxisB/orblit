import 'package:orblit_audio/orblit_audio.dart';
import 'package:test/test.dart';

void main() {
  test('a bus scales a voice by every bus above it', () {
    final mixer = Mixer();
    final music = mixer.add('music')..volume = 0.5;
    final menu = mixer.add('menu', under: music)..volume = 0.5;
    mixer.master.volume = 0.8;

    expect(music.gain, closeTo(0.4, 1e-9));
    expect(menu.gain, closeTo(0.2, 1e-9));
  });

  test('a mute anywhere above silences a bus and an unmute restores it', () {
    final mixer = Mixer();
    final music = mixer.add('music');
    final menu = mixer.add('menu', under: music);

    music.muted = true;
    expect(menu.gain, 0);

    music.muted = false;
    expect(menu.gain, 1);
  });

  test('a bus is found by name, and the master bus is one of them', () {
    final mixer = Mixer();
    final sfx = mixer.add('sfx');

    expect(mixer.bus('sfx'), same(sfx));
    expect(mixer.bus('master'), same(mixer.master));
    expect(() => mixer.bus('nope'), throwsArgumentError);
  });

  test('a name is used once, and a parent must be in the same mixer', () {
    final mixer = Mixer()..add('sfx');
    final stranger = Mixer().add('sfx');

    expect(() => mixer.add('sfx'), throwsArgumentError);
    expect(() => mixer.add('ui', under: stranger), throwsArgumentError);
  });

  test('a volume must be a finite number of zero or more', () {
    final bus = Mixer().add('sfx');

    expect(() => bus.volume = -0.1, throwsArgumentError);
    expect(() => bus.volume = double.nan, throwsArgumentError);
    expect(() => bus.volume = double.infinity, throwsArgumentError);
    bus.volume = 2;
    expect(bus.volume, 2);
  });
}
