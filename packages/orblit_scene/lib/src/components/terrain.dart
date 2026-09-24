import '../component.dart';
import '../values.dart';

/// Ground this entity puts in the scene: an `.oterrain` file, by its path in
/// the project.
///
/// The file is the terrain and the scene only says it is here. The heights
/// are regions of their own beside the settings file, far too large to write
/// into a scene, and a terrain shared by two scenes is one set of files
/// rather than two copies that drift apart.
///
/// Laid in the world where its own texels say, whatever the entity's
/// transform: a terrain is the ground the world is measured from, and ground
/// that could be turned or scaled would make every height asked of it a
/// question about which entity it belongs to.
class TerrainComponent extends SceneComponent {
  const TerrainComponent({
    this.file,
    this.castShadows = true,
    this.receiveShadows = true,
  });

  static TerrainComponent fromJson(Map<String, Object?> json) =>
      TerrainComponent(
        file: Values.text(json, 'file'),
        castShadows: Values.flag(json, 'castShadows', fallback: true),
        receiveShadows: Values.flag(json, 'receiveShadows', fallback: true),
      );

  /// Project-relative, or null until somebody chooses one.
  final String? file;

  final bool castShadows;

  final bool receiveShadows;

  @override
  String get type => SceneComponents.terrain;

  @override
  Map<String, Object?> toJson() => Values.pruned({
    'file': file,
    if (!castShadows) 'castShadows': false,
    if (!receiveShadows) 'receiveShadows': false,
  });
}
