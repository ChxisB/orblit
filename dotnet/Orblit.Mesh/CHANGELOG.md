# Changelog

## 0.6.0

- A port of `orblit_mesh` 0.6.0.
- A vertex index or a material read from JSON is held in 32 bits. A value outside that range is clamped.
- A set of faces or of edges is an `OrderedSet<T>`, which keeps
  insertion order. A mesh edge is the tuple `MeshEdge`
  (`global using`), smaller index first.
- A NaN written into a binary STL, PLY or glb is `0x7FC00000`, the positive
  quiet NaN. .NET makes a negative one from arithmetic, and a file should not
  change with the CPU that made the number.
- A sort of more than 32 items with ties is stable. See decision 75.
- `ReadLimits.LinesOf` splits text one way, so every
  reader counts a line once.
- The extensions on `Mesh` are C# 14 extension blocks in static classes:
  `MeshEdits`, `MeshActions`, `MeshCut`, `MeshHandles`, `MeshShell` and
  `MeshUvs`. A member that does not read the mesh is a plain static on the
  same class: `MeshEdits.VerticesOf`, `MeshEdits.EdgesOf`,
  `MeshActions.Shrink`, `MeshActions.EdgeOf`, `MeshUvs.ReleaseUvs` and
  `MeshUvs.NudgeUvs`.
- The free drawing functions are static members of
  `Drawing`.
- `OnFace` and its three cases are records.
- `Shape` is a record, so `with` replaces `copyWith`.
- The binary readers take `byte[]` and a generic index overload.
- Text output escapes characters above U+007F.
