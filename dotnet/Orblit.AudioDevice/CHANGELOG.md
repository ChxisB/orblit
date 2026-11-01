# Changelog

## 0.2.0

- A port of `orblit_audio_device` 0.2.0. `DeviceAudio` is an `IAudioBackend`
  over the same native miniaudio engine, now built by CMake and shipped
  under `runtimes/<rid>/native`.
- `Start` takes an `offset` and begins that many seconds into the clip. A
  clip that does not loop and is asked to begin past its end starts nothing.
  A loop wraps the offset.
- `DeviceAudio.Offline` mixes with no device and hands the samples back from
  `Render`. `DeviceAudio.Open` throws `AudioDeviceException` when there is no
  output device.
- The browser half of the Dart package is not ported. Web is out of scope for
  the .NET build.
