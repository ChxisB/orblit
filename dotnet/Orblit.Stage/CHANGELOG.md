# Changelog

## 0.1.0

- A port of the first part of `orblit_stage` 0.18.0: `DebugLines`,
  `MaterialView`, `TerrainView`, `ScatterView`, `CookedSurface` and
  `StaticMerge`. The document view, the skin binding and the particle, sound
  and cutscene views follow in later versions.
- `CookedParam` is the declaration a cook wrote for one uniform. It has the
  Dart name `SurfaceParam` there. The name changed so it does not meet
  `Orblit.Asset.SurfaceParam` in a project that uses both.
- `CookedSurface.FromParams` reads decoded JSON: a list of maps, with
  `long`, `double` and `bool` values. A hint argument that is not text is kept
  as its JSON text.
- A hint range is read with the invariant culture and `NumberStyles.Float`.
  Dart's parser also takes a hex number. This one does not.
- `StaticMerge` takes a `StaticBaker` and an options record. A merge is
  started from `Observe` and lands on a later task, so `OnReady` never runs
  inside `Observe`. A merge that is disposed releases every file it provided.
- An error thrown by `StaticMergeOptions.OnReady` goes unobserved and the
  merge stands. Dart sends it to the zone. A .NET task has no zone to send it
  to.
- A colour that is NaN is never merged, as in Dart.
- `ScatterViewOptions.Mesh` and `Material` run once for each group. Dart runs
  them once for each member. The answers are the same when the resolvers are
  pure.
- A scatter group with a million models or more throws `ArgumentException`.
  The limit has no test, because the group constructor is internal.
- `StaticMergeOptions.CellSize` refuses a value that is nought or less with
  `ArgumentOutOfRangeException`. Dart's check lets NaN through. A NaN cell
  size is not tested here.
