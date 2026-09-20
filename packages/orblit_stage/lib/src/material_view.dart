import 'package:orblit_filament/orblit_filament.dart';
import 'package:orblit_scene/orblit_scene.dart';
import 'package:vector_math/vector_math_64.dart';

/// Which map slots hold a colour somebody picked, and therefore arrive sRGB.
///
/// The other four are measurements — a direction, a roughness, an occlusion —
/// and decoding those as though they were colours bends every one of them.
const Set<String> _colourMaps = {'baseColour', 'emissive', 'blendBaseColour'};

/// Turns a resolved material into the one the renderer draws with.
///
/// The one place the names in a `.omat` meet the renderer's own fields. It is
/// a plain switch rather than anything reflective on purpose: a parameter that
/// is added to [MaterialFields] and not handled here is a parameter that
/// silently does nothing, and a switch is the only shape where that shows up
/// as a test failure rather than as a surface that looks slightly wrong.
///
/// [locate] turns a project-relative asset path into one the renderer can
/// open. Anything it cannot place is left out rather than guessed at.
OrblitMaterial materialFrom(
  ResolvedMaterial resolved, {
  required int key,
  String? Function(String path)? locate,
}) {
  OrblitTexture? map(String slot) {
    final path = resolved.maps[slot];
    if (path == null) return null;
    final found = locate == null ? path : locate(path);
    if (found == null) return null;
    return OrblitTexture(found, srgb: _colourMaps.contains(slot));
  }

  Vector4? rgba(String name) {
    final numbers = resolved.numbers(name);
    return numbers == null
        ? null
        : Vector4(numbers[0], numbers[1], numbers[2], numbers[3]);
  }

  Vector3? rgb(String name) {
    final numbers = resolved.numbers(name);
    return numbers == null ? null : Vector3(numbers[0], numbers[1], numbers[2]);
  }

  Vector2? pair(String name) {
    final numbers = resolved.numbers(name);
    return numbers == null ? null : Vector2(numbers[0], numbers[1]);
  }

  // The renderer's own defaults stand in for anything the chain never set, so
  // that a material which says nothing draws the same as no material at all.
  const fallback = OrblitMaterial(key: 0);

  final bearing = resolved.number('windBearing');
  final speed = resolved.number('windSpeed');
  final strength = resolved.number('windStrength');

  return OrblitMaterial(
    key: key,
    shading:
        Values.named(OrblitShading.values, resolved.choice('shading')) ??
        fallback.shading,
    blend:
        Values.named(OrblitBlend.values, resolved.choice('blend')) ??
        fallback.blend,
    culling:
        Values.named(OrblitCulling.values, resolved.choice('culling')) ??
        fallback.culling,
    doubleSided: resolved.flag('doubleSided') ?? fallback.doubleSided,
    baseColour: rgba('baseColour'),
    metallic: resolved.number('metallic') ?? fallback.metallic,
    roughness: resolved.number('roughness') ?? fallback.roughness,
    reflectance: resolved.number('reflectance') ?? fallback.reflectance,
    clearCoat: resolved.number('clearCoat') ?? fallback.clearCoat,
    clearCoatRoughness:
        resolved.number('clearCoatRoughness') ?? fallback.clearCoatRoughness,
    anisotropy: resolved.number('anisotropy') ?? fallback.anisotropy,
    sheenColour: rgb('sheenColour'),
    sheenRoughness:
        resolved.number('sheenRoughness') ?? fallback.sheenRoughness,
    // Still air unless the material says otherwise, and stated as one value so
    // that a material setting only a speed still gets the default strength.
    wind: bearing == null && speed == null && strength == null
        ? OrblitWind.none
        : OrblitWind(
            bearing: bearing ?? 0,
            speed: speed ?? 0,
            strength: strength ?? 1,
          ),
    emissive: rgb('emissive'),
    emissiveIntensity:
        resolved.number('emissiveIntensity') ?? fallback.emissiveIntensity,
    ambientOcclusion:
        resolved.number('ambientOcclusion') ?? fallback.ambientOcclusion,
    normalScale: resolved.number('normalScale') ?? fallback.normalScale,
    tiling: pair('tiling'),
    offset: pair('offset'),
    maskThreshold: resolved.number('maskThreshold') ?? fallback.maskThreshold,
    depthWrite: resolved.flag('depthWrite') ?? fallback.depthWrite,
    depthBias: resolved.number('depthBias') ?? fallback.depthBias,
    wrap:
        Values.named(OrblitWrap.values, resolved.choice('wrap')) ??
        fallback.wrap,
    filter:
        Values.named(OrblitFilter.values, resolved.choice('filter')) ??
        fallback.filter,
    screenMapped: resolved.flag('screenMapped') ?? fallback.screenMapped,
    blendMode:
        Values.named(OrblitBlendMode.values, resolved.choice('blendMode')) ??
        fallback.blendMode,
    blendAmount: resolved.number('blendAmount') ?? fallback.blendAmount,
    blendSharpness:
        resolved.number('blendSharpness') ?? fallback.blendSharpness,
    blendTiling: pair('blendTiling'),
    blendOffset: pair('blendOffset'),
    baseColourMap: map('baseColour'),
    normalMap: map('normal'),
    metallicRoughnessMap: map('metallicRoughness'),
    occlusionMap: map('occlusion'),
    emissiveMap: map('emissive'),
    blendBaseColourMap: map('blendBaseColour'),
    blendMaskMap: map('blendMask'),
  );
}
