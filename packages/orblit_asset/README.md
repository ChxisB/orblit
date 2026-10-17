# orblit_asset

What an asset is called and where its bytes are: project-relative asset ids,
content hashes, the sources assets are read from, a store that files bytes by
their hash, the cache that keeps the results of cooking them, and the pack
file that holds a cooked bundle as one file.

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

## Packs

`dart run orblit_asset:pack build <bundle> --target <name> -o game.opack`
writes a cooked bundle as one file. `AssetPack.open` reads it without
reading the blobs, and a pack is an `AssetSource`.

```dart
final pack = await AssetPack.open(FilePackBytes('game.opack'));
final bytes = await pack.read(AssetId.parse('models/robot.glb'));
```

`pack list` shows what a file holds. `pack check` hashes every blob and
exits 1 when a byte has changed. `pack diff old.opack new.opack` lists what
was added, removed and changed by name, and exits 1 when a document in the
new pack still names an asset it dropped. Give it the packs a patch stands
on with `--base`.

`pack build ... --base game.opack` writes a patch that holds only what
changed. `MountedPacks` mounts a game, its patches and any mods as one
source:

```dart
final packs = MountedPacks(target: CookTargets.android)
  ..start(game)
  ..patch(patch)
  ..addMod(mod);
```

A build hook packs the project for the platform it builds, and the app
opens the pack in place:

```dart
await packDuringBuild(
  packageRoot: input.packageRoot.toFilePath(),
  targetOs: input.config.code.targetOS.name,
  dependencies: output.addDependencies,
);
```

The file is `game.<target>.opack`, which `packFileName` spells. List each
name in `pubspec.yaml` with a `platforms:` entry. `bundledPackBytes` finds it
beside the executable on macOS, iOS, Linux and Windows. On Android,
`OrblitAssetFiles` in `orblit_filament` says where it sits in the APK.

On the web the pack stays on the server. `RangePackBytes` asks for the
header and the directory and then a range per asset, and a store keeps each
blob under its hash:

```dart
final store = await CacheStorageContentStore.open();
final pack = await AssetPack.open(
  RangePackBytes(HttpTransport(), Uri.base.resolve('game.web.opack')),
  cache: store,
);
```

The host has to answer `Range` with `206`. `PackRangeFailed` says so when
it does not. A pack on another origin also needs a
`Access-Control-Expose-Headers: Content-Range` header, or the page cannot
read how long the pack is.

A patch the game fetches after install goes in a `PackFolder`. The download
resumes from the bytes it holds, is checked as a pack, and is filed under
its hash only when it is whole. At start-up `PatchChain` puts what is on disk
in mounting order, whatever order the listing gave:

```dart
final folder = PackFolder(patchesPath);
final pack = await folder.download(
  PackOffer(Uri.parse('https://cdn.example.com/patch-2.opack'), patchHash),
  transport: HttpTransport(),
  origin: AssetOrigin(Uri.parse('https://cdn.example.com/')),
);

final chain = PatchChain.of(shipped, (await folder.list()).packs);
chain.ordered.forEach(packs.patch);
chain.standalone.forEach(packs.addMod);
```

A pack over `FetchPolicy.maxBytes` (256 MiB) is refused, so raise it in the
origin's policy for a large patch.

The guide is at
[orblitengine.com](https://orblitengine.com/docs/guides/asset-packs/).

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
