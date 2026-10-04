# orblit_asset

What an asset is called and where its bytes are: project-relative asset ids,
content hashes, the sources assets are read from, a store that files bytes by
their hash, and the cache that keeps the results of cooking them.

A cook cache is keyed on the whole recipe: the source bytes, the files it
read, which importer ran and which version of it, the settings it resolved,
and the device the result is for. That is what makes a rebuild with nothing
changed cook nothing, and an edit to one texture recook one texture.

Part of [Orblit](https://github.com/ChxisB/orblit), a Dart-first 3D game
engine. The documentation is at [orblitengine.com](https://orblitengine.com).

## Using it

```yaml
dependencies:
  orblit_asset:
    git:
      url: https://github.com/ChxisB/orblit.git
      path: packages/orblit_asset
```

Needs Dart alone. Runs anywhere Dart runs, including a headless CI runner.
The directory-backed classes need `dart:io`. The package still compiles for
the web, but constructing one of them there throws. `MemoryCookCache` works
everywhere.

## Static model collision

`CollisionMesh.fromGltf(id, bytes, source)` reads the active glTF/GLB scene
into immutable indexed triangles. Node transforms and repeated instances are
baked into the model frame; strips and fans become triangles. Set model
import settings to `collision: true` to cook a versioned `collision.json`
sidecar alongside the model, and use `CollisionMesh.decode` to read it.

These are fixed, two-sided surfaces for `orblit_physics.layMesh` or a scene
`BodyComponent` with `BodyShape.mesh`. The physics world cooks its spatial
tree and seams when the geometry is laid. Skins, morph targets, sparse
accessors and compressed geometry are refused; export a static uncompressed
glTF first.

## Status

Pre-alpha. Nothing here is API-stable, and the version is bumped for every
feature. See [VERSIONING.md](../../VERSIONING.md).

## Licence

MPL-2.0, © 2026 Chris Beckett. See [LICENSE](LICENSE), and the repository root
for the third-party notices that apply to builds linking the renderer.
