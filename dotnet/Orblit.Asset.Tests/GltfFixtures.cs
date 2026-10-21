using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Nodes;
using Orblit.Json;

namespace Orblit.Asset.Tests;

internal static class GltfFixtures
{
    /// <summary>A glTF map read from JSON text, so a test can write the document as a glTF file shows it.</summary>
    internal static GltfMap Map(string text) => (GltfMap)DartJson.Decode(text)!;

    /// <summary>A GLB holding <paramref name="json"/> and <paramref name="binary"/>, for tests that need a real container rather than a mock of one.</summary>
    internal static byte[] Glb(GltfMap json, byte[] binary)
    {
        var text = Encoding.UTF8.GetBytes(DartJson.Encode(json));
        var jsonLength = text.Length + ((4 - (text.Length % 4)) % 4);
        var binLength = binary.Length + ((4 - (binary.Length % 4)) % 4);
        var length = 12 + 8 + jsonLength + 8 + binLength;

        var glb = new byte[length];
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(0), 0x46546C67);
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(4), 2);
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(8), (uint)length);
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(12), (uint)jsonLength);
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(16), 0x4E4F534A);
        glb.AsSpan(20, jsonLength).Fill(0x20);
        text.CopyTo(glb, 20);
        var at = 20 + jsonLength;
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(at), (uint)binLength);
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(at + 4), 0x004E4942);
        binary.CopyTo(glb, at + 8);
        return glb;
    }

    /// <summary>A glb as its JSON text and its binary chunk, the way the Dart exporters wrote it.</summary>
    internal static JsonObject GlbParts(byte[] glb)
    {
        var at = 20 + (int)BinaryPrimitives.ReadUInt32LittleEndian(glb.AsSpan(12));
        var end = at + 8 + (int)BinaryPrimitives.ReadUInt32LittleEndian(glb.AsSpan(at));
        return new JsonObject
        {
            ["json"] = Encoding.UTF8.GetString(glb, 20, at - 20),
            ["bin"] = Convert.ToBase64String(glb, at + 8, end - at - 8),
        };
    }

    internal static byte[] Bytes(params float[] values) => MemoryMarshal.AsBytes(values.AsSpan()).ToArray();

    internal static byte[] Bytes(params ushort[] values) => MemoryMarshal.AsBytes(values.AsSpan()).ToArray();

    internal static float[] Floats(ReadOnlyMemory<byte> bytes) => MemoryMarshal.Cast<byte, float>(bytes.Span).ToArray();
}
