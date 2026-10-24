using System.Globalization;
using Orblit.Mesh;

namespace Orblit.Stage.Tests;

// A glb as a test reads it. The JSON is maps and lists of whatever number type the decoder chose, so
// the number helpers take a value as it comes.
internal sealed class Glb
{
    public Glb(byte[] bytes)
    {
        var (json, binary) = GltfFiles.GltfParts(bytes);
        Json = json;
        Accessors = new GltfAccessors(json, binary);
    }

    public IReadOnlyDictionary<string, object?> Json { get; }

    public GltfAccessors Accessors { get; }

    public IReadOnlyDictionary<string, object?> Primitive =>
        Map(List(First("meshes")["primitives"])[0]);

    public IReadOnlyList<object?> ListOf(string name) => List(Json[name]);

    public IReadOnlyDictionary<string, object?> First(string name) => Map(ListOf(name)[0]);

    public int Attribute(string name) => Whole(Map(Primitive["attributes"])[name]);

    public static IReadOnlyList<object?> List(object? value) => (IReadOnlyList<object?>)value!;

    public static IReadOnlyDictionary<string, object?> Map(object? value) =>
        (IReadOnlyDictionary<string, object?>)value!;

    public static int Whole(object? value) => Convert.ToInt32(value, CultureInfo.InvariantCulture);

    public static double Number(object? value) => Convert.ToDouble(value, CultureInfo.InvariantCulture);
}
