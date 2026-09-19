/// What an asset is called, where its bytes are, and what cooking it produced.
///
/// The ground floor of the asset pipeline. Nothing here reads a model, encodes
/// a texture or fetches over a network; what it answers is the questions
/// everything above has to agree on first. An [AssetId] is what a project
/// calls a thing — `models/robot.glb` — and a [ContentHash] is what the thing
/// actually is, byte for byte. An [AssetManifest] is the one place the two
/// meet.
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
///
/// Cooking is expensive and mostly repeated, so a [CookCache] keeps what it
/// produced. What makes that safe rather than merely fast is [CookKey], which
/// writes down everything that decided the result — not only the file, but the
/// files it read, the importer and its version, the settings and the target
/// device. A key that leaves any of those out answers "already done" when the
/// truth is "done differently", and the build then ships bytes no clean build
/// will reproduce. [ImportSettings] is where a project says how a file should
/// be cooked, since a file never says what it is.
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
export 'src/cook_cache.dart'
    show CookCache, CookFailure, CookOutput, CookedAsset, MemoryCookCache;
export 'src/cook_cache_directory.dart' show DirectoryCookCache;
export 'src/cook_key.dart' show CookKey;
export 'src/cook_project.dart'
    show
        CookFailed,
        CookProject,
        bundleHash,
        cookDuringBuild,
        currentCookTarget;
export 'src/cook_targets.dart' show CookTargets;
export 'src/cook.dart'
    show
        Cook,
        CookReport,
        CookResult,
        CookState,
        CookStatus,
        CookTargetProperties,
        TextureFamily;
export 'src/directory.dart' show DirectoryAssetSource, DirectoryContentStore;
export 'src/import_settings.dart' show ImportSettings, ImportSettingsReader;
export 'src/importers/atlas_importer.dart' show AtlasImporter;
export 'src/importers/gltf_importer.dart' show GltfImporter, SceneImporter;
export 'src/importers/native_importers.dart';
export 'src/runtime_import.dart' show RuntimeImport;
export 'src/importer.dart'
    show
        CookTarget,
        ImportFailure,
        ImportRequest,
        ImportResult,
        Importer,
        ImporterRegistry;
