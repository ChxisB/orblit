# Changelog

## 0.16.0

- **`CollisionMesh.fromObj`.** Builds a collider from a Wavefront OBJ, every
  group as one mesh with its polygons cut into triangles. A cooked FBX is a
  GLB by then and already went through `fromGltf`. The package depends on
  `orblit_mesh` for the reader.

## 0.15.0

- **`TerrainImporter`.** Claims `.oterrain` and `.oregion` and is one of the
  default importers, for a project cook and for `RuntimeImport`. Both files
  are carried into the bundle as written, as `<id>.oterrain` and
  `<id>.oregion`, after the checks a build would otherwise leave to the run.
- **What it checks.** A region file is named for the place it holds. A
  terrain depends on every region it lists and on every image, model and
  material its sets and scatter layers name, so a missing one fails the
  terrain's cook, and editing a region cooks the terrain again. A listed
  region of another size than the terrain, or one holding another place, is
  a failure that names the file. What loading left out becomes a note.
- **`CookedTerrain.open(source, file)`.** Reads a cooked terrain back from
  any `AssetSource`, a bundle or `MountedPacks`. It gives the `Terrain` with
  no regions in it and a `RegionSource` for `TerrainStreamer`. A listed
  region the source lacks is named in `problems` and never asked for.
- New dependency on `orblit_terrain`.

## 0.14.0

- **`MountedPacks.ownerOf(id)`.** The mounted pack that answers for an id,
  or null when none does. A mod above the chain owns what it covers and a
  patch owns what it replaced. It reads the map a read already uses.
- **`PackSummary.added` and `PackSummary.replaced`.** Split the assets a
  patch holds into ids the base did not serve and ids it served with other
  bytes. `added + replaced == assets`, and a pack with no base is all
  added. `removed` is as before.
- **`pack build` names the changes of a patch.** With `--base` it prints
  `N new, N replaced, N removed` after the sizes.

## 0.13.0

- **`PackFolder`.** A folder of packs fetched after install. A pack is
  `<hash>.opack` and a download in progress is `<hash>.part`, so a file only
  gets a pack's name once it has been checked. `list()` opens what is there,
  `download(offer, ...)` fetches one and `remove(hash)` deletes one. It needs
  `dart:io`. On the web every method throws `UnsupportedError`.
- **Downloads resume.** The part file is appended to, from the byte it
  ends at, and kept when a reply ends short. A server that answers `200`
  restarts the file. A server that resumes at another byte, or a part that
  turns out to be the start of a different file, is dropped and fetched
  whole. A `416` for a part that is already the whole pack finishes it.
- **A pack is checked as a pack.** The download has to hash to the offer's
  directory hash and every blob has to match the directory, or it throws
  `FetchCorrupt` or `FormatException` and leaves no file with a pack's name.
  A pack over `FetchPolicy.maxBytes` is refused from its declared length,
  and from the bytes as they arrive when none is declared.
- **Retries cost the missing bytes.** `attempts` (5) with doubling `backoff`
  (1s) and noise. A 5xx, 408, 425, 429 and a dropped connection are tried
  again. A 404, a refused host and a wrong pack are not. This is not
  `AssetFetcher`, which keeps its bytes in memory and files them by hash.
- **`PackOffer`.** The URL and the directory hash a server says the pack
  has, which is what `pack build` prints and a patch names as its base.
- **`PackListing`.** The packs that opened, the files that look like packs
  and are not (`UnreadablePack`, with a reason) and the hashes of downloads
  that began and did not finish. One bad file does not stop the rest.
- **`PatchChain.of(shipped, found)`.** Puts packs found on disk in mounting
  order by following each patch's `base` from the shipped pack, whatever
  order they were listed in. `ordered` is the chain, `standalone` is what
  has no base and so mounts as a mod, and `skipped` is a `SkippedPack` with
  a reason for a patch whose base is missing, a fork, a pack for another
  target or one older than the shipped pack.
- **`AssetOrigin.requireAllowed(url)`.** Throws `FetchRefused` for a URL
  the policy does not allow. `allows` answers the same question as a bool.

## 0.12.0

- **`RangePackBytes`.** A `PackBytes` over an `AssetTransport` and a URL.
  It asks for the header, then the directory, then one range per blob read,
  so a pack on a server is opened without being downloaded. The first
  request also says how long the pack is, and is not asked for twice.
- **`PackRangeFailed`.** Says what the server did when a part could not be
  read: it ignored the range, answered another one, answered an error, or
  the pack changed length part way through. A `200` is refused and its body
  is not read. A pack on another origin has to send
  `Access-Control-Expose-Headers: Content-Range`, and the error names it.
- **`FetchRequest.through`.** The last byte wanted, counted as HTTP counts
  it. `HttpTransport` sends `Range: bytes=<from>-<through>`, and
  `MapTransport` answers `206` with the bytes between and cuts a range at
  the end of the page. A request with no `through` is open-ended as before.
- **`HttpTransport` reports the start the server sent.** `FetchReply.from`
  for a `206` is read from the `content-range`, and is the request's `from`
  only when the server sent none. A server that answers another range is
  now visible to the caller. A `206` with no readable `content-range` has
  no length, where it used to take the part's `content-length` for it.
- **`AssetPack.open(bytes, cache:)`.** A blob read from the pack is filed in
  the `ContentStore` under its hash and checked against it on the way in. A
  later read, in this session or the next, comes from the store. A rebuilt
  pack shares the hash of every blob that did not change, so a returning
  player fetches the directory and the blobs that changed.

## 0.11.0

- **`packDuringBuild`.** A build hook cooks the project and packs it in one
  call, and returns the `PackSummary`. It takes what `cookDuringBuild` takes
  and writes `<out>/game.<target>.opack` beside the cooked bundle.
- **The target is in the file name.** `packFileName(target)` gives
  `game.macos.opack`. A hook is skipped when nothing it watched has changed,
  so a fixed name would hold whatever the last build for any platform wrote.
  List each name in `pubspec.yaml` under `platforms:` and a build ships only
  its own. A name the build does not list is never bundled, so a pack for
  another platform does not add to the download.
- **`bundledPackBytes(key)`.** Finds a bundled pack as a file beside the
  executable and returns `FilePackBytes` over it, so the pack is read in
  place. It answers null on Android, the web and under the Dart VM, where
  there is no such file. `flutterAssetsDirectory` is the path arithmetic
  under it, for macOS, iOS, Linux and Windows layouts.
- **A hook no longer runs again on every build.** `cookDuringBuild` named a
  `.import.json` file for each asset whether or not it existed. The hook
  runner reads a file that is not there as changed, so the hook ran every
  time and rewrote its output. It now names the files that exist, the assets
  folder and each folder under it, so a new asset or a new settings file
  still runs it once.

## 0.10.0

- **Patches.** `packBundle(..., base: [game, patch1])` writes a pack that
  holds only what changed since the packs already shipped. An asset is held
  when its hash differs from what the chain serves or the chain lacks it.
  Ids the bundle dropped go in `removed`. The patch names the last pack in
  `base` by hash. Nothing in `base` is read for its bytes. On the command
  line, `--base` is given once per pack, in mounting order.
- **`MountedPacks`.** One `AssetSource` over a chain. `start` takes a pack
  with no base, `patch` takes one built against the top of the chain, and
  `addMod` takes one with no base and puts it above the chain. A pack for
  another target, or in the wrong place, throws `PackMountRefused`.
- **Mount order cannot undo a mod.** Mods always sit above the chain, later
  above earlier, whatever order the app mounted them in. A patch downloaded
  after a mod still lands beneath it.
- **Resolved once.** Which pack answers for an id is worked out when a pack
  is mounted, so a read is one map lookup. `manifest` is the merged view and
  `chainHash` is what the next patch must name.
- A pack that removes ids with no base, removes one twice, or both removes
  and holds one is refused at open.

## 0.9.0

- **Packs.** `packBundle` writes a cooked bundle as one `.opack` file, and
  `AssetPack` reads one. A pack is an `AssetSource`, so it layers under a
  directory the way every other source does. The file is a 32-byte header,
  the blobs, and a JSON directory last.
- **One copy of each blob.** Assets with the same bytes share one blob, and
  each blob starts on a 16-byte boundary so a typed view of it is aligned.
  The same bundle always packs to the same bytes.
- **Opened from the end.** `AssetPack.open` reads the header and the
  directory, never the blobs. A 2 GB pack opens as fast as a small one.
  `PackBytes` is where the bytes come from: `MemoryPackBytes`, or
  `FilePackBytes` on a platform with files, which also reads a pack that
  sits inside another file.
- **Refused whole.** A pack that is cut short, has the wrong marker, comes
  from a newer format, or lays blobs over each other or over the directory
  throws a `FormatException` that says which. `AssetPack.read` does not hash
  what it reads. `findCorrupt` hashes every blob and names the assets that
  changed.
- **`dart run orblit_asset:pack`** with `build`, `list` and `check`. Exit 0
  when it worked, 1 when a pack or bundle was bad, 2 for bad arguments.

## 0.8.0

- Bake static collision triangles from glTF node transforms with `CollisionMesh`.
- Cook a `collision.json` sidecar when model settings include `collision: true`.


## 0.7.0

- **Assets over the network.** `AssetFetcher` fetches an asset from an origin
  and hands back its bytes, with the four things that separate a download from
  a loader: several callers asking for one thing share one request, a queue
  that takes what is on screen before what is not, downloads that resume where
  a dropped connection left them, and bytes checked against the manifest's
  hash before anything decodes them.
- `NetworkAssetSource` is an ordinary `AssetSource`, so it layers under a
  directory the way every other source does. A 404 becomes an `AssetNotFound`
  and the layer below is tried; a 500 does not, because falling through on a
  server having a moment quietly serves a stale layer whenever a CDN hiccups.
- **Nothing is ever written to a temporary file.** Bytes go from the socket to
  memory to the content store, keyed by hash. That is partly because the web
  has no files to write, and partly because a half-written temporary that
  outlives the process is the oldest way to serve a truncated asset and swear
  it is fine.
- **HTTPS by default, and an origin that is a boundary rather than a prefix.**
  `AssetOrigin` refuses plain HTTP unless asked, refuses `file:` and anything
  without a host, and allows only its own host plus whatever is listed. A
  glTF naming `../../etc/passwd` beside itself is refused for resolving
  outside the origin's path, and a host list matches exactly, so
  `cdn.example.com.evil.net` is not `cdn.example.com`.
- **Size and dimensions are checked before decoding, and while the bytes are
  still arriving.** A declared length over the limit never starts; a body that
  outgrows it stops at the byte that passes it; and a picture is read from its
  header — PNG, JPEG, WebP in all three flavours and KTX2 — as soon as the
  first chunk lands. A sixteen-gigapixel PNG is a small download and an
  enormous decode, so before the rest of it is even sent is the only useful
  place to catch it. Every mip level, array slice and cube face counts toward
  the pixel limit, because a limit that ignores the mip chain lets a third
  more through than it says.
- **Caches that survive a launch.** `DirectoryFetchRecords` keeps what each
  URL last was in one JSON file, written by the same write-and-rename
  `DirectoryContentStore` uses; `CacheStorageContentStore` and
  `CacheStorageFetchRecords` do the same in the browser's Cache Storage.
  Writes are coalesced: five hundred assets loading is one write, not five
  hundred, and a set nothing changed is not written at all.
- A cached copy the manifest vouches for is served with **no request at all**,
  not even a round trip to ask — the hash is a promise about the bytes, not
  about the URL. Where there is no such promise, the stored ETag goes out as
  `If-None-Match` and a 304 costs headers rather than a file.
- **Offline is a first-class answer.** When the attempts run out and a copy is
  held, that copy is served; an old asset beats a blank screen, and being
  offline is the ordinary reason to be there.
- `fetchInStages` hands over a stand-in while the real asset loads, and skips
  it when the real one arrives first — so a warm cache shows no stand-in
  rather than flashing one for a frame.
- **A texture draws its own small levels before the rest of it arrives.**
  KTX2 stores a mip chain smallest-first, so the front of the file is already
  a complete set of small levels: `Ktx2Chain` reads the level index out of the
  bytes as they stream past and rewrites that prefix into a whole, valid KTX2
  file. It costs no extra request and not one extra byte — the picture is
  built out of bytes that were arriving anyway. On the 1254×1254 logo (eleven
  levels, 782 KB) a 627-wide stand-in is ready after 27.6% of the file, a
  313-wide one after 7.9% and a 156-wide one after 2.4%. A file with no mip
  chain, or one that is not a texture at all, simply arrives as before.
- `HttpTransport` is the default way out, and `AssetTransport` is the seam:
  a test answers without a network, an app can pass a client already carrying
  authentication or a pinned certificate, and a platform with something better
  underneath can use it.

## 0.6.1

- A rewritten document with no geometry in it — a scene of lights and cameras
  is one — no longer names a buffer of nought bytes or writes an empty `BIN`
  chunk. Both are errors in the format, and the file came back out invalid.

## 0.6.0

- **Model texture atlases.** `atlas: true` on a `.gltf` or `.glb` packs the
  model's small textures onto shared pages, remaps its UVs, merges the
  materials that then say the same thing and merges the static primitives that
  then share one. Packing alone saves nothing — Filament batches by material,
  so without the merges it is the same draw calls sampling one image.
- The layout is solved once per **material**, against that material's largest
  map, then replayed into one page per role. That is what lets base colour,
  normal and metallic-roughness land on the same rectangle of different pages,
  so one UV remap serves all of them. Roles never share a page: a colour page
  is sRGB and a normal page is linear, and they are cooked with different
  flags.
- A material can be handed a map slot it never had, because every role's blank
  is the value glTF samples when that slot is absent. That is the precondition
  for merging: a material with no normal map draws identically after being
  given one.
- Colour, metallic-roughness and emissive factors are folded into the texels
  and reset to identity, which is what turns materials differing only by tint
  into a single material. Colour factors fold in linear light.
- Nothing is all-or-nothing. A material whose maps tile across the surface,
  whose slot carries a `KHR_texture_transform`, whose texture is used as two
  different things, or which carries a material extension this does not
  understand, is left exactly as it was and said so in the import notes, while
  the rest of the model is packed around it.
- Pages are named through `KHR_texture_basisu`, which is where the spec puts a
  KTX2, and the portable file is always written whatever families the target
  asked for, because a model fetches its textures by the URI it was given.
- Atlasing is a setting on `GltfImporter` rather than an importer of its own:
  `Importer.handles` is given a path and not the settings, so a second importer
  could never know which models had asked. `GltfImporter.version` is 2, so
  bundles cooked before the setting existed are not served for it.
- **A glTF reader and writer**, in `lib/src/gltf/`. `GltfDocument` parses the
  JSON to a `Map` and edits only the keys a rewrite owns, so extensions,
  `extras`, variants, skins and animation survive because they were never
  looked at. It resolves buffers from a GLB chunk, a data URI or a file beside
  the document, and writes back one self-contained GLB. Accessors read
  interleaved views at their stride and the normalized integer forms
  `KHR_mesh_quantization` uses, not only floats.

## 0.5.0

- **Asking where an asset stands without cooking it.** `Cook.stateOf` answers
  `cooked`, `stale`, `failed` or `ignored` for one asset, and `stateOfAll` for
  many at once, sharing the dependency hashes so a texture forty models depend
  on is read once. This is for the editor, which wants a mark beside every
  asset in a list and cannot run a build to get one.
- It reaches that answer down the same path a cook does — resolve the settings,
  pick the importer, hash the source and everything under it, build the key —
  and stops at the cache lookup instead of running the importer. An editor that
  worked the answer out its own way would drift from the build the first time
  an importer changed how it resolves a setting, and a stale mark on a fresh
  asset is worse than no mark at all.
- `CookProject.statesOf` asks the same question for a whole project, or for a
  few assets of it, through the project's own assets folder, cache and
  importers — so the mark an editor shows is decided by what the build will
  actually do.
- **Cooks remember what would not cook.** A cook that fails files the reason in
  the cache against the key it failed under, and `CookCache.lookUpFailure`
  reads it back, so an editor can mark a broken asset without running the
  importer that breaks on it. Recording is not refusing: no cook consults one
  before working, because importers fail for reasons that are not in the key —
  a tool that was not installed, a full disk, a killed process — and a cache
  that treated those as settled would turn a bad afternoon into a project that
  never builds again. Cooking a key clears its failure, and so does changing
  the asset, since that is different work.

## 0.4.0

- **Cooking a whole project, from a command.** `dart run orblit_asset:cook
  --target ios` walks a project's assets, cooks each one for that target and
  writes a bundle: the files, and a manifest saying what each asset now is.
  Repeat `--target` and it writes one bundle per target, side by side, so an
  app build ships only the formats its own target can read — an iOS build
  carries ASTC and no BC, and a desktop build the reverse.
- `CookTargets` names the six platforms Orblit builds for and what each one
  wants: which texture families it can decode, how large a texture it will
  take, and whether it has half-float textures. They are ordinary `CookTarget`
  values, so a project that needs a seventh writes one rather than waiting for
  this to grow.
- `CookProject` is the same run without the command around it, for a build hook
  to call. It empties a bundle before writing it, so an asset deleted yesterday
  stops shipping today rather than lingering because nothing overwrote it.
- `bundleHash` is one hash over a whole bundle's manifest, which is how a build
  says "this is the same set of assets as last time" in one comparison. Two
  clean cooks of an unchanged project produce the same hash; changing one asset
  changes it.
- `cookDuringBuild` is the same cook for a build hook to call, taking the
  platform as the hook spells it and handing back every file it read so the
  hook re-runs on an edit and is skipped otherwise. It names no hook types, so
  depending on `orblit_asset` does not pull `hooks` and `code_assets` into a
  project that only reads assets. It does not produce a data asset: those are
  master-channel only, so a cooked bundle ships as an ordinary Flutter asset
  and the project lists it under `flutter: assets:` until that changes.
- **Packing sprites into atlases, as part of the cook.** `AtlasImporter`
  claims a small document — `ui.atlas.json` — naming the sprites it wants
  packed, and runs `orblit_sprite`'s packer over them. An atlas is not a file
  somebody has but one they want made, so the document is the asset and the
  sprites are its dependencies: adding a sprite edits the document, editing a
  sprite changes a dependency, and either repacks while nothing else moves.
- Sprites are listed rather than globbed. A glob would make the cook's answer
  depend on what happened to be in a folder, which is the one thing a
  content-addressed cache cannot key on, and it would pick up the stray `.png`
  somebody left there mid-export.
- Pages come out as PNG and a descriptor per page, not as compressed textures.
  Packing and encoding are separate jobs, and the pages want cooking per target
  like any other picture — doing it here would mean doing it twice.
- A sprite too large for a page fails the atlas rather than being noted. An
  atlas that packed nine of ten sprites gives a game that draws nothing where
  the tenth was, and the first anyone hears of it is at run time.
- **Importing a file a user hands the app.** `RuntimeImport` is the same cook
  with the parts a project supplies replaced by the parts a running app has:
  the device's cache, the device's own target, and one file somebody chose
  instead of a folder full of them. Because the key covers the file's bytes,
  importing the same file twice is free, and so is importing one the build
  machine already cooked.
- `RuntimeImport.nameFor` turns whatever a file picker hands back — a
  container path, a Windows path, a URL with a query on it — into an asset id,
  keeping the part the user would recognise and dropping the machine's
  directory layout, which is the thing an id exists not to carry.
- A `.gltf` a user picked on its own is a fragment, since it names its buffers
  and images in other files. `alongside` is where those go, so the same
  importer that works in a build works on a folder somebody dropped in.
- Importers that drive a command-line tool are still registered at runtime,
  because a desktop app is a running app and has them. A phone gets a failure
  naming the tool it could not find rather than a file that imported to
  nothing.
- `currentCookTarget` is what the machine this is running on wants, since a
  runtime import has no target to choose and offering the choice would only be
  a way to get it wrong.
- The command's exit code is what a build reads: 0 when everything cooked, 1
  when an asset failed, and 2 when the arguments were wrong. Failures are
  repeated at the end rather than only where they happened, because the one
  line that matters should not be a thousand lines up.

## 0.3.0

- **Importers, and the cook that drives them.** `Importer` is the contract: a
  name, a version, the extensions it claims, the settings it reads and the
  other assets it depends on. `Cook` puts the steps in the only order that
  saves anything — settings resolved, dependencies found, key built, cache
  asked, and only then the work — and reports what happened per asset rather
  than throwing, so one unreadable texture does not end a build of a thousand.
- `CookTarget` is what a cook is cooking *for*, as plain data rather than a
  device profile, so `orblit_asset` still builds where the renderer does not.
  Only put things in it that change the bytes: a device's thread count changes
  how fast a cook runs and never what it writes, and putting it in the target
  would split the cache for nothing.
- `GltfImporter` finds the buffers and images a `.gltf` points at, reading a
  `.glb`'s JSON chunk for the same answer, and passes the bytes through —
  Filament's loader reads glTF well, and re-encoding it here would be work that
  changes nothing. `SceneImporter` collects what a scene places, matching keys
  by shape rather than by a fixed list, so a component added later is found
  without this being edited.
- `TextureImporter`, `ModelImporter`, `SplatImporter` and `EnvironmentImporter`
  drive `orblit_texture_cook`, `orblit_import`, `orblit_splat_cook` and
  Filament's `cmgen`. The flags come from `.import.json` and the target instead
  of from a file-name convention — `tool/cook_textures.sh` had to guess that a
  name ending `_normal` was a normal map, and a guess is wrong for exactly the
  assets whose names came from somewhere else.
- The native importers are absent on the web, the same way `DirectoryCookCache`
  is: a browser cannot start a program, and it does not need to, because it
  loads what a build machine cooked with `--target web`.

## 0.2.0

- **A cook cache.** `CookKey` writes down everything that decided what a cooked
  asset is — the source bytes, the files it read, which importer ran and its
  version, the resolved settings and the target — as canonical JSON, and hashes
  that. `CookCache` files results under it, so a rebuild with nothing changed
  cooks nothing and editing one texture recooks that texture alone. Raising an
  importer's version is what reaches caches that already exist, since a fix to
  an importer changes nothing on disk.
- `DirectoryCookCache` is the cache a machine shares between builds: bytes in a
  content store, a JSON index written whole or not at all, and a file lock so
  two builds cooking at once do not lose each other's entries. Give it
  `limitBytes` and it evicts oldest-first, counting shared bytes once and never
  taking a file out from under an entry that still points at it.
  `MemoryCookCache` is the same contract for tests.
- **Import settings.** An `.import.json` beside an asset says how to cook it,
  and one in a folder says it for everything under that folder — a folder of
  sprites is all pixel art — with the nearer file winning key by key.
  `ImportSettingsReader` works out what applies to an asset and reads each file
  once.

## 0.1.0

- First cut of `orblit_asset`. Pre-alpha: everything is subject to change.
