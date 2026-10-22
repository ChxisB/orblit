# Changelog

## 0.24.0

- A port of `orblit_scene` 0.24.0, parts S1 to S3: the components, the
  document, the migrations, the diff, the materials and the prefab instances.
  The streaming, export and import parts follow.
- `SceneComponents.Read` and `Order` cover the 22 component types that are not
  `data` or `prefab`. An unknown type is kept whole as `UnknownComponent`.
- `EnumNames.Of` writes the name a scene file uses, which starts with a
  lower-case letter.
- A component is a sealed class with `CopyWith`. `JointGive`, `JointRange` and
  `EmitterBurst` are records, so they compare by value.
- `Values.Bits` gives its fallback for a non-finite double.
  A file cannot hold one.
- A list a component exposes is a read-only wrapper over a copy, and writing
  to it throws `NotSupportedException`, as `List.unmodifiable` does.
- A broken rule is an `ArgumentException` that always runs.
- `BodyComponent` keeps `Size`, `Centre`, `Surface`, `CentreOfMass` and
  `Inertia` as given, and `CopyWith` clones them.
- `SceneDocument.Decode` and `Encode` read and write the `.oscene` file at
  format version 5. A file with no version, a newer version or no entity list throws
  `SceneFormatException`. Anything else that cannot be read is dropped and
  named in `SceneLoad.Problems`.
- `SceneMigrations` carries a file forward from every older version, and
  `Entity` does the same for one object, as a clipboard or a prefab needs. A
  step builds a new map and leaves its input alone.
- `SceneDiff.Between` gives the operations that turn one document into
  another. `ApplyTo` and `Inverse` run them forward and back, and `FromJson`
  reads them from the form an undo stack stores.
- `DataComponent` and `PrefabComponent` complete the 24 component types.
- The text after "This is not a scene file:" comes from `Orblit.Json`, so it
  differs from the fixture's message. The note for an object with no kind in a
  version-three file reads "has no kind this editor can read". Neither is
  compared in the fixture.
- The seeded run and property tests use .NET `Random`.
- `MaterialDocument` reads and writes an `.omat` file. A value of the wrong
  kind, an unknown parameter and an unknown map slot are named in
  `MaterialLoad.Problems` and dropped. `MaterialLibrary.Resolve` lays a
  material over its parents, eldest first, and then its group. A loop, a
  missing parent and a missing group each end the chain and say so.
- `PrefabDocument` reads and writes an `.oprefab` file. A file whose named
  root is missing falls back to the first entity with nothing above it.
- `Instances` expands, folds, makes, applies, reverts, refreshes and unpacks
  instances of prefabs. The parts of an instance take ids that are paths, for
  example `lamp1/bulb`. Prefabs inside prefabs open all the way down. A copy
  stamped in before instances were links is relinked and keeps what was
  changed about it. A prefab that cannot be read, or that contains itself,
  leaves its instances folded and is named in `SceneLoad.Problems`.
- `MaterialComponent.ToJson` writes `looks`. Part S1 read them back from a
  file but never wrote them.
- Fold and refresh skip an open instance that is itself a part. Revert and
  unpack accept one, and apply refuses it with its own message.
