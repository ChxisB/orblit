import '../component.dart';
import '../values.dart';

/// Files this entity carries: scripts, tables, whatever a game needs beside
/// what it draws.
///
/// Paths rather than contents, and a list rather than one, because the thing
/// people actually do is hang three or four related files off one object —
/// behaviour, a stat table, some dialogue — and making that require three
/// child entities gives the outliner a tree of things that are not in the
/// scene in any spatial sense.
class DataComponent extends SceneComponent {
  const DataComponent({this.paths = const []});

  static DataComponent fromJson(Map<String, Object?> json) =>
      DataComponent(paths: Values.texts(json['paths']));

  /// Project-relative, in the order somebody put them in.
  final List<String> paths;

  @override
  String get type => SceneComponents.data;

  @override
  Map<String, Object?> toJson() => {'paths': paths};
}

/// Where this entity came from, when it came from a prefab.
///
/// A component rather than a field on the entity so that an entity is nothing
/// but an id, a name, a place in the tree and a bag of components — no
/// exceptions to carry through every piece of code that walks one. It is also
/// honestly a fact *about* the entity rather than part of its identity: a
/// prefab instance that somebody has finished editing stops being one, and
/// that should be a component going away rather than a field going null.
class PrefabComponent extends SceneComponent {
  const PrefabComponent({this.asset});

  static PrefabComponent fromJson(Map<String, Object?> json) =>
      PrefabComponent(asset: Values.text(json, 'asset'));

  /// The prefab, relative to the project.
  final String? asset;

  @override
  String get type => SceneComponents.prefab;

  @override
  Map<String, Object?> toJson() => Values.pruned({'asset': asset});
}
