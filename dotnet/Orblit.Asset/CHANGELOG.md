# Changelog

## 0.25.0

- A port of the first part of `orblit_asset` 0.25.0. It holds `AssetId`,
  `ContentHash`, the content stores, the asset sources, `AssetManifest`,
  `ImportSettings`, `Importer` and `ImporterRegistry`, `CookKey` and the cook
  caches.
- A manifest, an import settings file and a cook cache directory read the
  same on both sides. The Dart cache and the C# cache write the same bytes.
- Not ported yet: packs, patches, the network sources, kits, glTF, runtime
  import, the cook and the importers.
- The C# cache holds its lock in `index.lock` with a `FileShare.None` open.
  Dart takes a POSIX byte lock, and a Dart build and a C# build on one
  directory at the same moment may not exclude each other.
