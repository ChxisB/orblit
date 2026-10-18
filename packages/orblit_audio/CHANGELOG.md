# Changelog

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
