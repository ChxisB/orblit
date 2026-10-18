@TestOn('vm')
library;

import 'dart:io';
import 'dart:typed_data';

import 'package:orblit_audio/orblit_audio.dart';
import 'package:orblit_audio_device/orblit_audio_device.dart';
import 'package:test/test.dart';

import 'support/wav.dart';

const rate = 48000;

/// Writes a sine to [path] and returns the peak it decodes to.
double writeSine(String path, {double seconds = 0.5, int rate = 48000}) {
  final wav = sineWav(seconds: seconds, rate: rate);
  File(path).writeAsBytesSync(wav.bytes);
  return wav.peak;
}

void main() {
  late Directory dir;
  late double peak;
  late DeviceAudio device;

  setUpAll(() {
    dir = Directory.systemTemp.createTempSync('orblit_audio_device');
    peak = writeSine('${dir.path}/tone.wav');
  });
  tearDownAll(() => dir.deleteSync(recursive: true));

  setUp(() => device = DeviceAudio.offline(directory: dir.path));
  tearDown(() => device.dispose());

  Float32List heard(int frames) => device.render(frames);

  test('a clip is heard on both sides at its own level', () {
    device.start(1, 'tone.wav', loop: false, level: const Level());

    final out = heard(rate ~/ 10);

    expect(out, hasLength(rate ~/ 10 * 2));
    expect(peakOf(out, channel: 0), closeTo(peak, 0.01));
    expect(peakOf(out, channel: 1), closeTo(peak, 0.01));
    expect(device.isPlaying(1), isTrue);
  });

  test('gain scales what is heard', () {
    device.start(1, 'tone.wav', loop: false, level: const Level(gain: 0.5));

    expect(peakOf(heard(rate ~/ 10), channel: 0), closeTo(peak / 2, 0.01));
  });

  test('pan moves a clip to one side', () {
    device.start(1, 'tone.wav', loop: true, level: const Level(pan: -1));
    final left = heard(rate ~/ 10);
    device.update(1, const Level(pan: 1));
    final right = heard(rate ~/ 10);

    expect(peakOf(left, channel: 0), greaterThan(0.2));
    expect(peakOf(left, channel: 1), 0);
    expect(peakOf(right, channel: 0), 0);
    expect(peakOf(right, channel: 1), greaterThan(0.2));
  });

  test('a clip ends when it has played through', () {
    device.start(1, 'tone.wav', loop: false, level: const Level());

    heard(rate ~/ 4);
    expect(device.isPlaying(1), isTrue);
    heard(rate ~/ 2);
    expect(device.isPlaying(1), isFalse);
  });

  test('a higher pitch plays through sooner', () {
    device.start(1, 'tone.wav', loop: false, level: const Level(pitch: 2));

    heard((rate * 0.3).round());

    expect(device.isPlaying(1), isFalse);
  });

  test('a loop keeps playing past its length', () {
    device.start(1, 'tone.wav', loop: true, level: const Level());

    heard(rate * 2);

    expect(device.isPlaying(1), isTrue);
  });

  test('a stopped voice is silent and no longer playing', () {
    device.start(1, 'tone.wav', loop: true, level: const Level());
    heard(rate ~/ 10);

    device.stop(1);

    expect(device.isPlaying(1), isFalse);
    expect(peakOf(heard(rate ~/ 10), channel: 0), 0);
  });

  test('two voices of one clip are mixed', () {
    device.start(1, 'tone.wav', loop: true, level: const Level());
    device.start(2, 'tone.wav', loop: true, level: const Level());

    expect(peakOf(heard(rate ~/ 10), channel: 0), closeTo(peak * 2, 0.02));
  });

  test('a clip at another sample rate keeps its length', () {
    writeSine('${dir.path}/slow.wav', rate: 22050, seconds: 1);
    device.start(1, 'slow.wav', loop: false, level: const Level());

    heard((rate * 0.9).round());
    expect(device.isPlaying(1), isTrue);
    heard((rate * 0.2).round());
    expect(device.isPlaying(1), isFalse);
  });

  test('a file over the stream size is streamed and heard the same', () {
    final streamed = DeviceAudio.offline(directory: dir.path, streamAbove: 0);
    addTearDown(streamed.dispose);
    streamed.start(1, 'tone.wav', loop: false, level: const Level());

    expect(
      peakOf(streamed.render(rate ~/ 10), channel: 0),
      closeTo(peak, 0.01),
    );
  });

  test('an Ogg Vorbis file is decoded', () {
    device = DeviceAudio.offline(directory: 'test/support');
    device.start(1, 'sine.ogg', loop: false, level: const Level());

    expect(peakOf(heard(rate ~/ 10), channel: 0), greaterThan(0.05));
  });

  test('a missing file throws when nothing handles it', () {
    expect(
      () => device.start(1, 'nope.wav', loop: false, level: const Level()),
      throwsA(isA<AudioDeviceException>()),
    );
    expect(device.isPlaying(1), isFalse);
  });

  test('a missing file goes to the handler and the voice never plays', () {
    final failures = <String>[];
    device = DeviceAudio.offline(
      directory: dir.path,
      onError: (clip, reason) => failures.add(clip),
    );

    device.start(1, 'nope.wav', loop: false, level: const Level());

    expect(failures, ['nope.wav']);
    expect(device.isPlaying(1), isFalse);
  });

  test('a file that is not audio is refused', () {
    File('${dir.path}/text.wav').writeAsStringSync('not a sound at all');

    expect(
      () => device.start(1, 'text.wav', loop: false, level: const Level()),
      throwsA(isA<AudioDeviceException>()),
    );
  });

  test('a game drives it through Audio', () {
    final audio = Audio(device);
    final voice = audio.play('tone.wav');

    heard(rate ~/ 10);
    audio.advance(0.1);
    expect(voice.isPlaying, isTrue);
    heard(rate);
    audio.advance(0.1);

    expect(voice.isPlaying, isFalse);
    expect(audio.voices, isEmpty);
  });

  test('closing with voices still playing is safe', () {
    device.start(1, 'tone.wav', loop: true, level: const Level());
    device.start(2, 'tone.wav', loop: true, level: const Level());
    heard(rate ~/ 10);

    device.dispose();
  });

  test('disposing twice is safe and every voice is then over', () {
    device.start(1, 'tone.wav', loop: true, level: const Level());

    device.dispose();
    device.dispose();

    expect(device.isPlaying(1), isFalse);
    expect(
      () => device.start(2, 'tone.wav', loop: false, level: const Level()),
      throwsStateError,
    );
    expect(() => device.render(10), throwsStateError);
  });

  test('an absolute clip path ignores the directory', () {
    device.dispose();
    device = DeviceAudio.offline(directory: 'test/support');

    device.start(1, '${dir.path}/tone.wav', loop: false, level: const Level());

    expect(peakOf(heard(rate ~/ 10), channel: 0), closeTo(peak, 0.01));
  });
}
