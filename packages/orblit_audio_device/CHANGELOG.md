# Changelog

## 0.1.0

- First cut of `orblit_audio_device`. Pre-alpha: everything is subject to
  change.
- `DeviceAudio` is an `AudioBackend` over miniaudio, compiled by a build hook
  from a vendored single header. It decodes WAV, FLAC, MP3 and Ogg Vorbis and
  streams files over a size you set.
- `DeviceAudio.offline` mixes with no device and hands the samples back from
  `render`, which is how every test here runs.
- With no output device `DeviceAudio.open` throws. miniaudio's null device is
  compiled out, so a silent fallback cannot hide a missing speaker.
- In a browser the same class plays over Web Audio. A clip is a URL,
  fetched and decoded on first use. The offline form renders once.
- Heard on no device yet. The device half is built and tested on macOS only.
  The browser half is tested in Chrome only, on rendered samples.
