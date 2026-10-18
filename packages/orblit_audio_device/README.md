# orblit_audio_device

Plays an [`orblit_audio`](../orblit_audio) game through the output device.

```dart
final audio = Audio(DeviceAudio.open(directory: 'assets/sound'));

audio.play('door.wav', at: Emitter(position: Vector3(3, 0, 1)));
audio.play('theme.ogg', loop: true, bus: audio.mixer.add('music'));

// Once a frame.
audio.advance(dt);
```

On a device a clip is a path to a WAV, FLAC, MP3 or Ogg Vorbis file. A file
over `streamAbove` bytes is read as it plays, and `streamAbove` is 1 MiB by
default. A smaller one is decoded once and shared by every voice playing it.

In a browser a clip is a URL, under `directory` when you give one. It is
fetched and decoded on first use over Web Audio, in whatever formats the
browser plays. A browser keeps sound off until the page has had a click or a
key, so start the first sound from one.

`DeviceAudio.open` throws `AudioDeviceException` when there is no output
device. Catch it and play with `RecordedAudio` to run silent. A clip that
cannot start throws the same exception, or goes to `onError` when you give
one, and the voice never plays. In a browser the load is asynchronous. A voice
counts as playing from `start`, silent until its clip has loaded, and a failed
load reaches `onError` or the zone as an uncaught error and ends the voice.

Pan is not the same on both. A device leaves a mono clip at full level on each
side in the middle and lowers only the far side as you pan. Web Audio pans with
equal power, so the middle is 0.71 on each side. A hard pan is full level on
one side in both.

## Testing without a speaker

`DeviceAudio.offline()` mixes real files with no device. On a device,
`render(frames)` returns interleaved stereo floats, so a test reads every
sample. In a browser, `DeviceAudio.offline(frames: n)` fixes the length and
`render()` mixes it once.

## What is not here

- A cache with a budget. A decoded effect is freed when its last voice ends,
  and decoded again at the next play. In a browser every clip stays decoded,
  music included, because there is no streaming path.
- A decode off the game thread. An effect is decoded where it is played, so a
  large one can hitch the first frame. Lower `streamAbove` to stream it.
- Smoothing of level changes on a device. A fade or a moving emitter changes
  gain, pan and pitch each frame, and a big jump in one frame can click. The
  browser glides over about 15 ms.
- Prompt release of a clip that ended. Its slot is freed when the next voice
  starts.
- One `render` for both. On a device it is synchronous and takes a frame count
  each call. In a browser it is a one-shot future over the length fixed when
  the engine was made.
- Non-ASCII file names on Windows.
- Recovery from a device that is unplugged. The game goes on and plays
  nothing until the device is back.

miniaudio and stb_vorbis are vendored in `third_party/`. Both are public
domain or MIT. Their text is in the repository's `LICENSES/`.
