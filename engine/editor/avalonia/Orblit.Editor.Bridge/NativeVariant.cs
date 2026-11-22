using System.Numerics;
using System.Runtime.InteropServices;

namespace Orblit.Editor.Bridge;

/// <summary>
/// One engine Variant. It holds a string, a whole number, a number, a boolean, a vector, a colour or an object.
/// Dispose it to free the engine's copy.
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

    /// <summary>
    /// A Variant of this value. Null is a Nil. A Vector2 and a Vector3 are the engine's vectors, and a Vector4 is a
    /// Color in red, green, blue and alpha. A value of any other type than these throws.
    /// </summary>
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
            case Vector2 flat:
                made.Fill(VariantType.Vector2, &flat);
                break;
            case Vector3 point:
                made.Fill(VariantType.Vector3, &point);
                break;
            case Vector4 color:
                made.Fill(VariantType.Color, &color);
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

    internal object? Read() => Read(_slot);

    /// <summary>
    /// The value as a string, a boolean, a double, a long, a vector, a Vector4 for a colour or a nint for an object,
    /// and null for a Nil. A StringName reads as a string. A Variant of any other type reads as null too.
    /// shortcut: other types read as null, add a case when the editor shows one.
    /// </summary>
    internal static object? Read(void* slot)
    {
        switch ((VariantType)NativeApi.VariantGetType(slot))
        {
            case VariantType.Bool:
                ulong flag = 0;
                Take(slot, VariantType.Bool, &flag);
                return (flag & 0xFF) != 0;
            case VariantType.Int:
                long whole = 0;
                Take(slot, VariantType.Int, &whole);
                return whole;
            case VariantType.Float:
                double number = 0;
                Take(slot, VariantType.Float, &number);
                return number;
            case VariantType.Vector2:
                Vector2 flat = default;
                Take(slot, VariantType.Vector2, &flat);
                return flat;
            case VariantType.Vector3:
                Vector3 point = default;
                Take(slot, VariantType.Vector3, &point);
                return point;
            case VariantType.Color:
                Vector4 color = default;
                Take(slot, VariantType.Color, &color);
                return color;
            case VariantType.Object:
                nint pointer = 0;
                Take(slot, VariantType.Object, &pointer);
                return pointer == 0 ? null : pointer;
            case VariantType.String:
                using (var scope = new NativeScope())
                {
                    var text = scope.Result(VariantType.String);
                    Take(slot, VariantType.String, text);
                    return NativeText.ReadString(text);
                }
            case VariantType.StringName:
                using (var scope = new NativeScope())
                {
                    var name = scope.Result(VariantType.StringName);
                    Take(slot, VariantType.StringName, name);
                    return NativeText.ReadStringName(name);
                }
            default:
                return null;
        }
    }

    /// <summary>
    /// Reads an engine Array of Dictionary, such as a property list. Each row holds the values of these keys in
    /// their order, read as <see cref="Read(void*)"/> does. A missing key reads as null.
    /// </summary>
    internal static object?[][] ReadDictionaries(void* array, IReadOnlyList<string> keys)
    {
        using var scope = new NativeScope();
        var count = Count(array, scope);
        var keyVariants = keys.Select(From).ToArray();
        try
        {
            var rows = new object?[count][];
            for (var i = 0; i < count; i++)
            {
                // The to-type call builds the Dictionary in place, so the slot starts zeroed and is not built first.
                var row = scope.Result(VariantType.Dictionary);
                Take(NativeApi.ArrayAtConst(array, i), VariantType.Dictionary, row);
                rows[i] = new object?[keys.Count];
                for (var k = 0; k < keys.Count; k++)
                {
                    rows[i][k] = Read(NativeApi.DictionaryAtConst(row, keyVariants[k].Slot));
                }
            }
            return rows;
        }
        finally
        {
            foreach (var key in keyVariants)
            {
                key.Dispose();
            }
        }
    }

    /// <summary>Reads an engine Array of signal Dictionaries into each signal's name and the names of its arguments.</summary>
    internal static (string Name, string[] Arguments)[] ReadSignals(void* array)
    {
        using var scope = new NativeScope();
        using var nameKey = From("name");
        using var argumentsKey = From("args");
        var signals = new (string, string[])[Count(array, scope)];
        for (var i = 0; i < signals.Length; i++)
        {
            var row = scope.Result(VariantType.Dictionary);
            Take(NativeApi.ArrayAtConst(array, i), VariantType.Dictionary, row);
            var arguments = scope.Result(VariantType.Array);
            Take(NativeApi.DictionaryAtConst(row, argumentsKey.Slot), VariantType.Array, arguments);
            signals[i] = ((string?)Read(NativeApi.DictionaryAtConst(row, nameKey.Slot)) ?? "", [.. ReadDictionaries(arguments, ["name"]).Select(argument => (string?)argument[0] ?? "")]);
        }
        return signals;
    }

    /// <summary>Reads an engine Array of connection Dictionaries into the object and method each one calls, and its flags.</summary>
    internal static (nint Target, string Method, long Flags)[] ReadConnections(void* array)
    {
        using var scope = new NativeScope();
        using var callableKey = From("callable");
        using var flagsKey = From("flags");
        var getObject = NativeApi.BuiltinMethod((int)VariantType.Callable, scope.StringName("get_object"), EngineCalls.CallableGetObjectHash);
        var getMethod = NativeApi.BuiltinMethod((int)VariantType.Callable, scope.StringName("get_method"), EngineCalls.CallableGetMethodHash);
        var connections = new (nint, string, long)[Count(array, scope)];
        // A Callable is two pointers, so it does not fit a scope slot. The to-type call assigns into it, so it starts zeroed each time.
        nint* callable = stackalloc nint[2];
        for (var i = 0; i < connections.Length; i++)
        {
            var row = scope.Result(VariantType.Dictionary);
            Take(NativeApi.ArrayAtConst(array, i), VariantType.Dictionary, row);
            (callable[0], callable[1]) = (0, 0);
            Take(NativeApi.DictionaryAtConst(row, callableKey.Slot), VariantType.Callable, callable);
            try
            {
                nint target = 0;
                getObject(callable, null, &target, 0);
                var method = scope.Result(VariantType.StringName);
                getMethod(callable, null, method, 0);
                connections[i] = (target, NativeText.ReadStringName(method), (long?)Read(NativeApi.DictionaryAtConst(row, flagsKey.Slot)) ?? 0);
            }
            finally
            {
                NativeApi.VariantDestructor((int)VariantType.Callable)(callable);
            }
        }
        return connections;
    }

    /// <summary>Builds a Callable of a method on an object into a zeroed two-pointer slot. The caller frees it with the Callable destructor.</summary>
    internal static void BuildCallable(nint* slot, nint target, string method)
    {
        using var scope = new NativeScope();
        void** args = stackalloc void*[] { &target, scope.StringName(method) };
        NativeApi.VariantConstructor((int)VariantType.Callable, 2)(slot, args);
    }

    static long Count(void* array, NativeScope scope)
    {
        var size = NativeApi.BuiltinMethod((int)VariantType.Array, scope.StringName("size"), EngineCalls.ArraySizeHash);
        long count = 0;
        size(array, null, &count, 0);
        return count;
    }

    void Fill(VariantType type, void* raw) => NativeApi.VariantFromType((int)type)(_slot, raw);

    static void Take(void* slot, VariantType type, void* raw) => NativeApi.VariantToType((int)type)(raw, slot);

    public void Dispose()
    {
        NativeApi.VariantDestroy(_slot);
        NativeMemory.Free(_slot);
    }
}
