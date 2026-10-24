using System.Runtime.InteropServices;
using Orblit.Interop;

namespace Orblit.Filament.Avalonia;

/// <summary>
/// A Metal shared event, which the renderer's side and the compositor's side both count on. Apple only.
/// </summary>
internal sealed partial class SharedEvent : IDisposable
{
    private const string Library = "orblit_filament_apple";

    private nint _event;

    static SharedEvent() => NativeLoader.Register(typeof(SharedEvent).Assembly, Library);

    public SharedEvent()
    {
        _event = Create();
        if (_event == nint.Zero)
        {
            throw new InvalidOperationException("Metal would not make a shared event.");
        }
    }

    /// <summary>The <c>MTLSharedEvent</c> itself, for the compositor to import. It lives as long as this does.</summary>
    public nint Handle => _event != nint.Zero
        ? _event
        : throw new ObjectDisposedException(nameof(SharedEvent));

    /// <summary>The highest value anyone has signalled.</summary>
    public ulong Value => Read(Handle);

    public void Signal(ulong value) => Write(Handle, value);

    public void Dispose()
    {
        if (_event == nint.Zero)
        {
            return;
        }

        Release(_event);
        _event = nint.Zero;
    }

    [LibraryImport(Library, EntryPoint = "orblit_apple_event_create")]
    private static partial nint Create();

    [LibraryImport(Library, EntryPoint = "orblit_apple_event_signal")]
    private static partial void Write(nint handle, ulong value);

    [LibraryImport(Library, EntryPoint = "orblit_apple_event_value")]
    private static partial ulong Read(nint handle);

    [LibraryImport(Library, EntryPoint = "orblit_apple_event_release")]
    private static partial void Release(nint handle);
}
