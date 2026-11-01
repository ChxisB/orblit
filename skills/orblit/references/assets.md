# Assets: materials, scene files, splats, packs and the network

Everything here is plain C# in `Orblit.Scene`, `Orblit.Asset` and `Orblit.Mesh`.
None of it needs a renderer, and `Orblit.Scene` touches no filesystem. That is
deliberate. The editor, a command-line cook step and a game all have to agree
about what a material and a scene *are*, and only some of those have a disk.

Names were checked against `Orblit.Scene` 0.24.0, `Orblit.Asset` 0.25.0,
`Orblit.Mesh` 0.6.0 and `Orblit.Stage` 0.5.0. Orblit is pre-alpha, so check a
name against the source before writing against it. The `///` comments in
`dotnet/Orblit.Scene/*.cs` and `dotnet/Orblit.Asset/*.cs` are the reference.
The fences below were compiled, and none was run against real project files
unless the text says so.

The CLIs are .NET tools. In the engine checkout run them as
`dotnet run --project dotnet/Orblit.Pack -- <arguments>`, and likewise
`Orblit.Kit` and `Orblit.Cook`. Installed as tools they are `orblit-pack`,
`orblit-kit` and `orblit-cook`.

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

`MaterialDocument.FromJson(json)` returns a `MaterialLoad` with `Document` and
`Problems`. A material with one unreadable parameter still loads, because the
alternative is losing a wall because somebody typed `roughness: "half"`. `json`
is the decoded file as an `IReadOnlyDictionary<string, object?>`. `JsonText.Decode`
in `Orblit.Json` returns one for a JSON object.

`MaterialLibrary` holds them by project path and spends the inheritance:

```csharp
using Orblit.Scene;

var library = new MaterialLibrary(materials, groups);
var resolved = library.Resolve("materials/brick.omat");
var roughness = resolved.Number("roughness"); // or resolved.Problems, if a parent is missing
```

`materials` and `groups` are `IReadOnlyDictionary<string, MaterialDocument>`.

Order of resolution is **eldest ancestor first, then each descendant over the
top, then the group**. The group wins on purpose. A group exists to override a
whole set of materials from outside, and one that lost to every material that
had bothered to state a value could override almost nothing.

- `Put` and `PutGroup` clear the *whole* resolved cache, not one entry.
  Anything that named the changed material as a parent resolved through it.
- A chain that loops is reported in `Problems`, not thrown. The material still
  draws, in the colours it states itself.
- A parameter nothing in the chain set comes back null, so the renderer's own
  default stands in. A material that says nothing looks the same whether it
  resolved through ten ancestors or none.
- `ResolvedMaterial` reads as `Number(name)`, `Flag(name)`, `Numbers(name)`
  and `Choice(name)`, each null when unset. `Values` and `Maps` are the whole
  tables.

**Parameters** (`MaterialFields.All`, and `MaterialFields.Has(name)` to test
one): `shading` (lit/unlit/video/shadowCatcher), `blend`
(opaque/transparent/fade/masked/add), `culling` (back/front/none),
`doubleSided`, `baseColour`, `metallic`, `roughness`, `reflectance`,
`clearCoat`, `clearCoatRoughness`, `anisotropy`, `sheenColour`,
`sheenRoughness`, `emissive`, `emissiveIntensity`, `ambientOcclusion`,
`normalScale`, `tiling`, `offset`, `wrap` (repeat/clamp/mirror), `filter`
(smooth/sharp), `maskThreshold`, `depthWrite`, `depthBias`, `screenMapped`,
`litScreen` (a video or feed screen that lights shade, opaque only),
`blendMode` (none/linear/masked/maskedDepth), `blendAmount`, `blendSharpness`,
`blendTiling`, `blendOffset`, and the foliage wind trio `windBearing`,
`windSpeed` and `windStrength`.

**Textures** live in their own `maps` object, never in `values`:
`baseColour`, `normal`, `metallicRoughness`, `occlusion`, `emissive`,
`blendBaseColour`, `blendMask` (`MaterialFields.Maps`). Anything that needs a
material's dependencies, such as the importer or the bundler, reads that one
key and is done.

### Looks

`MaterialComponent` is per entity:

```csharp
using Orblit.Scene;

var brick = new MaterialComponent(
    asset: "materials/brick.omat",
    looks: new Dictionary<string, string> { ["winter"] = "materials/brick_snow.omat" });
```

It is the same shape `KHR_materials_variants` uses, and for the same reason.
The names live once for the whole scene and each object says only what it
swaps to. An object with nothing to say about a look keeps `Asset`, so a
"winter" look is authored by naming the dozen things that change, not the four
hundred that do not.

A look is **not** a scene setting, because it is not a fact about the scene.
It is how a viewer is asking to see it, so it is stated when staging, on the
`DocumentView` in `Orblit.Stage`:

```csharp
using Orblit.Stage;

var view = new DocumentView(document, materials: library, look: "winter");
view.SetLook(null); // back to what each object wears by default
```

`SetLook` rebuilds rather than diffs, because it is a scene-wide swap.
`view.Looks` lists the names the scene knows.

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
  kept in `params.json`.
- Render modes: `blend_mix`, `blend_add`, `blend_mul`, `blend_premul_alpha`,
  `cull_back`, `cull_front`, `cull_disabled`, `depth_draw_opaque`,
  `depth_draw_always`, `depth_draw_never`, `depth_test_default`,
  `depth_test_disabled`, `unshaded`. No blend mode means opaque.
- `fragment()` writes `ALBEDO`, `ALPHA`, `METALLIC`, `ROUGHNESS`, `AO`,
  `EMISSION`, `NORMAL_MAP` and reads `UV` and `TIME`. `TIME` is the
  renderer's clock.
- `#include "x.oshaderinc"` only. A cycle names both files.
- Refused with the line: `vertex()`, `light()`, `varying`, another
  `shader_type`, any other render mode or hint.

An entity wears one through its material, with values of its own:

```csharp
using Orblit.Scene;

var pulse = new MaterialComponent(
    asset: "assets/pulse.oshader",
    values: new Dictionary<string, object>
    {
        ["speed"] = 4.0,
        ["tint"] = new[] { 0.2, 1.0, 0.4, 1.0 },
    });
```

A value is a number, a flag, one to four numbers, or a picture path. A name
the shader does not declare, or a value of the wrong width, is dropped.
Entities that state nothing share one material.

Stage the cooked shaders on the view. The package and `params.json` are
`<bundle>/<id>.material.filamat` and `<bundle>/<id>.params.json`:

```csharp
using Orblit.Json;
using Orblit.Stage;

view.SetSurfaces(new Dictionary<string, CookedSurface>
{
    ["assets/pulse.oshader"] = CookedSurface.FromParams(package, JsonText.Decode(paramsJson)),
});
```

`package` is the `.filamat` path or name and `paramsJson` is the text of the
`params.json` file.

Errors read `file:line: message`. Only macOS draws a surface so far. The
site's shader guide is at `/guides/shaders/`.

---

## Scene files (`.oscene`)

`SceneDocument.Decode(text)` returns a `SceneLoad` with `Document` and
`Problems`. `document.Encode()` goes the other way. A component this build has
never heard of survives both as an `UnknownComponent`, which is what lets two
editor versions share a project.

### Prefab instances

An instance is saved as a link and a diff, never as a copy. The entity carries
`PrefabComponent(asset: "props/lamp.oprefab", overrides: diff)`, where the
diff addresses the prefab's own ids. Open instances before drawing or editing,
and fold them before saving. `Instances` in `Orblit.Scene` does both and takes
a `PrefabSource`, a function from the asset path to a `PrefabDocument?`:

```csharp
using Orblit.Scene;
using Orblit.Stage;

PrefabDocument? PrefabAt(string asset)
{
    var path = Path.Combine(projectRoot, asset);
    return File.Exists(path) ? PrefabDocument.Decode(File.ReadAllText(path)).Prefab : null; // null leaves the instance folded
}

var load = Instances.Expand(SceneDocument.Decode(text).Document, PrefabAt);
var view = new DocumentView(load.Document);
// ... edit ...
var saved = Instances.Fold(view.Document, PrefabAt).Encode();
```

- **The id is the path.** An open instance's parts are entities with ids like
  `street1/lamp3/bulb`: one id per document crossed, joined by `/`
  (`EntityPath`). The instance root keeps its own id. `/` is reserved in ids.
- A parent, a selection or an animation track names a part by its path, like
  any other id. Something hung off a part that is not the prefab's own stays in
  the scene and keeps its path parent.
- `Instances.MakePrefab`, `Apply`, `Revert`, `Unpack` and `Refresh` are the
  editor's operations. Each returns what was renamed or opened, so a selection
  can follow.
- A prefab that cannot be read leaves its instance folded, exactly as saved.
  A version-four scene's stamped copies are relinked on expand, with their
  edits kept as overrides.

### Writing a scene out

`WriteAs` is an extension on `SceneDocument`:

```csharp
using Orblit.Scene;

var written = document.WriteAs(
    SceneFormat.Glb, // or Gltf, or Obj
    name: "old town",
    materials: library, // without it, a material is its mesh's colour
    files: new Dictionary<string, byte[]> { ["models/tree.glb"] = bytes }); // whatever the scene points at
var first = written.First; // the file to name the export after
var sidecars = written.Files.Skip(1); // in the order to save them
var problems = written.Problems; // what the format could not hold. Empty is normal.
```

Each `Written` in `Files` has a `Name` and its `Bytes`.

- `Gltf` is JSON with the bytes beside it, the one for version control. `Glb`
  is one file, the one to ship. `Obj` is world-space corners and faces with a
  `.mtl`. Everything reads it and it keeps almost nothing.
- **Bytes, not paths.** `Orblit.Scene` has no filesystem and will not get one.
  Only the caller knows whether it is in the editor, a cook step or a game.
- Problems are collected, not thrown. An export that silently drops half a
  scene is worse than one that says so.

### Reading a scene back

```csharp
using Orblit.Scene;

var read = SceneImport.ReadSceneFrom(
    bytes,
    files: new Dictionary<string, byte[]> { ["old town.bin"] = sidecar });
var document = read.Document;
var mine = read.WasWrittenHere; // true if Orblit wrote this glTF
var problems = read.Problems; // what could not be carried across
```

The container is sniffed from the bytes, not the extension. A `.gltf` that is
really a GLB is an ordinary thing to be handed. Anything that is not glTF is
read as an OBJ: one entity per `o` or `g` group, with its faces as editable
geometry and the colour of its first material. Pass the `.mtl` in `files`.
A group that wears several materials is noted, and its pictures are not read.

Below a scene, `ReadObj`, `ReadStl` and `ReadPly` in `Orblit.Mesh` read one
file. They check the size first and each count as they read, against a
`ReadLimits` you can lower for a file you do not trust, and a file over a limit
throws `FormatException`. `ReadObj` is lenient otherwise. A line it cannot read
is a note in `ObjModel.Problems` and the rest of the file is still read, but a
file with no face throws. `BoundsOfObj` gives an OBJ's box without building a
mesh, as `BoundsOfGlb` does for a GLB. `CollisionMesh.FromObj` in
`Orblit.Asset` makes a collider from one.

`Orblit.Mesh` has no FBX reader. The cook turns an FBX or an OBJ into a GLB
with `ModelImporter`, which needs the native `orblit_import` tool
(`ORBLIT_IMPORT` names it). Cook an FBX first, then read the GLB.

A scene Orblit wrote comes back **as itself**: same ids, same order, same
parents, same components, `Encode()` for `Encode()`. The exporter writes a
full record into `extras.orblit` on every node, so importing one is a read
rather than a re-derivation.

A glTF from anywhere else is *interpreted* and says so (`WasWrittenHere` is
false). Nodes become entities, a matrix or TRS becomes a transform, punctual
lights come back at the power they left at, and triangles become geometry that
can still be edited. An orthographic camera, a primitive that is not triangles
or an accessor reaching past its buffer lands in `Problems`.

Only bytes that are neither a GLB nor a glTF document nor readable as an OBJ
throw `SceneFormatException`. A scene with one unreadable node is still worth
opening.

---

## Splats

```csharp
using Orblit.Scene;

var street = new SplatsComponent(asset: "captures/street.osplat", budget: 400000);
```

`Asset` is a `.ply`, a `.spz` or a cooked `.osplat`. `Budget` is the most
splats to draw with, or null for as many as fit. `Harmonics` is how many bands
of spherical harmonics to keep, or null for the file's own.

---

## Cooked assets

`Orblit.Asset` turns source files into what a device can load. `GltfImporter`
handles models. `"atlas": true` in a model's import settings packs its small
textures together, remaps the UVs, and merges the materials and static
primitives that then match. It is a setting rather than a second importer, and
the cook key includes it, so turning it on re-cooks. Settings live in a file
beside the asset, such as `models/tree.glb.import.json`. `ImportSettings`
reads and writes it, and `ImportSettings.FolderFileFor` names the file that
applies to a whole folder.

`TerrainImporter` carries `.oterrain` and `.oregion` files into the bundle as
written, after checking each region against its name and its terrain's size
and each image, model and material the terrain names against the project.
Stream a cooked terrain with `Orblit.Terrain`
([packages.md](packages.md#orblitterrain)).

`SplatImporter` cooks a `.ply` or `.spz` to an `.osplat`, and needs the native
`orblit_splat_cook` tool (`ORBLIT_SPLAT_COOK` names it).

`RuntimeImport` runs the same importers over files a user hands the app, with
the results kept in an `ICookCache` (`DirectoryCookCache` on disk,
`MemoryCookCache` for a test). `orblit-cook --target <name> --out <dir>` cooks
a whole project.

---

## Packs

A cooked bundle is a folder. A pack is the same bundle as one `.opack` file,
for a game that ships, downloads or mounts it as a unit.

```sh
dotnet run --project dotnet/Orblit.Pack -- build build/assets/macos --target macos -o game.opack
dotnet run --project dotnet/Orblit.Pack -- list game.opack
dotnet run --project dotnet/Orblit.Pack -- check game.opack
dotnet run --project dotnet/Orblit.Pack -- diff old.opack new.opack
```

```csharp
using Orblit.Asset;

await using var bytes = new FilePackBytes("game.opack");
var pack = await AssetPack.OpenAsync(bytes);
var source = new LayeredAssetSource([new DirectoryAssetSource("assets"), pack]);
```

- **`AssetPack` is an `IAssetSource`.** It layers like any other. Opening reads
  the 32-byte header and the directory at the end. It never reads a blob.
- **`FilePackBytes` takes a path**, with optional `start` and `bytes` to read a
  pack that sits inside another file. `MemoryPackBytes` is for a pack that
  arrived as bytes. Dispose a `FilePackBytes` when the pack is done with.
- **`ReadAsync` does not hash.** `FindCorruptAsync()` hashes every blob and
  returns the ids that changed. Run it after a download, not on every read.
- **`pack diff old new` compares by name.** It lists added, removed and
  changed assets, and exits 1 when a document in the new pack still names
  an asset it dropped. Pass `--base` for the packs a patch stands on.
  `PackDiff.BetweenAsync` and `MountedPacks.WithBases` do the same in code.
- **The target is stored.** A pack is for one cook target, because the
  textures in it are. `pack.Target` says which.
- **Same bytes, one blob.** Two assets with identical bytes share a blob, and
  repacking the same bundle gives the same file.
- **Refusals are `FormatException`.** A cut-short file, a wrong marker, a
  newer format and blobs that overlap each throw, with the reason in the
  message. A pack is never half-read.

### Patches and mods

```sh
dotnet run --project dotnet/Orblit.Pack -- build build/assets/macos --target macos \
  --base game.opack --base patch-1.opack -o patch-2.opack
```

```csharp
using Orblit.Asset;

var packs = new MountedPacks(CookTargets.Macos);
packs.Start(game);   // a pack with no base
packs.Patch(patch1); // built against game
packs.Patch(patch2); // built against patch1
packs.AddMod(mod);   // no base, above the whole chain
```

- **A patch holds what changed.** Held when the hash differs from what the
  chain serves, or the chain lacks the id. Dropped ids go in `Removed`.
  Compared by hash, so no base blob is read.
- **`--base` goes once per pack, in mounting order**, the whole pack first.
  The patch names the last one by hash.
- **Three methods, not a flag.** `Start` refuses a pack with a base. `Patch`
  refuses one with none, or one built against something else. `AddMod`
  refuses one with a base. A refusal is a `PackMountRefusedException`.
- **A mod is above the chain whatever the mount order.** A patch mounted
  after a mod does not undo it. Later mods are above earlier ones.
- **A mod cannot remove.** `Removed` needs a base, so only a patch hides an
  id. A pack above can add the id back.
- **Nothing unmounts.** Switching a mod off takes a restart.
- `ChainHash` is what the next patch must name as its base. `Manifest` is
  the merged view, removals and mods spent. `OwnerOf(id)` says which pack
  answers for an id.

### Shipping one in an app

`BuildHook.PackDuringBuildAsync` cooks a project and packs the result as
`<out>/<name>.<target>.opack`. It takes `CookDuringBuildAsync`'s arguments. A
null `targetOs` means the web. A plain script or a build step runs it before
the app is published:

```csharp
using Orblit.Asset;

var summary = await BuildHook.PackDuringBuildAsync(
    packageRoot: projectFolder,
    targetOs: "macos");
Console.WriteLine($"{summary.Assets} assets in {summary.Bytes} bytes, hash {summary.Hash}");
```

The app reads the pack back by the name `PackName.FileName(target)` gives, for
example `game.macos.opack`. One way to ship it on a desktop is to copy it
beside the executable and open it by that name:

```csharp
using Orblit.Asset;

var name = PackName.FileName(CookTargets.Macos);
await using var bytes = new FilePackBytes(Path.Combine(AppContext.BaseDirectory, name));
var pack = await AssetPack.OpenAsync(bytes);
```

In the `.csproj`, an `<Content Include="assets/cooked/game.macos.opack"
CopyToOutputDirectory="PreserveNewest" />` item puts the file there. This
route has compiled and has not been run in a published app.

- **The target is in the file name.** A build that ships several platforms
  would otherwise ship the last platform's pack. Include only the file for the
  platform being built.
- **No package source ships yet.** `AssetFiles` in `Orblit.Filament` answers
  null everywhere today, so a pack inside an Android or iOS bundle cannot be
  read. Mobile waits on the platform scope.
- **The web is read in parts.** See the next section.

### Over HTTP

`RangePackBytes` reads a pack from any HTTP host that answers byte ranges, so a
large pack never has to arrive whole:

```csharp
using Orblit.Asset;

var store = new DirectoryContentStore("pack-cache");
var bytes = new RangePackBytes(new HttpTransport(), WebUrl.Parse("https://cdn.example.com/game.web.opack"));
var pack = await AssetPack.OpenAsync(bytes, cache: store);
```

- **`RangePackBytes` asks for a range at a time.** The header, then the
  directory, then one range for each asset read. The first answer says how
  long the pack is, so `LengthAsync()` is not a second request.
- **Reads made together are one request.** A read waits 2 ms. Reads with 16
  KiB or less between them are joined, up to 1 MiB, with at most four
  requests out. `new RangePackBytes(transport, url, new RangeBatching { ... })`
  sets those, and `ReadAsync(at, bytes, RangePriority.Later)` lets a read wait
  behind urgent ones. An `AssetPack` reads with `Now`. The defaults have not
  been timed on a real host.
- **The host has to answer `Range` with `206`.** A host that sends the whole
  file is refused with `PackRangeFailedException`, and the body is not read.
  Serve `.opack` as `application/octet-stream` with no content encoding.
- **`cache` is keyed by hash.** A blob is checked against the directory's
  hash before it is kept. A rebuilt pack fetches the directory and only the
  blobs whose hash changed. Without a store every read is a request.
- **A pack replaced mid-session is refused.** An answer that gives a
  different length than the first one throws `PackRangeFailedException`. Open
  the pack again.
- **A small pack is cheaper whole.** Read `LengthAsync()`, and when it is
  under the app's threshold take `ReadAsync(0, length)` into a
  `MemoryPackBytes`. A range that covers the file may come back as a `200`,
  which is accepted.
- **No retries.** A failed request throws. Wrap the transport to retry.

### Exporting from the editor

File › Export… in the editor, or the palette's Export, cooks the project for
each target chosen and packs the result into `exports/` beside the project. The
code is `ExportRun` in `Orblit.Editor.Core`, with an `ExportRequest` per run.

- **A whole pack is `game.<target>.opack`.** That is the name an app ships
  and `PackName.FileName` returns. A new whole pack sets the one it replaces
  aside as `game.<target>.<hash12>.opack`, so a patch built on it still
  finds its base.
- **A patch is `game.<target>.<hash12>.opack`.** It is built on a pack
  picked from the folder (`ExportRequest.PatchOf`). The name is its own hash,
  so a patch never overwrites the one it stands on.
- **A chain is found by hash.** Each patch names its base by directory hash
  and the folder is searched for it. File names and dates do not matter. A
  base that is missing is an `ExportChainBrokenException`.
- **Nothing that changed is not written.** A whole pack identical to the
  last one, and a patch that holds nothing and removes nothing, report
  `ExportOutcome.Unchanged`.
- **A failed cook packs nothing for the target.** The outcome is
  `CookFailed`, the assets and the reasons are listed in `Failures`, and the
  last good pack is left as it was. The next target still runs.
- **Each target runs on the thread pool.** A result is shown as it lands.
  The pool shares the editor's process, so a cook that runs out of memory
  takes the editor down with it. `ExportRun.RunAsync` is the whole run, and
  `ExportTargetAsync` is one target and never throws. A project with no
  `assets` folder is `Failed`.
- **`PackSummary.Added` and `Replaced` split `Assets`.** For a patch, `Added`
  is what the base did not serve and `Replaced` is what it served with other
  bytes. A pack with no base has every asset added.
- **Not run live** with a real texture encoder, or with the native tools of a
  target other than the machine's own.

### Downloaded packs

```csharp
using Orblit.Asset;

var folder = new PackFolder(patchesPath);

var pack = await folder.DownloadAsync(
    new PackOffer(WebUrl.Parse(patchUrl), patchHash),
    new HttpTransport(),
    AssetOrigin.Parse("https://cdn.example.com/"),
    onProgress: progress => Console.WriteLine($"{progress.Received} of {progress.Total}"));

// At start-up, whatever the listing order:
var listing = await folder.ListAsync();
var chain = PatchChain.Of(shipped, listing.Packs);
foreach (var patch in chain.Ordered)
{
    packs.Patch(patch);
}

foreach (var mod in chain.Standalone)
{
    packs.AddMod(mod);
}
```

- **`PackOffer` is a URL and the directory hash.** That is what
  `pack build` prints and what a patch names as its base. The folder files
  the pack as `<hash>.opack`.
- **A download resumes from the file.** Bytes go to `<hash>.part` and are
  kept when a reply ends short. A retry asks for the missing bytes only.
- **The pack is checked as a pack.** It has to hash to the offer and every
  blob has to match the directory. A failure throws `FetchCorruptException` or
  `FormatException` and leaves nothing a mount would take.
- **A server that cannot resume is handled.** A `200` restarts the file. A
  `206` from another byte, or a part that is the start of a different file,
  drops the part and fetches whole.
- **Retries cover a dropped connection and 5xx, 408, 425 and 429.** A 404, a
  refused host, a wrong pack and a full disk throw at once. `attempts`
  counts all tries, five by default, and `backoff` starts at one second and
  doubles.
- **`FetchPolicy.MaxBytes` is 256 MiB.** A larger pack is refused from its
  declared length, or from the bytes as they arrive. Set it in the origin's
  policy.
- **A second call for the same hash shares the first.** Its `onProgress` is
  not called.
- **`PatchChain.Of(shipped, found)` orders by `Base`.** `Ordered` follows
  each patch from the shipped pack's hash, `Standalone` has no base, and
  `Skipped` says why for a missing base, a fork, another target or an older
  pack.
- **`ListAsync` does not throw for one bad file.** `Unreadable` has the path
  and a reason. A file whose contents do not hash to its name is reported
  and not opened. `Unfinished` lists the downloads to resume.
- **Remove before mounting.** `folder.RemoveAsync(hash)` deletes the pack and
  its part. A mounted pack holds its file open.
- **A mods folder is not a `PackFolder`.** Open those files with
  `FilePackBytes` and `AssetPack.OpenAsync`, whatever they are called.
- **Not `AssetFetcher`.** That keeps bytes in memory and files them by
  hash, which is right for a texture and wrong for a pack.

### Kits

A kit is a pack for the target `source`. It holds files for a project's own
folders and a `kit.json`. A game never mounts it.

```sh
dotnet run --project dotnet/Orblit.Kit -- pack crate-pack -o crate-pack.opack
dotnet run --project dotnet/Orblit.Kit -- check crate-pack.opack
dotnet run --project dotnet/Orblit.Kit -- extract crate-pack.opack path/to/project
```

```csharp
using Orblit.Asset;

await using var bytes = new FilePackBytes("kit.opack");
var kit = await Kit.OpenAsync(await AssetPack.OpenAsync(bytes));
var problems = await kit.CheckAsync();
var clashes = await kit.ConflictsInAsync(new DirectoryAssetSource("my_project"));
await KitExtraction.ExtractAsync(kit, "my_project");
```

- **The folder holds files and a `kit.json`.** The manifest names the
  `slug`, `publisher`, `name`, `summary`, `version`, `licence` (`spdx` and
  `url`), `engine` (`from` and `to`), `tags` and `previews`. The tool sets
  `containsScripts` from the file names, so a value typed there is ignored.
- **Packing refuses what a download could abuse.** A link, a hidden file
  such as `.DS_Store`, a name that ends in a dot or a space, a Windows
  device name, a character outside letters, digits, spaces and `_.()+-`, two
  names that differ only in case, a name that is a file and a folder, a file
  over 64 MiB, more than 5000 files, more than 1 GiB in all and a path over
  240 characters. Every problem is listed at once. `containsScripts` is a
  hint, because a file with no extension can still be a program.
  `KitRules` holds the limits and `KitRules.Problems` the checks.
- **Installing checks the kit again and never overwrites by surprise.**
  `KitExtraction.ExtractAsync` runs the same rules and the blob hashes, so a
  pack built by hand gets no further than a folder would. It throws
  `KitConflictsException`, or a `FormatException` for a link or a file in the
  way, before it writes the first file, unless `Overwrite` is true in its
  `KitExtractionOptions`. A file that is already identical is left alone. It
  never writes `kit.json` and never writes through a link. A file that
  `Overwrite` replaces gets the default permissions.
- **Files are written one at a time.** A full disk half way leaves the
  earlier ones, and running the install again is safe.
- **Refusals are `FormatException`**, except a project folder that does not
  exist, which is an `ArgumentException`.

#### A store of kits

`dotnet run --project dotnet/Orblit.Kit -- index <entries> -o <folder>` builds
the files a kit browser reads. `<entries>` holds `<publisher>/<slug>.json`, and
each file is `{"releases": [...]}` with no other key. A release is `version`,
`url` (https at a public host name, no address, `localhost`, port other than
443, space, backslash, `#` or `@`), `hash` (the pack hash that `kit pack`
prints), `bytes`, `released` (`2026-10-19`) and an optional one line `notes`.
The kit's name and summary come from its `kit.json`, not the entry.

- **It checks every release.** It downloads each one from its url, up to the
  size the entry gives and for up to ten minutes. The url may redirect,
  because the hash vouches for the bytes. The pack must have the entry's hash
  and size, every blob must match, the kit must pass `CheckAsync`, and
  `kit.json` must name the entry's publisher, slug and version. Every problem
  is listed, nothing is written until all pass, and the output folder must not
  exist.
- **It writes `index.json`, `kits/<publisher>/<slug>/releases.json` and the
  latest release's previews.** Read them with `KitIndex.Parse` and
  `KitReleases.Parse`. A kit names up to six previews in its `kit.json`, each
  a png, jpg, jpeg or webp file, and the store keeps each at 1 MiB or less
  (`KitStoreRules.IsPreviewName` says which names it may give). The first is
  the kit's thumbnail. Unknown keys in these files are ignored and a newer
  format is refused.
- **`KitStoreBuilder.BuildAsync` is the same thing as a function** and takes
  an `IAssetTransport`, so a test serves the packs from a `MapTransport`.
- **Every run downloads every release.** Keep packs by hash between runs
  when a store holds hundreds of kits.

#### Listing a kit

`dotnet run --project dotnet/Orblit.Kit -- publish <folder> --url <url>
[-o <file>] [--entries <folder>] [--notes <text>]` packs the folder, opens the
file and checks it as `kit check` does, and prints the entry with the hash and
size filled in. `KitPacking.PublishAsync(new KitPublishRequest { ... })` is the
same in code and returns a `KitPublished`.

- **The file is named after the end of the url**, or by `-o`. A url that ends
  in no plain file name is a usage error, exit 2.
- **`--entries` writes `<publisher>/<slug>.json`** into a folder of entries by
  rename and adds the release to the ones listed. A version already listed is
  refused, because a listed release is not edited. Raise `version` in
  `kit.json`. Without `--entries` it only prints the entry.
- **The version comes from `kit.json`**, the hash and size from the file, and
  the day from the clock. `KitPublishRequest.Released` takes the day as an
  argument.
- **A refusal leaves the kit file as it was.** The file is packed beside its
  name and moved into place after every check. An out path inside the folder
  is refused, because the next pack would take the file in.
- **It uploads nothing and runs no git command.** It prints the steps left.
- `KitEntry.Encode()` writes the entry file in the form `KitEntry.Parse`
  reads.

#### Reading a store and installing from it

```csharp
using Orblit.Asset;

var store = new KitStoreClient(WebUrl.Parse("https://example.com/store/"), new HttpTransport());
var row = (await store.IndexAsync()).Rows[0];
var latest = (await store.ReleasesAsync(row)).Releases[0];
var listed = await store.OpenAsync(latest.Release); // the file list only

var pack = await new PackFolder("downloads").DownloadAsync(
    latest.Release.Offer,
    new HttpTransport(),
    latest.Release.Origin);
var kit = await Kit.OpenAsync(pack);
var problems = await kit.CheckAsync();
var clashes = await kit.ConflictsInAsync(new DirectoryAssetSource("my_project"), into: "assets/kits/crates");
await KitExtraction.ExtractAsync(
    kit,
    "my_project",
    new KitExtractionOptions { Into = "assets/kits/crates" });
```

- **The client reads and never writes.** `KitStoreClient` takes an https
  base and an `IAssetTransport`, so a test serves a store from a
  `MapTransport`. It reads `IndexAsync()`, `ReleasesAsync(row)`,
  `PreviewAsync(row, name)` and `ThumbnailAsync(row)`, each to the limit of its
  file and within `Timeout` (30 seconds). A reply that is not a 200, a body
  past its limit or no answer in time throws `KitStoreFailedException`.
  `KitStoreClient.AddressProblem(base)` says why an address cannot be a store.
- **`OpenAsync(release)` is for showing, not installing.** It lists files by
  ranged requests to the host that holds the kit file, and it reads at most
  4 MiB in one piece. The server must answer `Range`, or it throws
  `PackRangeFailedException`. Nothing has checked its bytes against the index
  hash. Install from a download made with `PackFolder` and
  `KitRelease.Origin`, which checks the hash and throws
  `FetchCorruptException` with both hashes.
- **`Into` and `Only` belong to `KitExtractionOptions` and `ConflictsInAsync`.**
  `Into` is a folder of the project, held to the rules a file name is. A hidden
  folder such as `.git/hooks` is refused. `Only` is a set of the kit's own ids,
  and an id the kit does not hold is an `ArgumentException`. Conflicts are
  counted at the new place. With no `Into`, a kit file called `kits.lock` in
  any letter case is refused, because it would replace the list of installed
  kits.
- **`kit.Scripts` lists what can run.** Show it before an install. An install
  never runs a script.
- **`KitEngine.Fits(version)` is inclusive at both ends.** It answers true
  for a version it cannot read, because it cannot judge. Read the range from
  `kit.Manifest.Engine`.
- **`kits.lock` is at the project root.** `KitLock.Parse(bytes)` reads it
  and `Encode()` writes it. A `LockedKit` holds the publisher, slug, version,
  pack hash, `Into` and the hash of each file written. A file the person left
  out is not listed. `Adding` replaces the older entry of the same kit. A
  newer format is refused. `ExtractAsync` does not write the lock, so
  whoever installs does. `KitUpdatePlan.Plan` says what an update would
  replace, add, remove and keep, from the lock, the new release and the files
  on disk, and changes nothing.
- **The editor does this in File › Kit store….** It goes online only when the
  person presses a button, reads a kit's file list only when asked, and
  replaces a file of the project only when the person ticks it.

---

## Network assets

```csharp
using Orblit.Asset;

var origin = new AssetOrigin(
    WebUrl.Parse("https://cdn.example.com/assets/"),
    new FetchPolicy(hosts: ["images.example.com"]));
var fetcher = new AssetFetcher(origin, new HttpTransport());
var source = new NetworkAssetSource(fetcher);
```

- **The policy is checked before any connection.** `Hosts` is matched exactly
  and case-insensitively. `cdn.example.com` does not allow
  `evil.cdn.example.com`, because a suffix match is how an allow-list becomes
  an allow-anything. The origin's own host is always allowed. `AllowInsecure`
  is off, because a texture over plain HTTP is bytes fed to a decoder that
  anyone in between can replace. `MaxBytes` and `MaxPixels` stop a download
  while it is still arriving, not after.
- **A 404 becomes `AssetNotFoundException`**, which is what makes a network
  source work as a layer in `LayeredAssetSource`. Every other failure passes
  through untouched. "The server has not got it" and "the server could not be
  reached" must not lead to the same place, or an outage quietly serves
  something old.
- **ETags.** The stored tag goes out as `If-None-Match`. A 304 whose bytes
  have gone forgets the record and refetches rather than being told 304 again.
- **`MapTransport`** answers from a dictionary of `TransportPage`, for tests
  and offline demos, and keeps what it was sent in `Sent`.
- **Urgency.** `fetcher.Fetch(...)` and `NetworkAssetSource` take a
  `FetchUrgency`: `OnScreen` before `Soon` before `Eventually`, and within one
  level the earliest ask first.

### Progressive loading

```csharp
using Orblit.Asset;

await foreach (var stage in fetcher.FetchInStagesAsync(id, standIn: lowResId))
{
    if (stage.IsFinal)
    {
        // the real asset: replace whatever is showing
    }
    else
    {
        // a stage worth drawing now
    }
}
```

Up to three stages. First comes a stand-in asset, if one is named and the real
one is slow (`standInAfter`). Then, for a KTX2 with a mip chain, comes the
texture's **own** coarse levels, built from the front of the file while the
rest is still arriving. Last is the whole thing. The middle stage costs no
extra request and no extra byte, because KTX2 stores mip levels
smallest-first, so a prefix of the file is a complete set of small levels. It
supersedes the stand-in and is never followed by one. A texture with no mip
chain never produces it. Ending the enumeration early cancels the fetches.

---

## Traps

- **`new TransformComponent(position: ...)` on its own resets rotation and
  scale.** Carry the fields you are not changing.
- **Material maps go in `maps`, not `values`.** A texture path in `values` is
  reported as "no parameter called ...".
- **`MaterialLibrary.Put` clears the whole resolved cache**, so building a
  library by repeated `Put` inside a loop that also resolves is quadratic.
  Construct it with its maps, then resolve.
- **A grafted model's nodes are not entities.** When a scene names
  `models/tree.glb`, the export copies that model in whole under the node that
  draws it. On import, only nodes carrying `extras.orblit` become entities.
  Anything else doubles the outliner on every round trip.
- **Interleaved buffer views.** Anything reading glTF accessors by hand must
  honour `byteStride`. Reading a strided view as packed gives positions that
  are *wrong* rather than geometry that is missing, which is far harder to
  notice.
- **`.obj` keeps almost nothing.** No tree, no lights, no cameras, no
  animation. Check `SceneWritten.Problems` and show them to the user rather
  than letting an export look successful.
