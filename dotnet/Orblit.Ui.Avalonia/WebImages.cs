using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace Orblit.Ui.Avalonia;

/// <summary>
/// Fetches the pictures an interface names from the web. Only http and https are read, so a
/// description cannot make a surface open a file on the machine it runs on.
/// </summary>
public sealed class WebImages(HttpClient http)
{
    /// <summary>The picture at an address, or null for anything that cannot be fetched or decoded.</summary>
    public async Task<IImage?> LoadAsync(string source)
    {
        if (!Uri.TryCreate(source, UriKind.Absolute, out var address) || address.Scheme is not ("http" or "https"))
        {
            return null;
        }

        try
        {
            using var bytes = new MemoryStream(await http.GetByteArrayAsync(address));
            return new Bitmap(bytes);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or ArgumentException or InvalidOperationException or NotSupportedException)
        {
            return null;
        }
    }
}
