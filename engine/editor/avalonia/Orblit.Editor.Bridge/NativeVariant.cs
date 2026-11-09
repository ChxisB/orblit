using System.Runtime.InteropServices;

namespace Orblit.Editor.Bridge;

/// <summary>
/// One engine Variant. It holds a string, a whole number, a number, a boolean or an object. Dispose it to free
/// the engine's copy.
/// </summary>
internal sealed unsafe class NativeVariant : IDisposable
{
    // The engine is built with single precision, where the dump gives a Variant 24 bytes. It starts zeroed, which is a Nil.
    const int Size = 24;

    readonly void* _slot = NativeMemory.AllocZeroed(Size);

    NativeVariant()
    {
    }

    internal void* Slot => _slot;

    internal static NativeVariant Create() => new();

    /// <summary>A Variant of this value. Null is a Nil. A value of any other type than the five throws.</summary>
    internal static NativeVariant From(object? value)
    {
        var made = new NativeVariant();
        switch (value)
        {
            case null:
                break;
            case bool flag:
                byte raw = (byte)(flag ? 1 : 0);
                made.Fill(VariantType.Bool, &raw);
                break;
            case long whole:
                made.Fill(VariantType.Int, &whole);
                break;
            case double number:
                made.Fill(VariantType.Float, &number);
                break;
            case nint pointer:
                made.Fill(VariantType.Object, &pointer);
                break;
            case string text:
                using (var scope = new NativeScope())
                {
                    made.Fill(VariantType.String, scope.String(text));
                }
                break;
            default:
                made.Dispose();
                throw new ArgumentException($"A {value.GetType().Name} cannot be a Variant.", nameof(value));
        }
        return made;
    }

    /// <summary>
    /// The value as a string, a boolean, a double, a long or a nint for an object, and null for a Nil. A Variant of
    /// any other type reads as null too.
    /// shortcut: other types read as null, add a case when the editor shows one.
    /// </summary>
    internal object? Read()
    {
        switch ((VariantType)NativeApi.VariantGetType(_slot))
        {
            case VariantType.Bool:
                ulong flag = 0;
                Take(VariantType.Bool, &flag);
                return (flag & 0xFF) != 0;
            case VariantType.Int:
                long whole = 0;
                Take(VariantType.Int, &whole);
                return whole;
            case VariantType.Float:
                double number = 0;
                Take(VariantType.Float, &number);
                return number;
            case VariantType.Object:
                nint pointer = 0;
                Take(VariantType.Object, &pointer);
                return pointer == 0 ? null : pointer;
            case VariantType.String:
                using (var scope = new NativeScope())
                {
                    var text = scope.Result(VariantType.String);
                    Take(VariantType.String, text);
                    return NativeText.ReadString(text);
                }
            default:
                return null;
        }
    }

    void Fill(VariantType type, void* raw) => NativeApi.VariantFromType((int)type)(_slot, raw);

    void Take(VariantType type, void* raw) => NativeApi.VariantToType((int)type)(raw, _slot);

    public void Dispose()
    {
        NativeApi.VariantDestroy(_slot);
        NativeMemory.Free(_slot);
    }
}
