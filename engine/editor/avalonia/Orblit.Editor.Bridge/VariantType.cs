namespace Orblit.Editor.Bridge;

/// <summary>The engine's variant type numbers for the builtin types the bridge handles.</summary>
internal enum VariantType
{
    Nil = 0,
    Bool = 1,
    Int = 2,
    Float = 3,
    String = 4,
    Vector2 = 5,
    Vector3 = 9,
    Color = 20,
    StringName = 21,
    Object = 24,
    Dictionary = 27,
    Array = 28,
    PackedByteArray = 29,
    PackedStringArray = 34,
}
