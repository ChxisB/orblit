import 'package:orblit_light/orblit_light.dart' show Tint;
import 'package:orblit_mesh/orblit_mesh.dart' show Mesh, PolyShape, Shape;

import '../component.dart';
import '../values.dart';

/// Something with geometry, and how it is drawn.
///
/// The geometry can arrive four ways and they are not alternatives so much as
/// a history. A box is [shape] — three numbers — right up until somebody pulls
/// a face off it, at which point [geometry] is what it is and the shape's
/// numbers are a record of what it was made from. An [outline] is a third: a
/// plan somebody drew and pulled up, kept beside the mesh so a wall can still
/// be moved by dragging the corner it belongs to. And [asset] is none of
/// those — a file on disk that this entity draws.
///
/// All four are kept when all four are known, because each answers a question
/// the others cannot, and throwing the earlier ones away is what makes an edit
/// irreversible a week later.
class MeshComponent extends SceneComponent {
  const MeshComponent({
    this.asset,
    this.shape,
    this.geometry,
    this.outline,
    this.boundary,
    this.surfaces,
    this.colour = const Tint.hex(0xD9634F),
    this.castShadows = true,
    this.receiveShadows = true,
    this.sway = 0,
    this.authored = false,
  });

  static MeshComponent fromJson(Map<String, Object?> json) => MeshComponent(
    asset: Values.text(json, 'asset'),
    shape: Shape.fromJson(json['shape']),
    geometry: Mesh.fromJson(json['geometry']),
    outline: PolyShape.fromJson(json['outline']),
    boundary: json['boundary'] is Map<String, Object?>
        ? json['boundary']! as Map<String, Object?>
        : null,
    surfaces: json['surfaces'] is List ? json['surfaces']! as List : null,
    colour: Values.tint(json['colour']),
    castShadows: Values.flag(json, 'castShadows', fallback: true),
    receiveShadows: Values.flag(json, 'receiveShadows', fallback: true),
    sway: Values.number(json, 'sway', 0),
    authored: Values.flag(json, 'authored', fallback: false),
  );

  /// A mesh file, relative to the project, or null for geometry held here.
  final String? asset;

  final Shape? shape;
  final Mesh? geometry;
  final PolyShape? outline;

  /// What this is to walk into, kept as it was written.
  ///
  /// Not read into a type here, and deliberately. A boundary is a collision
  /// shape and a surface is a material, and both belong to packages that do
  /// not exist yet — the material system is a phase of its own. Holding them
  /// as the file wrote them means the document round-trips them exactly today
  /// and the editor keeps reading them with the types it already has, without
  /// this package pretending to understand either.
  final Map<String, Object?>? boundary;

  /// The materials painted onto this shape's faces, kept as they were written.
  final List<Object?>? surfaces;

  /// The colour it is drawn in, before any material overrides it.
  final Tint colour;

  final bool castShadows;
  final bool receiveShadows;

  /// How much this moves in wind, from nothing to fully.
  final double sway;

  /// Whether the geometry belongs to this document rather than to a file.
  ///
  /// The difference between something somebody is modelling here — a box they
  /// can still pull a face off — and something exported from elsewhere that
  /// this scene only places. A tool offers to edit the first and not the
  /// second, and the two are otherwise indistinguishable: an authored shape
  /// that has not been touched yet has no geometry stored, and a referenced
  /// model given a boundary has some.
  ///
  /// Stated rather than guessed from which fields are filled in, because the
  /// guess is wrong in both directions and what it costs is somebody's
  /// modelling panel disappearing when they reopen the file.
  final bool authored;

  @override
  String get type => SceneComponents.mesh;

  @override
  Map<String, Object?> toJson() => Values.pruned({
    'asset': asset,
    'shape': shape?.toJson(),
    'geometry': geometry?.toJson(),
    'outline': outline?.toJson(),
    'boundary': boundary,
    'surfaces': surfaces,
    'colour': Values.tintToJson(colour),
    'castShadows': castShadows,
    'receiveShadows': receiveShadows,
    // Left out when there is none, because almost nothing sways and a key
    // on every mesh in every scene is noise in somebody's diff.
    'sway': sway > 0 ? sway : null,
    // Likewise: most things in most scenes are placed, not modelled.
    'authored': authored ? true : null,
  });
}

/// A material this entity wears, overriding whatever its geometry brought.
///
/// Its own component rather than a key on the mesh, because a material can be
/// worn by things that are not meshes — a sprite, a tilemap — and because
/// what it carries grows: groups and inheritance live in the material file,
/// looks live here.
class MaterialComponent extends SceneComponent {
  const MaterialComponent({this.asset, this.looks = const {}});

  static MaterialComponent fromJson(Map<String, Object?> json) {
    final looks = <String, String>{};
    for (final entry in Values.object(json['looks']).entries) {
      final path = entry.value;
      if (path is String && path.isNotEmpty) looks[entry.key] = path;
    }
    return MaterialComponent(asset: Values.text(json, 'asset'), looks: looks);
  }

  /// The material, relative to the project.
  final String? asset;

  /// What this entity wears instead under each named look, by look name.
  ///
  /// The same shape `KHR_materials_variants` uses, and for the same reason it
  /// chose it: the names live once, for the whole scene, and each object says
  /// only which material it swaps to under each. An object with nothing to say
  /// about a look keeps [asset] — so a "winter" look is authored by naming the
  /// dozen things that change, not by restating the four hundred that do not.
  final Map<String, String> looks;

  /// The material to wear under [look], falling back to [asset] when this
  /// entity has nothing different to wear.
  String? under(String? look) => look == null ? asset : (looks[look] ?? asset);

  /// Every look this entity has something of its own for.
  Iterable<String> get lookNames => looks.keys;

  @override
  String get type => SceneComponents.material;

  @override
  Map<String, Object?> toJson() =>
      Values.pruned({'asset': asset, 'looks': looks.isEmpty ? null : looks});
}

/// A Gaussian splat capture this entity draws.
///
/// The budget is stated here and not only asked of the device, because the two
/// mean different things: the device profile says what this machine can carry,
/// and this says what the scene was authored to look right at. The renderer
/// takes the smaller. A capture with no [budget] is drawn as fully as the
/// device allows.
class SplatsComponent extends SceneComponent {
  const SplatsComponent({this.asset, this.budget, this.harmonics});

  static SplatsComponent fromJson(Map<String, Object?> json) => SplatsComponent(
    asset: Values.text(json, 'asset'),
    budget: Values.maybeNumber(json, 'budget')?.round(),
    harmonics: Values.maybeNumber(json, 'harmonics')?.round(),
  );

  /// The capture — `.ply`, `.spz` or a cooked `.osplat` — as a project path.
  final String? asset;

  /// The most splats this should be drawn with, or null for as many as fit.
  final int? budget;

  /// How many bands of spherical harmonics to keep, or null for the file's.
  final int? harmonics;

  @override
  String get type => SceneComponents.splats;

  @override
  Map<String, Object?> toJson() =>
      Values.pruned({'asset': asset, 'budget': budget, 'harmonics': harmonics});
}
