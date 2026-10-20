using System.Runtime.InteropServices;
using Orblit.Interop;

namespace Orblit.Core;

/// <summary>
/// The C ABI of <c>orblit_core.h</c>, declared and nothing else. Handles are
/// <see cref="nint"/>. What a result means and who owns it is in
/// <see cref="World"/>.
/// </summary>
internal static partial class NativeMethods
{
    private const string Library = "orblit_core";

    static NativeMethods() => NativeLoader.Register(typeof(NativeMethods).Assembly, Library);

    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct Transforms
    {
        public readonly uint Local;
        public readonly uint World;
        public readonly uint Parent;
    }

    [LibraryImport(Library, EntryPoint = "orblit_world_create")]
    internal static partial nint WorldCreate();

    [LibraryImport(Library, EntryPoint = "orblit_world_destroy")]
    internal static partial void WorldDestroy(nint world);

    [LibraryImport(Library, EntryPoint = "orblit_world_version")]
    internal static partial ulong WorldVersion(nint world);

    [LibraryImport(Library, EntryPoint = "orblit_world_tick")]
    internal static partial void WorldTick(nint world, double delta);

    [LibraryImport(Library, EntryPoint = "orblit_world_elapsed")]
    internal static partial double WorldElapsed(nint world);

    [LibraryImport(Library, EntryPoint = "orblit_component_register", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial uint ComponentRegister(nint world, string name, uint size, uint alignment);

    [LibraryImport(Library, EntryPoint = "orblit_component_count")]
    internal static partial uint ComponentCount(nint world);

    [LibraryImport(Library, EntryPoint = "orblit_entity_create")]
    internal static partial ulong EntityCreate(nint world);

    [LibraryImport(Library, EntryPoint = "orblit_entity_destroy")]
    internal static partial void EntityDestroy(nint world, ulong entity);

    [LibraryImport(Library, EntryPoint = "orblit_entity_alive")]
    [return: MarshalAs(UnmanagedType.U1)]
    internal static partial bool EntityAlive(nint world, ulong entity);

    [LibraryImport(Library, EntryPoint = "orblit_entity_count")]
    internal static partial uint EntityCount(nint world);

    [LibraryImport(Library, EntryPoint = "orblit_entity_add")]
    [return: MarshalAs(UnmanagedType.U1)]
    internal static partial bool EntityAdd(nint world, ulong entity, uint component, nint value);

    [LibraryImport(Library, EntryPoint = "orblit_entity_remove")]
    [return: MarshalAs(UnmanagedType.U1)]
    internal static partial bool EntityRemove(nint world, ulong entity, uint component);

    [LibraryImport(Library, EntryPoint = "orblit_entity_has")]
    [return: MarshalAs(UnmanagedType.U1)]
    internal static partial bool EntityHas(nint world, ulong entity, uint component);

    [LibraryImport(Library, EntryPoint = "orblit_entity_get")]
    internal static partial nint EntityGet(nint world, ulong entity, uint component);

    [LibraryImport(Library, EntryPoint = "orblit_query_create")]
    internal static partial nint QueryCreate(nint world, ReadOnlySpan<uint> components, uint count);

    [LibraryImport(Library, EntryPoint = "orblit_query_destroy")]
    internal static partial void QueryDestroy(nint query);

    [LibraryImport(Library, EntryPoint = "orblit_query_chunk_count")]
    internal static partial uint QueryChunkCount(nint query);

    [LibraryImport(Library, EntryPoint = "orblit_query_chunk_length")]
    internal static partial uint QueryChunkLength(nint query, uint chunk);

    [LibraryImport(Library, EntryPoint = "orblit_query_chunk_column")]
    internal static partial nint QueryChunkColumn(nint query, uint chunk, uint slot);

    [LibraryImport(Library, EntryPoint = "orblit_query_chunk_entities")]
    internal static partial nint QueryChunkEntities(nint query, uint chunk);

    [LibraryImport(Library, EntryPoint = "orblit_query_chunk_components")]
    internal static partial uint QueryChunkComponents(nint query, uint chunk, Span<uint> output, uint capacity);

    [LibraryImport(Library, EntryPoint = "orblit_query_chunk_component_column")]
    internal static partial nint QueryChunkComponentColumn(nint query, uint chunk, uint component);

    [LibraryImport(Library, EntryPoint = "orblit_transform_register")]
    internal static partial Transforms TransformRegister(nint world);

    [LibraryImport(Library, EntryPoint = "orblit_transform_propagate")]
    internal static partial uint TransformPropagate(nint world);

    /// <summary>A view of native memory. The one place a pointer becomes a span.</summary>
    internal static unsafe Span<T> View<T>(nint pointer, int count)
        where T : unmanaged => new((void*)pointer, count);
}
