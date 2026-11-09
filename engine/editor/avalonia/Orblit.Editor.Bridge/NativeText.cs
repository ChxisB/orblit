using System.Text;

namespace Orblit.Editor.Bridge;

/// <summary>Reads the engine's String and StringName slots into managed strings.</summary>
internal static unsafe class NativeText
{
    // Constructor 2 of String takes a StringName.
    const int StringFromStringName = 2;

    public static string ReadString(void* slot)
    {
        var length = NativeApi.StringToUtf8(slot, null, 0);
        if (length == 0)
        {
            return "";
        }
        var buffer = new byte[length];
        fixed (byte* bytes = buffer)
        {
            NativeApi.StringToUtf8(slot, bytes, length);
        }
        return Encoding.UTF8.GetString(buffer);
    }

    /// <summary>The names in an engine Array of StringName.</summary>
    public static string[] ReadStringNames(void* array)
    {
        using var scope = new NativeScope();
        var size = NativeApi.BuiltinMethod((int)VariantType.Array, scope.StringName("size"), EngineCalls.ArraySizeHash);
        long count = 0;
        size(array, null, &count, 0);
        var names = new string[count];
        for (var i = 0; i < count; i++)
        {
            var name = scope.Result(VariantType.StringName);
            NativeApi.VariantToType((int)VariantType.StringName)(name, NativeApi.ArrayAtConst(array, i));
            names[i] = ReadStringName(name);
        }
        return names;
    }

    public static string ReadStringName(void* slot)
    {
        using var scope = new NativeScope();
        var text = scope.Result(VariantType.String);
        void** args = stackalloc void*[1] { slot };
        NativeApi.VariantConstructor((int)VariantType.String, StringFromStringName)(text, args);
        return ReadString(text);
    }
}
