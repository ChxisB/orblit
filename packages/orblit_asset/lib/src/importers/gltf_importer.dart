import 'dart:convert';
import 'dart:typed_data';

import '../asset_id.dart';
import '../import_settings.dart';
import '../importer.dart';

/// Reads a `.gltf` or `.glb` and files it for loading, having first found
/// every file it points at.
///
/// A `.gltf` is JSON with its buffers and images beside it, so editing one of
/// those has to recook the model — which only happens if the scan below finds
/// them. A `.glb` has them inside it already and depends on nothing, but the
/// same importer handles both, because which one an artist exported is not a
/// decision the rest of the pipeline should have to care about.
///
/// The bytes are passed through rather than rewritten. Filament's loader reads
/// glTF directly and well; re-encoding it here would be work that changes
/// nothing, and a chance to lose something the loader understands. What the
/// cook adds is the dependency list and a stable key, which is what makes a
/// scene's rebuild correct.
class GltfImporter extends Importer {
  const GltfImporter();

  @override
  String get name => 'gltf';

  @override
  int get version => 1;

  @override
  Set<String> get extensions => const {'gltf', 'glb'};

  @override
  Map<String, Object?> resolveSettings(ImportSettings settings) => const {};

  @override
  Future<Set<AssetId>> dependenciesOf(
    AssetId id,
    Uint8List bytes,
    Map<String, Object?> settings,
  ) async {
    final json = id.extension == 'glb' ? _jsonChunkOf(id, bytes) : bytes;
    if (json == null) return const {};
    return urisIn(id, json);
  }

  @override
  Future<ImportResult> import(ImportRequest request) async =>
      ImportResult(outputs: {request.id.extension: request.bytes});

  /// Every file a glTF document refers to, as ids relative to the document.
  ///
  /// Exposed because a scene importer needs the same answer about a model it
  /// includes, and because a wrong answer here is the failure that is hardest
  /// to recognise later: the cook simply stops noticing an edit.
  ///
  /// Only `buffers` and `images` can carry a URI in glTF 2.0. Both may instead
  /// carry a data URI, which is bytes already in the file and therefore not a
  /// dependency, or a `bufferView`, same. A URI is percent-encoded, so it is
  /// decoded before it becomes an id.
  static Set<AssetId> urisIn(AssetId document, List<int> json) {
    final Object? parsed;
    try {
      parsed = jsonDecode(utf8.decode(json));
    } on FormatException catch (error) {
      throw ImportFailure(
        document,
        'is not readable as glTF JSON',
        cause: error,
      );
    }
    if (parsed is! Map<String, Object?>) {
      throw ImportFailure(
        document,
        'is not a glTF document: its JSON is not an object',
      );
    }

    final found = <AssetId>{};
    for (final section in const ['buffers', 'images']) {
      final entries = parsed[section];
      if (entries is! List) continue;
      for (final entry in entries) {
        if (entry is! Map<String, Object?>) continue;
        final uri = entry['uri'];
        if (uri is! String || uri.isEmpty) continue;
        if (uri.startsWith('data:')) continue;

        final relative = _decode(uri);
        if (relative == null) continue;
        final id = document.resolve(relative);
        if (id == null) {
          throw ImportFailure(
            document,
            'refers to "$uri", which is not a name inside the project. A glTF '
            'that reaches outside the project cannot be cooked, because the '
            'file it wants will not be there on another machine.',
          );
        }
        found.add(id);
      }
    }
    return found;
  }

  /// A URI with an absolute path or a scheme is not ours to resolve, and a
  /// relative one is percent-encoded. Returns null for the ones to leave
  /// alone.
  static String? _decode(String uri) {
    if (uri.contains(':')) return null; // http:, file:, or a Windows drive.
    try {
      return Uri.decodeComponent(uri);
    } on ArgumentError {
      return uri;
    }
  }

  /// The JSON chunk of a GLB, or null when the container is not one we read.
  ///
  /// A GLB is a 12-byte header — magic, version, length — then chunks of
  /// length, type, payload. The first chunk is the JSON. A malformed one is
  /// worth an error rather than an empty dependency list, because an empty
  /// list is indistinguishable from a model that depends on nothing, and that
  /// would cook silently and wrongly.
  static Uint8List? _jsonChunkOf(AssetId id, Uint8List bytes) {
    const magic = 0x46546C67; // 'glTF', little-endian.
    const jsonChunk = 0x4E4F534A; // 'JSON'.
    if (bytes.length < 20) {
      throw ImportFailure(
        id,
        'is too short to be a GLB (${bytes.length} bytes)',
      );
    }
    final view = ByteData.view(bytes.buffer, bytes.offsetInBytes, bytes.length);
    if (view.getUint32(0, Endian.little) != magic) {
      throw ImportFailure(
        id,
        'is named .glb but does not start with the glTF magic. An exporter '
        'that wrote a .gltf under a .glb name is the usual cause.',
      );
    }

    final length = view.getUint32(12, Endian.little);
    if (view.getUint32(16, Endian.little) != jsonChunk) {
      throw ImportFailure(
        id,
        "'s first chunk is not JSON, and a GLB's has to be",
      );
    }
    if (20 + length > bytes.length) {
      throw ImportFailure(
        id,
        'says its JSON chunk is $length bytes, which runs past the end of the '
        'file. It is truncated.',
      );
    }
    return Uint8List.sublistView(bytes, 20, 20 + length);
  }
}

/// Reads a scene file for the assets it names, so that editing a model
/// recooks the scene that places it.
///
/// A scene holds no bytes worth cooking — it is already the small file it
/// wants to be — so what this produces is the scene itself and, much more
/// importantly, the list of everything under it. That list is what a build
/// watches, and what decides whether a bundle carries a model at all.
class SceneImporter extends Importer {
  const SceneImporter();

  @override
  String get name => 'scene';

  @override
  int get version => 1;

  @override
  Set<String> get extensions => const {'oscene'};

  @override
  Map<String, Object?> resolveSettings(ImportSettings settings) => const {};

  @override
  Future<Set<AssetId>> dependenciesOf(
    AssetId id,
    Uint8List bytes,
    Map<String, Object?> settings,
  ) async {
    final Object? parsed;
    try {
      parsed = jsonDecode(utf8.decode(bytes));
    } on FormatException catch (error) {
      throw ImportFailure(id, 'is not readable as a scene', cause: error);
    }
    final found = <AssetId>{};
    _collect(id, parsed, found);
    return found;
  }

  @override
  Future<ImportResult> import(ImportRequest request) async =>
      ImportResult(outputs: {'oscene': request.bytes});

  /// Walks the whole document looking for asset references, rather than
  /// reading the fields a scene is known to have today.
  ///
  /// A scene's schema grows — a new component with a new texture field is a
  /// normal thing to add — and a collector that knew the schema would keep
  /// working while quietly missing the new field. Since every reference in a
  /// scene is a string under a key that says what it is, looking at every such
  /// string finds them all, including the ones added after this was written.
  static void _collect(AssetId scene, Object? node, Set<AssetId> into) {
    if (node is List) {
      for (final child in node) {
        _collect(scene, child, into);
      }
      return;
    }
    if (node is! Map<String, Object?>) return;
    for (final entry in node.entries) {
      final value = entry.value;
      if (value is String && _namesAnAsset(entry.key)) {
        final id = _reference(scene, value);
        if (id != null) into.add(id);
      } else {
        _collect(scene, value, into);
      }
    }
  }

  /// What a scene's reference names.
  ///
  /// A scene holds asset ids, not paths: `models/floor.glb` is that asset
  /// wherever the scene lives, so moving a scene between folders does not
  /// break it. This is the opposite of glTF, which resolves its URIs against
  /// the document, and the difference is deliberate — glTF is a format the
  /// engine reads rather than one it defines, and its rule is the spec's.
  ///
  /// A reference that starts `./` or `../` is the exception, and is read
  /// against the scene's own folder. It is spelled that way precisely to say
  /// "near me", and a scene generated beside its assets uses it.
  ///
  /// A string without a file extension is not a reference. A `material` field
  /// naming a material in the scene's own list, or `"matte white"`, is a
  /// normal thing for a scene to hold, and it is not a file. Treating it as
  /// one would make the cook fail on a scene that is perfectly valid — and
  /// the failure would read "no such asset: matte white", which explains
  /// nothing.
  ///
  /// Every asset this pipeline cooks has an extension, so requiring one costs
  /// nothing and removes the whole class of false reference.
  static AssetId? _reference(AssetId scene, String value) {
    final id = value.startsWith('./') || value.startsWith('../')
        ? scene.resolve(value)
        : AssetId.tryParse(value);
    return id != null && id.extension.isNotEmpty ? id : null;
  }

  /// Which keys hold an asset reference.
  ///
  /// A suffix rather than a fixed list, so that `baseColorTexture` and
  /// `emissiveTexture` are both found without either being named here. The
  /// cost of a wrong guess is a dependency too many, which recooks once too
  /// often — far cheaper than the other mistake.
  static bool _namesAnAsset(String key) {
    const suffixes = [
      'asset',
      'Asset',
      'model',
      'Model',
      'texture',
      'Texture',
      'mesh',
      'Mesh',
      'splats',
      'Splats',
      'environment',
      'Environment',
      'material',
      'Material',
      'sprite',
      'Sprite',
      'atlas',
      'Atlas',
    ];
    return suffixes.any(key.endsWith);
  }
}
