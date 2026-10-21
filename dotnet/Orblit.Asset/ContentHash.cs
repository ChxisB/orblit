using System.Security.Cryptography;

namespace Orblit.Asset;

/// <summary>
/// What some bytes are, as the SHA-256 of them. An <see cref="AssetId"/> says what a project
/// calls something and stays the same while the file changes. A hash says exactly which bytes it
/// is and changes when one of them does, so it is safe to key a cache or a download on.
/// </summary>
public sealed class ContentHash : IEquatable<ContentHash>
{
    private ContentHash(string hex) => Hex = hex;

    /// <summary>The hash as 64 lower-case hexadecimal digits.</summary>
    public string Hex { get; }

    /// <summary>The first twelve digits, which is as much of a hash as a message needs to name it.</summary>
    internal string Short => Hex[..12];

    /// <summary>The first two digits, which a store uses as a folder name.</summary>
    public string Shard => Hex[..2];

    /// <summary>The hash of <paramref name="bytes"/>.</summary>
    public static ContentHash Of(ReadOnlySpan<byte> bytes) => new(Convert.ToHexStringLower(SHA256.HashData(bytes)));

    /// <summary>
    /// The hash of everything <paramref name="chunks"/> delivers, without holding it all at once.
    /// The answer is the same as <see cref="Of"/> on the whole thing however the chunks are cut.
    /// </summary>
    public static async Task<ContentHash> OfStreamAsync(IAsyncEnumerable<ReadOnlyMemory<byte>> chunks)
    {
        using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        await foreach (var chunk in chunks.ConfigureAwait(false))
        {
            digest.AppendData(chunk.Span);
        }

        return new ContentHash(Convert.ToHexStringLower(digest.GetHashAndReset()));
    }

    /// <summary>
    /// Reads a hash written as 64 hexadecimal digits. Upper-case digits are accepted and
    /// lower-cased. Throws <see cref="AssetFormatException"/> saying what is wrong when it is not one.
    /// </summary>
    public static ContentHash Parse(string text)
    {
        if (text.Length != 64)
        {
            throw new AssetFormatException(
                $"\"{text}\" is not a content hash, because it is {text.Length} characters long. A SHA-256 hash is 64 hexadecimal digits.",
                text);
        }

        for (var i = 0; i < text.Length; i++)
        {
            if (!IsHexDigit(text[i]))
            {
                throw new AssetFormatException(
                    $"\"{text}\" is not a content hash, because \"{text[i]}\" is not a hexadecimal digit.", text, i);
            }
        }

        return new ContentHash(text.ToLowerInvariant());
    }

    public bool Equals(ContentHash? other) => other is not null && other.Hex == Hex;

    public override bool Equals(object? obj) => Equals(obj as ContentHash);

    public override int GetHashCode() => Hex.GetHashCode(StringComparison.Ordinal);

    public override string ToString() => Hex;

    public static bool operator ==(ContentHash? left, ContentHash? right) => left?.Equals(right) ?? right is null;

    public static bool operator !=(ContentHash? left, ContentHash? right) => !(left == right);

    private static bool IsHexDigit(char c) => c is (>= '0' and <= '9') or (>= 'A' and <= 'F') or (>= 'a' and <= 'f');
}
