using System.Text.Json;
using System.Text.Json.Nodes;
using Orblit.Parity;
using Orblit.Progress;

namespace Orblit.Dialogue.Tests;

public class DialogueParityTests
{
    private static readonly Fixture Golden = Fixture.Load("orblit_dialogue", "dialogue");

    private const int MaxDepth = 8;

    [Fact]
    public void ParsedFilesMatchTheFixture() => Golden.Check("parse", Parse);

    [Fact]
    public void BooksMatchTheFixture() => Golden.Check("book", RunBook);

    private static JsonNode? Parse(JsonElement c)
    {
        try
        {
            var d = DialogueDefinition.Parse(c.GetProperty("text").GetString()!);
            return new JsonObject
            {
                ["key"] = d.Key,
                ["variants"] = Dump.Arr(d.Variants, v => new JsonArray(v.Start, Dump.Arr(v.When, Need))),
                ["nodes"] = Dump.Arr(d.Nodes.Keys.ToList(), k => k),
                ["beforeNames"] = Dump.Arr(d.BeforeNames, n => n),
                ["encoded"] = d.Encode(),
                ["stable"] = DialogueDefinition.Parse(d.Encode()).Encode() == d.Encode(),
            };
        }
        catch (DialogueFormatException error)
        {
            return Refused(error, c);
        }
    }

    private static JsonNode Need(Condition c) => new JsonArray(c.Name, c.Compare.Symbol, c.Value);

    // The words after a JSON syntax error or a printed condition are each language's own.
    private static JsonObject Refused(Exception error, JsonElement c)
    {
        var after = c.GetProperty("cutAfter").GetString();
        var at = after is null ? -1 : error.Message.IndexOf(after, StringComparison.Ordinal);
        var message = at < 0 ? error.Message : error.Message[..(at + after!.Length)];
        return new JsonObject { ["throws"] = true, ["message"] = message };
    }

    private static JsonNode? RunBook(JsonElement c)
    {
        var names = c.GetProperty("names").EnumerateArray().Select(Name).ToList();
        var schema = new ProgressSchema(1, names);
        var log = new List<string>();
        var hooks = c.GetProperty("hooks").EnumerateArray()
            .Select(h => h.GetString()!)
            .ToDictionary(h => h, h => Hook(h, log));
        var parsed = c.GetProperty("dialogues").EnumerateArray()
            .Select(d => DialogueDefinition.Parse(d.GetRawText()))
            .ToList();
        try
        {
            var book = new DialogueBook(schema, hooks, parsed);
            return new JsonObject
            {
                ["keys"] = Dump.Arr(book.Dialogues.ToList(), d => d.Key),
                ["before"] = Dump.Arr(book.Dialogues.ToList(), d => Dump.Arr(d.BeforeNames, n => n)),
                ["tables"] = Dump.Arr(
                    c.GetProperty("tables").EnumerateArray().ToList(),
                    t => Tabled(book, schema, t, log)),
            };
        }
        catch (ArgumentException error)
        {
            return Refused(error, c);
        }
    }

    private static ProgressName Name(JsonElement n) => n[0].GetString() == "w"
        ? new Whole(n[1].GetString()!)
        : new Slice(n[1].GetString()!, n[2].GetString()!, n[3].GetInt32(), n[4].GetInt32());

    /// <summary>The functions a case may register. Both languages hold the same four.</summary>
    private static Before Hook(string name, List<string> log) => name switch
    {
        "take" => table => Logged(log, $"take:{table.Get("gold")}", () => table.Set("gold", 0).Table),
        "bump" => table => Logged(log, $"bump:{table.Get("gold")}", () => table.Set("gold", table.Get("gold") + 1).Table),
        "id" => table => Logged(log, "id", () => table),
        "boom" => _ => Boom(log),
        _ => throw new ArgumentException($"{name} is not a hook.", nameof(name)),
    };

    // The entry is written before the change, so a change that throws still leaves its mark.
    private static ProgressTable Logged(List<string> log, string entry, Func<ProgressTable> change)
    {
        log.Add(entry);
        return change();
    }

    private static ProgressTable Boom(List<string> log)
    {
        log.Add("boom");
        throw new InvalidOperationException("no");
    }

    private static JsonObject Tabled(DialogueBook book, ProgressSchema schema, JsonElement sets, List<string> log) =>
        Arg(() =>
        {
            var table = new ProgressTable(schema);
            foreach (var a in sets.EnumerateArray())
            {
                table = table.Set(a[0].GetString()!, a[1].GetInt64()).Table;
            }

            var dialogues = book.Dialogues.ToList();
            return new JsonObject
            {
                ["table"] = Convert.ToHexStringLower(table.Encode()),
                ["startFor"] = Dump.Arr(dialogues, d => new JsonArray(d.Key, d.StartFor(table))),
                ["unknown"] = new JsonArray(
                    Known(() => book.Dialogue("nope").Key),
                    Known(() => book.Start("nope", table).IsOver)),
                ["walks"] = Dump.Arr(dialogues, d => Walk(book, d.Key, table, log)),
            };
        });

    // Narrower than Dump.Attempt: only a refused argument counts, so a surprise stays a failure.
    private static JsonObject Arg(Func<JsonNode?> body)
    {
        try
        {
            return new JsonObject { ["value"] = body() };
        }
        catch (ArgumentException)
        {
            return new JsonObject { ["throws"] = true };
        }
    }

    // A rule this package wrote carries its text, worded the same way in the fixture.
    private static JsonObject Known(Func<JsonNode?> body)
    {
        try
        {
            return new JsonObject { ["value"] = body() };
        }
        catch (ArgumentException error)
        {
            return new JsonObject { ["throws"] = true, ["message"] = error.Message };
        }
    }

    private static JsonObject Walk(DialogueBook book, string key, ProgressTable table, List<string> log)
    {
        var walker = new Walker(book, key, table, log);
        var failed = Failure(() => walker.Explore([]));
        return new JsonObject { ["key"] = key, ["threw"] = failed, ["states"] = new JsonArray([.. walker.States]) };
    }

    // How a call failed, in words both languages share.
    private static string? Failure(Action body)
    {
        try
        {
            body();
            return null;
        }
        catch (ArgumentOutOfRangeException)
        {
            return "range";
        }
        catch (ArgumentException)
        {
            return "arg";
        }
        catch (InvalidOperationException)
        {
            return "state";
        }
        catch (Exception)
        {
            return "other";
        }
    }

    private static JsonObject State(DialogueRunner runner, List<string> log)
    {
        var node = runner.Current;
        return new JsonObject
        {
            ["over"] = runner.IsOver,
            ["at"] = node?.Id,
            ["kind"] = node switch
            {
                LineNode => "line",
                ChoiceNode => "choice",
                _ => null,
            },
            ["say"] = node is LineNode line ? new JsonArray(line.Speaker, line.Text, line.Next) : null,
            ["options"] = Dump.Arr(runner.Options, o => new JsonArray(o.Text, o.Next, o.When.Count)),
            ["table"] = Convert.ToHexStringLower(runner.Table.Encode()),
            ["log"] = Dump.Arr(log.ToList(), l => l),
        };
    }

    // Every state a conversation can reach in MaxDepth steps, depth first. Each state is
    // replayed from the start, so a hook sees the same calls in both languages.
    private sealed class Walker(DialogueBook book, string key, ProgressTable table, List<string> log)
    {
        internal List<JsonNode> States { get; } = [];

        private DialogueRunner Replay(List<int> path)
        {
            log.Clear();
            var runner = book.Start(key, table);
            foreach (var step in path)
            {
                Step(runner, step);
            }

            return runner;
        }

        private static void Step(DialogueRunner runner, int step)
        {
            if (step < 0)
            {
                runner.Next();
            }
            else
            {
                runner.Choose(step);
            }
        }

        internal void Explore(List<int> path)
        {
            var runner = Replay(path);
            var node = runner.Current;
            var probes = new JsonArray();
            if (node is not LineNode)
            {
                probes.Add(Failure(runner.Next));
            }

            probes.Add(Failure(() => runner.Choose(-1)));
            probes.Add(Failure(() => runner.Choose(runner.Options.Count)));
            var here = State(runner, log);
            here["path"] = string.Join(',', path);
            here["probes"] = probes;
            States.Add(here);

            var steps = node switch
            {
                LineNode => [-1],
                ChoiceNode => Enumerable.Range(0, runner.Options.Count).ToList(),
                _ => new List<int>(),
            };
            foreach (var step in steps)
            {
                var next = new List<int>(path) { step };
                var again = Replay(path);
                var failed = Failure(() => Step(again, step));
                if (failed is not null)
                {
                    States.Add(new JsonObject
                    {
                        ["path"] = string.Join(',', next),
                        ["threw"] = failed,
                        ["after"] = State(again, log),
                    });
                }
                else if (next.Count < MaxDepth)
                {
                    Explore(next);
                }
            }
        }
    }
}
