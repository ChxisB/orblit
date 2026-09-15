/// What an asset is called, and where its bytes are.
///
/// The ground floor of the asset pipeline. Nothing here cooks, caches by size
/// or fetches over a network; what it answers is the two questions everything
/// above has to agree on first. An [AssetId] is what a project calls a thing —
/// `models/robot.glb` — and a [ContentHash] is what the thing actually is,
/// byte for byte. An [AssetManifest] is the one place the two meet.
///
/// Keeping names and contents apart is the whole design. A name is what people
/// type and scenes store, so it has to stay put while the file behind it
/// changes. A hash is what caches and downloads can trust, so it has to change
/// whenever a single byte does. Anything that mixes the two ends up either
/// re-downloading what it already has or keeping what it should have thrown
/// away.
///
/// Reading is behind [AssetSource], so a directory while developing, a Flutter
/// bundle in a shipped app and a map in a test are interchangeable; storing by
/// hash is behind [ContentStore], for the same reason.
library;

export 'src/asset_id.dart' show AssetId;
export 'src/asset_manifest.dart'
    show AssetEntry, AssetManifest, AssetManifestLoad;
export 'src/asset_source.dart'
    show
        AssetNotFound,
        AssetSource,
        CallbackAssetSource,
        LayeredAssetSource,
        MemoryAssetSource;
export 'src/content_hash.dart' show ContentHash;
export 'src/content_store.dart' show ContentStore, MemoryContentStore;
export 'src/directory.dart' show DirectoryAssetSource, DirectoryContentStore;
