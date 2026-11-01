using System.Buffers.Binary;
using System.Text;

namespace Orblit.AudioDevice.Tests;

internal static class Wav
{
    /// <summary>
    /// A 16 bit mono sine as a WAV file, with the peak a full decode should reach, which is the
    /// amplitude after the 16 bit rounding.
    /// </summary>
    internal static (byte[] Bytes, double Peak) Sine(
        double seconds = 0.5,
        int rate = 48000,
        double amplitude = 0.5,
        double hertz = 440)
    {
        var count = (int)Math.Round(seconds * rate, MidpointRounding.AwayFromZero);
        var data = new byte[44 + (count * 2)];
        Encoding.ASCII.GetBytes("RIFF").CopyTo(data, 0);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(4), (uint)(36 + (count * 2)));
        Encoding.ASCII.GetBytes("WAVEfmt ").CopyTo(data, 8);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(16), 16);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(20), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(22), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(24), (uint)rate);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(28), (uint)(rate * 2));
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(32), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(34), 16);
        Encoding.ASCII.GetBytes("data").CopyTo(data, 36);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(40), (uint)(count * 2));
        var peak = 0;
        for (var i = 0; i < count; i++)
        {
            var sample = (short)Math.Round(amplitude * 32767 * Math.Sin(2 * Math.PI * hertz * i / rate), MidpointRounding.AwayFromZero);
            peak = Math.Max(peak, Math.Abs((int)sample));
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(44 + (i * 2)), sample);
        }

        return (data, peak / 32768.0);
    }

    /// <summary>The largest absolute sample on one channel of interleaved stereo.</summary>
    internal static double PeakOf(float[] samples, int channel)
    {
        var peak = 0.0;
        for (var i = channel; i < samples.Length; i += 2)
        {
            peak = Math.Max(peak, Math.Abs(samples[i]));
        }

        return peak;
    }
}
