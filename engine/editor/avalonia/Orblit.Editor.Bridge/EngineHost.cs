using System.Runtime.InteropServices;

namespace Orblit.Editor.Bridge;

/// <summary>
/// Loads the shared engine library and drives its one instance. The engine allows a single instance
/// per process for the whole life of the process, so a second host throws even after Dispose.
/// Every call must come from the thread that created the host.
/// </summary>
public sealed unsafe class EngineHost : IDisposable
{
    [StructLayout(LayoutKind.Sequential)]
    struct Initialization
    {
        public int MinimumLevel;
        public void* Userdata;
        public delegate* unmanaged<void*, int, void> Initialize;
        public delegate* unmanaged<void*, int, void> Deinitialize;
    }

    // Object.NOTIFICATION_POSTINITIALIZE. A constructed object needs it before its setters are used.
    const long PostInitialize = 0;

    static int s_created;

    readonly nint _instance;
    readonly delegate* unmanaged<nint, void> _destroy;
    bool _disposed;

    EngineHost(nint instance, delegate* unmanaged<nint, void> destroy)
    {
        _instance = instance;
        _destroy = destroy;
    }

    /// <summary>Creates the engine. The library path must be absolute, because the engine changes directory.</summary>
    public static EngineHost Create(string libraryPath, IReadOnlyList<string> args)
    {
        libraryPath = Path.GetFullPath(libraryPath);
        if (!File.Exists(libraryPath))
        {
            throw new FileNotFoundException($"The engine library is missing. Build it with scons library_type=shared_library.", libraryPath);
        }
        if (Interlocked.Exchange(ref s_created, 1) != 0)
        {
            throw new InvalidOperationException("This process already created an engine. The engine allows one instance per process.");
        }

        var library = NativeLibrary.Load(libraryPath);
        var create = (delegate* unmanaged<int, byte**, delegate* unmanaged<void*, void*, Initialization*, byte>, nint>)
            NativeLibrary.GetExport(library, "liborblit_create_instance");
        var destroy = (delegate* unmanaged<nint, void>)NativeLibrary.GetExport(library, "liborblit_destroy_instance");

        // argv[0] is the program name, which the engine skips.
        var all = new List<string> { libraryPath };
        all.AddRange(args);
        var argv = (byte**)NativeMemory.Alloc((nuint)(all.Count * sizeof(byte*)));
        for (var i = 0; i < all.Count; i++)
        {
            argv[i] = (byte*)Marshal.StringToCoTaskMemUTF8(all[i]);
        }
        // The engine may read argv after create returns. One engine runs per process, so it is never freed.
        var instance = create(all.Count, argv, &InitExtension);

        return instance == 0
            ? throw new InvalidOperationException("The engine did not create an instance. Check the arguments.")
            : new EngineHost(instance, destroy);
    }

    [UnmanagedCallersOnly]
    static byte InitExtension(void* getProcAddress, void* library, Initialization* init)
    {
        NativeApi.Load((delegate* unmanaged<byte*, void*>)getProcAddress);
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

    public bool Start() => this.OrblitInstanceStart(_instance);

    public bool IsStarted() => this.OrblitInstanceIsStarted(_instance);

    /// <summary>Runs one frame. Returns true when the engine asks to quit.</summary>
    public bool Iteration() => this.OrblitInstanceIteration(_instance);

    public void FocusIn() => this.OrblitInstanceFocusIn(_instance);

    public void FocusOut() => this.OrblitInstanceFocusOut(_instance);

    /// <summary>Runs the given number of frames.</summary>
    public void Pump(int frames)
    {
        for (var i = 0; i < frames; i++)
        {
            Iteration();
        }
    }

    /// <summary>An engine singleton such as EditorInterface. Zero before the engine has started.</summary>
    public nint Singleton(string name)
    {
        using var scope = new NativeScope();
        return (nint)NativeApi.GlobalSingleton(scope.StringName(name));
    }

    /// <summary>
    /// Makes a new object of a built-in class, ready for its setters. Reference counted classes come back
    /// with one reference.
    /// </summary>
    public nint Construct(string className)
    {
        using var scope = new NativeScope();
        var made = NativeApi.ConstructObject(scope.StringName(className));
        if (made == 0)
        {
            throw new InvalidOperationException($"The engine cannot construct {className}.");
        }
        this.ObjectNotification(made, PostInitialize, false);
        return made;
    }

    /// <summary>Destroys an object that was constructed and never handed to the engine.</summary>
    public void Destroy(nint made) => NativeApi.ObjectDestroy((void*)made);

    /// <summary>Drops one reference and destroys the object when it was the last.</summary>
    public void Release(nint refCounted)
    {
        if (this.RefCountedUnreference(refCounted))
        {
            NativeApi.ObjectDestroy((void*)refCounted);
        }
    }

    internal void Call(EngineMethod method, nint target, void** args, void* result)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        NativeApi.Ptrcall((void*)method.Bind(), target, args, result);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _destroy(_instance);
    }
}
