using System.Text.Json;
using System.Text.Json.Nodes;
using Orblit.Parity;
using Orblit.Progress;

namespace Orblit.Quest.Tests;

public class QuestParityTests
{
    private static readonly Fixture Dart = Fixture.Load("orblit_quest", "quest");

    [Fact]
    public void ParsedFilesMatchTheDartTwin() => Dart.Check("parse", Parse);

    [Fact]
    public void BooksMatchTheDartTwin() => Dart.Check("book", RunBook);

    [Fact]
    public void StageNamesMatchTheDartTwin() =>
        Dart.Check("name", c => QuestNames.StageName(c.GetProperty("key").GetString()!));

    private static JsonNode? Parse(JsonElement c)
    {
        try
        {
            var quest = QuestDefinition.Parse(c.GetProperty("text").GetString()!);
            return new JsonObject
            {
                ["key"] = quest.Key,
                ["title"] = quest.Title,
                ["last"] = quest.Last,
                ["stages"] = Dump.Arr(quest.Stages, StageRow),
                ["encoded"] = quest.Encode(),
            };
        }
        catch (QuestFormatException error)
        {
            return Refused(error, c);
        }
    }

    // The words after a JSON syntax error or a printed map are each language's own.
    private static JsonObject Refused(Exception error, JsonElement c)
    {
        var after = c.GetProperty("cutAfter").GetString();
        var at = after is null ? -1 : error.Message.IndexOf(after, StringComparison.Ordinal);
        var message = at < 0 ? error.Message : error.Message[..(at + after!.Length)];
        return new JsonObject { ["throws"] = true, ["message"] = message };
    }

    private static JsonNode StageRow(Stage stage) => new JsonArray(
        stage.Id,
        stage.Journal,
        Dump.Arr(stage.Requires, c => new JsonArray(c.Name, c.Compare.Symbol, c.Value)));

    private static JsonNode? RunBook(JsonElement c)
    {
        var names = c.GetProperty("names").EnumerateArray().Select(Name).ToList();
        var schema = new ProgressSchema(1, names);
        try
        {
            var quests = c.GetProperty("quests").EnumerateArray().Select(Quest).ToList();
            var book = new QuestBook(schema, quests);
            var keys = book.Quests.Select(q => q.Key).ToList();
            return new JsonObject
            {
                ["keys"] = Dump.Arr(keys, k => k),
                ["tables"] = Dump.Arr(c.GetProperty("tables").EnumerateArray().ToList(), t => Tabled(book, schema, t)),
            };
        }
        catch (ArgumentException error)
        {
            return Refused(error, c);
        }
    }

    private static JsonObject Tabled(QuestBook book, ProgressSchema schema, JsonElement assigns) => Arg(() =>
    {
        var table = new ProgressTable(schema);
        foreach (var a in assigns.EnumerateArray())
        {
            table = table.Set(a[0].GetString()!, a[1].GetInt64()).Table;
        }

        var asks = book.Quests.Select(q => q.Key).Append("nope").Select(key => Ask(book, table, key)).ToList();
        return new JsonObject { ["table"] = Convert.ToHexStringLower(table.Encode()), ["asks"] = Dump.Arr(asks, a => a) };
    });

    private static JsonObject Ask(QuestBook book, ProgressTable table, string key)
    {
        var last = key == "nope" ? 1 : book.Quest(key).Last + 1;
        var moves = new JsonArray();
        for (var to = -1; to <= last; to++)
        {
            foreach (var back in new[] { false, true })
            {
                var target = to;
                moves.Add(Arg(() => Move(book.Advance(table, key, target, allowBack: back), key)));
            }
        }

        return new JsonObject
        {
            ["stage"] = Arg(() => book.StageOf(table, key)),
            ["started"] = Arg(() => book.IsStarted(table, key)),
            ["complete"] = Arg(() => book.IsComplete(table, key)),
            ["journal"] = Arg(() => book.Journal(table, key)),
            ["moves"] = moves,
        };
    }

    private static JsonObject Move(QuestMove move, string key) => move switch
    {
        Moved moved => new JsonObject
        {
            ["stage"] = moved.Table.Get(QuestNames.StageName(key)),
            ["completed"] = moved.Completed,
            ["back"] = moved.Back,
            ["changes"] = Convert.ToHexStringLower(moved.Changes.Encode()),
            ["table"] = Convert.ToHexStringLower(moved.Table.Encode()),
        },
        Refused refused => new JsonObject { ["refused"] = Camel(refused.Reason) },
        _ => throw new InvalidOperationException("Not a move."),
    };

    private static string Camel(QuestRefusal reason) => reason switch
    {
        QuestRefusal.NotForward => "notForward",
        QuestRefusal.Requirement => "requirement",
        _ => throw new InvalidOperationException("Not a reason."),
    };

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

    private static ProgressName Name(JsonElement n) => n[0].GetString() == "w"
        ? new Whole(n[1].GetString()!)
        : new Slice(n[1].GetString()!, n[2].GetString()!, n[3].GetInt32(), n[4].GetInt32());

    private static QuestDefinition Quest(JsonElement q) => new(
        q.GetProperty("key").GetString()!,
        q.GetProperty("title").GetString()!,
        [.. q.GetProperty("stages").EnumerateArray().Select(s => new Stage(
            s.GetProperty("id").GetString()!,
            s.GetProperty("journal").GetString()!,
            [.. s.GetProperty("requires").EnumerateArray().Select(Need)]))]);

    private static Condition Need(JsonElement r) =>
        new(r[0].GetString()!, Compare.BySymbol(r[1].GetString()!)!, r[2].GetInt64());
}
