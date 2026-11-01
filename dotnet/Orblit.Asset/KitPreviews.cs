namespace Orblit.Asset;

/// <summary>Reads the previews of a kit for the store.</summary>
internal static class KitPreviews
{
    /// <summary>The previews of <paramref name="kit"/>, named <c>preview-&lt;n&gt;.&lt;extension&gt;</c>.</summary>
    public static async Task<OrderedDictionary<string, byte[]>> ReadAsync(Kit kit)
    {
        var previews = new OrderedDictionary<string, byte[]>();
        foreach (var (index, id) in kit.Manifest.Previews.Index())
        {
            var size = kit.Files[id];
            if (size > KitStoreRules.PreviewBytes)
            {
                throw new FormatException(
                    $"The preview \"{KitText.Show(id.ToString())}\" is {size} bytes. A preview is at most {KitStoreRules.PreviewBytes}.");
            }

            var bytes = await kit.Pack.ReadAsync(id).ConfigureAwait(false);
            if (!IsImage(id.Extension, bytes))
            {
                throw new FormatException(
                    $"The preview \"{KitText.Show(id.ToString())}\" does not start like a {id.Extension} image.");
            }

            previews[$"preview-{index}.{id.Extension}"] = bytes;
        }

        return previews;
    }

    // True when bytes begin as a file of extension does.
    // shortcut: only the first bytes are read, so a damaged picture passes. The editor decodes under its own
    // pixel limit.
    private static bool IsImage(string extension, byte[] bytes) =>
        extension switch
        {
            "png" => bytes.AsSpan().StartsWith(new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a }),
            "jpg" or "jpeg" => bytes.AsSpan().StartsWith(new byte[] { 0xff, 0xd8, 0xff }),
            _ => bytes.AsSpan().StartsWith("RIFF"u8) && bytes.Length >= 12 && bytes.AsSpan(8, 4).SequenceEqual("WEBP"u8),
        };
}
