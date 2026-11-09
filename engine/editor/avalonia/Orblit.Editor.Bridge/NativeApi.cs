namespace Orblit.Editor.Bridge;

/// <summary>
/// The engine's C functions, found once when the engine starts. The engine hands them out by name
/// and keeps them valid until the process ends.
/// </summary>
internal static unsafe class NativeApi
{
    internal static delegate* unmanaged<void*, void*, long, void*> GetMethodBind;
    internal static delegate* unmanaged<void*, nint, void**, void*, void> Ptrcall;
    internal static delegate* unmanaged<void*, byte*, void> StringNameNew;
    internal static delegate* unmanaged<void*, void*> GlobalSingleton;
    internal static delegate* unmanaged<void*, byte*, void> StringNew;
    internal static delegate* unmanaged<void*, byte*, long, long> StringToUtf8;
    internal static delegate* unmanaged<void*, void> ObjectDestroy;
    internal static delegate* unmanaged<void*, long, byte*> ByteArrayAt;
    internal static delegate* unmanaged<void*, nint> ConstructObject;
    internal static delegate* unmanaged<int, delegate* unmanaged<void*, void>> VariantDestructor;
    internal static delegate* unmanaged<int, int, delegate* unmanaged<void*, void**, void>> VariantConstructor;
    internal static delegate* unmanaged<int, void*, long, delegate* unmanaged<void*, void**, void*, int, void>> BuiltinMethod;
    internal static delegate* unmanaged<void*, int> VariantGetType;
    internal static delegate* unmanaged<void*, void> VariantDestroy;
    internal static delegate* unmanaged<int, delegate* unmanaged<void*, void*, void>> VariantFromType;
    internal static delegate* unmanaged<int, delegate* unmanaged<void*, void*, void>> VariantToType;
    internal static delegate* unmanaged<void*, long, void*> ArrayAtConst;

    internal static void Load(delegate* unmanaged<byte*, void*> get)
    {
        GetMethodBind = (delegate* unmanaged<void*, void*, long, void*>)Find(get, "class_registry_get_method_bind");
        Ptrcall = (delegate* unmanaged<void*, nint, void**, void*, void>)Find(get, "object_method_bind_ptrcall");
        StringNameNew = (delegate* unmanaged<void*, byte*, void>)Find(get, "string_name_new_with_utf8_chars");
        GlobalSingleton = (delegate* unmanaged<void*, void*>)Find(get, "global_get_singleton");
        StringNew = (delegate* unmanaged<void*, byte*, void>)Find(get, "string_new_with_utf8_chars");
        StringToUtf8 = (delegate* unmanaged<void*, byte*, long, long>)Find(get, "string_to_utf8_chars");
        ObjectDestroy = (delegate* unmanaged<void*, void>)Find(get, "object_destroy");
        ByteArrayAt = (delegate* unmanaged<void*, long, byte*>)Find(get, "packed_byte_array_operator_index_const");
        ConstructObject = (delegate* unmanaged<void*, nint>)Find(get, "class_registry_construct_object3");
        VariantDestructor = (delegate* unmanaged<int, delegate* unmanaged<void*, void>>)Find(get, "variant_get_ptr_destructor");
        VariantConstructor = (delegate* unmanaged<int, int, delegate* unmanaged<void*, void**, void>>)Find(get, "variant_get_ptr_constructor");
        BuiltinMethod = (delegate* unmanaged<int, void*, long, delegate* unmanaged<void*, void**, void*, int, void>>)Find(get, "variant_get_ptr_builtin_method");
        VariantGetType = (delegate* unmanaged<void*, int>)Find(get, "variant_get_type");
        VariantDestroy = (delegate* unmanaged<void*, void>)Find(get, "variant_destroy");
        VariantFromType = (delegate* unmanaged<int, delegate* unmanaged<void*, void*, void>>)Find(get, "get_variant_from_type_constructor");
        VariantToType = (delegate* unmanaged<int, delegate* unmanaged<void*, void*, void>>)Find(get, "get_variant_to_type_constructor");
        ArrayAtConst = (delegate* unmanaged<void*, long, void*>)Find(get, "array_operator_index_const");
    }

    static void* Find(delegate* unmanaged<byte*, void*> get, string name)
    {
        var utf8 = System.Runtime.InteropServices.Marshal.StringToCoTaskMemUTF8(name);
        try
        {
            var found = get((byte*)utf8);
            return found != null ? found : throw new InvalidOperationException($"The engine has no function named {name}.");
        }
        finally
        {
            System.Runtime.InteropServices.Marshal.FreeCoTaskMem(utf8);
        }
    }
}
