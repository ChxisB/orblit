# Changelog

## 0.25.0

- A scene can hold a `portrait` and a `landscape` layout, so one scene can
  suit a phone held either way. `SceneLayouts` holds each as a `SceneDiff`
  against the base scene and allows only `SetField`, `SetComponent` and
  `SetVisible`. `SceneDocument.InLayout` returns the scene as it looks in
  one layout. `LayoutShapes.Of` picks the shape for a window: taller than
  wide is portrait and anything else is landscape.
- The format is 6. A scene with layouts is written at 6 and any other scene is
  written at `SceneDocument.PlainVersion`, 5, so a file with no layouts reads
  in older tools as it did. Prefab files and glTF extras write 5.
- `SceneLayouts.CountMissed` counts the edits in a layout that find nothing to
  change, such as one aimed at a part of a folded prefab instance.
- A file whose layout is not a list, or holds edits that cannot be read,
  loads without them and says so in `Problems`.

## 0.24.0

- A port of `orblit_scene` 0.24.0, parts S1 to S4: the components, the
  document, the migrations, the diff, the materials, the prefab instances and
  cell streaming. Part S5a adds the export and part S5b the import.
- `SceneImport.ReadSceneFrom` reads a scene back from the bytes of a glTF,
  GLB or OBJ file and returns a `SceneImported`: the document, what could not
  be carried across, and whether the file held Orblit's own record of the
  scene. It throws `SceneFormatException` only when there is nothing to read.
  A file from elsewhere is worked out from the parts of glTF that have an
  equivalent. A document written by Orblit before version four loses its parts
  and says why.
- `SceneDocument.WriteAs` writes a scene as glTF, GLB or OBJ and returns a
  `SceneWritten`: the files in the order to save them, and what the format
  could not hold. `ToGltf` and `ToObj` give the same output without the file
  names. `SceneFormat` names the three formats. FBX is not a format it writes.
- An export takes the bytes of every model and texture the scene names as a
  dictionary keyed by project path. The package reads no files.
- A model placed by a mesh component is copied into the output under the node
  that places it. A model with its data or pictures in files beside it, or in
  more than one buffer, is refused with a `GraftException` that names the
  file, and the export reports it as a problem.
- A picture is told apart by its first bytes, not its name. PNG, JPEG, WebP and
  KTX2 are embedded.
- Faces are put in material order with a stable sort, so the faces of one
  material keep the order they were made in.
- `CellStreamer`, `CellLoad` and `CellStep` stream a world of scene files
  into one scene as cells come near. `Orblit.Scene` now references
  `Orblit.Cells`. `Step` is called from one thread. A fetch may finish on any
  thread and is put into the scene on a later `Step`. A fetch that is already
  answered finishes inside the `Step` that asked for it, and one that fails at
  once frees its place for the next cell in the same `Step`.
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
