# Changelog

## 0.2.0

- `Audio.play` takes an `offset`, the seconds into the clip to begin at. The
  `AudioBackend.start` contract has the same optional parameter.
  `RecordedAudio` writes it down in `AudioStarted.offset`.
- Add `CueBoard`. It turns what a sequence says should be sounding into
  starts, stops and gains, and starts a clip once. After a seek, a bed
  starts part-way in and a one-shot starts nothing.
- Add `Music`, `MusicTrack` and `MusicMove`. One track loops at a time. A
  table of moves picks the change, which can wait for the next beat or bar
  and fade between tracks. A stinger plays over the top and turns the track
  down while it lasts.
- Add `ContactSounds` and `ContactRule`. Two surface names pick a clip, and
  the strength of the touch sets the volume. At most `limit` play at once.

## 0.1.1

- `Audio.play` drops a voice whose backend throws from `start`. Before, the
  voice stayed in the list as playing.

## 0.1.0

- First cut of `orblit_audio`. Pre-alpha: everything is subject to change.
- `Audio` takes the calls a game makes. A backend is given a final `Level`
  for each voice and plays it. `RecordedAudio` is a backend that plays
  nothing and writes every call down.
- A `Mixer` with a tree of `Bus`es. Each has a volume and a mute, and its
  `gain` is the product of every bus above it.
- A `Voice` can loop, change pitch and volume, fade, and stop with a fade.
- A `Listener` and an `Emitter` track velocity from their last two
  positions, and `Audio.advance` clears it after each frame. A positioned
  voice has a `Falloff`, an optional `Cone` and a Doppler amount, and is
  panned by direction, easing to the middle inside `near`.
- `RecordedAudio` throws `StateError` for a call the backend contract rules
  out, such as a stop of a voice that is not playing.
- No backend, so nothing is audible.
