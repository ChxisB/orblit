/// One entity as a listing shows it: its handle and the names of the
/// components it carries.
typedef EntityRow = ({int id, List<String> components});

/// What the server needs to look inside a world and change it.
///
/// Declared here, next to the code that calls it, so a world that is not an
/// `orblit_core` one can be driven by writing this and nothing else.
abstract interface class WorldInspector {
  /// The entities that carry every component named in [filter]. An empty
  /// filter lists them all. Throws [ArgumentError] for a name nobody knows.
  List<EntityRow> entities(List<String> filter);

  /// The components of entity [id] by name, each as a map of field to value.
  /// A component with no field names is listed with a single `undescribed`
  /// entry saying how it is laid out. Null when [id] is not alive.
  Map<String, Map<String, Object?>>? read(int id);

  /// Sets one field of one component. Throws [ArgumentError] for a component
  /// the entity lacks or a field nobody named, and [StateError] for a dead
  /// entity.
  void write(int id, String component, String field, num value);
}
