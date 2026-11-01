using System.Globalization;

namespace Orblit.Scene;

/// <summary>
/// The samplers, images and textures of a glTF being written, each made once however many
/// materials ask for it.
/// </summary>
internal sealed class GltfTextures(GltfOutput output)
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47];
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] Ktx2 = [0xAB, 0x4B, 0x54, 0x58];
    private static readonly byte[] Riff = [0x52, 0x49, 0x46, 0x46];
    private static readonly byte[] Webp = [0x57, 0x45, 0x42, 0x50];

    private readonly Dictionary<string, long> _imageAt = [];
    private readonly Dictionary<string, long> _samplerAt = [];
    private readonly Dictionary<string, long> _textureAt = [];

    public long Sampler(ResolvedMaterial? resolved)
    {
        var wrap = resolved?.Choice("wrap") ?? "repeat";
        var filter = resolved?.Choice("filter") ?? "smooth";
        var key = $"{wrap}|{filter}";
        if (_samplerAt.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var mode = wrap switch
        {
            "clamp" => 33071L,
            "mirror" => 33648L,
            _ => 10497L,
        };
        var sharp = filter == "sharp";
        return _samplerAt[key] = output.Add("samplers", new OrderedDictionary<string, object?>
        {
            ["magFilter"] = sharp ? 9728L : 9729L,
            ["minFilter"] = sharp ? 9984L : 9987L,
            ["wrapS"] = mode,
            ["wrapT"] = mode,
        });
    }

    /// <summary>A texture over <paramref name="path"/>, or null when its bytes were not supplied.</summary>
    public long? Texture(string path, long sampler)
    {
        var key = string.Create(CultureInfo.InvariantCulture, $"{path}|{sampler}");
        if (_textureAt.TryGetValue(key, out var cached))
        {
            return cached;
        }

        if (Image(path) is not { } image)
        {
            return null;
        }

        var kind = KindOf(output.Files[path]);
        var texture = new OrderedDictionary<string, object?>();
        var extension = kind switch
        {
            "image/ktx2" => "KHR_texture_basisu",
            "image/webp" => "EXT_texture_webp",
            _ => null,
        };
        if (extension is null)
        {
            texture["source"] = image;
        }
        else
        {
            texture["extensions"] = new OrderedDictionary<string, object?>
            {
                [extension] = new OrderedDictionary<string, object?> { ["source"] = image },
            };
        }

        texture["sampler"] = sampler;
        if (extension is not null)
        {
            output.Used.Add(extension);
        }

        return _textureAt[key] = output.Add("textures", texture);
    }

    private long? Image(string path)
    {
        if (_imageAt.TryGetValue(path, out var cached))
        {
            return cached;
        }

        if (!output.Files.TryGetValue(path, out var bytes))
        {
            return null;
        }

        var kind = KindOf(bytes);
        if (kind is null)
        {
            output.Problems.Add(
                $"{path} is not a picture this can embed. glTF carries PNG, JPEG, WebP and KTX2, and this is none of them.");
            return null;
        }

        return _imageAt[path] = output.Add("images", new OrderedDictionary<string, object?>
        {
            ["name"] = path.Split('/')[^1],
            ["mimeType"] = kind,
            ["bufferView"] = (long)output.Buffer.AddView<byte>(bytes),
        });
    }

    /// <summary>
    /// What a picture is, read from the first few bytes rather than the name. A file called
    /// <c>.png</c> that is a JPEG is common enough; a <c>mimeType</c> that disagrees with the bytes is
    /// a texture that does not load, in a file that validates.
    /// </summary>
    internal static string? KindOf(byte[] bytes)
    {
        var start = bytes.AsSpan();
        if (start.StartsWith(Png))
        {
            return "image/png";
        }

        if (start.StartsWith(Jpeg))
        {
            return "image/jpeg";
        }

        if (start.StartsWith(Ktx2))
        {
            return "image/ktx2";
        }

        return start.StartsWith(Riff) && bytes.Length >= 12 && start[8..12].SequenceEqual(Webp) ? "image/webp" : null;
    }
}
