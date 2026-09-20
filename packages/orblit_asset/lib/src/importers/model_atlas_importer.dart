import 'dart:convert';
import 'dart:io';
import 'dart:typed_data';

import 'package:orblit_sprite/orblit_sprite.dart'
    show PngFormatException, decodePng, encodePng;

import '../cook.dart';
import '../gltf/atlas_pixels.dart';
import '../gltf/document.dart';
import '../gltf/model_atlas.dart';
import '../gltf/texture_roles.dart';
import '../importer.dart';
import 'native_tool.dart';

/// Cooks a model into one self-contained GLB with its textures packed onto
/// shared pages.
///
/// Run from [GltfImporter] when a model's settings ask for it, and separate
/// from it because it is a different kind of work: the pass-through importer
/// runs anywhere and touches nothing, while this reads every texture, decodes
/// it, packs it, rewrites the document and cooks the result — a build machine's
/// job, done once, so that the device does none of it.
///
/// What comes out is a GLB that names its pages as siblings, in the same shape
/// Phase 4 gave every other cooked texture: `atlas0.basecolour.ktx2` with
/// `atlas0.basecolour.bc.ktx2` and the rest beside it, and the loader taking
/// the first one its device can sample.
class ModelAtlasCook {
  ModelAtlasCook({NativeTool? tool})
    : tool =
          tool ??
          NativeTool(
            'orblit_texture_cook',
            environmentVariable: 'ORBLIT_TEXTURE_COOK',
          );

  final NativeTool tool;

  Future<ImportResult> run(
    ImportRequest request,
    Map<String, Object?> settings,
  ) async {
    final notes = <String>[];
    final document = await GltfDocument.read(
      request.id,
      request.bytes,
      request.source,
    );

    final families = request.target.textureFamilies;
    // Always basis, whatever the target asked for. A model names its textures
    // by URI and gltfio fetches the name it is given, so `atlas0.basecolour
    // .ktx2` has to be a file that exists — the family siblings beside it are
    // for the loader to prefer once the model path picks between them, the way
    // the material path already does.
    final targets = <String>{
      TextureFamily.basis,
      ...(families.isEmpty ? TextureFamily.all : families).where(
        (family) => family != TextureFamily.raw,
      ),
    }.toList();
    final budget = request.target.maxTextureSize ?? 8192;
    final maxPageSize = (settings['maxPageSize'] as int).clamp(64, budget);
    if (maxPageSize != settings['maxPageSize']) {
      notes.add(
        'pages held to ${maxPageSize}px, which is what this target '
        'samples',
      );
    }

    return withTemporaryDirectory('model_atlas', (work) async {
      // Keyed on the image rather than on its index, because dropping the
      // images the atlas swallowed renumbers every one after them.
      final bytes = <Map<String, Object?>, Uint8List?>{};

      Future<Uint8List?> bytesOf(Map<String, Object?> image) async {
        if (bytes.containsKey(image)) return bytes[image];
        return bytes[image] = await _imageBytes(request, document, image);
      }

      final result = await atlasModel(
        document,
        options: ModelAtlasOptions(
          maxPageSize: maxPageSize,
          padding: settings['padding'] as int,
          extrude: settings['extrude'] as int,
          maxCellSize: settings['maxCellSize'] as int,
          minMaterials: settings['minMaterials'] as int,
          mergePrimitives: settings['mergePrimitives'] as bool,
          maxMergedVertices: settings['maxMergedVertices'] as int,
        ),
        decode: (index, definition) async {
          final source = await bytesOf(definition);
          if (source == null) return null;
          return _decode(request, work, source, definition);
        },
      );
      notes.addAll(result.notes);

      if (!result.changed) {
        // Nothing was packed, so the document was never edited and the bytes
        // that came in are still the right answer. Re-emitting a GLB built
        // from them would be a different file for no gain, and a different
        // file is a cache miss for everything downstream.
        notes.add('left as it was');
        return ImportResult(
          outputs: {request.id.extension: request.bytes},
          notes: notes,
        );
      }

      final outputs = <String, List<int>>{};

      // The pages, cooked with the flags their role calls for. A normal map
      // encoded as colour and a colour map encoded as a normal map are both
      // wrong in ways that read as a lighting bug, which is why the role is
      // carried this far rather than guessed at from the file name.
      for (final page in result.pages) {
        outputs.addAll(
          await _cook(
            request,
            work,
            '${page.stem}.png',
            encodePng(page.image.width, page.image.height, page.image.pixels),
            page.role,
            targets,
            budget,
          ),
        );
      }

      // Before cooking anything else, throw away what the atlas swallowed.
      // A packed material's old texture is still in the document, pointing at
      // its old image — nothing samples either, and cooking that image would
      // be the most expensive part of this run spent on a file no one reads.
      _dropUnused(document);

      // Then everything the packed materials did not take. A model half cooked
      // is a model that draws its other half untextured, and the source PNG
      // the URI still names is not beside the cooked file.
      outputs.addAll(
        await _cookTheRest(
          request,
          work,
          document,
          bytesOf,
          targets,
          budget,
          notes,
        ),
      );

      outputs['glb'] = document.toGlb();
      return ImportResult(outputs: outputs, notes: notes);
    });
  }

  /// One image's bytes, wherever glTF let it be put.
  Future<Uint8List?> _imageBytes(
    ImportRequest request,
    GltfDocument document,
    Map<String, Object?> image,
  ) async {
    final view = image['bufferView'];
    if (view is int) return document.viewBytes(view);

    final uri = image['uri'];
    if (uri is! String || uri.isEmpty) return null;
    if (uri.startsWith('data:')) {
      final comma = uri.indexOf(',');
      if (comma < 0 || !uri.substring(0, comma).endsWith(';base64')) {
        return null;
      }
      try {
        return base64Decode(uri.substring(comma + 1));
      } on FormatException {
        return null;
      }
    }
    if (uri.contains(':')) return null; // Not ours to fetch.
    final id = request.id.resolve(_percentDecoded(uri));
    if (id == null) return null;
    try {
      return await request.source.read(id);
    } on Object {
      return null;
    }
  }

  /// `source` as RGBA8.
  ///
  /// PNG is decoded here because the codec is already in the repository and a
  /// cook that needs no native tool is a cook that runs in a test. Everything
  /// else goes to `orblit_texture_cook`, which already decodes JPEG and KTX2
  /// on its way to encoding them — one flag on a tool that exists beats a
  /// JPEG decoder written in Dart.
  Future<Rgba?> _decode(
    ImportRequest request,
    Directory work,
    Uint8List source,
    Map<String, Object?> definition,
  ) async {
    if (_looksLikePng(source)) {
      try {
        final png = decodePng(source);
        return Rgba(png.width, png.height, png.pixels);
      } on PngFormatException {
        return null;
      }
    }
    if (tool.locate() == null) return null;

    final input = File(inside(work, 'decode.${_extensionOf(definition)}'));
    await input.writeAsBytes(source, flush: true);
    final out = File(inside(work, 'decode.raw'));
    await tool.run([
      input.path,
      inside(work, ''),
      '--decode',
      out.path,
      '--quiet',
    ], on: request.id);
    if (!out.existsSync()) return null;

    final raw = await out.readAsBytes();
    await out.delete();
    if (raw.length < 8) return null;
    final header = ByteData.sublistView(raw, 0, 8);
    final width = header.getUint32(0, Endian.little);
    final height = header.getUint32(4, Endian.little);
    if (raw.length - 8 != width * height * 4) return null;
    return Rgba(width, height, Uint8List.sublistView(raw, 8));
  }

  /// Cooks the images the pack did not take, and points them at the result.
  Future<Map<String, List<int>>> _cookTheRest(
    ImportRequest request,
    Directory work,
    GltfDocument document,
    Future<Uint8List?> Function(Map<String, Object?>) bytesOf,
    List<String> targets,
    int budget,
    List<String> notes,
  ) async {
    final roles = rolesOf(document).roles;
    final images = document.list('images');
    final textures = document.list('textures');

    // Which images the atlas added — they are cooked already.
    final cooked = <int>{};
    for (final texture in textures) {
      final extensions = texture['extensions'];
      if (extensions is! Map<String, Object?>) continue;
      final basis = extensions['KHR_texture_basisu'];
      if (basis is Map<String, Object?> && basis['source'] is int) {
        cooked.add(basis['source'] as int);
      }
    }

    final outputs = <String, List<int>>{};
    final names = <String>{};
    for (var t = 0; t < textures.length; t++) {
      final source = textures[t]['source'];
      if (source is! int || source < 0 || source >= images.length) continue;
      if (cooked.contains(source)) continue;

      final bytes = await bytesOf(images[source]);
      if (bytes == null) {
        notes.add(
          'image $source could not be read, so the material using it '
          'will draw untextured',
        );
        continue;
      }
      final role = roles[t] ?? TextureRole.baseColour;
      var stem = _stemFor(images[source], source);
      while (!names.add(stem)) {
        stem = '${stem}_';
      }
      outputs.addAll(
        await _cook(
          request,
          work,
          '$stem.${_extensionOf(images[source])}',
          bytes,
          role,
          targets,
          budget,
        ),
      );

      images[source]
        ..remove('bufferView')
        ..['uri'] = '$stem.ktx2'
        ..['mimeType'] = 'image/ktx2';
      textures[t]
        ..remove('source')
        ..['extensions'] = <String, Object?>{
          ...?(textures[t]['extensions'] as Map<String, Object?>?),
          'KHR_texture_basisu': <String, Object?>{'source': source},
        };
      cooked.add(source);
    }
    return outputs;
  }

  /// Runs the texture cook over one image and returns what it wrote.
  Future<Map<String, List<int>>> _cook(
    ImportRequest request,
    Directory work,
    String name,
    List<int> bytes,
    TextureRole role,
    List<String> targets,
    int budget,
  ) async {
    final input = File(inside(work, name));
    await input.writeAsBytes(bytes, flush: true);
    final out = Directory(inside(work, 'cooked'));
    if (out.existsSync()) await out.delete(recursive: true);
    await out.create();

    await tool.run([
      input.path,
      beside(out.path, ''),
      if (targets.isNotEmpty) ...['--targets', targets.join(',')],
      if (role.srgb) '--srgb' else '--linear',
      if (role == TextureRole.normal) '--normal',
      if (role == TextureRole.occlusion) '--single-channel',
      '--max-size',
      '$budget',
      '--uastc',
      '2',
      '--zstd',
      '19',
      '--quiet',
    ], on: request.id);

    final written = <String, List<int>>{};
    await for (final file in out.list()) {
      if (file is! File) continue;
      written[file.path.split(Platform.pathSeparator).last] = await file
          .readAsBytes();
    }
    await input.delete();
    return written;
  }

  /// Removes the textures and images nothing points at any more, and
  /// renumbers what is left.
  ///
  /// Worth doing rather than leaving them: gltfio asks a model for the files
  /// it names and the loader reads every one of them before drawing anything,
  /// so an image left behind after its material moved onto a page is a file
  /// read, a decode and a texture upload for something never sampled — and,
  /// run before the rest are cooked, a whole texture cook saved as well.
  ///
  /// Textures go first because they are what keeps the images alive: a packed
  /// material stopped naming its old texture, but the old texture still names
  /// its old image, so dropping images alone would drop nothing at all.
  void _dropUnused(GltfDocument document) {
    // What a material names, found by walking the material rather than by
    // listing the slots this file knows: `clearcoatTexture` and the rest of
    // the KHR_materials_* maps are texture references too, and a material
    // carrying one was left unpacked precisely because they are not
    // understood. Dropping a texture one of them names would break it.
    final naming = <Map<String, Object?>>[];
    void walk(Object? value) {
      if (value is List) {
        for (final one in value) {
          walk(one);
        }
        return;
      }
      if (value is! Map<String, Object?>) return;
      if (value['index'] is int) naming.add(value);
      for (final entry in value.values) {
        walk(entry);
      }
    }

    for (final material in document.list('materials')) {
      walk(material);
    }
    _keep(
      document,
      'textures',
      {for (final slot in naming) slot['index'] as int},
      naming,
      'index',
    );

    // Then the images, which only a surviving texture can keep alive.
    final sourcing = <Map<String, Object?>>[];
    for (final texture in document.list('textures')) {
      sourcing.add(texture);
      final extensions = texture['extensions'];
      if (extensions is! Map<String, Object?>) continue;
      for (final entry in extensions.values) {
        if (entry is Map<String, Object?>) sourcing.add(entry);
      }
    }
    _keep(
      document,
      'images',
      {
        for (final holder in sourcing)
          if (holder['source'] is int) holder['source'] as int,
      },
      sourcing,
      'source',
    );
  }

  /// Keeps only the entries of `document.json[key]` that [used] names, and
  /// points every [holder]'s [field] at where its entry moved to.
  void _keep(
    GltfDocument document,
    String key,
    Set<int> used,
    List<Map<String, Object?>> holders,
    String field,
  ) {
    final list = document.list(key);
    if (list.isEmpty || used.length == list.length) return;

    final kept = <Map<String, Object?>>[];
    final to = <int, int>{};
    for (var i = 0; i < list.length; i++) {
      if (!used.contains(i)) continue;
      to[i] = kept.length;
      kept.add(list[i]);
    }
    for (final holder in holders) {
      final at = holder[field];
      if (at is int && to.containsKey(at)) holder[field] = to[at];
    }
    document.json[key] = kept;
  }

  static String _stemFor(Map<String, Object?> image, int index) {
    final uri = image['uri'];
    if (uri is String && uri.isNotEmpty && !uri.startsWith('data:')) {
      final name = _percentDecoded(uri).split('/').last;
      final dot = name.lastIndexOf('.');
      final stem = (dot > 0 ? name.substring(0, dot) : name).replaceAll(
        RegExp(r'[^A-Za-z0-9_.-]'),
        '_',
      );
      if (stem.isNotEmpty) return stem;
    }
    final name = image['name'];
    if (name is String && name.isNotEmpty) {
      final cleaned = name.replaceAll(RegExp(r'[^A-Za-z0-9_.-]'), '_');
      if (cleaned.isNotEmpty) return cleaned;
    }
    return 'image$index';
  }

  static String _extensionOf(Map<String, Object?> image) {
    final mime = image['mimeType'];
    if (mime == 'image/png') return 'png';
    if (mime == 'image/jpeg') return 'jpg';
    if (mime == 'image/ktx2') return 'ktx2';
    final uri = image['uri'];
    if (uri is String) {
      final dot = uri.lastIndexOf('.');
      if (dot > 0 && dot < uri.length - 1) {
        final extension = uri.substring(dot + 1).toLowerCase();
        if (RegExp(r'^[a-z0-9]{2,5}$').hasMatch(extension)) return extension;
      }
    }
    return 'png';
  }

  static bool _looksLikePng(Uint8List bytes) =>
      bytes.length > 8 &&
      bytes[0] == 137 &&
      bytes[1] == 80 &&
      bytes[2] == 78 &&
      bytes[3] == 71;

  static String _percentDecoded(String uri) {
    try {
      return Uri.decodeComponent(uri);
    } on ArgumentError {
      return uri;
    }
  }
}
