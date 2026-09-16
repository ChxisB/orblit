import 'component.dart';
import 'values.dart';

/// One thing in a scene.
///
/// An id, a name, where it sits in the tree, and what it is — which is the set
/// of components it has and nothing else. There is deliberately no `kind`
/// field: the question "is this a light" is answered by asking whether it has
/// a light component, which means a lamp can be a mesh and a light at once
/// instead of being two objects somebody has to keep in step by hand.
///
/// Identified by [id] rather than by name because a rename is an ordinary edit
/// and everything that points at an entity — a parent link, the selection, an
/// undo step — has to survive one.
class SceneEntity {
  const SceneEntity({
    required this.id,
    required this.name,
    this.parent,
    this.visible = true,
    this.components = const {},
  });

  /// One entity out of its JSON, with anything unrecognised kept whole.
  ///
  /// Returns null when there is no usable id, because an entity nothing can
  /// refer to cannot be parented to, selected or undone — it is not a
  /// recoverable entity, it is a hole.
  static SceneEntity? fromJson(Map<String, Object?> json) {
    final id = Values.text(json, 'id');
    if (id == null || id.isEmpty) return null;

    final raw = Values.object(json['components']);
    return SceneEntity(
      id: id,
      name: Values.text(json, 'name') ?? id,
      parent: Values.text(json, 'parent'),
      visible: Values.flag(json, 'visible', fallback: true),
      components: {
        for (final entry in raw.entries)
          if (entry.value is Map<String, Object?>)
            entry.key: SceneComponents.read(
              entry.key,
              entry.value! as Map<String, Object?>,
            ),
      },
    );
  }

  final String id;

  /// What it is called, which need not be unique and is not identity.
  final String name;

  /// The entity this hangs from, or null when it sits at the top level.
  final String? parent;

  /// Hidden is not deleted: it keeps its place in the tree, its children and
  /// its id, so showing it again is immediate.
  final bool visible;

  /// What this entity is, keyed by component type.
  final Map<String, SceneComponent> components;

  SceneComponent? operator [](String type) => components[type];

  bool has(String type) => components.containsKey(type);

  SceneEntity copyWith({
    String? id,
    String? name,
    String? parent,
    bool clearParent = false,
    bool? visible,
    Map<String, SceneComponent>? components,
  }) => SceneEntity(
    id: id ?? this.id,
    name: name ?? this.name,
    parent: clearParent ? null : (parent ?? this.parent),
    visible: visible ?? this.visible,
    components: components ?? this.components,
  );

  /// The same entity with one component set, or removed when [component] is
  /// null.
  SceneEntity withComponent(String type, SceneComponent? component) {
    final next = Map<String, SceneComponent>.of(components);
    if (component == null) {
      next.remove(type);
    } else {
      next[type] = component;
    }
    return copyWith(components: next);
  }

  /// The entity as it goes into the file.
  ///
  /// Components are written in the order [SceneComponents.order] gives, with
  /// anything unrecognised after them in the order it was read. Two saves of
  /// one scene therefore produce the same bytes, which is what lets a scene
  /// file live in a repository without turning every commit into a diff
  /// nobody can review.
  Map<String, Object?> toJson() {
    final known = SceneComponents.order.where(components.containsKey);
    final rest = components.keys.where(
      (type) => !SceneComponents.order.contains(type),
    );

    return Values.pruned({
      'id': id,
      'name': name,
      'parent': parent,
      // Written only when it is false, so the ordinary case stays out of the
      // file and out of everybody's diffs.
      'visible': visible ? null : false,
      'components': {
        for (final type in [...known, ...rest])
          type: components[type]!.toJson(),
      },
    });
  }
}
