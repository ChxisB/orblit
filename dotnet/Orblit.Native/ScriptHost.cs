using System.Runtime.InteropServices;
using Orblit.Core;
using Orblit.Interop;

namespace Orblit.Native;

/// <summary>
/// The host table, alive for as long as the scripts using it.
/// One per world rather than one per script: the table holds no per-script
/// state, and forty scripts each allocating their own copy of the same function
/// pointers would be forty allocations saying the same thing.
/// </summary>
public sealed unsafe class ScriptHost : IDisposable
{
    /// <summary>
    /// The version this host was built to. A script reporting anything else is
    /// refused rather than called.
    /// </summary>
    public const int Abi = 2;

    // The core is already loaded by Orblit.Core. Loading it again returns the
    // same image, and the table points into that one.
    private static readonly nint CoreLibrary = NativeLoader.Load("orblit_core", AppContext.BaseDirectory);

    private readonly IScriptValues _values;
    private readonly Action<string>? _onLog;
    private readonly ScriptHostTable* _table = (ScriptHostTable*)NativeMemory.AllocZeroed((nuint)sizeof(ScriptHostTable));

    // The delegates live in fields because the table holds only their addresses.
    private readonly LogFn _log;
    private readonly NumberFn _number;
    private readonly ToggleFn _toggle;
    private readonly TextFn _text;
    private readonly NumberAtFn _numberAt;
    private readonly ToggleAtFn _toggleAt;
    private readonly TextAtFn _textAt;

    /// <summary>
    /// Where each resolved value lives, by asset and key. One small allocation
    /// each, never moved and never freed until the host is, because a script
    /// holds the address. A single growing block would be fewer allocations and
    /// would invalidate every pointer the moment a new key was resolved.
    /// </summary>
    private readonly Dictionary<Address, nint> _numbers = [];
    private readonly Dictionary<Address, nint> _toggles = [];
    private readonly Dictionary<Address, nint> _texts = [];

    // The fallback each resolved value was asked for with, so a refresh answers
    // the same way the first read did.
    private readonly Dictionary<Address, double> _numberFallbacks = [];
    private readonly Dictionary<Address, bool> _toggleFallbacks = [];

    // The strings currently pointed at, freed when they are replaced.
    private readonly Dictionary<Address, nint> _held = [];

    // The text handed back by the last Text call. One buffer, freed when the next
    // call replaces it, which is what the header promises: a script that needs
    // the string to outlive the line that read it copies it.
    private nint _lastText;
    private int _seen = -1;
    private bool _disposed;

    public ScriptHost(World world, IScriptValues? values = null, Action<string>? onLog = null)
    {
        World = world;
        _values = values ?? new NoValues();
        _onLog = onLog;
        _log = Log;
        _number = Number;
        _toggle = Toggle;
        _text = Text;
        _numberAt = NumberAt;
        _toggleAt = ToggleAt;
        _textAt = TextAt;
        Fill();
    }

    private delegate void LogFn(nint message);

    private delegate double NumberFn(nint asset, nint key, double fallback);

    private delegate byte ToggleFn(nint asset, nint key, byte fallback);

    private delegate nint TextFn(nint asset, nint key);

    private delegate nint NumberAtFn(nint asset, nint key, double fallback);

    private delegate nint ToggleAtFn(nint asset, nint key, byte fallback);

    private delegate nint TextAtFn(nint asset, nint key);

    private readonly record struct Address(string Asset, string Key);

    public World World { get; }

    internal nint Pointer =>
        _disposed ? throw new InvalidOperationException("This ScriptHost has been disposed.") : (nint)_table;

    /// <summary>
    /// Puts the current values into the slots scripts are reading through.
    /// Costs one read per resolved key, not one per read: a script looping over
    /// a hundred thousand entities and reading the same value each time costs
    /// this once. Does nothing when nothing has changed, which is most frames.
    /// </summary>
    public void Refresh()
    {
        if (_disposed)
        {
            return;
        }

        var revision = _values.Revision;
        if (revision == _seen)
        {
            return;
        }

        _seen = revision;
        foreach (var (at, slot) in _numbers)
        {
            *(double*)slot = _values.Number(at.Asset, at.Key, _numberFallbacks.GetValueOrDefault(at));
        }

        foreach (var (at, slot) in _toggles)
        {
            *(byte*)slot = Flag(_values.Toggle(at.Asset, at.Key, _toggleFallbacks.GetValueOrDefault(at)));
        }

        foreach (var (at, slot) in _texts)
        {
            WriteText(at, slot, _values.Text(at.Asset, at.Key));
        }
    }

    /// <summary>
    /// Frees the table and the callbacks. Every script using it must have been
    /// stopped first: a script holding this pointer after this is holding freed
    /// memory.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_lastText != 0)
        {
            Marshal.FreeCoTaskMem(_lastText);
        }

        foreach (var slot in _numbers.Values.Concat(_toggles.Values).Concat(_texts.Values))
        {
            NativeMemory.Free((void*)slot);
        }

        foreach (var held in _held.Values)
        {
            Marshal.FreeCoTaskMem(held);
        }

        NativeMemory.Free(_table);
    }

    private static string Read(nint text) => Marshal.PtrToStringUTF8(text) ?? string.Empty;

    private static byte Flag(bool value) => value ? (byte)1 : (byte)0;

    private static nint Allocate(int size) => (nint)NativeMemory.AllocZeroed((nuint)size);

    private static nint Export(string name) => NativeLibrary.GetExport(CoreLibrary, "orblit_" + name);

    private static nint Function(Delegate callback) => Marshal.GetFunctionPointerForDelegate(callback);

    private void Fill()
    {
        // Taken from the core's own exports, so there is no second description
        // of the core's API here to drift from the first.
        *_table = new ScriptHostTable
        {
            Abi = Abi,
            Size = (uint)sizeof(ScriptHostTable),
            World = World.NativeHandle,
            Log = Function(_log),
            ComponentRegister = Export("component_register"),
            ComponentLookup = Export("component_lookup"),
            EntityCreate = Export("entity_create"),
            EntityDestroy = Export("entity_destroy"),
            EntityAlive = Export("entity_alive"),
            EntityCount = Export("entity_count"),
            EntityAdd = Export("entity_add"),
            EntityRemove = Export("entity_remove"),
            EntityHas = Export("entity_has"),
            EntityGet = Export("entity_get"),
            QueryCreate = Export("query_create"),
            QueryDestroy = Export("query_destroy"),
            QueryChunkCount = Export("query_chunk_count"),
            QueryChunkLength = Export("query_chunk_length"),
            QueryChunkColumn = Export("query_chunk_column"),
            QueryChunkEntities = Export("query_chunk_entities"),
            TransformRegister = Export("transform_register"),
            DataNumber = Function(_number),
            DataToggle = Function(_toggle),
            DataText = Function(_text),
            NumberAt = Function(_numberAt),
            ToggleAt = Function(_toggleAt),
            TextAt = Function(_textAt),
        };
    }

    private void Log(nint message) => (_onLog ?? Console.WriteLine)(Read(message));

    private double Number(nint asset, nint key, double fallback) => _values.Number(Read(asset), Read(key), fallback);

    private byte Toggle(nint asset, nint key, byte fallback) =>
        Flag(_values.Toggle(Read(asset), Read(key), fallback != 0));

    private nint Text(nint asset, nint key)
    {
        var said = _values.Text(Read(asset), Read(key));
        if (_lastText != 0)
        {
            Marshal.FreeCoTaskMem(_lastText);
        }

        _lastText = said is null ? 0 : Marshal.StringToCoTaskMemUTF8(said);
        return _lastText;
    }

    private nint NumberAt(nint asset, nint key, double fallback)
    {
        var at = new Address(Read(asset), Read(key));
        _numberFallbacks[at] = fallback;
        var slot = Slot(_numbers, at, sizeof(double));
        *(double*)slot = _values.Number(at.Asset, at.Key, fallback);
        return slot;
    }

    private nint ToggleAt(nint asset, nint key, byte fallback)
    {
        var at = new Address(Read(asset), Read(key));
        _toggleFallbacks[at] = fallback != 0;
        var slot = Slot(_toggles, at, sizeof(byte));
        *(byte*)slot = Flag(_values.Toggle(at.Asset, at.Key, fallback != 0));
        return slot;
    }

    private nint TextAt(nint asset, nint key)
    {
        var at = new Address(Read(asset), Read(key));
        var slot = Slot(_texts, at, sizeof(nint));
        WriteText(at, slot, _values.Text(at.Asset, at.Key));
        return slot;
    }

    private static nint Slot(Dictionary<Address, nint> slots, Address at, int size)
    {
        if (!slots.TryGetValue(at, out var slot))
        {
            slot = Allocate(size);
            slots[at] = slot;
        }

        return slot;
    }

    /// <summary>
    /// Replaces the string a text slot points at. The old one is freed here,
    /// which is why the header says to read through the slot every time rather
    /// than keeping what it held.
    /// </summary>
    private void WriteText(Address at, nint slot, string? said)
    {
        if (_held.Remove(at, out var previous))
        {
            Marshal.FreeCoTaskMem(previous);
        }

        if (said is null)
        {
            *(nint*)slot = 0;
            return;
        }

        var fresh = Marshal.StringToCoTaskMemUTF8(said);
        _held[at] = fresh;
        *(nint*)slot = fresh;
    }
}
