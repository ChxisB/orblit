namespace Orblit.Stage.Tests;

public class IncrementalTests
{
    [Fact]
    public void AViewMovedByADiffShowsWhatOneBuiltFromScratchShows()
    {
        foreach (var seed in Enumerable.Range(0, 200))
        {
            var random = new Random(seed);
            var size = 1 + random.Next(8);
            var before = RandomDocuments.Of(random, size);
            var after = RandomDocuments.Of(random, size);

            var moved = new DocumentView(before);
            moved.Replace(after);

            Assert.True(
                ViewShape.Of(new DocumentView(after)) == ViewShape.Of(moved),
                $"seed {seed}");
        }
    }

    [Fact]
    public void ALongRunOfEditsNeverDrifts()
    {
        var random = new Random(7);
        var view = new DocumentView(RandomDocuments.Of(random, 6));

        foreach (var step in Enumerable.Range(0, 150))
        {
            var next = RandomDocuments.Of(random, 1 + random.Next(8));
            view.Replace(next);

            Assert.True(
                ViewShape.Of(new DocumentView(next)) == ViewShape.Of(view),
                $"step {step}");
        }
    }
}
