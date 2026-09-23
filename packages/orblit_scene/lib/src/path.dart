/// Where an entity is, counted through the prefabs it sits inside.
///
/// An entity that belongs to a prefab instance has an id made of the id it has
/// in each document it is inside, outermost first, joined by [separator]:
/// `street1/lamp3/bulb` is the entity `bulb` in `lamp.oprefab`, inside the
/// instance `lamp3` in `street.oprefab`, inside the instance `street1` in this
/// scene. The id *is* the path. Anything that names an entity — a parent link,
/// the selection, an animation track — names one inside an instance the same
/// way it names one outside, and resolving a path is looking the id up.
///
/// One id per document crossed, and nothing for the entities in between,
/// because the path has to survive what people do inside a document. Moving
/// the bulb from one arm of the lamp to the other is a reparent inside
/// `lamp.oprefab`, and a path that spelled out every ancestor would change
/// under it and orphan the animation that pointed at it. Only crossing into a
/// different document changes a path, and that is a different entity.
///
/// The root of an instance takes the instance's own id, so there is exactly
/// one name for it: `street1`, never `street1/street`.
abstract final class EntityPath {
  /// Reserved in ids for this. An entity with a plain id belongs to the
  /// document it is written in; one with a separator in its id belongs to an
  /// instance.
  static const String separator = '/';

  /// The id of [local] inside [instance].
  static String join(String instance, String local) =>
      '$instance$separator$local';

  /// Whether [id] names something inside an instance.
  static bool isPart(String id) => id.contains(separator);

  /// Whether [id] is [instance] or anything inside it.
  static bool within(String instance, String id) =>
      id == instance || id.startsWith('$instance$separator');

  /// The instance, in the outermost document, that [id] belongs to, or null
  /// when it belongs to that document itself.
  static String? instanceOf(String id) {
    final at = id.indexOf(separator);
    return at < 0 ? null : id.substring(0, at);
  }

  /// [id] as the prefab of [instance] knows it, where [root] is the id the
  /// prefab gives its own root.
  ///
  /// Returns null when [id] is not inside [instance] at all.
  static String? localTo(String instance, String id, {required String root}) {
    if (id == instance) return root;
    final prefix = '$instance$separator';
    return id.startsWith(prefix) ? id.substring(prefix.length) : null;
  }

  /// [local], an id in the prefab of [instance], as the document holding the
  /// instance knows it. The inverse of [localTo].
  static String inside(String instance, String local, {required String root}) =>
      local == root ? instance : join(instance, local);

  /// The documents [id] crosses, outermost first.
  static List<String> segments(String id) => id.split(separator);

  /// Every id [id] is inside, nearest first and not including itself.
  ///
  /// `street1/lamp3/bulb` is inside `street1/lamp3` and then `street1`. What
  /// a link to a part that has gone away falls back to, so something hung off
  /// a part a prefab no longer has lands on the nearest thing that is still
  /// there instead of at the top of the scene.
  static List<String> enclosing(String id) {
    final found = <String>[];
    var at = id.lastIndexOf(separator);
    while (at > 0) {
      found.add(id.substring(0, at));
      at = id.lastIndexOf(separator, at - 1);
    }
    return found;
  }
}
