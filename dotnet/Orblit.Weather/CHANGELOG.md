# Changelog

## 0.1.0

- A port of `orblit_weather` 0.1.0.
- `DayCycle.At` gives the sky at an hour. `CameraExposure.ForIlluminance`
  meters it. `WeatherState` holds one weather and `Lerp` crosses between two.
  `Strike.At` works lightning out from the clock.
- An enum with a `label` has a `Label()` extension.
- `WeatherState`, `SkyState`, `CameraExposure` and `Strike` are records.
  `with` replaces `copyWith`, and records compare by value.
- `WeatherState.Presets` is a frozen dictionary.
- `Strike.None` is one shared object, and its `Direction` is
  mutable.
