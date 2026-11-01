using System.Runtime.InteropServices;
using Orblit.Interop;

namespace Orblit.AudioDevice;

/// <summary>The C ABI of <c>orblit_audio_device.h</c>, declared and nothing else.</summary>
internal static unsafe partial class NativeMethods
{
    private const string Library = "orblit_audio_device";

    static NativeMethods() => NativeLoader.Register(typeof(NativeMethods).Assembly, Library);

    [LibraryImport(Library, EntryPoint = "orblit_audio_create")]
    internal static partial nint Create(int offline, uint sampleRate, out int result);

    [LibraryImport(Library, EntryPoint = "orblit_audio_destroy")]
    internal static partial void Destroy(nint audio);

    // F2: nine parameters, because that is the C ABI.
    [LibraryImport(Library, EntryPoint = "orblit_audio_start", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int Start(
        nint audio, long voice, string path, int loop, int stream, float gain, float pitch, float pan, float offset);

    [LibraryImport(Library, EntryPoint = "orblit_audio_update")]
    internal static partial void Update(nint audio, long voice, float gain, float pitch, float pan);

    [LibraryImport(Library, EntryPoint = "orblit_audio_stop")]
    internal static partial void Stop(nint audio, long voice);

    [LibraryImport(Library, EntryPoint = "orblit_audio_is_playing")]
    internal static partial int IsPlaying(nint audio, long voice);

    [LibraryImport(Library, EntryPoint = "orblit_audio_render")]
    internal static partial ulong Render(nint audio, float* output, ulong frames);

    [LibraryImport(Library, EntryPoint = "orblit_audio_describe")]
    internal static partial nint Describe(int result);
}
