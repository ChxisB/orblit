using System.Runtime.InteropServices;
using System.Runtime.InteropServices.JavaScript;

namespace Orblit.Web;

// Same layout as EntityPosition in web_game_bridge.cpp.
[StructLayout(LayoutKind.Sequential)]
public struct EntityPosition
{
    public int Handle;
    public float X;
    public float Y;
    public float Z;
}

// The engine is a second wasm module, so every call goes through the page.
// Each call copies its records between the two memories, about 20 µs per
// 64 KB. Pass many entities per call.
public static partial class WebEngine
{
    // The path starts below the stage root, such as "Main/Cube". Returns 0
    // when it names no Entity3D.
    [JSImport("bindEntity", "orblit-engine")]
    public static partial int BindEntity(string path);

    // Frees a handle from BindEntity. The entity stays in the stage. A handle
    // that is not live is ignored, so a second release does nothing.
    [JSImport("releaseEntity", "orblit-engine")]
    public static partial void Release(int handle);

    public static void SetPositions(Span<EntityPosition> records) =>
        SetPositionBytes(MemoryMarshal.AsBytes(records));

    public static void GetPositions(Span<EntityPosition> records) =>
        GetPositionBytes(MemoryMarshal.AsBytes(records));

    [JSImport("setPositions", "orblit-engine")]
    private static partial void SetPositionBytes([JSMarshalAs<JSType.MemoryView>] Span<byte> records);

    [JSImport("getPositions", "orblit-engine")]
    private static partial void GetPositionBytes([JSMarshalAs<JSType.MemoryView>] Span<byte> records);
}
