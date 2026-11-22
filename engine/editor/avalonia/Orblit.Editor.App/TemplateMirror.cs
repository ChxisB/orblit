using System.Net;
using System.Text.Json;

namespace Orblit.Editor.App;

/// <summary>
/// Downloads the export templates for a version of the engine. The site lists where each version's templates are
/// in a mirror list named by the engine's full version, the same list the engine reads.
/// </summary>
internal sealed class TemplateMirror
{
    /// <summary>Where the site serves its mirror lists.</summary>
    public const string Lists = "https://orblitengine.com/mirrorlist/";

    // A list names a few mirrors. A reply past this is not a mirror list.
    internal const int MaxListBytes = 64 * 1024;

    static readonly TimeSpan s_listTimeout = TimeSpan.FromSeconds(15);

    readonly HttpMessageHandler? _handler;

    /// <param name="handler">The transport. A test passes one that answers without a network.</param>
    public TemplateMirror(HttpMessageHandler? handler = null) => _handler = handler;

    /// <summary>
    /// The full version in what <c>orblit --version</c> prints, such as 1.1.stable.csharp from
    /// 1.1.stable.csharp.official.0a1b2c3d4. It is the number, the status and the parts the modules add.
    /// </summary>
    internal static string FullVersion(string printed)
    {
        var parts = printed.Trim().Split('.');
        var count = parts.TakeWhile(part => part.Length > 0 && part.All(char.IsAsciiDigit)).Count();
        if (count < 2 || count >= parts.Length)
        {
            throw new IOException($"The engine printed \"{printed.Trim()}\" as its version.");
        }

        // The status follows the number.
        count++;
        // shortcut: only the C# module and double precision add to the full version today. Add a part here when another module does.
        while (count < parts.Length && parts[count] is "csharp" or "double")
        {
            count++;
        }

        return string.Join('.', parts[..count]);
    }

    /// <summary>
    /// Downloads the templates for a full version, such as 1.1.stable.csharp, into a file of its own in the
    /// temporary folder and gives its path. Throws <see cref="IOException"/> with the reason when the site lists
    /// no templates for the version or the download fails.
    /// </summary>
    // shortcut: the download shows no progress and cannot be stopped. Add both when a slow mirror makes people wait.
    public async Task<string> DownloadAsync(string version, CancellationToken cancel)
    {
        using var client = _handler is null ? new HttpClient() : new HttpClient(_handler, false);
        try
        {
            var mirror = await MirrorAsync(client, version, cancel);
            var file = Path.Combine(Path.GetTempPath(), $"orblit-{version}-{Guid.NewGuid():N}.tpz");
            await SaveAsync(client, mirror, file, cancel);
            return file;
        }
        catch (Exception error) when (error is HttpRequestException or OperationCanceledException)
        {
            throw new IOException("The templates did not download. " + error.Message, error);
        }
    }

    async Task<Uri> MirrorAsync(HttpClient client, string version, CancellationToken cancel)
    {
        // HttpClient.Timeout ends at the headers when the body is streamed, so the whole read has its own limit.
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        limit.CancelAfter(s_listTimeout);
        using var response = await client.GetAsync(Lists + version + ".json", HttpCompletionOption.ResponseHeadersRead, limit.Token);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new IOException($"No export templates are published for {version}. Install a .tpz from a release instead.");
        }

        response.EnsureSuccessStatusCode();
        await using var body = await response.Content.ReadAsStreamAsync(limit.Token);
        using var bytes = new MemoryStream();
        var chunk = new byte[16 * 1024];
        int read;
        while ((read = await body.ReadAsync(chunk, limit.Token)) > 0)
        {
            if (bytes.Length + read > MaxListBytes)
            {
                throw new IOException("The site answered with more than a mirror list holds.");
            }

            bytes.Write(chunk, 0, read);
        }

        return FirstMirror(bytes.ToArray(), version);
    }

    // The list comes from the network, so only an https address in the shape the engine reads is taken.
    static Uri FirstMirror(byte[] list, string version)
    {
        try
        {
            using var json = JsonDocument.Parse(list);
            if (json.RootElement.ValueKind == JsonValueKind.Object
                && json.RootElement.TryGetProperty("mirrors", out var mirrors)
                && mirrors.ValueKind == JsonValueKind.Array)
            {
                foreach (var mirror in mirrors.EnumerateArray())
                {
                    if (mirror.ValueKind == JsonValueKind.Object
                        && mirror.TryGetProperty("url", out var url)
                        && Uri.TryCreate(url.GetString(), UriKind.Absolute, out var address)
                        && address.Scheme == Uri.UriSchemeHttps)
                    {
                        return address;
                    }
                }
            }
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException)
        {
            throw new IOException($"The mirror list for {version} is not one the editor can read.", error);
        }

        throw new IOException($"The mirror list for {version} names no https mirror.");
    }

    // A download that fails part way leaves no file behind.
    static async Task SaveAsync(HttpClient client, Uri mirror, string file, CancellationToken cancel)
    {
        try
        {
            using var response = await client.GetAsync(mirror, HttpCompletionOption.ResponseHeadersRead, cancel);
            response.EnsureSuccessStatusCode();
            await using var body = await response.Content.ReadAsStreamAsync(cancel);
            await using var output = File.Create(file);
            await body.CopyToAsync(output, cancel);
        }
        catch
        {
            File.Delete(file);
            throw;
        }
    }
}
