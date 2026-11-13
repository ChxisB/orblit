namespace Orblit.Editor.App.Graph;

/// <summary>The engine's Variant types by number, as a person reads them. Type 0 is a value of any kind.</summary>
internal static class VariantTypes
{
    static readonly string[] s_names =
    [
        "Any", "Bool", "Int", "Float", "String", "Vector2", "Vector2i", "Rect2", "Rect2i", "Vector3", "Vector3i",
        "Transform2D", "Vector4", "Vector4i", "Plane", "Quaternion", "AABB", "Basis", "Transform3D", "Projection",
        "Color", "StringName", "EntityPath", "RID", "Object", "Callable", "Signal", "Dictionary", "Array",
        "PackedByteArray", "PackedInt32Array", "PackedInt64Array", "PackedFloat32Array", "PackedFloat64Array",
        "PackedStringArray", "PackedVector2Array", "PackedVector3Array", "PackedColorArray", "PackedVector4Array",
    ];

    public static IReadOnlyList<string> Names => s_names;

    public static string NameOf(int type) => type >= 0 && type < s_names.Length ? s_names[type] : $"Type {type}";
}
