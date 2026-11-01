# Changelog

## 0.2.0

- A port of `orblit_audio` 0.2.0. `Audio` is `AudioPlayer`, because a
  type named `Audio` in the namespace `Orblit.Audio` is flagged by CA1724.
- `AudioBackend` is `IAudioBackend`. `speedOfSound` is `Acoustics.SpeedOfSound`.
- A voice id is a `long`.
