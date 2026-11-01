namespace Orblit.AudioDevice.Tests;

/// <summary>A folder with a half second sine in it, shared by the tests of one class.</summary>
public sealed class Tones : IDisposable
{
    public Tones()
    {
        var (bytes, peak) = Wav.Sine();
        Peak = peak;
        File.WriteAllBytes(Path.Combine(Folder.FullName, "tone.wav"), bytes);
    }

    public DirectoryInfo Folder { get; } = Directory.CreateTempSubdirectory("orblit_audio_device_");

    /// <summary>What <c>tone.wav</c> decodes to at its loudest.</summary>
    public double Peak { get; }

    public void Dispose() => Folder.Delete(recursive: true);
}
