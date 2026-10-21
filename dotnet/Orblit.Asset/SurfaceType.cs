using System.Diagnostics.CodeAnalysis;

namespace Orblit.Asset;

/// <summary>The types a surface uniform can have.</summary>
public enum SurfaceType
{
    Boolean,
    [SuppressMessage("Naming", "CA1720:Identifier contains type name", Justification = "The Dart name, for the shader word int.")]
    Integer,
    Scalar,
    Vector2,
    Vector3,
    Vector4,
    Texture,
}

/// <summary>What each <see cref="SurfaceType"/> is called by the author and by the material file.</summary>
public static class SurfaceTypeTools
{
    // In the order of the enum.
    private static readonly (string Word, string MatType, int Width)[] Rows =
    [
        ("bool", "bool", 1),
        ("int", "int", 1),
        ("float", "float", 1),
        ("vec2", "float2", 2),
        ("vec3", "float3", 3),
        ("vec4", "float4", 4),
        ("sampler2D", "sampler2d", 0),
    ];

    /// <summary>What the author writes.</summary>
    public static string Word(this SurfaceType type) => Rows[(int)type].Word;

    /// <summary>What the material file calls it.</summary>
    public static string MatType(this SurfaceType type) => Rows[(int)type].MatType;

    /// <summary>How many numbers a value takes. A texture takes none.</summary>
    public static int Width(this SurfaceType type) => Rows[(int)type].Width;

    /// <summary>The type the author's word names, or null.</summary>
    public static SurfaceType? OfWord(string word)
    {
        for (var i = 0; i < Rows.Length; i++)
        {
            if (Rows[i].Word == word)
            {
                return (SurfaceType)i;
            }
        }

        return null;
    }
}
