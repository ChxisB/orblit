namespace Orblit.AudioDevice;

/// <summary>The device or the engine could not start, or a clip could not be played.</summary>
public sealed class AudioDeviceException(string message) : Exception(message);
