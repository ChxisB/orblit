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
  read the same on both sides. The Dart writers and the C# writers give the
  same bytes, so a pack or a patch that one wrote opens in the other.
- `PackBundle.PackAsync` is the C# name for `packBundle`.
- Not ported: `bundled_pack_io`, which finds a pack in a Flutter asset
  folder. A .NET app opens its pack with `FilePackBytes`.
- A third part holds the network contract: `AssetOrigin`, `FetchPolicy`,
  `FetchRequest`, `FetchReply`, `IAssetTransport` and `MapTransport`. It also
  holds fetch records with their codec and three stores, `RangeQueue`,
  `PictureSize`, `Ktx2Chain` and `WebUrl`. `WebUrl` stands in for Dart `Uri`.
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
  reads and writes the same bytes as the Dart twin. A release address must be
  an https name that is public, on port 443, with no user name. A day is
  checked as Dart checks it, so year 0000 is read.
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
  over an internal class here. Output and exit codes match the Dart
  commands, but the text names the new tools.
- A tenth part holds the glTF document: `GltfDocument` reads a GLB or a
  glTF with its buffers, edits the JSON in place and writes one GLB.
  `ComponentType`, the accessor reads and adds, `TextureRole`,
  `TextureRoles` and `TextureSlot` come with it. The accessors are part of
  `GltfDocument`, because `Orblit.Mesh` has a `GltfAccessors` already.
- A buffer URI is percent decoded twice, as Dart does. A bad base64
  payload has no cause. A MAT2 or MAT3 accessor is read as packed.
- An eleventh part holds `CollisionMesh`: the constructor, `ToJson`,
  `Encode`, `Decode`, `FromObj` and `FromGltfAsync`. A glTF walk is
  `ModelGeometry`, internal. A scene, a node tree or a bare mesh list is
  read the way Dart reads it, and the refusal text of each case is the same.
- The node walk stops at 512 nested nodes with the cyclic hierarchy error.
  Dart has no limit, and a .NET stack that runs out ends the process. A
  collision mesh version of `1.0` is accepted, as `1.0 == 1` in Dart. An
  index past 31 bits is saturated, so it is refused as outside the mesh.
- Not ported yet: glTF merge, bake and atlas, runtime import, the cook and
  the importers. The cook test of the collision sidecar waits for them.
- The C# cache holds its lock in `index.lock` with a `FileShare.None` open.
  Dart takes a POSIX byte lock, and a Dart build and a C# build on one
  directory at the same moment may not exclude each other.
