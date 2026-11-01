namespace Orblit.Core;

/// <summary>
/// What a component is made of, so a column can be handed back as the right
/// span rather than as bytes the caller has to reinterpret.
/// </summary>
public enum ComponentKind
{
    Float32,
    Float64,
    Int32,
    UInt32,
    Int64,
    UInt8,
}

public static class ComponentKindExtensions
{
    public static int BytesPerElement(this ComponentKind kind) => kind switch
    {
        ComponentKind.Float32 or ComponentKind.Int32 or ComponentKind.UInt32 => 4,
        ComponentKind.Float64 or ComponentKind.Int64 => 8,
        ComponentKind.UInt8 => 1,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Not a component kind."),
    };
}

/// <summary>A registered component type.</summary>
/// <param name="Id">The native id. Zero means no such component.</param>
/// <param name="Name">The registered name.</param>
/// <param name="Kind">The element type.</param>
/// <param name="Arity">
/// How many elements one component holds: 3 for a position, 16 for a matrix,
/// 1 for a scalar.
/// </param>
public sealed record ComponentType(uint Id, string Name, ComponentKind Kind, int Arity)
{
    public int ByteSize => Kind.BytesPerElement() * Arity;

    public override string ToString() => $"ComponentType({Name}, {Kind} x{Arity})";
}

/// <summary>Thrown when a handle is used after the entity it named was destroyed.</summary>
public sealed class DeadEntityException(ulong entity) : InvalidOperationException(
    $"Entity {entity} is not alive. Its slot may have been reused; "
    + "generational handles make that detectable rather than silent.");
