using System.Runtime.InteropServices;
using Orblit.Core;

namespace Orblit.Remote;

/// <summary>
/// An <c>Orblit.Core</c> <see cref="World"/>, looked into by component name.
/// The world holds no names of its own, so the caller says which component types to look for. A
/// manifest entry, in the shape <c>Orblit.Codegen</c> writes, gives a component its field names. A
/// component with none is listed by name and cannot be set, because a number with no name is too
/// easy to write to the wrong place. Not thread safe: call it from the thread that owns the world.
/// </summary>
public sealed class CoreWorldInspector : IWorldInspector
{
    private static readonly Dictionary<string, string[]> BuiltIn = new()
    {
        ["orblit.LocalTransform"] =
        [
            "x", "y", "z", "rotationX", "rotationY", "rotationZ", "rotationW", "scaleX", "scaleY", "scaleZ",
        ],
    };

    private readonly World _world;
    private readonly Dictionary<string, ComponentType> _byName = [];
    private readonly Dictionary<uint, ComponentType> _byId = [];
    private readonly Dictionary<string, string[]> _fields = new(BuiltIn);
    private readonly string[] _sortedNames;

    /// <summary>
    /// Throws <see cref="ArgumentException"/> when a manifest entry for a type in
    /// <paramref name="types"/> has a different number of fields than the type has elements.
    /// </summary>
    public CoreWorldInspector(
        World world,
        IReadOnlyList<ComponentType> types,
        IEnumerable<IReadOnlyDictionary<string, object?>>? manifest = null)
    {
        _world = world;
        foreach (var type in types)
        {
            _byName[type.Name] = type;
            _byId[type.Id] = type;
        }

        foreach (var entry in manifest ?? [])
        {
            _fields[(string)entry["name"]!] = [.. ((IReadOnlyList<object?>)entry["fields"]!).Cast<string>()];
        }

        foreach (var type in _byName.Values)
        {
            if (_fields.TryGetValue(type.Name, out var fields) && fields.Length != type.Arity)
            {
                throw new ArgumentException($"{type.Name} holds {type.Arity} elements, not {fields.Length}.");
            }
        }

        _sortedNames = [.. _byName.Keys.Order(StringComparer.Ordinal)];
    }

    public IReadOnlyList<EntityRow> Entities(IReadOnlyList<string> filter)
    {
        using var query = _world.Query([.. filter.Select(TypeNamed)]);
        var rows = new List<EntityRow>();
        foreach (var chunk in query.Chunks)
        {
            // Every entity of a chunk has the same components, so name them once.
            var names = NamesOf(chunk.ComponentIds);
            foreach (var id in chunk.Entities)
            {
                rows.Add(new EntityRow(unchecked((long)id), names));
            }
        }

        rows.Sort((a, b) => a.Id.CompareTo(b.Id));
        return rows;
    }

    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>? Read(long id)
    {
        var entity = unchecked((ulong)id);
        if (!_world.IsAlive(entity))
        {
            return null;
        }

        var read = new OrderedDictionary<string, IReadOnlyDictionary<string, object?>>();
        foreach (var name in _sortedNames.Where(n => _world.Has(entity, _byName[n])))
        {
            read[name] = Describe(entity, _byName[name]);
        }

        return read;
    }

    public void Write(long id, string component, string field, double value)
    {
        var type = TypeNamed(component);
        if (!_fields.TryGetValue(component, out var fields))
        {
            throw new ArgumentException($"{component} has no field names.");
        }

        var index = Array.IndexOf(fields, field);
        if (index < 0)
        {
            throw new ArgumentException($"{field} is not a field of {component}.");
        }

        var entity = unchecked((ulong)id);
        if (!_world.IsAlive(entity))
        {
            throw new DeadEntityException(entity);
        }

        if (!_world.TryGetBytes(entity, type, out var bytes))
        {
            throw new ArgumentException($"{id} has no {component}.");
        }

        Put(bytes, type.Kind, index, value);
    }

    // The core hands back a view onto its own memory, so a float is read in the host's byte order.
    private static object Get(ReadOnlySpan<byte> data, ComponentKind kind, int index)
    {
        var at = data[(index * kind.BytesPerElement())..];
        return kind switch
        {
            ComponentKind.Float32 => Finite(MemoryMarshal.Read<float>(at)),
            ComponentKind.Float64 => Finite(MemoryMarshal.Read<double>(at)),
            ComponentKind.Int32 => (long)MemoryMarshal.Read<int>(at),
            ComponentKind.UInt32 => (long)MemoryMarshal.Read<uint>(at),
            ComponentKind.Int64 => MemoryMarshal.Read<long>(at),
            _ => (long)at[0],
        };
    }

    // A float that is not finite is its name, because that is exactly what someone looking for a
    // broken transform has found, and a JSON number cannot hold it.
    private static object Finite(double value) => double.IsFinite(value)
        ? value
        : double.IsNaN(value) ? "NaN" : value > 0 ? "Infinity" : "-Infinity";

    // shortcut: an integer wraps to the width of its column, and one past 2^53 loses its low bits
    // through the double. Add a range check when a component has a field where that hides a mistake.
    private static void Put(Span<byte> data, ComponentKind kind, int index, double value)
    {
        var at = data[(index * kind.BytesPerElement())..];
        if (kind is not (ComponentKind.Float32 or ComponentKind.Float64) && value % 1 != 0)
        {
            throw new ArgumentException($"{value} must be whole for {kind}.");
        }

        switch (kind)
        {
            case ComponentKind.Float32:
                MemoryMarshal.Write(at, (float)value);
                break;
            case ComponentKind.Float64:
                MemoryMarshal.Write(at, value);
                break;
            case ComponentKind.Int32:
            case ComponentKind.UInt32:
                MemoryMarshal.Write(at, unchecked((int)(long)value));
                break;
            case ComponentKind.Int64:
                MemoryMarshal.Write(at, (long)value);
                break;
            default:
                at[0] = unchecked((byte)(long)value);
                break;
        }
    }

    private ComponentType TypeNamed(string name) =>
        _byName.TryGetValue(name, out var type) ? type : throw new ArgumentException($"Unknown component {name}.");

    private string[] NamesOf(uint[] ids) =>
        [.. ids.Select(id => _byId.TryGetValue(id, out var type) ? type.Name : $"#{id}").Order(StringComparer.Ordinal)];

    private OrderedDictionary<string, object?> Describe(ulong entity, ComponentType type)
    {
        var described = new OrderedDictionary<string, object?>();
        if (!_fields.TryGetValue(type.Name, out var fields))
        {
            described["undescribed"] = $"{KindName(type.Kind)} x{type.Arity}";
            return described;
        }

        _ = _world.TryGetBytes(entity, type, out var data);
        for (var i = 0; i < fields.Length; i++)
        {
            described[fields[i]] = Get(data, type.Kind, i);
        }

        return described;
    }

    private static string KindName(ComponentKind kind) => kind switch
    {
        ComponentKind.Float32 => "float32",
        ComponentKind.Float64 => "float64",
        ComponentKind.Int32 => "int32",
        ComponentKind.UInt32 => "uint32",
        ComponentKind.Int64 => "int64",
        _ => "uint8",
    };
}
