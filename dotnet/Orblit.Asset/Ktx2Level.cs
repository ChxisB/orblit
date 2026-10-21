namespace Orblit.Asset;

/// <summary>Where one mip level's bytes are inside a KTX2 file.</summary>
/// <param name="Offset">From the start of the file.</param>
/// <param name="Length">As stored, which for a supercompressed file is the compressed length.</param>
/// <param name="UncompressedLength">What the level decompresses to.</param>
public readonly record struct Ktx2Level(long Offset, long Length, long UncompressedLength)
{
    /// <summary>The byte after this level's last.</summary>
    public long End => Offset + Length;
}
