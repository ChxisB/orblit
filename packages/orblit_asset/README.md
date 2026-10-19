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
exits 1 when a byte has changed.

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

## Kits

A kit is a pack for the target `source`. It holds files to copy into a
project and a `kit.json` that says what they are.

```sh
dart run orblit_asset:kit pack crate-pack -o crate-pack.opack
dart run orblit_asset:kit check crate-pack.opack
dart run orblit_asset:kit extract crate-pack.opack path/to/project
```

`extract` checks the kit again before it writes. It stops, with nothing
written, when the project already has a file with other contents, or has a
link or a file where the kit needs a folder. Pass `--overwrite` to replace a
file. A replaced file gets the default permissions. A game never mounts a
kit, because `MountedPacks` refuses a pack for another target.

File names in a kit use letters, digits, spaces and `_.()+-`. `kit pack`
lists every name it refuses.

```dart
final kit = await Kit.open(await AssetPack.open(FilePackBytes('kit.opack')));
final problems = await kit.check();
final clashes = await kit.conflictsIn(DirectoryAssetSource('my_project'));
```

### A store of kits

A store lists kits that people host themselves. Its source is a folder of
entries, `<publisher>/<slug>.json`, each one a list of releases. `kit publish`
writes an entry for you, as described below.

```json
{
  "releases": [
    {
      "version": "1.1.0",
      "url": "https://example.com/crate-pack-1.1.0.opack",
      "hash": "<the hash `kit pack` printed>",
      "bytes": 48213,
      "released": "2026-10-19",
      "notes": "Adds an open crate."
    }
  ]
}
```

```sh
dart run orblit_asset:kit index kits -o site
```

`index` downloads every release and checks it. The hash and the size must
match the file, the kit must pass the same rules as an install, and its
`kit.json` must name the publisher, slug and version of the entry. Any bad
release stops it with every problem listed and writes nothing. The folder it
writes holds `index.json`, a `releases.json` for each kit and the previews of
the latest release. It reads from a plain directory with no network.

### Listing a kit

`kit publish` packs a kit and writes its entry, so the hash and the size are
never copied by hand.

```sh
dart run orblit_asset:kit publish crate-pack \
  --url https://example.com/crate-pack-1.1.0.opack \
  --entries path/to/orblit-kits/kits --notes "Adds an open crate."
```

It packs the folder into `crate-pack-1.1.0.opack`, named after the end of the
url, or by `-o` when the url ends in no plain file name. It opens the file and
checks it as `kit check` does. The release takes its version from `kit.json`,
its hash and size from the file, and its date from today in UTC. With
`--entries`, the release goes into `<publisher>/<slug>.json` in that folder.
Without it, the entry is printed. A version the entry lists already is
refused, so raise `version` in `kit.json`. Nothing is uploaded and no git
command runs. The command prints the steps that are left: upload the file,
run `kit index` over the entries and open a pull request.

`publishKit(folder:, url:, outPath:, released:, notes:, entriesPath:)` does
the same in Dart and returns a `KitPublished` with the pack summary, the
release and the entry.

### Reading a store and installing from it

`KitStoreClient` reads a store over any `AssetTransport`. Its address must
be https. It reads the index, the releases of one kit, a preview and the file
list of a release, each to the size limit of that file and within a timeout.
A reply that is not a 200 throws `KitStoreFailed`.

```dart
final store = KitStoreClient(
  Uri.parse('https://example.com/store/'),
  HttpTransport(),
);
final row = (await store.index()).rows.first;
final latest = (await store.releases(row)).releases.first;
final listed = await store.open(latest.release);
```

`open` asks the server for byte ranges, so the files of the kit are not
downloaded. It is for showing what a kit holds. It reaches the host that
holds the kit file, which is not the store. Do not install from it, because
nothing has checked the bytes against the hash in the index.

To install, download the whole pack with `PackFolder`. It checks the hash and
throws `FetchCorrupt` with both hashes when they differ. Then check the kit
again, as the editor does.

```dart
final pack = await PackFolder('downloads').download(
  latest.release.offer,
  transport: HttpTransport(),
  origin: latest.release.origin,
);
final kit = await Kit.open(pack);
final problems = await kit.check();
```

`extractKit` takes `into`, a folder of the project to write under, and
`only`, the files to take. `kit.conflictsIn(project, into:, only:)` lists the
files the project has with other contents, so a person can choose before
anything is written. A kit file named `kits.lock` is refused at the root of a
project.

```dart
final clashes = await kit.conflictsIn(
  DirectoryAssetSource('my_project'),
  into: 'assets/kits/crates',
);
await extractKit(kit, 'my_project', into: 'assets/kits/crates');
```

`kit.scripts` lists the files that can run. An install never runs one, and a
person should see the list first.

`kits.lock` sits at the root of the project and lists each installed kit: its
publisher, slug, version, pack hash, folder and the hash of every file it
wrote. `KitLock.parse` reads it and `KitLock.adding` replaces the older entry
of a kit. The editor writes it. `extractKit` does not.

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
