@TestOn('browser')
library;

import 'dart:js_interop';
import 'dart:math' as math;

import 'package:orblit_audio/orblit_audio.dart';
import 'package:orblit_audio_device/src/device_audio_web.dart';
import 'package:test/test.dart';
import 'package:web/web.dart';

import 'support/wav.dart';

const rate = 48000;

/// What a mono clip sounds like on each side at the middle. Web Audio pans
/// with equal power.
final middle = math.sqrt(0.5);

void main() {
  late String tone;
  late double peak;

  setUpAll(() {
    final wav = sineWav();
    peak = wav.peak;
    final blob = Blob(
      [wav.bytes.toJS].toJS,
      BlobPropertyBag(type: 'audio/wav'),
    );
    tone = URL.createObjectURL(blob);
  });

  DeviceAudio make({int frames = rate ~/ 2}) {
    final device = DeviceAudio.offline(frames: frames, sampleRate: rate);
    addTearDown(device.dispose);
    return device;
  }

  test('a clip is heard on both sides at its own level', () async {
    final device = make();
    device.start(1, tone, loop: false, level: const Level());

    final out = await device.render();

    expect(peakOf(out, channel: 0), closeTo(peak * middle, 0.01));
    expect(peakOf(out, channel: 1), closeTo(peak * middle, 0.01));
  });

  test('a clip begins where it is asked to', () async {
    final device = make();
    device.start(1, tone, loop: false, level: const Level(), offset: 0.4);

    final out = await device.render();

    // The clip is half a second long, so from 0.4 it is over by 0.1.
    final head = out.sublist(0, rate ~/ 20 * 2);
    final tail = out.sublist(rate ~/ 5 * 2);
    expect(peakOf(head, channel: 0), greaterThan(0.1));
    expect(peakOf(tail, channel: 0), 0);
  });

  test('gain scales what is heard', () async {
    final device = make();
    device.start(1, tone, loop: false, level: const Level(gain: 0.5));

    final out = await device.render();

    expect(peakOf(out, channel: 0), closeTo(peak * middle / 2, 0.01));
  });

  test('pan moves a clip to one side', () async {
    final device = make();
    device.start(1, tone, loop: false, level: const Level(pan: -1));

    final out = await device.render();

    expect(peakOf(out, channel: 0), closeTo(peak, 0.01));
    expect(peakOf(out, channel: 1), 0);
  });

  test('a higher pitch plays through sooner', () async {
    final device = make();
    device.start(1, tone, loop: false, level: const Level(pitch: 2));

    final out = await device.render();

    // The clip lasts half a second, so at pitch 2 it is over by frame rate/4.
    final tail = out.sublist(rate ~/ 3 * 2);
    expect(peakOf(out, channel: 0), greaterThan(0.1));
    expect(peakOf(tail, channel: 0), 0);
  });

  test('a loop keeps sounding past its length', () async {
    final device = make(frames: rate);
    device.start(1, tone, loop: true, level: const Level());

    final out = await device.render();

    expect(peakOf(out.sublist(rate + rate ~/ 2), channel: 0), greaterThan(0.1));
  });

  test('a stopped voice is silent and no longer playing', () async {
    final device = make();
    device.start(1, tone, loop: true, level: const Level());
    expect(device.isPlaying(1), isTrue);

    device.stop(1);
    final out = await device.render();

    expect(device.isPlaying(1), isFalse);
    expect(peakOf(out, channel: 0), 0);
  });

  test('a voice stopped after its clip loaded is silent', () async {
    final device = make();
    device.start(1, tone, loop: true, level: const Level());
    // shortcut: a fixed wait for a blob fetch, upgrade to a loaded signal if
    // this ever flakes.
    await Future<void>.delayed(const Duration(milliseconds: 300));

    device.stop(1);
    final out = await device.render();

    expect(peakOf(out, channel: 0), 0);
  });

  test('disposing twice is safe', () {
    final device = make();
    device.start(1, tone, loop: true, level: const Level());

    device.dispose();
    device.dispose();

    expect(device.isPlaying(1), isFalse);
  });

  test('a failed load of a stopped voice is not reported', () async {
    final failures = <String>[];
    final device = DeviceAudio.offline(
      frames: rate ~/ 10,
      onError: (clip, reason) => failures.add(clip),
    );
    addTearDown(device.dispose);

    device.start(1, 'blob:nowhere', loop: false, level: const Level());
    device.stop(1);
    await device.render();

    expect(failures, isEmpty);
  });

  test('a clip that cannot load goes to the handler and ends', () async {
    final failures = <String>[];
    final device = DeviceAudio.offline(
      frames: rate ~/ 10,
      onError: (clip, reason) => failures.add(clip),
    );
    addTearDown(device.dispose);

    device.start(1, 'blob:nowhere', loop: false, level: const Level());
    await device.render();

    expect(failures, ['blob:nowhere']);
    expect(device.isPlaying(1), isFalse);
  });
}
