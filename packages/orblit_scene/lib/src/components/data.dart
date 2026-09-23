import '../component.dart';
import '../diff.dart';
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

/// This entity is an instance of a prefab: a link to it, and what is
/// different here.
///
/// A component rather than a field on the entity so that an entity is nothing
/// but an id, a name, a place in the tree and a bag of components — no
/// exceptions to carry through every piece of code that walks one. It is also
/// honestly a fact *about* the entity rather than part of its identity: an
/// instance that somebody unpacks stops being one, and that should be a
/// component going away rather than a field going null.
///
/// Sits on the instance's root only. What the instance is made of is the
/// prefab's, and is not written down here at all — see [PrefabState].
class PrefabComponent extends SceneComponent {
  const PrefabComponent({
    this.asset,
    this.state = PrefabState.folded,
    this.overrides = SceneDiff.none,
  });

  static PrefabComponent fromJson(Map<String, Object?> json) => PrefabComponent(
    asset: Values.text(json, 'asset'),
    state:
        Values.named(PrefabState.values, json['state']) ?? PrefabState.folded,
    overrides: SceneDiff.fromJson(json['overrides']),
  );

  /// The prefab, relative to the project.
  final String? asset;

  final PrefabState state;

  /// What this instance changes about the prefab, addressed by the ids the
  /// prefab's own document uses.
  ///
  /// Only meaningful while [state] is [PrefabState.folded]. Once the instance
  /// is open its parts are in the document and are the truth, and these are
  /// worked out again from them when the document is folded to be saved — so
  /// they stay as small as the edit, however many times it was made.
  final SceneDiff overrides;

  PrefabComponent copyWith({
    String? asset,
    PrefabState? state,
    SceneDiff? overrides,
  }) => PrefabComponent(
    asset: asset ?? this.asset,
    state: state ?? this.state,
    overrides: overrides ?? this.overrides,
  );

  @override
  String get type => SceneComponents.prefab;

  @override
  Map<String, Object?> toJson() => Values.pruned({
    'asset': asset,
    'state': state == PrefabState.folded ? null : state.name,
    'overrides': overrides.isEmpty ? null : overrides.toJson(),
  });
}

/// How much of an instance is in the document.
enum PrefabState {
  /// Only the link is: an entity with nothing but a name, a place and this
  /// component, and the prefab's parts left in the prefab. How every instance
  /// is saved, and how one whose prefab cannot be read stays — as a link that
  /// still says everything it said, rather than as a hole.
  folded,

  /// Every part is in the document, under ids that are paths into the
  /// instance, and they are what the instance looks like now. How an editor
  /// or a game holds one. Written down only if something writes an open
  /// document without folding it, and then it stops the parts being opened a
  /// second time on top of themselves.
  open,

  /// A copy of the prefab stamped into the scene before instances were links:
  /// every part is here under an id of its own, each one carrying this
  /// component. What a scene saved at format four holds, and turned into an
  /// open instance the first time the prefab can be read.
  stamped,
}
