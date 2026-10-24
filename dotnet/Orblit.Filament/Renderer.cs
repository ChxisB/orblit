using System.Runtime.InteropServices;

namespace Orblit.Filament;

/// <summary>
/// One renderer, started with a backend onto a surface. One thread drives it,
/// apart from <see cref="Resize"/>, which is safe from any thread. Dispose it
/// after every other thread has stopped calling it.
/// </summary>
public sealed partial class Renderer : IDisposable
{
    private readonly Held _held = new();
    private nint _renderer;

    /// <summary>Starts Filament at the given size. Headless unless <paramref name="surface"/> says otherwise.</summary>
    public Renderer(uint width, uint height, Backend backend = Backend.Default, Surface surface = default)
    {
        _renderer = NativeMethods.Create(backend, in surface, width, height);
        if (_renderer == nint.Zero)
        {
            throw new InvalidOperationException("The renderer would not start. The reason was logged.");
        }
    }

    /// <summary>The backend the engine was built with, with <see cref="Backend.Default"/> resolved.</summary>
    public Backend Backend => NativeMethods.BackendOf(Alive);

    /// <summary>
    /// Frames that reached the end of rendering, excluding skipped draws and
    /// draws while detached. This counts rendering, not display presentations.
    /// </summary>
    public ulong RenderedFrames => NativeMethods.RenderedFrames(Alive);

    internal nint Alive => _renderer != nint.Zero
        ? _renderer
        : throw new InvalidOperationException("This Renderer has been disposed.");

    /// <summary>New dimensions, applied at the top of the next frame.</summary>
    public void Resize(uint width, uint height) => Check(NativeMethods.Resize(Alive, width, height), "resize");

    /// <summary>
    /// Replaces the presentation surface: detaches whatever is attached and
    /// allocates fresh buffers onto the new one. A headless surface or a window
    /// is taken and the platform's own is refused. A failure here leaves the
    /// renderer presenting nowhere and does not stop it, so the call can be tried again.
    /// </summary>
    public void AttachSurface(Surface surface, uint width, uint height) =>
        Check(NativeMethods.AttachSurface(Alive, in surface, width, height), "attach_surface");

    /// <summary>Gives the surface back. Drawing while detached is safe and draws nothing.</summary>
    public void DetachSurface() => Check(NativeMethods.DetachSurface(Alive), "detach_surface");

    /// <summary>Draws one frame at <paramref name="seconds"/> and presents it.</summary>
    public void Draw(double seconds) => Check(NativeMethods.Draw(Alive, seconds), "draw");

    /// <summary>One answer about the device this runs on, or -1 for a question the renderer does not know.</summary>
    public int GetCapability(Capability which) => NativeMethods.CapabilityOf(Alive, which);

    /// <summary>What the device under this renderer can do, worked out from its answers to every question.</summary>
    public DeviceProfile GetProfile() =>
        DeviceProfile.FromCapabilities([.. Enum.GetValues<Capability>().Select(GetCapability)]);

    /// <summary>
    /// The frame most recently presented, for a <see cref="SurfaceKind.Platform"/> renderer, or null
    /// for any other. Safe from any thread. Dispose the result when the host has taken what it needs.
    /// </summary>
    public PresentedFrame? CopyPresented()
    {
        var buffer = NativeMethods.CopyPresented(Alive);
        return buffer == nint.Zero ? null : new PresentedFrame(buffer);
    }

    /// <summary>Asks for the next frame drawn to be read back into memory.</summary>
    public void RequestCapture() => Check(NativeMethods.RequestCapture(Alive), "request_capture");

    /// <summary>The last frame read back, or null until one has arrived, which is a frame or two after it was asked for.</summary>
    public Capture? ReadCapture()
    {
        var size = NativeMethods.ReadCapture(Alive, default, 0, out _, out _);
        if (size == 0)
        {
            return null;
        }

        var rgba = new byte[size];
        var copied = NativeMethods.ReadCapture(Alive, rgba, size, out var width, out var height);

        // A frame of another size can arrive between the two calls. Asking again gets it.
        return copied == size ? new Capture(width, height, rgba) : null;
    }

    public Stats GetStats()
    {
        Check(NativeMethods.ReadStats(Alive, out var stats), "stats");
        return stats;
    }

    /// <summary>What each pass of the last frame cost and drew.</summary>
    public PassTiming[] GetPassTimings()
    {
        var count = NativeMethods.PassTimings(Alive, default, default, 0);
        var milliseconds = new double[count];
        var drawn = new int[count];
        NativeMethods.PassTimings(Alive, milliseconds, drawn, count);
        return [.. milliseconds.Zip(drawn, (time, things) => new PassTiming(time, things))];
    }

    /// <summary>
    /// A snapshot of what the scene asked for that could not be given. It also
    /// holds a description of each model file the last scene built something
    /// new out of, as JSON, under <c>orblit.model:</c> and the path the scene
    /// named it by. A host that only wants problems passes those over.
    /// </summary>
    public Note[] GetNotes()
    {
        var notes = new Note[NativeMethods.NoteCount(Alive)];
        for (var index = 0; index < notes.Length; index++)
        {
            Check(NativeMethods.NoteAt(Alive, (uint)index, out var about, out var saying), "note");
            notes[index] = new Note(
                Marshal.PtrToStringUTF8(about) ?? string.Empty,
                Marshal.PtrToStringUTF8(saying) ?? string.Empty);
        }

        return notes;
    }

    public void Dispose()
    {
        if (_renderer == nint.Zero)
        {
            return;
        }

        NativeMethods.Destroy(_renderer);
        _renderer = nint.Zero;
    }

    // F2: the ABI's own signature.
    internal void SetCamera(
        ReadOnlySpan<float> position,
        ReadOnlySpan<float> target,
        float fieldOfView,
        bool orthographic,
        float viewHeight,
        double at)
    {
        if (position.Length != 3 || target.Length != 3)
        {
            throw new ArgumentException("A camera position and a target take three numbers each.");
        }

        Check(NativeMethods.SetCamera(Alive, position, target, fieldOfView, orthographic ? 1 : 0, viewHeight, at), "set_camera");
    }

    internal static void Check(int result, string call)
    {
        if (result < 0)
        {
            throw new RendererException((RendererResult)result, $"orblit_renderer_{call}");
        }
    }
}
