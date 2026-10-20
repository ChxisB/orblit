using System.Runtime.InteropServices;

namespace Orblit.Core;

/// <summary>
/// A standing question about which entities carry a set of components.
/// Queries are worth keeping across frames: the matching archetypes are cached
/// and only recomputed when the world's structure actually changes.
/// </summary>
public sealed class Query : IDisposable
{
    private readonly World _world;
    private nint _query;

    internal Query(World world, nint query, ComponentType[] types)
    {
        _world = world;
        _query = query;
        Types = types;
    }

    /// <summary>The queried components, in slot order.</summary>
    public IReadOnlyList<ComponentType> Types { get; }

    /// <summary>The matching runs. Iterating recomputes the match once, at the start.</summary>
    public ChunkEnumerable Chunks => new(Alive, Types);

    /// <summary>How many entities the query currently matches.</summary>
    public int EntityCount
    {
        get
        {
            var query = Alive;
            var total = 0;
            var count = NativeMethods.QueryChunkCount(query);
            for (uint i = 0; i < count; i++)
            {
                total += (int)NativeMethods.QueryChunkLength(query, i);
            }

            return total;
        }
    }

    private nint Alive
    {
        get
        {
            if (_query == nint.Zero)
            {
                throw new InvalidOperationException("This Query has been disposed.");
            }

            // A query over a destroyed world would read freed memory.
            _ = _world.Alive;
            return _query;
        }
    }

    public void Dispose()
    {
        if (_query == nint.Zero)
        {
            return;
        }

        NativeMethods.QueryDestroy(_query);
        _query = nint.Zero;
    }
}

/// <summary>What <c>foreach</c> over <see cref="Query.Chunks"/> walks.</summary>
public readonly ref struct ChunkEnumerable
{
    private readonly nint _query;
    private readonly IReadOnlyList<ComponentType> _types;

    internal ChunkEnumerable(nint query, IReadOnlyList<ComponentType> types)
    {
        _query = query;
        _types = types;
    }

    /// <summary>How many non-empty runs there are.</summary>
    public int Count
    {
        get
        {
            var count = 0;
            var chunks = GetEnumerator();
            while (chunks.MoveNext())
            {
                count++;
            }

            return count;
        }
    }

    public ChunkEnumerator GetEnumerator() => new(_query, _types);

    /// <summary>The first non-empty run. Throws if there is none.</summary>
    public Chunk First()
    {
        foreach (var chunk in this)
        {
            return chunk;
        }

        throw new InvalidOperationException("The query matches no entities.");
    }
}

public ref struct ChunkEnumerator
{
    private readonly nint _query;
    private readonly IReadOnlyList<ComponentType> _types;
    private readonly uint _count;
    private uint _next;

    internal ChunkEnumerator(nint query, IReadOnlyList<ComponentType> types)
    {
        _query = query;
        _types = types;
        _count = NativeMethods.QueryChunkCount(query);
        _next = 0;
        Current = default;
    }

    public Chunk Current { get; private set; }

    public bool MoveNext()
    {
        while (_next < _count)
        {
            var index = _next++;
            var length = NativeMethods.QueryChunkLength(_query, index);
            if (length > 0)
            {
                Current = new Chunk(_query, index, _types, (int)length);
                return true;
            }
        }

        return false;
    }
}

/// <summary>
/// One run of entities that all carry the queried components.
/// The columns are windows onto the engine's own memory. Reading and writing
/// them touches no boundary and copies nothing, which is the whole reason a
/// system asks for a chunk instead of asking about an entity.
/// A chunk is valid until the next structural change to the world: creating or
/// destroying an entity, or adding or removing a component. Those move rows
/// between archetypes, and a view taken before one is a view onto memory that
/// has since been reused. Finish the loop, then mutate.
/// </summary>
public readonly ref struct Chunk
{
    private readonly nint _query;
    private readonly uint _index;
    private readonly IReadOnlyList<ComponentType> _types;

    internal Chunk(nint query, uint index, IReadOnlyList<ComponentType> types, int length)
    {
        _query = query;
        _index = index;
        _types = types;
        Length = length;
    }

    /// <summary>How many entities this run holds.</summary>
    public int Length { get; }

    /// <summary>The entity handles, in the same order as every column.</summary>
    public ReadOnlySpan<ulong> Entities =>
        NativeMethods.View<ulong>(NativeMethods.QueryChunkEntities(_query, _index), Length);

    /// <summary>
    /// A column of float32 components, laid out end to end: entity <c>i</c>'s
    /// elements start at <c>i * arity</c>.
    /// </summary>
    public Span<float> Float32(int slot) => Column<float>(slot, ComponentKind.Float32);

    public Span<double> Float64(int slot) => Column<double>(slot, ComponentKind.Float64);

    public Span<int> Int32(int slot) => Column<int>(slot, ComponentKind.Int32);

    public Span<uint> UInt32(int slot) => Column<uint>(slot, ComponentKind.UInt32);

    public Span<long> Int64(int slot) => Column<long>(slot, ComponentKind.Int64);

    /// <summary>The raw bytes of a column, for a component with no natural element type.</summary>
    public Span<byte> Bytes(int slot) => NativeMethods.View<byte>(ColumnPointer(slot), Length * _types[slot].ByteSize);

    /// <summary>
    /// Every component the entities in this run carry, including ones the query
    /// did not ask for. The whole run shares one component set, so this is
    /// answered once per run rather than once per entity, which is what lets
    /// replication decide what to send without walking the world.
    /// </summary>
    public uint[] ComponentIds
    {
        get
        {
            var count = (int)NativeMethods.QueryChunkComponents(_query, _index, default, 0);
            var ids = new uint[count];
            NativeMethods.QueryChunkComponents(_query, _index, ids, (uint)count);
            return ids;
        }
    }

    /// <summary>
    /// The raw bytes of a component this run carries, whether or not the query
    /// named it. False when the component is absent.
    /// </summary>
    public bool TryGetBytes(ComponentType type, out Span<byte> column)
    {
        var pointer = NativeMethods.QueryChunkComponentColumn(_query, _index, type.Id);
        column = pointer == nint.Zero ? default : NativeMethods.View<byte>(pointer, Length * type.ByteSize);
        return pointer != nint.Zero;
    }

    /// <summary>A float32 column addressed by component rather than by slot.</summary>
    public bool TryGetFloat32(ComponentType type, out Span<float> column)
    {
        var pointer = NativeMethods.QueryChunkComponentColumn(_query, _index, type.Id);
        column = pointer == nint.Zero ? default : NativeMethods.View<float>(pointer, Length * type.Arity);
        return pointer != nint.Zero;
    }

    private nint ColumnPointer(int slot)
    {
        var pointer = NativeMethods.QueryChunkColumn(_query, _index, (uint)slot);
        return pointer == nint.Zero
            ? throw new ArgumentOutOfRangeException(nameof(slot), slot, "No column at this slot for this query.")
            : pointer;
    }

    private Span<T> Column<T>(int slot, ComponentKind kind)
        where T : unmanaged
    {
        if (_types[slot].Kind != kind)
        {
            throw new ArgumentException($"Slot {slot} is {_types[slot].Kind}, not {kind}.", nameof(slot));
        }

        return NativeMethods.View<T>(ColumnPointer(slot), Length * _types[slot].Arity);
    }
}
