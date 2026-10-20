using System.Runtime.InteropServices;

namespace Orblit.Native;

/// <summary>
/// A compiled script, loaded and running.
/// The four questions and nothing else. What a script is (a <c>.cpp</c>, a
/// TypeScript module) stops mattering at this line: everything above it sees
/// start, step and stop.
/// </summary>
public sealed unsafe class NativeScript
{
    private readonly nint _handle;
    private readonly FileInfo _file;
    private readonly delegate* unmanaged<nint, void> _start;
    private readonly delegate* unmanaged<double, void> _step;
    private readonly delegate* unmanaged<void> _stop;
    private bool _closed;

    private NativeScript(
        nint handle,
        FileInfo file,
        delegate* unmanaged<nint, void> start,
        delegate* unmanaged<double, void> step,
        delegate* unmanaged<void> stop)
    {
        _handle = handle;
        _file = file;
        Name = ScriptNames.Of(file.Name);
        _start = start;
        _step = step;
        _stop = stop;
    }

    /// <summary>What it is called, for a message about it.</summary>
    public string Name { get; }

    public bool IsRunning { get; private set; }

    /// <summary>
    /// Opens a built library and checks it is one of ours.
    /// Throws rather than returning null, because every failure here has a
    /// different cause and a caller that only knows "it did not load" cannot say
    /// anything useful about any of them.
    /// </summary>
    public static NativeScript Open(FileInfo library)
    {
        var name = ScriptNames.Of(library.Name);
        if (!File.Exists(library.FullName))
        {
            throw new ScriptException($"{library.FullName} is not there.");
        }

        var handle = Load(library, name);

        // The version first, before anything else is looked up: a library built
        // against a different contract has functions of these names that mean
        // something else, and calling one to find that out is a crash.
        if (!NativeLibrary.TryGetExport(handle, "orblit_script_abi", out var abi))
        {
            throw Refuse(handle, $"{name} does not look like a script: it has no orblit_script_abi. Did it use the ORBLIT_SCRIPT macro?");
        }

        var version = ((delegate* unmanaged<uint>)abi)();
        if (version != ScriptHost.Abi)
        {
            throw Refuse(handle, $"{name} was built against contract {version} and this engine speaks {ScriptHost.Abi}. Rebuild it.");
        }

        // Then the table itself. Two builds can agree about the version and
        // disagree about the struct, because one was compiled against a header
        // somebody had edited, and the only way to catch that is to compare the
        // thing rather than the number beside it.
        var theirs = NativeLibrary.TryGetExport(handle, "orblit_script_host_size", out var size)
            ? ((delegate* unmanaged<uint>)size)()
            : 0;
        if (theirs != sizeof(ScriptHostTable))
        {
            throw Refuse(
                handle,
                $"{name} was built against a host table of {theirs} bytes and this engine passes {sizeof(ScriptHostTable)}. The header it was compiled with is not this one.");
        }

        return new NativeScript(
            handle,
            library,
            (delegate* unmanaged<nint, void>)Entry(handle, name, "orblit_start"),
            (delegate* unmanaged<double, void>)Entry(handle, name, "orblit_step"),
            (delegate* unmanaged<void>)Entry(handle, name, "orblit_stop"));
    }

    public void Start(ScriptHost host)
    {
        if (IsRunning)
        {
            return;
        }

        IsRunning = true;
        _start(host.Pointer);
    }

    public void Step(double delta)
    {
        if (IsRunning)
        {
            _step(delta);
        }
    }

    public void Stop()
    {
        if (!IsRunning)
        {
            return;
        }

        IsRunning = false;
        _stop();
    }

    /// <summary>
    /// Stops it and unloads the library. Nothing may call into this script
    /// afterwards: its code is gone, and a held function pointer is an address
    /// in unmapped memory.
    /// The built file goes too. It was written for this load and there will be
    /// another for the next one, and a build folder that only grows is a build
    /// folder somebody eventually has to be told to empty.
    /// </summary>
    public void Close()
    {
        if (_closed)
        {
            return;
        }

        Stop();
        _closed = true;
        NativeLibrary.Free(_handle);
        try
        {
            File.Delete(_file.FullName);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Not worth failing a reload over: the next build writes its own
            // file and this one is only taking up space.
        }
    }

    private static nint Load(FileInfo library, string name)
    {
        try
        {
            return NativeLibrary.Load(library.FullName);
        }
        catch (Exception error) when (error is DllNotFoundException or BadImageFormatException)
        {
            throw new ScriptException($"{name} could not be loaded: {error.Message}");
        }
    }

    private static ScriptException Refuse(nint handle, string why)
    {
        NativeLibrary.Free(handle);
        return new ScriptException(why);
    }

    private static nint Entry(nint handle, string name, string wanted) =>
        NativeLibrary.TryGetExport(handle, wanted, out var found)
            ? found
            : throw Refuse(handle, $"{name} has no {wanted}.");
}
