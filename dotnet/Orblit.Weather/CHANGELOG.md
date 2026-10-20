# Changelog

## 0.1.0

- A port of `orblit_weather` 0.1.0.
- `DayCycle.At` gives the sky at an hour. `CameraExposure.ForIlluminance`
  meters it. `WeatherState` holds one weather and `Lerp` crosses between two.
  `Strike.At` works lightning out from the clock.
- A Dart enum with a `label` is a C# enum with a `Label()` extension.
- `WeatherState`, `SkyState`, `CameraExposure` and `Strike` are records.
  `with` replaces `copyWith`, and records compare by value where the Dart
  classes compare by identity.
- `WeatherState.Presets` is a frozen dictionary.
- `Strike.None` is one shared object, as in Dart, and its `Direction` is
  mutable.
