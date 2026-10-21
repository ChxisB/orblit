# Changelog

## 0.24.0

- A port of `orblit_scene` 0.24.0, part S1: the components. The document,
  diff, migration, material, prefab, streaming, export and import parts follow.
- `SceneComponents.Read` and `Order` cover the 22 component types that are not
  `data` or `prefab`. An unknown type is kept whole as `UnknownComponent`.
- A Dart enum is a C# enum. `EnumNames.Of` writes the Dart name, which starts
  with a lower-case letter.
- A component is a sealed class with `CopyWith`. `JointGive`, `JointRange` and
  `EmitterBurst` are records, so they compare by value where the Dart classes
  compare by identity.
- `Values.Bits` gives its fallback for a non-finite double, where Dart throws.
  A file cannot hold one.
- A list a component exposes is a read-only wrapper over a copy, and writing
  to it throws `NotSupportedException`, as `List.unmodifiable` does.
- A Dart `assert` is an `ArgumentException` that always runs.
- `BodyComponent` keeps `Size`, `Centre`, `Surface`, `CentreOfMass` and
  `Inertia` as given, and `CopyWith` clones them. Dart does the same.
