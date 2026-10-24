# Changelog

## 0.4.0

- `ParticleView`, `ParticleSheet` and `TrailRibbon`, a port of `particle_view`,
  `particle_sheet` and `trail_strip` from `orblit_stage`. The view runs the
  emitters of a document and hands out one population for each. The sheet is
  the cloth glb with a joint for each particle. The ribbon is a sheet two
  particles wide that turns its face to the eye.
- The population arrays are copies. Dart hands out views over its own buffers,
  so a later frame could change a population already handed out. A copy cannot.
- `TrailRibbon` is the Dart `OrblitTrailStrip`, renamed so it does not sit
  beside `TrailStrip` from `Orblit.Particles`. A ribbon is white unless a
  colour is given. A sheet is grey.
- `Poses` returns `JointPose` values, the type `SkinBinding` already uses.
- The messages of `ArgumentException` and of `ParticleView.Rejected` are .NET
  text. They name the parameter in the .NET way, so the case of the first letter
  differs from Dart.
- A burst count or a maximum particle count past the `int` range is clamped
  first. The `Burst` and `Emitter` constructors then refuse it or cap it.
- `ParticleView` does not expose its key or its budget.
- The fixture `particles.json` has 12 cases: 3 runs of a view, 4 sheets and 5
  ribbons. It compares every population key, revision, transform and colour,
  the whole glb as bytes, and every pose.

## 0.3.0

- `DocumentView`, a port of `document_view` from `orblit_stage`. It stages a
  scene document as a render scene and moves only what a diff touched.
  `DocumentStaging` holds the pure steps: lights, sprites, splats, sky, fog and
  rain.
- `Materials=`, `Surfaces=` and `Look=` are the methods `SetMaterials`,
  `SetSurfaces` and `SetLook`. A property that does work is a method here.
- `Looks` is a list in document order with no repeats. Dart returns a `Set`.
- A splat budget outside 1 to 0xFFFFFF is refused by `SplatCloud` in every
  build. Dart only asserts in debug. A budget past the `int` range is clamped
  first so it cannot wrap round to a valid one.
- The incremental tests draw from `System.Random`, so a seed makes a different
  document than the same seed does in Dart. The fixture carries its documents
  as text, so the parity checks do not depend on the generator.
- The fixture `document_view.json` has 96 cases: first staging, one
  replacement and a run of ten. It compares every key the view handed out, so
  the order keys are allocated in matches Dart.

## 0.2.0

- `SkinRig` and `SkinBinding`, a port of `skin_binding` from `orblit_stage`.
  `SkinRig.ArmatureOf` makes an armature from a model's skin. `SkinBinding`
  turns a pose, or a clip frame, into the joint transforms the renderer takes.
- `SkinRig.BoneNamesOf` and `SkinRig.ArmatureOf` are the Dart top level
  functions `boneNamesOfSkin` and `armatureOfSkin`.
- A pose that was never evaluated throws `ArmatureException`, and its message
  names `Pose.Evaluate`. A pose of another armature throws
  `ArgumentException`.
- The order of joints is found by walking up to the first joint already
  placed, not by recursion. A skin with a very long chain of joints does not
  overflow the stack. The order is the same as Dart's.

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
