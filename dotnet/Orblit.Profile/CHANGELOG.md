# Changelog

## 0.1.0

- Port of `orblit_profile` 0.1.0. `Profiler` is an abstract class with
  `Profiler.Create(...)` and `Profiler.Off`, because C# has no constructors on
  an interface. A snapshot is an `IReadOnlyDictionary<string, object?>` that
  `System.Text.Json` writes.
