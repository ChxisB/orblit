using System.Buffers.Binary;
using System.Globalization;
using Orblit.Parity;

namespace Orblit.Scene.Tests;

/// <summary>
/// Windows and Linux round a sine or a cosine in the last bit, so a written float that should be zero
/// comes out as 1e-16 on one system and 0 on another. The fixture was made on macOS. A float that this run
/// and the fixture agree on to the parity tolerance is taken as the fixture's. Every other byte, indices
/// included, is left as written and compared exactly.
/// </summary>
internal static class ExportedBuffers
{
    private const int FloatComponent = 5126;
    private const int FloatBytes = 4;

    /// <summary>
    /// <paramref name="written"/> with each float the <paramref name="header"/> addresses replaced by the
    /// fixture's where the two are close. Unchanged when there is no fixture or it is another length.
    /// </summary>
    public static byte[] ToFixtureWhereClose(JsonMap header, byte[] written, byte[]? fixture)
    {
        if (fixture is null || fixture.Length != written.Length)
        {
            return written;
        }

        var snapped = (byte[])written.Clone();
        foreach (var at in FloatOffsets(header, written.Length).Where(at => Close(fixture, written, at)))
        {
            fixture.AsSpan(at, FloatBytes).CopyTo(snapped.AsSpan(at));
        }

        return snapped;
    }

    private static bool Close(byte[] fixture, byte[] written, int at) => Tolerance.Close(
        BinaryPrimitives.ReadSingleLittleEndian(fixture.AsSpan(at)),
        BinaryPrimitives.ReadSingleLittleEndian(written.AsSpan(at)));

    private static IEnumerable<int> FloatOffsets(JsonMap header, int length)
    {
        if (!header.TryGetValue("accessors", out var accessors) || accessors is not JsonList accessorList)
        {
            return [];
        }

        var views = header["bufferViews"] as JsonList ?? [];
        return accessorList.Cast<JsonMap>()
            .Where(accessor => Number(accessor, "componentType") == FloatComponent && accessor.ContainsKey("bufferView"))
            .SelectMany(accessor => Offsets(accessor, (JsonMap)views[Number(accessor, "bufferView")]!))
            .Where(at => at >= 0 && at + FloatBytes <= length);
    }

    private static IEnumerable<int> Offsets(JsonMap accessor, JsonMap view)
    {
        var width = ComponentsOf((string)accessor["type"]!);
        var stride = view.ContainsKey("byteStride") ? Number(view, "byteStride") : width * FloatBytes;
        var start = Number(view, "byteOffset") + Number(accessor, "byteOffset");
        for (var element = 0; element < Number(accessor, "count"); element++)
        {
            for (var part = 0; part < width; part++)
            {
                yield return start + (element * stride) + (part * FloatBytes);
            }
        }
    }

    private static int ComponentsOf(string type) => type switch
    {
        "SCALAR" => 1,
        "VEC2" => 2,
        "VEC3" => 3,
        "VEC4" or "MAT2" => 4,
        "MAT3" => 9,
        "MAT4" => 16,
        _ => throw new FormatException($"Accessor type {type} is not a glTF type."),
    };

    // glTF leaves out a zero offset, and a parsed number may be a long or a double.
    private static int Number(JsonMap map, string key) =>
        map.TryGetValue(key, out var value) ? Convert.ToInt32(value, CultureInfo.InvariantCulture) : 0;
}
