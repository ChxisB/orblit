using static Orblit.Remote.Tests.Rpc;

namespace Orblit.Remote.Tests;

public sealed class RenderVerbsTests
{
    private const string PassText =
        """[{"name":"shadow","milliseconds":0.4,"draws":3},{"name":"colour","milliseconds":1.9,"draws":12}]""";

    private const string FirstDraw = """{"id":"object:1","mesh":null,"material":7,"instances":1}""";
    private const string SecondDraw = """{"id":"object:2","mesh":"rock.glb","material":7,"instances":1}""";
    private const string ThirdDraw = """{"id":"population:3","mesh":null,"material":null,"instances":500}""";

    private static readonly IReadOnlyList<IReadOnlyDictionary<string, object?>> _passRows =
    [
        Row(("name", "shadow"), ("milliseconds", 0.4), ("draws", 3L)),
        Row(("name", "colour"), ("milliseconds", 1.9), ("draws", 12L)),
    ];

    private static readonly IReadOnlyList<IReadOnlyDictionary<string, object?>> _drawRows =
    [
        Row(("id", "object:1"), ("mesh", null), ("material", 7L), ("instances", 1L)),
        Row(("id", "object:2"), ("mesh", "rock.glb"), ("material", 7L), ("instances", 1L)),
        Row(("id", "population:3"), ("mesh", null), ("material", null), ("instances", 500L)),
    ];

    private static RemoteVerbs Hosted(IReadOnlyList<IReadOnlyDictionary<string, object?>>? draws = null) =>
        new(new RemoteHost
        {
            Passes = () => Task.FromResult(_passRows),
            Draws = () => draws ?? _drawRows,
            Material = key => key == 7 ? Row(("key", 7L), ("roughness", 0.5)) : null,
        });

    private static IReadOnlyDictionary<string, object?> ResultOf(IReadOnlyDictionary<string, object?> reply) =>
        (IReadOnlyDictionary<string, object?>)reply["result"]!;

    [Fact]
    public async Task ViewPassesAnswersARowForEachPassTheHostReports()
    {
        Assert.Equal(PassText, Result(await Call(Hosted(), "view.passes")));
    }

    [Fact]
    public async Task ViewPassesWaitsOnlyAsLongAsTheHostTimeout()
    {
        var hung = new RemoteVerbs(new RemoteHost
        {
            Timeout = TimeSpan.FromMilliseconds(20),
            Passes = () => new TaskCompletionSource<IReadOnlyList<IReadOnlyDictionary<string, object?>>>().Task,
        });

        Assert.Equal(RemoteException.TimedOut, CodeOf(await Call(hung, "view.passes")));
    }

    [Fact]
    public async Task ViewDrawsAnswersTheCountAndTheRows()
    {
        Assert.Equal(
            $$"""{"count":3,"draws":[{{FirstDraw}},{{SecondDraw}},{{ThirdDraw}}],"truncated":false}""",
            Result(await Call(Hosted(), "view.draws")));
    }

    [Fact]
    public async Task ViewDrawsAnswersTheFirstHundredRowsUnlessAskedForMore()
    {
        var many = Hosted([.. Enumerable.Range(0, 150).Select(i => Row(("id", $"object:{i}")))]);

        var result = ResultOf(await Call(many, "view.draws"));

        Assert.Equal(150L, result["count"]);
        Assert.Equal(100, ((IReadOnlyList<object?>)result["draws"]!).Count);
        Assert.Equal(true, result["truncated"]);
    }

    [Fact]
    public async Task ViewDrawsCutsAtTheLimitAndSaysSo()
    {
        var result = ResultOf(await Call(Hosted(), "view.draws", """{"limit":2}"""));

        Assert.Equal(3L, result["count"]);
        Assert.Equal(2, ((IReadOnlyList<object?>)result["draws"]!).Count);
        Assert.Equal(true, result["truncated"]);
    }

    [Fact]
    public async Task ViewDrawsWithALimitOfZeroIsTheCountAlone()
    {
        Assert.Equal(
            """{"count":3,"draws":[],"truncated":true}""",
            Result(await Call(Hosted(), "view.draws", """{"limit":0}""")));
    }

    [Fact]
    public async Task ViewDrawsRefusesALimitThatIsBelowZeroOrNotANumber()
    {
        foreach (var limit in new[] { "-1", "\"ten\"", "1.5" })
        {
            var reply = await Call(Hosted(), "view.draws", $$"""{"limit":{{limit}}}""");
            Assert.True(RemoteException.InvalidParams == CodeOf(reply), limit);
        }
    }

    [Fact]
    public async Task ViewDrawsSaysAFloatJsonCannotWriteAsText()
    {
        var broken = Hosted([Row(("id", "object:1"), ("x", double.NaN), ("far", double.PositiveInfinity))]);

        var result = ResultOf(await Call(broken, "view.draws"));

        Assert.Equal(
            """[{"id":"object:1","x":"NaN","far":"Infinity"}]""",
            Orblit.Json.JsonText.Encode(result["draws"]));
    }

    [Fact]
    public async Task ViewDrawAnswersTheRowWithThatId()
    {
        Assert.Equal(SecondDraw, Result(await Call(Hosted(), "view.draw", """{"id":"object:2"}""")));
    }

    [Fact]
    public async Task ViewDrawWithAnIdNothingHasIsNotFound()
    {
        Assert.Equal(RemoteException.NotFound, CodeOf(await Call(Hosted(), "view.draw", """{"id":"object:99"}""")));
    }

    [Fact]
    public async Task ViewDrawNeedsAnIdAsText()
    {
        Assert.Equal(RemoteException.InvalidParams, CodeOf(await Call(Hosted(), "view.draw", """{"id":2}""")));
        Assert.Equal(RemoteException.InvalidParams, CodeOf(await Call(Hosted(), "view.draw")));
    }

    [Fact]
    public async Task ViewMaterialAnswersWhatTheHostHoldsForTheKey()
    {
        Assert.Equal(
            """{"key":7,"roughness":0.5}""",
            Result(await Call(Hosted(), "view.material", """{"key":7}""")));
    }

    [Fact]
    public async Task ViewMaterialWithAKeyTheHostHasNoMaterialForIsNotFound()
    {
        Assert.Equal(RemoteException.NotFound, CodeOf(await Call(Hosted(), "view.material", """{"key":8}""")));
    }

    [Fact]
    public async Task ViewMaterialNeedsAWholeNumberKey()
    {
        var reply = await Call(Hosted(), "view.material", """{"key":"brass"}""");

        Assert.Equal(RemoteException.InvalidParams, CodeOf(reply));
    }

    [Fact]
    public async Task AReadOnlySessionStillAnswersTheRenderVerbs()
    {
        var verbs = new RemoteVerbs(new RemoteHost { ReadOnly = true, Draws = () => _drawRows });

        Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>((await Call(verbs, "view.draws"))["result"]);
    }

    [Fact]
    public async Task AHostThatGaveNoRendererAnswersUnavailableForEachVerb()
    {
        var bare = new RemoteVerbs(new RemoteHost());

        foreach (var method in new[] { "view.passes", "view.draws", "view.draw", "view.material" })
        {
            var reply = await Call(bare, method, """{"id":"object:1","key":1}""");
            Assert.True(RemoteException.Unavailable == CodeOf(reply), method);
        }
    }

    [Fact]
    public async Task ARenderCallbackThatThrowsByItselfIsInternal()
    {
        var broken = new RemoteVerbs(new RemoteHost
        {
            Draws = () => throw new InvalidOperationException("scene is gone"),
            Material = _ => throw new ArgumentException("bad key"),
        });

        foreach (var (method, @params) in new[]
        {
            ("view.draws", "{}"),
            ("view.draw", """{"id":"object:1"}"""),
            ("view.material", """{"key":1}"""),
        })
        {
            Assert.True(RemoteException.Internal == CodeOf(await Call(broken, method, @params)), method);
        }
    }
}
