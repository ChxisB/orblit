using Orblit.Json;

namespace Orblit.Remote.Tests;

internal static class Rpc
{
    internal static async Task<IReadOnlyDictionary<string, object?>> Call(
        RemoteVerbs verbs,
        string method,
        string? @params = null)
    {
        var tail = @params is null ? "" : $",\"params\":{@params}";
        var reply = await verbs.HandleAsync($$"""{"jsonrpc":"2.0","id":1,"method":"{{method}}"{{tail}}}""");
        return Decode(reply!);
    }

    internal static IReadOnlyDictionary<string, object?> Decode(string text) =>
        (IReadOnlyDictionary<string, object?>)JsonText.Decode(text)!;

    internal static int CodeOf(IReadOnlyDictionary<string, object?> reply) =>
        (int)(long)((IReadOnlyDictionary<string, object?>)reply["error"]!)["code"]!;

    internal static string Result(IReadOnlyDictionary<string, object?> reply) => JsonText.Encode(reply["result"]);

    internal static OrderedDictionary<string, object?> Row(params ReadOnlySpan<(string Key, object? Value)> pairs)
    {
        var row = new OrderedDictionary<string, object?>();
        foreach (var (key, value) in pairs)
        {
            row[key] = value;
        }

        return row;
    }
}
