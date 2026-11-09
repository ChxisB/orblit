using System.Runtime.InteropServices;

namespace AvaloniaEngine;

/// <summary>
/// Route A host. Loads libgodot and drives its instance through the GDExtension C ABI.
/// Only one instance can exist per process, so this type is a singleton by construction.
/// </summary>
public sealed unsafe class Engine : IDisposable
{
    // Hashes come from `--dump-extension-api`. Both shapes take no arguments.
    const long ReturnsBool = 2240911060;
    const long ReturnsVoid = 3218959716;
    const int PackedByteArrayType = 29;

    [StructLayout(LayoutKind.Sequential)]
    struct Initialization
    {
        public int MinimumLevel;
        public void* Userdata;
        public delegate* unmanaged<void*, int, void> Initialize;
        public delegate* unmanaged<void*, int, void> Deinitialize;
    }

    static delegate* unmanaged<void*, void*, long, void*> s_getMethodBind;
    static delegate* unmanaged<void*, nint, void**, void*, void> s_ptrcall;
    static delegate* unmanaged<void*, byte*, byte, void> s_stringName;
    static delegate* unmanaged<void*, void*> s_singleton;
    static delegate* unmanaged<void*, byte*, void> s_stringNew;
    static delegate* unmanaged<void*, byte*, long, long> s_stringToUtf8;
    static delegate* unmanaged<void*, void> s_objectDestroy;
    static delegate* unmanaged<void*, long, byte*> s_byteArrayAt;
    static delegate* unmanaged<void*, void> s_byteArrayDestructor;

    readonly nint _library;
    readonly delegate* unmanaged<nint, void> _destroy;
    readonly nint _instance;
    readonly Dictionary<string, nint> _binds = new();

    Engine(nint library, nint instance, delegate* unmanaged<nint, void> destroy)
    {
        _library = library;
        _instance = instance;
        _destroy = destroy;
    }

    public static Engine? Create(string libraryPath, IReadOnlyList<string> args)
    {
        var library = NativeLibrary.Load(libraryPath);
        var create = (delegate* unmanaged<int, byte**, delegate* unmanaged<void*, void*, Initialization*, byte>, nint>)
            NativeLibrary.GetExport(library, "libgodot_create_godot_instance");
        var destroy = (delegate* unmanaged<nint, void>)NativeLibrary.GetExport(library, "libgodot_destroy_godot_instance");

        // argv[0] is the program name. The engine skips it.
        var all = new List<string> { libraryPath };
        all.AddRange(args);
        var argv = (byte**)NativeMemory.Alloc((nuint)(all.Count * sizeof(byte*)));
        for (var i = 0; i < all.Count; i++)
        {
            argv[i] = (byte*)Marshal.StringToCoTaskMemUTF8(all[i]);
        }

        var instance = create(all.Count, argv, &InitExtension);
        return instance == 0 ? null : new Engine(library, instance, destroy);
    }

    [UnmanagedCallersOnly]
    static byte InitExtension(void* getProcAddress, void* library, Initialization* init)
    {
        var get = (delegate* unmanaged<byte*, void*>)getProcAddress;
        s_getMethodBind = (delegate* unmanaged<void*, void*, long, void*>)Lookup(get, "class_registry_get_method_bind");
        s_ptrcall = (delegate* unmanaged<void*, nint, void**, void*, void>)Lookup(get, "object_method_bind_ptrcall");
        s_stringName = (delegate* unmanaged<void*, byte*, byte, void>)Lookup(get, "string_name_new_with_latin1_chars");
        s_singleton = (delegate* unmanaged<void*, void*>)Lookup(get, "global_get_singleton");
        s_stringNew = (delegate* unmanaged<void*, byte*, void>)Lookup(get, "string_new_with_utf8_chars");
        s_stringToUtf8 = (delegate* unmanaged<void*, byte*, long, long>)Lookup(get, "string_to_utf8_chars");
        s_objectDestroy = (delegate* unmanaged<void*, void>)Lookup(get, "object_destroy");
        s_byteArrayAt = (delegate* unmanaged<void*, long, byte*>)Lookup(get, "packed_byte_array_operator_index_const");
        var destructorFor = (delegate* unmanaged<int, delegate* unmanaged<void*, void>>)Lookup(get, "variant_get_ptr_destructor");
        s_byteArrayDestructor = destructorFor(PackedByteArrayType);
        init->MinimumLevel = 0;
        init->Userdata = null;
        init->Initialize = &NoLevel;
        init->Deinitialize = &NoLevel;
        return 1;
    }

    [UnmanagedCallersOnly]
    static void NoLevel(void* userdata, int level)
    {
    }

    static void* Lookup(delegate* unmanaged<byte*, void*> get, string name)
    {
        var utf8 = (byte*)Marshal.StringToCoTaskMemUTF8(name);
        var found = get(utf8);
        Marshal.FreeCoTaskMem((nint)utf8);
        return found;
    }

    /// <summary>The engine's StringName is one pointer. Leaked on purpose: a few per process.</summary>
    static void* NewStringName(string text)
    {
        var slot = NativeMemory.AllocZeroed((nuint)sizeof(void*));
        var utf8 = (byte*)Marshal.StringToCoTaskMemUTF8(text);
        s_stringName(slot, utf8, 1);
        Marshal.FreeCoTaskMem((nint)utf8);
        return slot;
    }

    nint Bind(string method, long hash)
    {
        if (_binds.TryGetValue(method, out var found))
        {
            return found;
        }
        var bind = (nint)s_getMethodBind(NewStringName("GodotInstance"), NewStringName(method), hash);
        if (bind == 0)
        {
            throw new InvalidOperationException($"No method bind for GodotInstance.{method}");
        }
        _binds[method] = bind;
        return bind;
    }

    bool CallBool(string method)
    {
        ulong result = 0;
        s_ptrcall((void*)Bind(method, ReturnsBool), _instance, null, &result);
        return (result & 0xFF) != 0;
    }

    void CallVoid(string method)
    {
        s_ptrcall((void*)Bind(method, ReturnsVoid), _instance, null, null);
    }

    public bool Start() => CallBool("start");

    public bool IsStarted() => CallBool("is_started");

    /// <summary>One frame. Returns true when the engine asks to quit.</summary>
    public bool Iteration() => CallBool("iteration");

    public void FocusIn() => CallVoid("focus_in");

    public void FocusOut() => CallVoid("focus_out");

    /// <summary>An engine singleton such as EditorInterface. Zero before the engine has started.</summary>
    public nint Singleton(string name) => (nint)s_singleton(NewStringName(name));

    /// <summary>Calls a bound method with typed arguments. Slots hold the engine's native layout.</summary>
    public void Call(string type, string method, long hash, nint target, void** args, void* result)
    {
        var key = $"{type}.{method}";
        if (!_binds.TryGetValue(key, out var bind))
        {
            bind = (nint)s_getMethodBind(NewStringName(type), NewStringName(method), hash);
            if (bind == 0)
            {
                throw new InvalidOperationException($"No method bind for {key} with hash {hash}");
            }
            _binds[key] = bind;
        }
        s_ptrcall((void*)bind, target, args, result);
    }

    /// <summary>A String is one pointer. Leaked on purpose: a few per run.</summary>
    public static void* NewString(string text)
    {
        var slot = NativeMemory.AllocZeroed((nuint)sizeof(void*));
        var utf8 = (byte*)Marshal.StringToCoTaskMemUTF8(text);
        s_stringNew(slot, utf8);
        Marshal.FreeCoTaskMem((nint)utf8);
        return slot;
    }

    public static string ReadString(void* slot)
    {
        var length = s_stringToUtf8(slot, null, 0);
        var buffer = new byte[length];
        fixed (byte* bytes = buffer)
        {
            s_stringToUtf8(slot, bytes, length);
        }
        return System.Text.Encoding.UTF8.GetString(buffer);
    }

    public static void DestroyObject(nint target) => s_objectDestroy((void*)target);

    /// <summary>Address of one byte of a PackedByteArray. The bytes are contiguous.</summary>
    public static byte* ByteAt(void* array, long index) => s_byteArrayAt(array, index);

    public static void DestroyByteArray(void* array) => s_byteArrayDestructor(array);

    public void Dispose()
    {
        _destroy(_instance);
    }
}
