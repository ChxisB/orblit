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
- Not ported yet: kit packing and extracting, the kit index, lock and store,
  glTF, runtime import, the cook and the importers.
- The C# cache holds its lock in `index.lock` with a `FileShare.None` open.
  Dart takes a POSIX byte lock, and a Dart build and a C# build on one
  directory at the same moment may not exclude each other.
