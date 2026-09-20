import 'document.dart';

/// What a texture is being used *as*.
///
/// This matters more than it looks. A base colour map is sRGB and a roughness
/// map is not, so the two cannot share a page — decoding one as the other
/// darkens or washes out every texel. Each role also cooks differently: a
/// normal map wants the two-channel path, occlusion wants a single channel,
/// and colour wants neither.
enum TextureRole {
  baseColour,
  metallicRoughness,
  normal,
  occlusion,
  emissive;

  /// Whether the page holding this role is stored sRGB-encoded.
  bool get srgb => this == baseColour || this == emissive;

  /// What an unused texel of this role's page should be, as RGBA8.
  ///
  /// Not arbitrary: each one is the value glTF samples when a material has no
  /// map in that slot at all, so a material handed a slot it never had draws
  /// exactly as it did before. White for everything multiplicative — a
  /// missing emissive map means white times an emissive factor of nought, not
  /// black times nothing — and flat for a normal.
  List<int> get blank => switch (this) {
    baseColour => const [255, 255, 255, 255],
    metallicRoughness => const [255, 255, 255, 255],
    normal => const [128, 128, 255, 255],
    occlusion => const [255, 255, 255, 255],
    emissive => const [255, 255, 255, 255],
  };

  /// The suffix its page gets, so `atlas0.basecolour.ktx2` says what it holds
  /// without anyone opening it.
  String get suffix => switch (this) {
    baseColour => 'basecolour',
    metallicRoughness => 'metallicroughness',
    normal => 'normal',
    occlusion => 'occlusion',
    emissive => 'emissive',
  };
}

/// One texture slot on one material.
class TextureSlot {
  const TextureSlot(this.role, this.owner, this.key, this.info);

  final TextureRole role;

  /// The material this slot belongs to.
  final Map<String, Object?> owner;

  /// Where the slot lives — `baseColorTexture` and the rest — for messages.
  final String key;

  /// The `textureInfo` object itself: `{index, texCoord, extensions?}`.
  final Map<String, Object?> info;

  int get texture => info['index'] as int? ?? -1;
  int get texCoord => info['texCoord'] as int? ?? 0;
}

/// Every texture slot a document's materials declare, in a fixed order.
///
/// The idea of walking the materials to learn a texture's role rather than
/// guessing from the image is the one thing worth taking from how other
/// engines import glTF: the file says what each texture is for, and reading
/// it is both exact and cheap.
List<TextureSlot> textureSlotsOf(GltfDocument document) {
  final slots = <TextureSlot>[];
  for (final material in document.list('materials')) {
    final pbr = material['pbrMetallicRoughness'];
    if (pbr is Map<String, Object?>) {
      _slot(slots, TextureRole.baseColour, material, pbr, 'baseColorTexture');
      _slot(
        slots,
        TextureRole.metallicRoughness,
        material,
        pbr,
        'metallicRoughnessTexture',
      );
    }
    _slot(slots, TextureRole.normal, material, material, 'normalTexture');
    _slot(slots, TextureRole.occlusion, material, material, 'occlusionTexture');
    _slot(slots, TextureRole.emissive, material, material, 'emissiveTexture');
  }
  return slots;
}

void _slot(
  List<TextureSlot> into,
  TextureRole role,
  Map<String, Object?> material,
  Map<String, Object?> holder,
  String key,
) {
  final info = holder[key];
  if (info is Map<String, Object?> && info['index'] is int) {
    into.add(TextureSlot(role, material, key, info));
  }
}

/// Which role each texture is used as, and which textures are used as more
/// than one.
///
/// A texture used both as base colour and as roughness has no single correct
/// colour space, so it is reported rather than packed: whichever page it went
/// on, half its uses would be wrong, and wrong by an amount that looks like a
/// lighting bug rather than an import bug.
({Map<int, TextureRole> roles, Set<int> conflicted}) rolesOf(
  GltfDocument document,
) {
  final roles = <int, TextureRole>{};
  final conflicted = <int>{};
  for (final slot in textureSlotsOf(document)) {
    final already = roles[slot.texture];
    if (already == null) {
      roles[slot.texture] = slot.role;
    } else if (already != slot.role) {
      conflicted.add(slot.texture);
    }
  }
  return (roles: roles, conflicted: conflicted);
}
