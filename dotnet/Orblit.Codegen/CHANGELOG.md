# Changelog

## 0.1.0

- A port of `orblit_codegen` 0.1.0, as a Roslyn incremental source generator.
- `[OrblitComponent]` marks a class or struct. It takes `Name`, `Kind`,
  `Replicated` and `OwnerWritable` as properties, where Dart took
  constructor arguments. The attribute source is added to the compilation, so
  a project needs no reference to this package at run time.
- The generator writes `OrblitComponents.g.cs`: a registry class that
  registers each component against a `World`, a typed `ref struct` column per
  component, `All`, `Replicated` and a static `OwnerWritable` set.
- The manifest is a `const string` named `Manifest` on the registry. A
  generator cannot write `orblit_components.json`. `ComponentEmitter.Manifest`
  writes the same bytes the Dart generator writes for the same components,
  and a fixture of 127 cases checks it.
- A C# `double` field is `float64`. Dart's `double` is `float32`, because
  Dart has one floating type. A `float` field is `float32`.
- A field whose type does not resolve is refused. The Dart scanner could not
  see that case, because it read text.
- A refused declaration is the error `ORBLIT001`, and nothing is written for
  that assembly until it is fixed.
- The source path in the manifest is relative to the project folder and uses
  forward slashes on every system.
- A name with a quote or a backslash is written as a C# string literal. The
  Dart emitter wrote it between single quotes unescaped.
- Components with one name keep their input order. Dart's sort is stable only
  up to 32 items. See decision 75.
- `Microsoft.CodeAnalysis.CSharp` 4.14.0 is the one new package. It loads in
  SDK 9 and 10. See decision 78.
