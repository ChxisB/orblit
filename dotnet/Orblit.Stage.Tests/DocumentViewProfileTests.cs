using Orblit.Profile;
using Orblit.Scene;
using Orblit.VectorMath;

namespace Orblit.Stage.Tests;

// The two phases the document view times: building the scene it hands the renderer, and applying a diff
// to what it holds.
public class DocumentViewProfileTests
{
    [Fact]
    public void TimesSceneBuildAndDiffAndNothingElse()
    {
        var profiler = Profiler.Create();
        profiler.Frame();
        var before = Documents.Of(Documents.Crate("crate", at: Vector3.Zero()));
        var view = new DocumentView(before, profiler: profiler);

        view.Apply(SceneDiff.Between(before, Documents.Of(Documents.Crate("crate", at: new Vector3(0, 1, 0)))));
        _ = view.Scene;
        profiler.Frame();

        var spans = (IReadOnlyDictionary<string, object?>)profiler.Snapshot()["spans"]!;
        Assert.Equal(["diff", "scene build"], spans.Keys.Order());
    }

    [Fact]
    public void IsSilentByDefault() =>
        Assert.Empty(new DocumentView(Documents.Of()).Profiler.Snapshot());
}
