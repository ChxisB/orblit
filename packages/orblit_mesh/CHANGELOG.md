# Changelog

## 0.4.0

- **One place writes glTF.** `GltfBuffer` accumulates a document's bytes and
  hands back the buffer views and accessors over them; `glbBytes` and
  `glbChunks` write and read the `.glb` container; `gltfText` writes the open
  form. `toGlb` is built on them, and so is everything that exports a scene.

  The rule the format cares about most is alignment, and it is the rule that
  is easiest to restate slightly wrong in a second exporter. Enforcing it in
  one place is the point; the shorter `toGlb` is a side effect.
- A mesh with nothing in it now exports an empty scene rather than a mesh made
  of nothing. A buffer view of nought bytes and an accessor counting nought
  elements are both errors in the format — nine of them, as the reference
  validator counted it.
- Accessors written for positions carry their own bounds, worked out from the
  positions rather than taken from the caller, and index buffer views now say
  they hold indices. `toGlb` still writes thirty-two bit indices always;
  `GltfBuffer.addIndices`, which is given its numbers rather than choosing a
  width in advance, narrows to the smallest that holds them.
- A model name with an accent in it survives export. The JSON chunk was
  measured in characters and written in UTF-8, so every name outside ASCII
  wrote a chunk length short of the text it described.
- `tool/dump_glb.dart` writes one file per shape, which `tool/check_gltf.sh`
  hands to the Khronos glTF-Validator in CI. Warnings fail it as well as
  errors.

## 0.3.0

- Meshes are indexed with thirty-two bits rather than sixteen. `Triangles.indices`
  is a `Uint32List`, and the glb accessor writes `componentType` 5125. **This is
  a breaking change for anything reading `indices` as a `Uint16List`.**

  Sixteen bits is enough for every mesh anybody authors by hand, and then
  something generates one. An unsigned short wraps at 65,536 and says nothing
  about it: the file writes, a loader reads it, the bounds come back right, and
  the mesh draws nothing, because every triangle past the wrap names the wrong
  corners.

  Always thirty-two now, rather than narrowing when a mesh happens to fit — two
  bytes an index is worth less than a second code path only exercised by small
  meshes, and therefore only ever correct for those.

## 0.2.0

- `boundsOfGltf` reads the size out of a `.gltf`, as `boundsOfGlb` already did
  for the container. The two are the same document with the buffers in
  different places and neither is read — the minimum and maximum are in the
  document itself. A `.gltf` beside its textures is how most model libraries
  publish, so leaving it out meant the commonest kind of imported model was
  the one that could not say how big it was, and was framed, picked and
  outlined as a two-metre cube.

## 0.1.0

- First cut of `orblit_mesh`. Pre-alpha: everything is subject to change.
