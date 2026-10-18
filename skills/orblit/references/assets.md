# Assets: materials, scene files, splats, packs and the network

Everything here is plain Dart in `orblit_scene` and `orblit_asset` — no
Flutter, no filesystem, no renderer. That is deliberate: the editor, a
command-line cook step and a browser all have to agree about what a material
and a scene *are*, and only one of those three has a disk.

Check names against the source before writing against them. Orblit is
pre-alpha. `packages/orblit_scene/lib/orblit_scene.dart` and
`packages/orblit_asset/lib/orblit_asset.dart` are the two barrels that say
what is public.

---

## Material files (`.omat`)

A material is a small JSON document that states only what *it* sets:

```json
{
  "parent": "materials/metal.omat",
  "group": "rust",
  "values": {"roughness": 0.4, "baseColour": [0.7, 0.2, 0.1, 1]},
  "maps": {"normal": "textures/brick_n.png"}
}
```

`MaterialDocument.fromJson(json)` returns a `MaterialLoad` with `document` and
`problems` — a material with one unreadable parameter still loads, because the
alternative is losing a wall because somebody typed `roughness: "half"`.

`MaterialLibrary` holds them by project path and spends the inheritance:

```dart
final library = MaterialLibrary(materials: {...}, groups: {...});
final resolved = library.resolve('materials/brick.omat');
resolved.values['roughness'];  // or resolved.problems, if a parent is missing
```

Order of resolution is **eldest ancestor first, then each descendant over the
top, then the group**. The group wins on purpose: a group exists to override a
whole set of materials from outside, and one that lost to every material that
had bothered to state a value could override almost nothing.

- `put` and `putGroup` clear the *whole* resolved cache, not one entry —
  anything that named the changed material as a parent resolved through it.
- A chain that loops is reported in `problems`, not thrown. The material still
  draws, in the colours it states itself.
- A parameter nothing in the chain set comes back null, so the renderer's own
  default stands in. A material that says nothing looks the same whether it
  resolved through ten ancestors or none.

**Parameters** (`MaterialFields.all`, and `MaterialFields.has(name)` to test
one): `shading` (lit/unlit/video/shadowCatcher), `blend`
(opaque/transparent/fade/masked/add), `culling` (back/front/none),
`doubleSided`, `baseColour`, `metallic`, `roughness`, `reflectance`,
`clearCoat`, `clearCoatRoughness`, `anisotropy`, `sheenColour`,
`sheenRoughness`, `emissive`, `emissiveIntensity`, `ambientOcclusion`,
`normalScale`, `tiling`, `offset`, `wrap` (repeat/clamp/mirror), `filter`
(smooth/sharp), `maskThreshold`, `depthWrite`, `depthBias`, `screenMapped`,
`litScreen` (a video or feed screen that lights shade, opaque only),
`blendMode`, `blendAmount`, `blendSharpness`, `blendTiling`.

**Textures** live in their own `maps` object, never in `values`:
`baseColour`, `normal`, `metallicRoughness`, `occlusion`, `emissive`,
`blendBaseColour`, `blendMask`. Anything that needs a material's dependencies
— the importer, the bundler — reads that one key and is done.

### Looks

`MaterialComponent(asset: ..., looks: {...})` is per entity:

```dart
MaterialComponent(
  asset: 'materials/brick.omat',
  looks: {'winter': 'materials/brick_snow.omat'},
)
```

The same shape `KHR_materials_variants` uses, and for the same reason: the
names live once for the whole scene and each object says only what it swaps
to. An object with nothing to say about a look keeps `asset` — so a "winter"
look is authored by naming the dozen things that change, not the four hundred
that do not.

A look is **not** a scene setting, because it is not a fact about the scene.
It is how a viewer is asking to see it, so it is stated when staging:

```dart
final view = OrblitDocumentView(document, materials: library, look: 'winter');
view.look = null;  // back to what each object wears by default
```

Setting `look` rebuilds rather than diffs — it is a scene-wide swap.

### Shaders you write (`.oshader`)

A surface the `.omat` table cannot make is one short text file. The cook
translates it to a Filament material and runs `matc`, so the cook machine
needs `matc` (`ORBLIT_MATC`, or on `PATH`). A running game reads only the
cooked package.

```glsl
shader_type spatial;
render_mode unshaded;

instance uniform vec4 tint : source_color = vec4(1.0, 0.5, 0.2, 1.0);
instance uniform float speed : hint_range(0.0, 10.0, 0.1) = 2.0;

void fragment() {
  ALBEDO = tint.rgb * (0.6 + 0.4 * sin(TIME * speed));
  ALPHA = tint.a;
}
```

- Types: `bool`, `int`, `float`, `vec2`, `vec3`, `vec4`, `sampler2D`. A
  picture cannot be an `instance uniform`.
- Hints the editor acts on: `source_color` (a colour row) and
  `hint_range(min, max[, step])` (a slider). The other known hints are
  kept in `params.json` and nothing reads them yet.
- Render modes: `blend_mix`, `blend_add`, `blend_mul`, `blend_premul_alpha`,
  `cull_back`, `cull_front`, `cull_disabled`, `depth_draw_opaque`,
  `depth_draw_always`, `depth_draw_never`, `depth_test_default`,
  `depth_test_disabled`, `unshaded`. No blend mode means opaque.
- `fragment()` writes `ALBEDO`, `ALPHA`, `METALLIC`, `ROUGHNESS`, `AO`,
  `EMISSION`, `NORMAL_MAP` and reads `UV` and `TIME`. `TIME` is the
  renderer's clock, not the scene's.
- `#include "x.oshaderinc"` only. A cycle names both files.
- Refused with the line: `vertex()`, `light()`, `varying`, another
  `shader_type`, any other render mode or hint.

An entity wears one through its material, with values of its own:

```dart
MaterialComponent(
  asset: 'assets/pulse.oshader',
  values: {'speed': 4, 'tint': [0.2, 1.0, 0.4, 1.0]},
)
```

A value is a number, a flag, one to four numbers, or a picture path. A name
the shader does not declare, or a value of the wrong width, is dropped.
Entities that state nothing share one material. Stage the cooked shaders with
`view.surfaces = {'assets/pulse.oshader': CookedSurface.fromParams(package,
jsonDecode(paramsJson))}`; the package and `params.json` are
`<bundle>/<id>.material.filamat` and `<bundle>/<id>.params.json`.

Errors read `file:line: message` at the author's line, from the translator or
from `matc`. Only macOS draws a surface so far. Guide: `/guides/shaders/`.

---

## Scene files (`.oscene`)

`SceneDocument.decode(text)` returns a `SceneLoad` with `document` and
`problems`; `encode()` goes the other way. A component this build has never
heard of survives both as an `UnknownComponent`, which is what lets two editor
versions share a project.

### Prefab instances

An instance is saved as a link and a diff, never as a copy. The entity carries
`PrefabComponent(asset: 'props/lamp.oprefab', overrides: diff)`, where the
diff addresses the prefab's own ids. Open instances before drawing or editing,
and fold them before saving:

```dart
PrefabDocument? prefabAt(String asset) {
  final file = File('$projectRoot/$asset');
  if (!file.existsSync()) return null; // the instance stays folded
  return PrefabDocument.decode(file.readAsStringSync()).prefab;
}

final load = expandInstances(SceneDocument.decode(text).document, prefabAt);
final view = OrblitDocumentView(load.document);
// …edit…
final saved = foldInstances(view.document, prefabAt).encode();
```

- **The id is the path.** An open instance's parts are entities with ids like
  `street1/lamp3/bulb`: one id per document crossed, joined by `/`
  (`EntityPath`). The instance root keeps its own id. `/` is reserved in ids.
- A parent, a selection or an animation track names a part by its path, like
  any other id. Something hung off a part that is not the prefab's own stays in
  the scene and keeps its path parent.
- `makePrefab`, `applyInstance`, `revertInstance`, `unpackInstance` and
  `refreshInstances` are the editor's operations. Each returns what was renamed
  or opened, so a selection can follow.
- A prefab that cannot be read leaves its instance folded, exactly as saved.
  A version-four scene's stamped copies are relinked on expand, with their
  edits kept as overrides.

### Writing a scene out

```dart
final written = document.writeAs(
  SceneFormat.glb,          // or .gltf, or .obj
  name: 'old town',
  materials: library,       // without it, a material is its mesh's colour
  files: {'models/tree.glb': bytes},  // whatever the scene points at
);
written.first;              // the file to name the export after
written.files.skip(1);      // its sidecars, in the order to save them
written.problems;           // what the format could not hold. Empty is normal.
```

- `.gltf` is JSON with the bytes beside it — the one for version control.
  `.glb` is one file — the one to ship. `.obj` is world-space corners and
  faces with a `.mtl`; everything reads it and it keeps almost nothing.
- **Bytes, not paths.** This package has no filesystem and will not get one.
  Only the caller knows whether it is in the editor, a cook step or a browser.
- Problems are collected, not thrown. An export that silently drops half a
  scene is worse than one that says so.

### Reading a scene back

```dart
final read = readSceneFrom(bytes, files: {'old town.bin': sidecar});
read.document;         // a SceneDocument
read.wasWrittenHere;   // true if Orblit wrote this glTF
read.problems;         // what could not be carried across
```

The container is sniffed from the bytes, not the extension — a `.gltf` that is
really a GLB is an ordinary thing to be handed.

A scene Orblit wrote comes back **as itself**: same ids, same order, same
parents, same components, `encode()` for `encode()`. The exporter writes a
full record into `extras.orblit` on every node, so importing one is a read
rather than a re-derivation.

A glTF from anywhere else is *interpreted* and says so (`wasWrittenHere` is
false): nodes become entities, a matrix or TRS becomes a transform, punctual
lights come back at the power they left at, triangles become geometry that can
still be edited. An orthographic camera, a primitive that is not triangles or
an accessor reaching past its buffer lands in `problems`.

Only bytes that are neither a GLB nor a glTF document throw
`SceneFormatException`. A scene with one unreadable node is still worth
opening.

---

## Splats

```dart
SplatsComponent(asset: 'captures/street.osplat', budget: 400000)
```

`asset` is a `.ply`, a `.spz` or a cooked `.osplat`. `budget` is the most
splats to draw with, or null for as many as fit. `harmonics` is how many bands
of spherical harmonics to keep, or null for the file's own.

---

## Cooked assets

`orblit_asset` turns source files into what a device can load. `GltfImporter`
handles models; `"atlas": true` in a model's import settings packs its small
textures together, remaps the UVs, and merges the materials and static
primitives that then match. It is a setting rather than a second importer, and
the cook key includes it, so turning it on re-cooks.

`TerrainImporter` carries `.oterrain` and `.oregion` files into the bundle as
written, after checking each region against its name and its terrain's size
and each image, model and material the terrain names against the project. Read
a cooked terrain back with `CookedTerrain.open` and stream it with
`TerrainStreamer` ([packages.md](packages.md#orblit_terrain)).

`RuntimeImport` runs the same importers over files a user hands the app, with
the results kept in the device's cache.

---

## Packs

A cooked bundle is a folder. A pack is the same bundle as one `.opack` file,
for a game that ships, downloads or mounts it as a unit.

```
dart run orblit_asset:pack build build/assets/android --target android -o game.opack
dart run orblit_asset:pack list game.opack
dart run orblit_asset:pack check game.opack
```

```dart
final pack = await AssetPack.open(FilePackBytes('game.opack'));
final source = LayeredAssetSource([DirectoryAssetSource('assets'), pack]);
```

- **`AssetPack` is an `AssetSource`.** It layers like any other. Open reads
  the 32-byte header and the directory at the end. It never reads a blob.
- **`FilePackBytes` takes a path**, not a `File`, so the web build still
  compiles. `start` and `bytes` read a pack that sits inside another file.
  `MemoryPackBytes` is for a pack that arrived as bytes. `close()` a
  `FilePackBytes` when the pack is done with.
- **`read` does not hash.** `findCorrupt()` hashes every blob and returns the
  ids that changed. Run it after a download, not on every read.
- **The target is stored.** A pack is for one cook target, because the
  textures in it are. `pack.target` says which.
- **Same bytes, one blob.** Two assets with identical bytes share a blob, and
  repacking the same bundle gives the same file.
- **Refusals are `FormatException`.** A cut-short file, a wrong marker, a
  newer format and blobs that overlap each throw, with the reason in the
  message. A pack is never half-read.

### Patches and mods

```
dart run orblit_asset:pack build build/assets/android --target android \
  --base game.opack --base patch-1.opack -o patch-2.opack
```

```dart
final packs = MountedPacks(target: CookTargets.android)
  ..start(game)      // a pack with no base
  ..patch(patch1)    // built against game
  ..patch(patch2)    // built against patch1
  ..addMod(mod);     // no base, above the whole chain
```

- **A patch holds what changed.** Held when the hash differs from what the
  chain serves, or the chain lacks the id. Dropped ids go in `removed`.
  Compared by hash, so no base blob is read.
- **`--base` goes once per pack, in mounting order**, the whole pack first.
  The patch names the last one by hash.
- **Three methods, not a flag.** `start` refuses a pack with a base. `patch`
  refuses one with none, or one built against something else. `addMod`
  refuses one with a base. A refusal is `PackMountRefused`.
- **A mod is above the chain whatever the mount order.** A patch mounted
  after a mod does not undo it. Later mods are above earlier ones.
- **A mod cannot remove.** `removed` needs a base, so only a patch hides an
  id. A pack above can add the id back.
- **Nothing unmounts.** Switching a mod off takes a restart.
- `chainHash` is what the next patch must name as its base. `manifest` is
  the merged view, removals and mods spent.

### Shipping one in an app

```dart
// hook/build.dart
await packDuringBuild(
  packageRoot: input.packageRoot.toFilePath(),
  targetOs: input.config.code.targetOS.name,
  dependencies: output.addDependencies,
);
```

```yaml
flutter:
  assets:
    - path: assets/cooked/game.macos.opack
      platforms: [macos]
    - path: assets/cooked/game.android.opack
      platforms: [android]
```

```dart
Future<PackBytes> bundledPack() async {
  final key = 'assets/cooked/${packFileName(currentCookTarget)}';
  final onDisk = bundledPackBytes(key);
  if (onDisk != null) return onDisk;
  final inApk = await OrblitAssetFiles.of(key);
  if (inApk != null) {
    return FilePackBytes(inApk.path, start: inApk.offset, bytes: inApk.length);
  }
  final data = await rootBundle.load(key);
  return MemoryPackBytes(data.buffer.asUint8List());
}
```

- **The target is in the file name.** `packFileName(target)` is
  `game.macos.opack`. A hook is skipped when nothing it watched changed, so
  one fixed name would ship the last platform's pack. Each `pubspec.yaml`
  entry carries `platforms:`, so a build bundles its own and no other.
- **`bundledPackBytes` is for desktop and iOS.** It finds the file beside
  the executable and returns null on Android, the web and the Dart VM.
- **Android reads the pack inside the APK.** `OrblitAssetFiles.of` in
  `orblit_filament` gives a path, an offset and a length. The asset has to
  be stored uncompressed: `androidResources { noCompress += "opack" }`. A
  compressed one is copied to the cache and logged.
- **`packDuringBuild` takes `cookDuringBuild`'s arguments.** It reports the
  assets folder and the settings files that exist, so it runs again for a
  new asset and not for an unchanged build.
- **The web has no file.** Read it in parts, as the next section says.

### On the web

```dart
final store = await CacheStorageContentStore.open();
final pack = await AssetPack.open(
  RangePackBytes(HttpTransport(), Uri.base.resolve('game.web.opack')),
  cache: store,
);
```

- **`RangePackBytes` asks for a range at a time.** The header, then the
  directory, then one range for each asset read. The first answer says how
  long the pack is, so `length()` is not a second request.
- **The host has to answer `Range` with `206`.** A host that sends the whole
  file is refused with `PackRangeFailed`, and the body is not read. Serve
  `.opack` as `application/octet-stream` with no content encoding. A range
  of an encoded reply counts the encoded bytes.
- **Another origin has to expose `Content-Range`.** Send
  `Access-Control-Expose-Headers: Content-Range` with the pack, or the page
  cannot read the length and `PackRangeFailed` names the header.
- **`cache:` is keyed by hash.** A blob is checked against the directory's
  hash before it is kept. A rebuilt pack fetches the directory and only the
  blobs whose hash changed. Without a store every read is a request.
- **A pack replaced mid-session is refused.** An answer that gives a
  different length than the first one throws `PackRangeFailed`. Open the
  pack again.
- **A small pack is cheaper whole.** Read `length()`, and when it is under
  the app's threshold take `read(0, length)` into `MemoryPackBytes`. A range
  that covers the file may come back as a `200`, which is accepted.
- **No retries.** A failed request throws. Wrap the transport to retry.

### Exporting from the editor

Scene > Export (or the palette's Export) cooks the project for each target
chosen and packs the result into `exports/` beside the project.

- **A whole pack is `game.<target>.opack`.** That is the name an app ships
  and `packFileName` returns. A new whole pack sets the one it replaces
  aside as `game.<target>.<hash12>.opack`, so a patch built on it still
  finds its base.
- **A patch is `game.<target>.<hash12>.opack`.** It is built on a pack
  picked from the folder, for that pack's target and no other. The name is
  its own hash, so a patch never overwrites the one it stands on.
- **A chain is found by hash.** Each patch names its base by directory hash
  and the folder is searched for it. File names and dates do not matter. A
  base that is missing is `ExportChainBroken` and names the hash.
- **Nothing that changed is not written.** A whole pack identical to the
  last one, and a patch that holds nothing and removes nothing, report
  `unchanged`.
- **A failed cook packs nothing for the target.** The assets and the
  reasons are listed, and the last good pack is left as it was. The next
  target still runs.
- **Each target is its own isolate.** A result is shown as it lands.
  `runExport` is the whole run, `exportTarget` is one target and never
  throws.
- **`PackSummary.added` and `replaced` split `assets`.** For a patch, `added`
  is what the base did not serve and `replaced` is what it served with other
  bytes. A pack with no base has every asset added. `MountedPacks.ownerOf(id)`
  says which pack answers for an id.
- **Contents reads directories only.** The list of what an export serves,
  and which pack each asset comes from, reads no blob.
- **Not run live** with a real texture encoder, or with the native tools of a
  target other than the machine's own.

### Downloaded packs

```dart
final folder = PackFolder(patchesPath);

final pack = await folder.download(
  PackOffer(patchUrl, patchHash),
  transport: HttpTransport(),
  origin: AssetOrigin(Uri.parse('https://cdn.example.com/')),
  onProgress: (p) => print('${p.received} of ${p.total}'),
);

// At start-up, whatever the listing order:
final listing = await folder.list();
final chain = PatchChain.of(shipped, listing.packs);
chain.ordered.forEach(packs.patch);
chain.standalone.forEach(packs.addMod);
```

- **`PackOffer` is a URL and the directory hash.** That is what
  `pack build` prints and what a patch names as its base. The folder files
  the pack as `<hash>.opack`.
- **A download resumes from the file.** Bytes go to `<hash>.part` and are
  kept when a reply ends short, in this run or after a crash. A retry asks
  for the missing bytes only.
- **The pack is checked as a pack.** It has to hash to the offer and every
  blob has to match the directory. A failure throws `FetchCorrupt` or
  `FormatException` and leaves nothing a mount would take.
- **A server that cannot resume is handled.** A `200` restarts the file. A
  `206` from another byte, or a part that is the start of a different file,
  drops the part and fetches whole.
- **Retries cover a dropped connection and 5xx, 408, 425 and 429.** A 404, a
  refused host, a wrong pack and a full disk throw at once. `attempts`
  counts all tries, five by default.
- **`FetchPolicy.maxBytes` is 256 MiB.** A larger pack is refused from its
  declared length, or from the bytes as they arrive. Set it in the origin's
  policy.
- **`PatchChain.of(shipped, found)` orders by `base`.** `ordered` follows
  each patch from the shipped pack's hash, `standalone` has no base, and
  `skipped` says why for a missing base, a fork, another target or an older
  pack.
- **`list()` does not throw for one bad file.** `unreadable` has the path
  and a reason. A file whose contents do not hash to its name is reported
  and not opened. `unfinished` lists the downloads to resume.
- **Remove before mounting.** `folder.remove(hash)` deletes the pack and
  its part. A mounted pack holds its file open.
- **A mods folder is not a `PackFolder`.** Open those files with
  `FilePackBytes` and `AssetPack.open`, whatever they are called.
- **Not `AssetFetcher`.** That keeps bytes in memory and files them by
  hash, which is right for a texture and wrong for a pack. A web build
  reads a patch with `RangePackBytes` and has no `PackFolder`.

---

## Network assets

```dart
final origin = AssetOrigin(
  Uri.parse('https://cdn.example.com/assets/'),
  policy: const FetchPolicy(hosts: {'images.example.com'}),
);
final fetcher = AssetFetcher(origin: origin, transport: HttpTransport());
final source = NetworkAssetSource(fetcher);
```

- **The policy is checked before any connection.** `hosts` is matched exactly
  and case-insensitively — `cdn.example.com` does not allow
  `evil.cdn.example.com`, because a suffix match is how an allow-list becomes
  an allow-anything. `allowInsecure` is off; a texture over plain HTTP is
  bytes fed to a decoder that anyone in between can replace. `maxBytes` and
  `maxPixels` stop a download while it is still arriving, not after.
- **A 404 becomes `AssetNotFound`**, which is what makes a network source work
  as a layer in `LayeredAssetSource`. Every other failure passes through
  untouched: "the server has not got it" and "the server could not be reached"
  must not lead to the same place, or an outage quietly serves something old.
- **ETags.** The stored tag goes out as `If-None-Match`. A 304 whose bytes
  have gone forgets the record and refetches rather than being told 304 again.
- **`MapTransport`** answers from a map, for tests and offline demos. It
  understands ETags, ranges, chunking (`cut`), a connection that drops partway
  (`stopAfter`) and a queue of `Fault`s.

### Progressive loading

```dart
await for (final stage in fetcher.fetchInStages(id, standIn: lowResId)) {
  if (stage.isFinal) { ... } else { ... }   // draw something now
}
```

Up to three stages: a stand-in asset if one is named and the real one is slow
(`standInAfter`), then — for a KTX2 with a mip chain — the texture's **own**
coarse levels built from the front of the file while the rest is still
arriving, then the whole thing. The middle stage costs no extra request and no
extra byte, because KTX2 stores mip levels smallest-first, so a prefix of the
file is a complete set of small levels. It supersedes the stand-in and is
never followed by one. A texture with no mip chain simply never produces it.

---

## Traps

- **`TransformComponent(position: ...)` on its own resets rotation and
  scale.** Carry the fields you are not changing.
- **Material maps go in `maps`, not `values`.** A texture path in `values` is
  reported as "no parameter called ...".
- **`MaterialLibrary.put` clears the whole resolved cache**, so building a
  library by repeated `put` inside a loop that also resolves is quadratic.
  Construct it with its maps, then resolve.
- **A grafted model's nodes are not entities.** When a scene names
  `models/tree.glb`, the export copies that model in whole under the node that
  draws it. On import, only nodes carrying `extras.orblit` become entities —
  anything else doubles the outliner on every round trip.
- **Interleaved buffer views.** Anything reading glTF accessors by hand must
  honour `byteStride`. Reading a strided view as packed gives positions that
  are *wrong* rather than geometry that is missing, which is far harder to
  notice.
- **`.obj` keeps almost nothing** — no tree, no lights, no cameras, no
  animation. Check `SceneWritten.problems` and show them to the user rather
  than letting an export look successful.
