# orblit_audio

Every sound call a game makes, run without a speaker.

Part of [Orblit](https://github.com/ChxisB/orblit), a Dart-first 3D game
engine. The documentation is at [orblitengine.com](https://orblitengine.com).

## Using it

```yaml
dependencies:
  orblit_audio:
    git:
      url: https://github.com/ChxisB/orblit.git
      path: packages/orblit_audio
```

Needs Dart alone. There is no backend yet, so nothing is audible. Use
`RecordedAudio` to see what a game would play.

```dart
final backend = RecordedAudio();
final audio = Audio(backend);
final music = audio.mixer.add('music')..volume = 0.6;

final theme = audio.play('theme', bus: music, loop: true);
final fire = audio.play(
  'fire',
  loop: true,
  at: Emitter(position: Vector3(4, 0, -2), falloff: Falloff(far: 30)),
);

// Once a frame.
audio.listener.moveTo(camera.position, delta);
audio.advance(delta);

theme.stop(fadeSeconds: 2);
```

## What it does

- **A mixer.** Buses in a tree. A mute or a volume on one scales every voice
  below it.
- **Voices.** Loop, pitch, volume, fade and stop with a fade.
- **Positioned sound.** Falloff by distance, an optional cone, Doppler and
  pan. Velocity comes from the last two positions.
- **A fake.** `RecordedAudio` stands in for a device in every test.

Nothing reaches the backend until `Audio.advance`. A frame of no time is a
valid advance.
