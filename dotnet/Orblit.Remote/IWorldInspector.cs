namespace Orblit.Remote;

/// <summary>
/// One entity as a listing shows it: its handle and the names of the components it carries.
/// The handle is the signed 64 bit number the wire carries, so a handle with its top bit set is
/// negative, as it is in Dart.
/// </summary>
public sealed record EntityRow(long Id, IReadOnlyList<string> Components);

/// <summary>
/// What the server needs to look inside a world and change it.
/// Declared here, next to the code that calls it, so a world that is not an
/// <c>Orblit.Core</c> one can be driven by writing this and nothing else.
/// </summary>
public interface IWorldInspector
{
    /// <summary>
    /// The entities that carry every component named in <paramref name="filter"/>. An empty filter
    /// lists them all. Throws <see cref="ArgumentException"/> for a name nobody knows.
    /// </summary>
    IReadOnlyList<EntityRow> Entities(IReadOnlyList<string> filter);

    /// <summary>
    /// The components of entity <paramref name="id"/> by name, each as a map of field to value. A
    /// component with no field names is listed with a single <c>undescribed</c> entry saying how it
    /// is laid out. Null when <paramref name="id"/> is not alive.
    /// </summary>
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>? Read(long id);

    /// <summary>
    /// Sets one field of one component. Throws <see cref="ArgumentException"/> for a component the
    /// entity lacks or a field nobody named, and <see cref="InvalidOperationException"/> for a dead
    /// entity.
    /// </summary>
    void Write(long id, string component, string field, double value);
}
