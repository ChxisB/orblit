using System.Runtime.InteropServices;
using Orblit.Audio;

namespace Orblit.AudioDevice;

/// <summary>
/// An <see cref="IAudioBackend"/> that plays through the machine's output device. A clip is a
/// path to a WAV, FLAC, MP3 or Ogg Vorbis file. Call it from one thread, as
/// <see cref="AudioPlayer"/> does.
/// </summary>
public sealed class DeviceAudio : IAudioBackend, IDisposable
{
    private readonly nint _engine;
    private readonly DeviceAudioOptions _options;
    private readonly Dictionary<string, bool> _streams = [];
    private bool _closed;

    private DeviceAudio(bool offline, int sampleRate, DeviceAudioOptions? options)
    {
        _options = options ?? new DeviceAudioOptions();
        _engine = Create(offline, sampleRate);
        SampleRate = offline ? sampleRate : null;
    }

    /// <summary>Frames per second of <see cref="Render"/>. Only an offline engine has one.</summary>
    public int? SampleRate { get; }

    /// <summary>Opens the default output device. Throws when there is none, such as on a server.</summary>
    public static DeviceAudio Open(DeviceAudioOptions? options = null) => new(false, 0, options);

    /// <summary>An engine with no device, mixed by <see cref="Render"/>. A test hears nothing and reads every sample.</summary>
    public static DeviceAudio Offline(int sampleRate = 48000, DeviceAudioOptions? options = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleRate);
        return new DeviceAudio(true, sampleRate, options);
    }

    public void Start(long voice, string clip, bool loop, Level level, double offset = 0)
    {
        ObjectDisposedException.ThrowIf(_closed, this);
        var path = _options.Directory.Length == 0 || Path.IsPathRooted(clip)
            ? clip
            : Path.Combine(_options.Directory, clip);
        if (!TryStreams(path, out var stream, out var reason))
        {
            Report(clip, reason);
            return;
        }

        var result = NativeMethods.Start(
            _engine, voice, path, loop ? 1 : 0, stream ? 1 : 0, (float)level.Gain, (float)level.Pitch, (float)level.Pan, (float)offset);
        if (result != 0)
        {
            Report(clip, Describe(result));
        }
    }

    // After Dispose a voice is silent, so a game that goes on calling Advance sees every voice end.
    public void Update(long voice, Level level)
    {
        if (!_closed)
        {
            NativeMethods.Update(_engine, voice, (float)level.Gain, (float)level.Pitch, (float)level.Pan);
        }
    }

    public void Stop(long voice)
    {
        if (!_closed)
        {
            NativeMethods.Stop(_engine, voice);
        }
    }

    public bool IsPlaying(long voice) => !_closed && NativeMethods.IsPlaying(_engine, voice) != 0;

    /// <summary>Mixes the next <paramref name="frames"/> of an offline engine as interleaved stereo.</summary>
    public unsafe float[] Render(int frames)
    {
        ObjectDisposedException.ThrowIf(_closed, this);
        ArgumentOutOfRangeException.ThrowIfNegative(frames);
        var buffer = new float[frames * 2];
        fixed (float* output = buffer)
        {
            var written = NativeMethods.Render(_engine, output, (ulong)frames);
            return buffer[..(int)(written * 2)];
        }
    }

    /// <summary>Stops every voice and closes the device. A second call does nothing.</summary>
    public void Dispose()
    {
        if (_closed)
        {
            return;
        }

        _closed = true;
        NativeMethods.Destroy(_engine);
    }

    private static nint Create(bool offline, int sampleRate)
    {
        var engine = NativeMethods.Create(offline ? 1 : 0, (uint)sampleRate, out var result);
        if (engine == 0)
        {
            var what = offline ? "cannot start the engine" : "no output device";
            throw new AudioDeviceException($"{what}: {Describe(result)}");
        }

        return engine;
    }

    private static string Describe(int result) => Marshal.PtrToStringUTF8(NativeMethods.Describe(result)) ?? "";

    private bool TryStreams(string path, out bool stream, out string reason)
    {
        reason = "";
        if (_streams.TryGetValue(path, out stream))
        {
            return true;
        }

        try
        {
            stream = new FileInfo(path).Length > _options.StreamAbove;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            reason = error.Message;
            return false;
        }

        _streams[path] = stream;
        return true;
    }

    private void Report(string clip, string reason)
    {
        if (_options.OnError is not { } handler)
        {
            throw new AudioDeviceException($"{clip}: {reason}");
        }

        handler(clip, reason);
    }
}
