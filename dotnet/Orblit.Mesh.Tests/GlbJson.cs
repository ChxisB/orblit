using System.Buffers.Binary;
using System.Text;
using Orblit.Json;

namespace Orblit.Mesh.Tests;

/// <summary>
/// Reads the JSON chunk back out of a glb, without the library's own reader, so a test of the writer
/// does not lean on the reader being right.
/// </summary>
internal static class GlbJson
{
    internal static IReadOnlyDictionary<string, object?> Of(byte[] glb)
    {
        var length = (int)BinaryPrimitives.ReadUInt32LittleEndian(glb.AsSpan(12));
        var text = Encoding.UTF8.GetString(glb, 20, length).TrimEnd();
        return (IReadOnlyDictionary<string, object?>)JsonText.Decode(text)!;
    }

    internal static IReadOnlyDictionary<string, object?> Map(object? value) => (IReadOnlyDictionary<string, object?>)value!;

    internal static IReadOnlyList<object?> List(object? value) => (IReadOnlyList<object?>)value!;

    /// <summary>A JSON number as a double, whether it was written as a whole number or not.</summary>
    internal static double Number(object? value) => JsonValues.Num(value)!.Value;

    /// <summary>The primitives of the first mesh in the document.</summary>
    internal static IReadOnlyList<object?> PrimitivesOf(IReadOnlyDictionary<string, object?> json) =>
        List(Map(List(json["meshes"])[0])["primitives"]);
}
