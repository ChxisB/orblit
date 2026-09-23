# Assets: materials, scene files, splats and the network

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

`RuntimeImport` runs the same importers over files a user hands the app, with
the results kept in the device's cache.

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
