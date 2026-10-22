using System.Diagnostics.CodeAnalysis;

namespace Orblit.Asset;

/// <summary>glTF component types, by their numbers.</summary>
[SuppressMessage("Naming", "CA1720:Identifier contains type name", Justification = "glTF's own words for the types.")]
public static class ComponentType
{
    public const int Byte = 5120;
    public const int UnsignedByte = 5121;
    public const int Short = 5122;
    public const int UnsignedShort = 5123;
    public const int UnsignedInt = 5125;
    public const int Float = 5126;

    /// <summary>How wide one component of <paramref name="type"/> is.</summary>
    public static int SizeOf(int type) => type switch
    {
        Byte or UnsignedByte => 1,
        Short or UnsignedShort => 2,
        UnsignedInt or Float => 4,
        _ => throw new FormatException($"{type} is not a glTF component type"),
    };

    /// <summary>How many components a glTF accessor type holds.</summary>
    public static int ComponentsOf(string type) => type switch
    {
        "SCALAR" => 1,
        "VEC2" => 2,
        "VEC3" => 3,
        "VEC4" => 4,
        "MAT2" => 4,
        "MAT3" => 9,
        "MAT4" => 16,
        _ => throw new FormatException($"\"{type}\" is not a glTF accessor type"),
    };
}
