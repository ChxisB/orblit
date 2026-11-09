namespace Orblit.Editor.App;

/// <summary>Reads the store's <c>index.json</c> over https, and only when asked.</summary>
internal sealed class KitIndexSource
{
    /// <summary>Where the store serves its index.</summary>
    public const string Address = "https://orblitengine.com/store/index.json";

    // The index lists every kit, so a few thousand rows fit. A reply past this is not an index.
    internal const int MaxBytes = 4 * 1024 * 1024;

    static readonly TimeSpan s_defaultTimeout = TimeSpan.FromSeconds(15);

    readonly HttpMessageHandler? _handler;
    readonly TimeSpan _timeout;

    /// <param name="handler">The transport. A test passes one that answers without a network.</param>
    /// <param name="timeout">How long the whole read may take, body included. A test passes a short one.</param>
    public KitIndexSource(HttpMessageHandler? handler = null, TimeSpan? timeout = null)
    {
        _handler = handler;
        _timeout = timeout ?? s_defaultTimeout;
    }

    /// <summary>The text of the index. Throws <see cref="IOException"/> when the store cannot be read or answers with too much.</summary>
    public async Task<string> ReadAsync(CancellationToken cancel)
    {
        using var client = _handler is null ? new HttpClient() : new HttpClient(_handler, false);
        // HttpClient.Timeout ends at the headers when the body is streamed, so the whole read has its own limit.
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        limit.CancelAfter(_timeout);
        var token = limit.Token;
        try
        {
            using var response = await client.GetAsync(Address, HttpCompletionOption.ResponseHeadersRead, token);
            response.EnsureSuccessStatusCode();
            await using var body = await response.Content.ReadAsStreamAsync(token);
            using var bytes = new MemoryStream();
            var chunk = new byte[16 * 1024];
            int read;
            while ((read = await body.ReadAsync(chunk, token)) > 0)
            {
                if (bytes.Length + read > MaxBytes)
                {
                    throw new IOException("The store answered with more than an index holds.");
                }

                bytes.Write(chunk, 0, read);
            }

            return System.Text.Encoding.UTF8.GetString(bytes.GetBuffer(), 0, (int)bytes.Length);
        }
        catch (Exception error) when (error is HttpRequestException or OperationCanceledException)
        {
            throw new IOException("The store did not answer. " + error.Message, error);
        }
    }
}
