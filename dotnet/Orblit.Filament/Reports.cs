using System.Runtime.InteropServices;

namespace Orblit.Filament;

/// <summary>
/// Medians of recent frames, in milliseconds, which are nought until the
/// backend has reported, and what the last scene batched. The layout is the C struct's.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct Stats(
    double GpuMilliseconds,
    double CpuMilliseconds,
    uint BatchedObjects,
    uint BatchGroups,
    uint PassCount);

/// <summary>What one pass of the last frame cost and how many things it drew.</summary>
public readonly record struct PassTiming(double Milliseconds, int Drawn);

/// <summary>Something the scene asked for that could not be given: what it is about, and what it says.</summary>
public readonly record struct Note(string About, string Saying);

/// <summary>A frame read back: RGBA8, top row first.</summary>
public readonly record struct Capture(uint Width, uint Height, ReadOnlyMemory<byte> Rgba);
