# Changelog

## 0.25.0

- A port of the first two parts of `orblit_asset` 0.25.0. The first holds
  `AssetId`, `ContentHash`, the content stores, the asset sources,
  `AssetManifest`, `ImportSettings`, `Importer` and `ImporterRegistry`,
  `CookKey` and the cook caches. The second holds packs and patches:
  `AssetPack`, `PackBundle`, `MountedPacks`, `PackDiff`, `PatchChain`,
  `PackFolder` (list and remove), `PackName`, `FilePackBytes`,
  `MemoryPackBytes`, `CookTargets` and `TextureFamily`.
- A manifest, an import settings file, a cook cache directory and a pack
  read the same on every machine. Every writer gives the same bytes, so a
  pack or a patch that one wrote opens in another.
- `PackBundle.PackAsync` is the C# name for `packBundle`.
- Not ported: `bundled_pack_io`, which finds a pack in a Flutter asset
  folder. A .NET app opens its pack with `FilePackBytes`.
- A third part holds the network contract: `AssetOrigin`, `FetchPolicy`,
  `FetchRequest`, `FetchReply`, `IAssetTransport` and `MapTransport`. It also
  holds fetch records with their codec and three stores, `RangeQueue`,
  `PictureSize`, `Ktx2Chain` and `WebUrl`. `WebUrl` reads a URL by the rules of RFC 3986.
- A fetch record time without a zone reads as UTC, and a year outside 1 to
  9999 is dropped.
- A fourth part holds the fetcher: `AssetFetcher` with `Fetch`, `FetchUrl`
  and `ReadAsync`, `FetchJob`, `FetchUrgency`, `FetchProgress`, the three
  fetch exceptions, `NetworkAssetSource` and `ProgressiveFetch`. `Dispose`
  stands in for `close`.
- A callback that throws counts as a failed attempt. The fetcher runs its
  callbacks outside its lock.
- A fifth part holds `HttpTransport` over `HttpClient`, `RangePackBytes` and
  `PackRangeFailedException`, and `PackFolder.DownloadAsync` with `PackOffer`.
  A download is filed under its hash only after it has been checked as a
  pack, and one that was cut off resumes from the bytes held.
- A body that fails in the network is a `SocketFailureException`, which a
  download retries. A disk `IOException` is not retried.
- A sixth part opens the kit format: `KitManifest`, `KitLicence`, `KitEngine`, `KitRules`,
  `KitUpdatePlan` and `Kit`. A kit is a pack for the `source` target that holds
  `kit.json`. The kit rules and the manifest are public so a separate tool can
  call them. A licence address with a port past 65535 is refused.
- A seventh part holds the files a kit store publishes and a project keeps:
  `KitEntry` with `KitRelease`, `KitIndex` with `KitRow`, `KitReleases` with
  `PublishedRelease`, `KitLock` with `LockedKit`, and `KitStoreRules`. Each
  reads and writes the same bytes every time. A release address must be
  an https name that is public, on port 443, with no user name. A day is
  checked by the calendar, so year 0000 is read.
- A release address and a licence address with a port past 65535 are
  refused in C#, as `WebUrl` refuses every such address.
- An eighth part holds the kit tools: `KitPacking` (pack and publish),
  `KitExtraction`, `KitStoreClient` with `KitStoreFailedException`, and
  `KitStoreBuilder`. A release is a `DateTime` written `yyyy-MM-dd`. A folder
  is walked in id order. The client has no cancellation token. A wait that
  runs out stops waiting, and the request goes on.
- A ninth part holds the command lines. `orblit-kit` runs `pack`, `check`,
  `extract`, `publish` and `index`, and `orblit-pack` runs `build`, `list`,
  `check` and `diff`. Each is its own project, `Orblit.Kit` and `Orblit.Pack`,
  over an internal class here. Output and exit codes are fixed, and
  the text names the tools.
- A tenth part holds the glTF document: `GltfDocument` reads a GLB or a
  glTF with its buffers, edits the JSON in place and writes one GLB.
  `ComponentType`, the accessor reads and adds, `TextureRole`,
  `TextureRoles` and `TextureSlot` come with it. The accessors are part of
  `GltfDocument`, because `Orblit.Mesh` has a `GltfAccessors` already.
- A buffer URI is percent decoded twice. A bad base64
  payload has no cause. A MAT2 or MAT3 accessor is read as packed.
- An eleventh part holds `CollisionMesh`: the constructor, `ToJson`,
  `Encode`, `Decode`, `FromObj` and `FromGltfAsync`. A glTF walk is
  `ModelGeometry`, internal. A scene, a node tree or a bare mesh list is
  read by fixed rules, and the refusal text of each case is fixed.
- The node walk stops at 512 nested nodes with the cyclic hierarchy error.
  A .NET stack that runs out ends the process. A
  collision mesh version of `1.0` is accepted, as `1.0` is the number 1. An
  index past 31 bits is saturated, so it is refused as outside the mesh.
- A twelfth part holds the glTF edits: `GltfMerge` (`MergeMaterials`,
  `MergePrimitives`, `PrimitiveGroupKey`) and `GltfBake.BakeInstancesAsync`.
  A bake returns null when the model cannot be baked. A transform that is not
  a number throws.
- A bake keeps the first template of its batches for every later batch,
  so a later batch carries the `extras` of the first. An index at
  or past 2^31 throws `OverflowException`.
- A thirteenth part holds the glTF atlas: `AtlasPixels` (`Resample`,
  `Renormalise`, `MultiplyInto`, `Blit`, `BlankPage`), `Rgba` and
  `ModelAtlas.AtlasModelAsync`, which finds the materials worth packing,
  packs their maps onto shared pages, remaps the UVs and merges what
  became identical. The cook decodes the images and passes the decoder in.
- `Rgba` throws on a pixel length that does not fit its size. An image index
  outside the document throws `ArgumentOutOfRangeException`.
- A fourteenth part holds the surface translator: `SurfaceTranslator.Translate`
  turns an `.oshader` surface into a Filament material source with its
  uniform parameters, `SurfaceLines` expands includes and blanks comments,
  `SurfaceLiterals` reads hints and defaults, and `SurfaceDiagnostics` maps a
  compiler message back to the file and line it came from. A refusal is a
  `SurfaceException` with the file, the line and the reason.
- The file formats' regex classes are written out in `CharClasses`, because
  .NET reads `\s`, `\w`, `\d`, `.` and `$` more widely than the formats do.
- A fifteenth part holds the cook: `Cook` with `CookOneAsync`, `StateOfAsync`,
  `StateOfAllAsync` and `CookAllAsync`, `CookResult`, `CookReport`, `CookStatus`,
  `CookState` and `CookTargetProperties`. A cook builds the key before it runs an
  importer, and a cached result runs none.
- `CookResult.Error` is an `Exception`, so the stack trace is on it and there is
  no `trace` field. `TextureFamilies`, `MaxTextureSize` and `HalfFloatTextures`
  are extension methods on `CookTarget`. `MaxTextureSize` needs a whole number
  boxed as a `long`, and a bad property throws `ArgumentException` naming it.
- Workers run on threads. `CookAllAsync` calls `onResult` one call at a time, and
  a dependency that many assets share is read once.
- Not ported yet: the cook of a project and runtime import. The cook test
  of the collision sidecar waits for them.
- A sixteenth part holds the importers that drive a native program:
  `NativeTool`, `TextureImporter`, `ModelImporter`, `SplatImporter`,
  `EnvironmentImporter` and `MaterialImporter`. `NativeImporters.Create()`
  returns the five, and `NativeWork` holds the temporary directory and the
  path helpers they share.
- A program is found by an explicit path, then its environment variable,
  then `NativeTool.SearchPath`, then `which` or `where`. `ORBLIT_TOOL_PATH`
  adds directories to the search, split on a colon. A bad exit is an
  `ImportFailureException` that carries what the program wrote, errors first.
- `MaterialImporter` asks matc for its version once. A failed ask is not kept.
  `CookingNotSupported` is not ported, by decision 71.
- The C# cache holds its lock in `index.lock` with a `FileShare.None` open.
  A POSIX byte lock taken by another tool on one
  directory at the same moment may not exclude it.
- A seventeenth part holds the glTF and scene importers. `GltfImporter`
  passes a model through, or with `"atlas": true` packs its textures onto
  shared pages through `ModelAtlasCook` and merges the materials and
  primitives that come to match. `"collision": true` adds `collision.json`.
  `SceneImporter` reads a scene for the assets it names. Neither is in
  `NativeImporters.Create()`.
- `DependenciesOfAsync` of both runs on the thread pool, so a file that
  cannot be read faults the task and does not throw at the call.
- The atlas cook needs `orblit_texture_cook`. Pages are held to the largest
  size the target samples, or 8192 when it names none, and the cook says so.
- An eighteenth part holds `AtlasImporter`. A `.atlas.json` document names
  sprites, and the importer packs them onto pages through `AtlasPacker`.
  It writes `page{N}.png` and `page{N}.json` for each page and says how full
  each page is. Settings given at the call win over the document's own.
  It is not in `NativeImporters.Create()`.
- A setting of the wrong type fails with `InvalidCastException` and a text
  such as `type 'String' is not a subtype of type 'int?' in type
  cast`. A page size or count out of range throws `ArgumentException` with
  `Invalid argument (key): …`.
- `DependenciesOfAsync` runs on the thread pool, so a document that is not
  JSON faults the task and does not throw at the call.
- The test that the project and the runtime registries both hold the atlas
  importer waits for part F5, which holds the runtime one.
- A nineteenth part holds `TerrainImporter` and `CookedTerrain`. A terrain
  is an `.oterrain` file and an `.oregion` file for each region it lists.
  Neither is cooked into anything smaller. The importer checks a region
  against its own name and a terrain against each region's key and size,
  and a terrain depends on its regions and on every image, model and
  material its sets and scatter layers name. It is not in
  `NativeImporters.Create()`.
- The bundle files the outputs as `<asset id>.<output>`, so `world.oterrain`
  becomes `world.oterrain.oterrain`. `CookedTerrain.OpenAsync` reads that
  back from a bundle or a pack, and names a listed region the pack does not
  hold in `Problems` and never asks for it.
- `Orblit.Asset` now references `Orblit.Terrain`. Inside this namespace the
  name `Terrain` is that namespace, so the files that need the type use
  `using TerrainData = Orblit.Terrain.Terrain;`.
- Four of the 27 tests of the part cook through `CookProject`. They are
  ported with part F4. The one that builds the runtime registry waits for
  part F5.
- A twentieth part holds `CookProject`, `BuildHook`, `BundleHash` and
  `CookFailedException`. A project cooks everything under an assets
  folder for one target and writes the bundle and its manifest. The hook
  cooks, or cooks and packs, for the platform a build names, and reports
  every file and folder it read.
- `BuildHook.CookDuringBuildAsync` and `PackDuringBuildAsync` are the C#
  names for `cookDuringBuild` and `packDuringBuild`. `BundleHash.Of` is
  `bundleHash`. `CookProject.CurrentCookTarget` throws
  `NotSupportedException` for a platform Orblit does not cook for. The
  `out` parameter is `@out`.
- A link under the assets folder is not followed, and a hidden file is
  listed and then left out by name. A folder that cannot be
  read stops the listing. A bundle file is written to a temporary file
  and renamed.
- A string in an `Invalid argument (name): …` message is quoted. A list or a
  map prints as its contents.
- A last part holds `RuntimeImport` and the cook command. `RuntimeImport`
  imports one file a running app was handed: `NameFor`, `Handles`,
  `ImportAsync`, `ReadAsync` and `ReadOnlyAsync`. `ImportSettingsReader` is
  open and its `ForAssetAsync` virtual, so the settings given at the call
  can be laid over the file's own.
- `orblit-cook` is the tool for the cook command. It is `Orblit.Cook` over
  an internal `CookCli` here, as the kit and pack tools are. The exit codes
  are fixed. A missing assets folder prints .NET's own text.
- `RuntimeImport.DefaultImporters()` returns `CookProject.DefaultImporters()`,
  so there is one list.
- A pack header whose directory start or size passes 63 bits is refused,
  and a blob is checked against the directory without a sum that can wrap.
  A kit host can send such a
  pack, so it ends in a `FormatException` here.
- A fetch or pack download shares `Random.Shared` for its jitter, and the
  retry wait stops growing at one day. A long loop of retries no longer
  passes what `Task.Delay` takes.
- `orblit-cook` ends with 1 and a `cook:` line when the disk refuses a
  write.
