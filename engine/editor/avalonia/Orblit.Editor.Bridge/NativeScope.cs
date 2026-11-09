using System.Runtime.InteropServices;

namespace Orblit.Editor.Bridge;

/// <summary>
/// Owns the engine strings made for one call. A String or StringName is one pointer that the engine
/// assigns into, so each lives in a zeroed 8-byte slot until the scope ends.
/// </summary>
internal sealed unsafe class NativeScope : IDisposable
{
    readonly List<(nint Slot, VariantType Type)> _slots = [];

    public void* String(string text) => Make(VariantType.String, text);

    public void* StringName(string text) => Make(VariantType.StringName, text);

    /// <summary>An empty slot for the engine to write a String or StringName result into.</summary>
    public void* Result(VariantType type)
    {
        var slot = NativeMemory.AllocZeroed((nuint)sizeof(void*));
        _slots.Add(((nint)slot, type));
        return slot;
    }

    void* Make(VariantType type, string text)
    {
        var slot = Result(type);
        var utf8 = Marshal.StringToCoTaskMemUTF8(text);
        try
        {
            if (type == VariantType.String)
            {
                NativeApi.StringNew(slot, (byte*)utf8);
            }
            else
            {
                NativeApi.StringNameNew(slot, (byte*)utf8);
            }
        }
        finally
        {
            Marshal.FreeCoTaskMem(utf8);
        }
        return slot;
    }

    public void Dispose()
    {
        foreach (var (slot, type) in _slots)
        {
            NativeApi.VariantDestructor((int)type)((void*)slot);
            NativeMemory.Free((void*)slot);
        }
        _slots.Clear();
    }
}
