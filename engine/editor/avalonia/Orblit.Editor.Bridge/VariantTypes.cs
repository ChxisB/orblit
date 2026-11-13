namespace Orblit.Editor.Bridge;

/// <summary>The engine's Variant types by number, as a person reads them. Type 0 is a value of any kind.</summary>
public static class VariantTypes
{
    static readonly string[] s_names =
    [
        "Any", "Bool", "Int", "Float", "String", "Vector2", "Vector2i", "Rect2", "Rect2i", "Vector3", "Vector3i",
        "Transform2D", "Vector4", "Vector4i", "Plane", "Quaternion", "AABB", "Basis", "Transform3D", "Projection",
        "Color", "StringName", "EntityPath", "RID", "Object", "Callable", "Signal", "Dictionary", "Array",
        "PackedByteArray", "PackedInt32Array", "PackedInt64Array", "PackedFloat32Array", "PackedFloat64Array",
        "PackedStringArray", "PackedVector2Array", "PackedVector3Array", "PackedColorArray", "PackedVector4Array",
    ];

    // How many parts the engine splits a type into: the first members its Decompose step lists.
    static readonly Dictionary<int, int> s_parts = new()
    {
        [5] = 2, [6] = 2, [7] = 2, [8] = 2, [9] = 3, [10] = 3, [11] = 3, [12] = 4, [13] = 4, [14] = 4,
        [15] = 4, [16] = 2, [17] = 3, [18] = 2, [19] = 4, [20] = 4,
    };

    public static IReadOnlyList<string> Names => s_names;

    /// <summary>The number of parts a value of the type splits into. Zero for a type that does not split.</summary>
    public static int PartsOf(int type) => s_parts.GetValueOrDefault(type);

    public static string NameOf(int type) => type >= 0 && type < s_names.Length ? s_names[type] : $"Type {type}";
}
