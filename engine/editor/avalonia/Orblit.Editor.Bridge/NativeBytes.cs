using System.Runtime.InteropServices;

namespace Orblit.Editor.Bridge;

/// <summary>A PackedByteArray the engine filled in. Dispose it to free the engine's copy.</summary>
public sealed unsafe class NativeBytes : IDisposable
{
    // A PackedByteArray is two pointers. The engine assigns into it, so it starts zeroed.
    const int SlotSize = 16;

    readonly void* _slot = NativeMemory.AllocZeroed(SlotSize);
    bool _disposed;

    NativeBytes()
    {
    }

    internal static NativeBytes Create() => new();

    /// <summary>A copy of the bytes in an engine array. Dispose it after the call that takes it.</summary>
    internal static NativeBytes From(ReadOnlySpan<byte> data)
    {
        var made = new NativeBytes();
        if (data.Length > 0)
        {
            made.Resize(data.Length);
            data.CopyTo(new Span<byte>(NativeApi.ByteArrayAt(made._slot, 0), data.Length));
        }
        return made;
    }

    void Resize(int length)
    {
        using var scope = new NativeScope();
        var resize = NativeApi.BuiltinMethod((int)VariantType.PackedByteArray, scope.StringName("resize"), EngineCalls.PackedByteArrayResizeHash);
        long size = length;
        long error = 0;
        void** args = stackalloc void*[] { &size };
        resize(_slot, args, &error, 1);
        if (error != 0)
        {
            throw new InvalidOperationException($"The engine could not make a byte array of {length} bytes.");
        }
    }

    internal void* Slot => _slot;

    public int Length
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            using var scope = new NativeScope();
            var size = NativeApi.BuiltinMethod((int)VariantType.PackedByteArray, scope.StringName("size"), EngineCalls.PackedByteArraySizeHash);
            long result = 0;
            size(_slot, null, &result, 0);
            return (int)result;
        }
    }

    /// <summary>The bytes, valid until Dispose. The engine keeps them in one block.</summary>
    public ReadOnlySpan<byte> Bytes
    {
        get
        {
            var length = Length;
            return length == 0 ? [] : new ReadOnlySpan<byte>(NativeApi.ByteArrayAt(_slot, 0), length);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        NativeApi.VariantDestructor((int)VariantType.PackedByteArray)(_slot);
        NativeMemory.Free(_slot);
    }
}
