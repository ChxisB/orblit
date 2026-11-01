using System.Runtime.InteropServices;

namespace Orblit.Core;

/// <summary>
/// An entity-component world.
/// The caller owns the lifetime and drives the frame; the storage and the
/// systems that run per entity live in C++. See <see cref="Query"/> for the
/// reason that split is worth the boundary.
/// </summary>
public sealed class World : IDisposable
{
    private nint _world;

    public World()
    {
        _world = NativeMethods.WorldCreate();
        if (_world == nint.Zero)
        {
            throw new InvalidOperationException("Could not create an Orblit world.");
        }
    }

    public bool IsDisposed => _world == nint.Zero;

    /// <summary>
    /// Increments on every structural change. A view taken at one version is not
    /// safe to use at another.
    /// </summary>
    public ulong Version => NativeMethods.WorldVersion(Alive);

    public int EntityCount => (int)NativeMethods.EntityCount(Alive);

    public int ComponentCount => (int)NativeMethods.ComponentCount(Alive);

    /// <summary>Seconds accumulated across <see cref="Tick"/> calls.</summary>
    public double Elapsed => NativeMethods.WorldElapsed(Alive);

    /// <summary>
    /// This world as the C ABI sees it. Public because another front end onto the
    /// same core needs it: a native script is handed exactly this pointer. One
    /// held past <see cref="Dispose"/> points at freed memory.
    /// </summary>
    public nint NativeHandle => Alive;

    internal nint Alive => _world != nint.Zero
        ? _world
        : throw new InvalidOperationException("This World has been disposed.");

    /// <summary>
    /// Registers a component type, or returns the existing one if
    /// <paramref name="name"/> was already registered with this exact layout.
    /// </summary>
    public ComponentType RegisterComponent(string name, ComponentKind kind, int arity = 1)
    {
        var size = kind.BytesPerElement() * arity;
        var id = NativeMethods.ComponentRegister(Alive, name, (uint)size, (uint)kind.BytesPerElement());
        if (id == 0)
        {
            throw new ArgumentException($"Component \"{name}\" is already registered with a different layout.", nameof(name));
        }

        return new ComponentType(id, name, kind, arity);
    }

    public ulong CreateEntity() => NativeMethods.EntityCreate(Alive);

    public void DestroyEntity(ulong entity) => NativeMethods.EntityDestroy(Alive, entity);

    public bool IsAlive(ulong entity) => NativeMethods.EntityAlive(Alive, entity);

    /// <summary>Adds <paramref name="type"/> to <paramref name="entity"/>, zeroed.</summary>
    public void Add(ulong entity, ComponentType type) => AddRaw(entity, type, nint.Zero);

    /// <summary>
    /// Adds <paramref name="type"/> to <paramref name="entity"/> with the first
    /// <see cref="ComponentType.ByteSize"/> bytes of <paramref name="value"/>.
    /// </summary>
    public unsafe void Add<T>(ulong entity, ComponentType type, ReadOnlySpan<T> value)
        where T : unmanaged
    {
        var bytes = MemoryMarshal.AsBytes(value);
        if (bytes.Length < type.ByteSize)
        {
            throw new ArgumentException($"{type.Name} needs {type.ByteSize} bytes and got {bytes.Length}.", nameof(value));
        }

        fixed (byte* pointer = bytes)
        {
            AddRaw(entity, type, (nint)pointer);
        }
    }

    public bool Remove(ulong entity, ComponentType type) => NativeMethods.EntityRemove(Alive, entity, type.Id);

    public bool Has(ulong entity, ComponentType type) => NativeMethods.EntityHas(Alive, entity, type.Id);

    /// <summary>
    /// A view of one entity's component, or false if it does not have it. Valid
    /// until the next structural change.
    /// Convenient, and the slow path by construction: a system touching many
    /// entities should take a <see cref="Query"/> instead, which costs one
    /// crossing rather than one per entity.
    /// </summary>
    public bool TryGetFloat32(ulong entity, ComponentType type, out Span<float> value)
    {
        var pointer = NativeMethods.EntityGet(Alive, entity, type.Id);
        value = pointer == nint.Zero ? default : NativeMethods.View<float>(pointer, type.Arity);
        return pointer != nint.Zero;
    }

    /// <summary>The raw bytes of one entity's component. See <see cref="TryGetFloat32"/>.</summary>
    public bool TryGetBytes(ulong entity, ComponentType type, out Span<byte> value)
    {
        var pointer = NativeMethods.EntityGet(Alive, entity, type.Id);
        value = pointer == nint.Zero ? default : NativeMethods.View<byte>(pointer, type.ByteSize);
        return pointer != nint.Zero;
    }

    /// <summary>
    /// A standing query over every entity carrying all of <paramref name="types"/>.
    /// The caller owns it and should keep it rather than rebuild it per frame.
    /// </summary>
    public Query Query(params ReadOnlySpan<ComponentType> types)
    {
        Span<uint> ids = stackalloc uint[types.Length];
        for (var i = 0; i < types.Length; i++)
        {
            ids[i] = types[i].Id;
        }

        return new Query(this, NativeMethods.QueryCreate(Alive, ids, (uint)ids.Length), types.ToArray());
    }

    /// <summary>Registers the built-in transform components, or returns the existing ids.</summary>
    public TransformComponents RegisterTransforms()
    {
        var ids = NativeMethods.TransformRegister(Alive);
        return new TransformComponents(
            new ComponentType(ids.Local, "orblit.LocalTransform", ComponentKind.Float32, 10),
            new ComponentType(ids.World, "orblit.WorldTransform", ComponentKind.Float32, 16),
            new ComponentType(ids.Parent, "orblit.Parent", ComponentKind.Int64, 1));
    }

    /// <summary>
    /// Derives every world transform from local transforms and parent links,
    /// returning how many were written. Each entity is resolved once however many
    /// children hang off it, so a deep chain costs its depth rather than its depth
    /// times its breadth.
    /// </summary>
    public int PropagateTransforms() => (int)NativeMethods.TransformPropagate(Alive);

    /// <summary>Runs the native systems once. Managed systems run around this, over views.</summary>
    public void Tick(double delta) => NativeMethods.WorldTick(Alive, delta);

    public void Dispose()
    {
        if (_world == nint.Zero)
        {
            return;
        }

        NativeMethods.WorldDestroy(_world);
        _world = nint.Zero;
    }

    private void AddRaw(ulong entity, ComponentType type, nint value)
    {
        if (!IsAlive(entity))
        {
            throw new DeadEntityException(entity);
        }

        if (!NativeMethods.EntityAdd(Alive, entity, type.Id, value))
        {
            throw new InvalidOperationException($"Entity {entity} already has {type.Name}.");
        }
    }
}
