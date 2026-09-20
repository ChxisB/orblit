import 'dart:math' as math;
import 'dart:typed_data';

import 'package:orblit_sprite/orblit_sprite.dart'
    show AtlasPackOptions, AtlasSprite, packAtlas;

import 'accessor.dart';
import 'atlas_pixels.dart';
import 'document.dart';
import 'merge.dart';
import 'texture_roles.dart';

/// How a model's textures are packed.
class ModelAtlasOptions {
  const ModelAtlasOptions({
    this.maxPageSize = 2048,
    this.padding = 4,
    this.extrude = 2,
    this.maxCellSize = 0,
    this.minMaterials = 2,
    this.mergePrimitives = true,
    this.maxMergedVertices = 65536,
  });

  /// The largest a page may be. Comes from the target's texture budget, not
  /// from a constant — a page the device has to halve on load is a page that
  /// was packed at the wrong size.
  final int maxPageSize;

  /// Texels left around each cell, which the extrusion is written into.
  final int padding;

  /// How far a cell's edge is copied outwards. Clamped to [padding].
  final int extrude;

  /// A cap on a single material's cell, or nought for none.
  ///
  /// Worth setting when a model mixes a 4096 hero texture with fifty 256
  /// props: without it the hero decides the page size and the props get a
  /// few texels each.
  final int maxCellSize;

  /// How many materials have to be packable before packing is worth it.
  ///
  /// One material in an atlas saves nothing and costs a rewrite, so the
  /// default declines to do it.
  final int minMaterials;

  /// Whether primitives that end up sharing a material are merged.
  ///
  /// This is where the frame time actually comes from. Sharing a texture
  /// saves texture binds; sharing a *draw* saves the draw, and a kitbashed
  /// model is usually a hundred small primitives that become one.
  final bool mergePrimitives;

  /// The vertex count a merged primitive will not be pushed past.
  ///
  /// 65,536 rather than a larger round number on purpose: one more vertex
  /// forces 32-bit indices, which doubles the index bandwidth of every draw
  /// for the rest of the model's life. Two draws are cheaper than that.
  final int maxMergedVertices;
}

/// One page of one role, ready to cook.
class AtlasPage {
  const AtlasPage(this.index, this.role, this.image);

  final int index;
  final TextureRole role;
  final Rgba image;

  /// `atlas0.basecolour`, to which the cook adds `.ktx2` and its siblings.
  String get stem => 'atlas$index.${role.suffix}';
}

/// What packing a model did.
class ModelAtlasResult {
  const ModelAtlasResult(this.pages, this.notes);

  final List<AtlasPage> pages;
  final List<String> notes;

  bool get changed => pages.isNotEmpty;
}

/// Decodes one of a document's images to RGBA8, or returns null when it
/// cannot — a format this build has no decoder for, say.
typedef ImageDecoder = Future<Rgba?> Function(
  int image,
  Map<String, Object?> definition,
);

/// Packs a model's material textures onto shared pages, remaps its texture
/// coordinates onto them, merges the materials that become identical and then
/// merges the primitives that come to share one.
///
/// `document` is edited in place and stays a glTF throughout: every key this
/// does not name travels through untouched, so extensions, `extras`, skins,
/// animation and variants survive a rewrite that has never heard of them.
///
/// Nothing here is all-or-nothing. A material that cannot be packed — it
/// tiles, it carries a texture transform, its textures disagree about what
/// they are — is left exactly as it was, said so in [ModelAtlasResult.notes],
/// and the rest of the model is still packed around it.
Future<ModelAtlasResult> atlasModel(
  GltfDocument document, {
  ModelAtlasOptions options = const ModelAtlasOptions(),
  required ImageDecoder decode,
}) async {
  final notes = <String>[];
  final materials = document.list('materials');
  if (materials.isEmpty) return ModelAtlasResult(const [], notes);

  final textures = document.list('textures');
  final images = document.list('images');
  final roles = rolesOf(document);
  final usedBy = _primitivesByMaterial(document);

  // Which materials can be packed, and why the rest cannot.
  final candidates = <_Candidate>[];
  for (var m = 0; m < materials.length; m++) {
    final candidate = _consider(
      document,
      materials[m],
      m,
      textures,
      roles,
      usedBy[m] ?? const [],
      notes,
    );
    if (candidate != null) candidates.add(candidate);
  }

  if (candidates.length < options.minMaterials) {
    if (candidates.isNotEmpty) {
      notes.add('left unpacked: only ${candidates.length} of '
          '${materials.length} materials could be packed, and '
          '${options.minMaterials} is the point at which it pays');
    }
    return ModelAtlasResult(const [], notes);
  }

  // Decode once per image, not once per use: two materials sharing a base
  // colour map are common, and a 4096 JPEG is not cheap to turn into texels.
  final decoded = <int, Rgba?>{};
  Future<Rgba?> pixelsOf(int image) async {
    if (decoded.containsKey(image)) return decoded[image];
    final it = await decode(image, images[image]);
    return decoded[image] = it;
  }

  final ready = <_Candidate>[];
  for (final candidate in candidates) {
    var all = true;
    for (final entry in candidate.images.entries) {
      final pixels = await pixelsOf(entry.value);
      if (pixels == null) {
        notes.add('${candidate.label}: left unpacked, because its '
            '${entry.key.name} image could not be decoded here');
        all = false;
        break;
      }
      candidate.pixels[entry.key] = pixels;
    }
    if (all) ready.add(candidate);
  }
  if (ready.length < options.minMaterials) {
    return ModelAtlasResult(const [], notes);
  }

  // A cell is as big as the material's largest map, so no map is thrown away
  // and the smaller ones are stretched to meet it. Stretching a 512 roughness
  // onto a 2048 cell costs page area and no detail, which is the right way
  // round: the alternative is a second page size and a second UV remap.
  final ceiling = options.maxCellSize > 0
      ? math.min(options.maxCellSize, options.maxPageSize - 2 * options.padding)
      : options.maxPageSize - 2 * options.padding;
  for (final candidate in ready) {
    var width = 1, height = 1;
    for (final pixels in candidate.pixels.values) {
      width = math.max(width, pixels.width);
      height = math.max(height, pixels.height);
    }
    final scale = math.min(1.0, ceiling / math.max(width, height));
    candidate.cellWidth = math.max(1, (width * scale).round());
    candidate.cellHeight = math.max(1, (height * scale).round());
  }

  final extrude = math.min(options.extrude, options.padding);
  final packed = packAtlas(
    [
      for (final candidate in ready)
        AtlasSprite(
          name: '${candidate.material}',
          width: candidate.cellWidth,
          height: candidate.cellHeight,
          // The packer wants texels and the base colour page wants composing,
          // so the composing happens here and the packer gets the answer.
          pixels: _cell(candidate, TextureRole.baseColour).pixels,
        ),
    ],
    AtlasPackOptions(
      maxPageSize: options.maxPageSize,
      padding: options.padding,
      extrude: extrude,
      // Every one of these is load-bearing, not a preference. Trimming would
      // shrink a cell without shrinking the UV rectangle that names it;
      // rotating would turn the texture a quarter turn under UVs that are
      // axis-aligned; and merging duplicates would give two materials one
      // region, which is right for sprites and wrong here, because the other
      // roles' maps are not duplicates just because the base colour is.
      trim: false,
      allowRotation: false,
      mergeDuplicates: false,
    ),
  );

  if (packed.problems.isNotEmpty) {
    notes.add('left unpacked: ${packed.problems.join('; ')}');
    return ModelAtlasResult(const [], notes);
  }

  // Where each material landed.
  final placed = <int, ({int page, int x, int y, int width, int height})>{};
  for (var p = 0; p < packed.pages.length; p++) {
    packed.pages[p].regions.forEach((name, region) {
      placed[int.parse(name)] = (
        page: p,
        x: region.x,
        y: region.y,
        width: region.width,
        height: region.height,
      );
    });
  }
  final byMaterial = {for (final one in ready) one.material: one};

  // Every role any packed material uses becomes a page for all of them. A
  // material given a role it never had gets the value that makes that role do
  // nothing, so it draws as it did — and it is that uniformity which lets the
  // materials collapse into one afterwards, which is the whole point.
  final usedRoles = <TextureRole>{
    for (final one in ready) ...one.images.keys,
  };
  final order = TextureRole.values.where(usedRoles.contains).toList();

  final pages = <AtlasPage>[];
  for (var p = 0; p < packed.pages.length; p++) {
    final page = packed.pages[p];
    for (final role in order) {
      final canvas = blankPage(role, page.width, page.height);
      page.regions.forEach((name, region) {
        final candidate = byMaterial[int.parse(name)]!;
        blit(canvas, _cell(candidate, role), region.x, region.y,
            extrude: extrude);
      });
      pages.add(AtlasPage(p, role, canvas));
    }
  }

  _rewrite(
    document,
    ready,
    placed,
    [for (final page in packed.pages) (width: page.width, height: page.height)],
    order,
    options,
    notes,
  );
  return ModelAtlasResult(pages, notes);
}

/// One material's map for `role`, at its cell size, with the factor folded in.
Rgba _cell(_Candidate candidate, TextureRole role) {
  final source = candidate.pixels[role];
  final width = candidate.cellWidth;
  final height = candidate.cellHeight;
  if (source == null) {
    final blank = Rgba.filled(width, height, role.blank);
    final factor = candidate.factors[role];
    if (factor != null) multiplyInto(blank, factor, srgb: role.srgb);
    return blank;
  }
  final sized = resample(source, width, height,
      normal: role == TextureRole.normal);
  // A copy, because the same decoded image can be two materials' map and
  // folding one material's tint into it would tint the other as well.
  final cell = Rgba(width, height, Uint8List.fromList(sized.pixels));
  final factor = candidate.factors[role];
  if (factor != null) multiplyInto(cell, factor, srgb: role.srgb);
  return cell;
}

/// Which primitives use each material.
Map<int, List<Map<String, Object?>>> _primitivesByMaterial(
  GltfDocument document,
) {
  final out = <int, List<Map<String, Object?>>>{};
  for (final mesh in document.list('meshes')) {
    final primitives = mesh['primitives'];
    if (primitives is! List) continue;
    for (final primitive in primitives) {
      if (primitive is! Map<String, Object?>) continue;
      final material = primitive['material'];
      if (material is int) {
        (out[material] ??= []).add(primitive);
      }
    }
  }
  return out;
}

/// A material that can be packed, and everything needed to pack it.
class _Candidate {
  _Candidate(this.material, this.definition, this.texCoord, this.images,
      this.factors, this.primitives);

  final int material;
  final Map<String, Object?> definition;
  final int texCoord;

  /// Which image each of its roles reads.
  final Map<TextureRole, int> images;

  /// The factor to fold into each role's cell, for the roles where folding it
  /// in is what makes two materials become one.
  final Map<TextureRole, List<double>> factors;

  final List<Map<String, Object?>> primitives;
  final Map<TextureRole, Rgba> pixels = {};

  int cellWidth = 1;
  int cellHeight = 1;

  String get label => definition['name'] is String
      ? '"${definition['name']}"'
      : 'material $material';
}

/// Material extensions a packed material may carry.
///
/// An allow list rather than a deny list, and deliberately short: every other
/// `KHR_materials_*` extension adds texture slots this rewrite does not know
/// to pack, and packing a material while leaving its clearcoat map pointing at
/// the old UVs would put the clearcoat somewhere else on the model. Adding one
/// here means teaching [textureSlotsOf] about its slots first.
const _packableExtensions = {
  'KHR_materials_emissive_strength',
  'KHR_materials_unlit',
  'KHR_materials_ior',
};

_Candidate? _consider(
  GltfDocument document,
  Map<String, Object?> material,
  int index,
  List<Map<String, Object?>> textures,
  ({Map<int, TextureRole> roles, Set<int> conflicted}) roles,
  List<Map<String, Object?>> primitives,
  List<String> notes,
) {
  final name = material['name'] is String
      ? '"${material['name']}"'
      : 'material $index';

  final extensions = material['extensions'];
  if (extensions is Map<String, Object?>) {
    final unknown =
        extensions.keys.where((key) => !_packableExtensions.contains(key));
    if (unknown.isNotEmpty) {
      notes.add('$name: left unpacked, because it uses '
          '${unknown.join(', ')} and this pack does not know where those keep '
          'their textures');
      return null;
    }
  }

  final slots = textureSlotsOf(document)
      .where((slot) => identical(slot.owner, material))
      .toList();
  if (slots.isEmpty) return null;

  final texCoords = slots.map((slot) => slot.texCoord).toSet();
  if (texCoords.length > 1) {
    notes.add('$name: left unpacked, because its maps read different UV sets '
        '(${texCoords.join(', ')}) and one atlas rectangle can only serve '
        'one');
    return null;
  }
  final texCoord = texCoords.single;

  final images = <TextureRole, int>{};
  for (final slot in slots) {
    if (roles.conflicted.contains(slot.texture)) {
      notes.add('$name: left unpacked, because texture ${slot.texture} is '
          'used in two roles, one of them ${slot.role.name}, so there is no '
          'colour space and no cook that is right for it');
      return null;
    }
    if (slot.info['extensions'] is Map &&
        (slot.info['extensions'] as Map).containsKey('KHR_texture_transform')) {
      notes.add('$name: left unpacked, because its ${slot.key} carries a '
          'KHR_texture_transform, which would be applied to the atlas '
          'rectangle rather than to the map');
      return null;
    }
    if (slot.texture < 0 || slot.texture >= textures.length) {
      notes.add('$name: left unpacked, because its ${slot.key} names texture '
          '${slot.texture}, which the document does not have');
      return null;
    }
    final image = _imageOf(textures[slot.texture]);
    if (image == null) {
      notes.add('$name: left unpacked, because its ${slot.key} names a '
          'texture with no readable image');
      return null;
    }
    images[slot.role] = image;
  }

  if (primitives.isEmpty) return null;

  // The test that actually decides it: a map is tileable only if something
  // tiles it, and what tiles it is UVs outside the unit square. Reading the
  // wrap mode would be guessing — plenty of models say REPEAT and never leave
  // 0..1 — and packing a genuinely tiled map turns a brick wall into one
  // brick stretched over a building.
  for (final primitive in primitives) {
    final attributes = primitive['attributes'];
    if (attributes is! Map<String, Object?>) return null;
    final accessor = attributes['TEXCOORD_$texCoord'];
    if (accessor is! int) {
      notes.add('$name: left unpacked, because a primitive that uses it has '
          'no TEXCOORD_$texCoord to remap');
      return null;
    }
    final Float32List uv;
    try {
      uv = document.readVec2(accessor);
    } on FormatException catch (error) {
      notes.add('$name: left unpacked — ${error.message}');
      return null;
    }
    for (final value in uv) {
      if (value < -0.001 || value > 1.001) {
        notes.add('$name: left unpacked, because its UVs run outside 0 to 1, '
            'which means the map is tiled across the surface');
        return null;
      }
    }
  }

  return _Candidate(
      index, material, texCoord, images, _factorsOf(material), primitives);
}

/// The factors worth folding into the cells, with the ones that cannot be
/// folded left where they are.
Map<TextureRole, List<double>> _factorsOf(Map<String, Object?> material) {
  final out = <TextureRole, List<double>>{};
  final pbr = material['pbrMetallicRoughness'];
  if (pbr is Map<String, Object?>) {
    final base = _numbers(pbr['baseColorFactor']) ?? const [1.0, 1.0, 1.0, 1.0];
    if (base.any((value) => value != 1.0)) {
      out[TextureRole.baseColour] = base;
    }
    final metallic = (pbr['metallicFactor'] as num?)?.toDouble() ?? 1.0;
    final roughness = (pbr['roughnessFactor'] as num?)?.toDouble() ?? 1.0;
    if (metallic != 1.0 || roughness != 1.0) {
      // glTF keeps roughness in green and metalness in blue.
      out[TextureRole.metallicRoughness] = [1.0, roughness, metallic, 1.0];
    }
  }
  final emissive = _numbers(material['emissiveFactor']);
  // A factor above one is a brightness, not a tint — it cannot be folded into
  // eight bits — so a material using one keeps its factor and merges only
  // with materials that share it.
  if (emissive != null &&
      emissive.any((value) => value != 1.0) &&
      emissive.every((value) => value <= 1.0)) {
    out[TextureRole.emissive] = [...emissive, 1.0];
  }
  return out;
}

List<double>? _numbers(Object? value) => value is List
    ? [for (final one in value) if (one is num) one.toDouble()]
    : null;

int? _imageOf(Map<String, Object?> texture) {
  final extensions = texture['extensions'];
  if (extensions is Map<String, Object?>) {
    final basis = extensions['KHR_texture_basisu'];
    if (basis is Map<String, Object?> && basis['source'] is int) {
      return basis['source'] as int;
    }
  }
  return texture['source'] as int?;
}

/// Points the packed materials at their pages, moves their texture
/// coordinates onto the rectangles they were given, and then collapses what
/// has become identical.
void _rewrite(
  GltfDocument document,
  List<_Candidate> packed,
  Map<int, ({int page, int x, int y, int width, int height})> placed,
  List<_Page> pages,
  List<TextureRole> roles,
  ModelAtlasOptions options,
  List<String> notes,
) {
  // One sampler for every page. Clamped, because a cell's neighbour is on the
  // other side of its edge: a page sampled with repeat wraps the far edge of
  // the atlas into a cell, which draws a stripe of an unrelated material
  // along one side of every surface.
  final sampler = document.add('samplers', <String, Object?>{
    'magFilter': 9729, // LINEAR
    'minFilter': 9987, // LINEAR_MIPMAP_LINEAR
    'wrapS': 33071, // CLAMP_TO_EDGE
    'wrapT': 33071,
  });

  final texture = <String, int>{};
  for (var p = 0; p < pages.length; p++) {
    for (final role in roles) {
      final image = document.add('images', <String, Object?>{
        'uri': 'atlas$p.${role.suffix}.ktx2',
        'mimeType': 'image/ktx2',
      });
      texture['$p.${role.name}'] = document.add('textures', <String, Object?>{
        'sampler': sampler,
        // A KTX2 image is named through KHR_texture_basisu and not through
        // `source`, which the spec reserves for a PNG or JPEG fallback. The
        // cook writes no fallback, so the extension is required rather than
        // merely used — a reader that cannot transcode it should say so
        // instead of drawing an untextured model.
        'extensions': <String, Object?>{
          'KHR_texture_basisu': <String, Object?>{'source': image},
        },
      });
    }
  }
  _declare(document, 'extensionsUsed', 'KHR_texture_basisu');
  _declare(document, 'extensionsRequired', 'KHR_texture_basisu');

  // An accessor shared by two materials has to be remapped once per material,
  // because the two land on different rectangles.
  final remapped = <String, int>{};

  for (final candidate in packed) {
    final at = placed[candidate.material]!;
    final page = pages[at.page];
    final material = candidate.definition;
    final pbr = material.putIfAbsent(
        'pbrMetallicRoughness', () => <String, Object?>{}) as Map<String, Object?>;

    for (final role in roles) {
      final index = texture['${at.page}.${role.name}']!;
      switch (role) {
        case TextureRole.baseColour:
          pbr['baseColorTexture'] =
              _slotFor(pbr['baseColorTexture'], index, candidate.texCoord);
        case TextureRole.metallicRoughness:
          pbr['metallicRoughnessTexture'] = _slotFor(
              pbr['metallicRoughnessTexture'], index, candidate.texCoord);
        case TextureRole.normal:
          material['normalTexture'] =
              _slotFor(material['normalTexture'], index, candidate.texCoord);
        case TextureRole.occlusion:
          material['occlusionTexture'] = _slotFor(
              material['occlusionTexture'], index, candidate.texCoord);
        case TextureRole.emissive:
          material['emissiveTexture'] =
              _slotFor(material['emissiveTexture'], index, candidate.texCoord);
      }
    }

    // What was folded into the cell must stop being applied a second time on
    // top of it. This is the step that makes two materials which differed
    // only by tint into one material, and skipping it would tint everything
    // twice.
    if (candidate.factors.containsKey(TextureRole.baseColour)) {
      pbr.remove('baseColorFactor');
    }
    if (candidate.factors.containsKey(TextureRole.metallicRoughness)) {
      pbr..remove('metallicFactor')..remove('roughnessFactor');
    }
    if (candidate.factors.containsKey(TextureRole.emissive)) {
      material['emissiveFactor'] = <double>[1, 1, 1];
    }

    final scaleU = at.width / page.width;
    final scaleV = at.height / page.height;
    final offsetU = at.x / page.width;
    final offsetV = at.y / page.height;

    for (final primitive in candidate.primitives) {
      final attributes = primitive['attributes'] as Map<String, Object?>;
      final key = 'TEXCOORD_${candidate.texCoord}';
      final accessor = attributes[key] as int;
      final cached = remapped['$accessor:${candidate.material}'];
      if (cached != null) {
        attributes[key] = cached;
        continue;
      }
      final uv = document.readVec2(accessor);
      final moved = Float32List(uv.length);
      for (var i = 0; i < uv.length; i += 2) {
        // glTF measures v from the top of the image and so does the packer,
        // so there is no flip here. There is a clamp: a UV a thousandth
        // outside the unit square was harmless when it addressed its own
        // image and reads a neighbour's texels once it addresses a cell.
        moved[i] = uv[i].clamp(0.0, 1.0) * scaleU + offsetU;
        moved[i + 1] = uv[i + 1].clamp(0.0, 1.0) * scaleV + offsetV;
      }
      final index = document.addVec2(moved);
      remapped['$accessor:${candidate.material}'] = index;
      attributes[key] = index;
    }
  }

  notes.add('packed ${packed.length} materials onto ${pages.length} '
      '${pages.length == 1 ? 'page' : 'pages'} of '
      '${roles.map((role) => role.suffix).join(', ')}');

  final materials = mergeMaterials(document);
  if (materials > 0) {
    notes.add('$materials materials became copies of another and were '
        'pointed at it');
  }
  if (options.mergePrimitives) {
    final primitives =
        mergePrimitives(document, maxVertices: options.maxMergedVertices);
    if (primitives > 0) {
      notes.add('$primitives primitives were joined into the ones they now '
          'share a material with, which is $primitives fewer draws');
    }
  }
}

/// A texture slot pointing at `index`, keeping whatever the old slot said
/// about itself.
///
/// `scale` on a normal map and `strength` on an occlusion map are the
/// material's, not the texture's: they survive the move to a shared page and
/// dropping them would flatten or un-shade the surface.
Map<String, Object?> _slotFor(Object? existing, int index, int texCoord) {
  final out = <String, Object?>{
    'index': index,
    if (texCoord != 0) 'texCoord': texCoord,
  };
  if (existing is Map<String, Object?>) {
    if (existing['scale'] != null) out['scale'] = existing['scale'];
    if (existing['strength'] != null) out['strength'] = existing['strength'];
    if (existing['extras'] != null) out['extras'] = existing['extras'];
  }
  return out;
}

void _declare(GltfDocument document, String key, String extension) {
  final declared = document.json.putIfAbsent(key, () => <Object?>[]);
  if (declared is List && !declared.contains(extension)) {
    declared.add(extension);
  }
}

/// Just the size of a packed page, which is all the rewrite needs of it.
typedef _Page = ({int width, int height});
