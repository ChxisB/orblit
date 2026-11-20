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

    // The types each type takes a value from besides its own. This follows the engine's strict convert rule, which
    // is what its graph compiler checks a wire against.
    static readonly Dictionary<int, int[]> s_takes = new()
    {
        [1] = [2, 3], [2] = [1, 3], [3] = [1, 2], [4] = [21, 22], [5] = [6], [6] = [5], [7] = [8], [8] = [7],
        [9] = [10], [10] = [9], [11] = [18], [12] = [13], [13] = [12], [15] = [17], [17] = [15],
        [18] = [11, 15, 17, 19], [19] = [18], [20] = [4, 2], [21] = [4], [22] = [4], [23] = [24],
        [28] = [29, 30, 31, 32, 33, 34, 35, 36, 37, 38],
        [29] = [28], [30] = [28], [31] = [28], [32] = [28], [33] = [28], [34] = [28], [35] = [28], [36] = [28],
        [37] = [28], [38] = [28],
    };

    public static IReadOnlyList<string> Names => s_names;

    /// <summary>Whether a value of the first type can go into a pin of the second. Any fits either way.</summary>
    public static bool Fits(int from, int to) =>
        from == 0 || to == 0 || from == to || (s_takes.TryGetValue(to, out var takes) && takes.Contains(from));

    /// <summary>The number of parts a value of the type splits into. Zero for a type that does not split.</summary>
    public static int PartsOf(int type) => s_parts.GetValueOrDefault(type);

    public static string NameOf(int type) => type >= 0 && type < s_names.Length ? s_names[type] : $"Type {type}";
}
