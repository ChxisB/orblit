using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using Orblit.VectorMath;
using static Orblit.Parity.Dump;

namespace Orblit.Terrain.Tests;

/// <summary>Writes what the terrain port computed in the shape the Dart exporter wrote.</summary>
internal static class TerrainDump
{
    /// <summary>The bytes of a typed array, as the exporter's dataB64 wrote them.</summary>
    public static string Base64<T>(T[] items)
        where T : unmanaged =>
        Convert.ToBase64String(MemoryMarshal.AsBytes(items.AsSpan()));

    public static JsonArray KeyJson(RegionKey key) => new(Big(key.X), Big(key.Z));

    public static JsonNode? Vec(Vector3? v) =>
        v is { } vector ? new JsonArray(Num(vector.X), Num(vector.Y), Num(vector.Z)) : null;
}
