using Orblit.Json;
using Args = System.Collections.Generic.IReadOnlyDictionary<string, object?>;

namespace Orblit.Remote.Tests;

/// <summary>
/// A tool server over a fake game. <paramref name="answers"/> maps a verb to its result, an
/// exception is thrown instead, and every call is kept in <see cref="Seen"/>.
/// </summary>
internal sealed class FakeGame
{
    internal FakeGame(Args? answers = null, bool allowWrite = false) =>
        Server = new ToolServer(
            (method, @params) =>
            {
                Seen.Add((method, @params));
                var answer = answers?.GetValueOrDefault(method);
                return answer is Exception error ? Task.FromException<object?>(error) : Task.FromResult(answer);
            },
            allowWrite);

    internal ToolServer Server { get; }

    internal List<(string Method, Args Params)> Seen { get; } = [];

    internal async Task<Args> Ask(string method, Args? @params = null)
    {
        var request = Wire.Map(("jsonrpc", "2.0"), ("id", 1), ("method", method));
        if (@params is not null)
        {
            request["params"] = @params;
        }

        return Rpc.Decode((await Server.HandleAsync(JsonText.Encode(request)))!);
    }

    internal Task<Args> Tool(string name, Args? arguments = null) =>
        Ask("tools/call", Wire.Map(("name", name), ("arguments", arguments ?? Wire.Map())));

    internal static Args Result(Args reply) => (Args)reply["result"]!;

    internal static List<Args> Content(Args reply) =>
        [.. ((IReadOnlyList<object?>)Result(reply)["content"]!).Cast<Args>()];

    /// <summary>The text of the one block a tool answered, as written.</summary>
    internal static string Written(Args reply) => (string)Content(reply).Single()["text"]!;

    /// <summary>The text of the one block a tool answered, decoded.</summary>
    internal static object? Text(Args reply) => JsonText.Decode(Written(reply));

    internal static bool IsError(Args reply) => (bool)Result(reply)["isError"]!;

    internal static IReadOnlyList<string> Names(Args reply) =>
        [.. ((IReadOnlyList<object?>)Result(reply)["tools"]!).Select(tool => (string)((Args)tool!)["name"]!)];
}
