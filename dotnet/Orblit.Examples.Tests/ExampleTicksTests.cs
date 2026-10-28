namespace Orblit.Examples.Tests;

/// <summary>
/// What each example sends the renderer over its first 300 frames, against the
/// fixture. Every frame is a whole scene, so the fixture keeps FNV-1a digests:
/// one of each frame and one of each key across all frames.
/// </summary>
public sealed class ExampleTicksTests
{
    private const int Frames = 300;

    public static TheoryData<string> Names() => ExampleFixture.Names();

    [Fact]
    public void TheFixtureIsFromCommittedSource() => Assert.DoesNotContain("+dirty", ExampleFixture.Golden.SourceCommit);

    [Theory]
    [MemberData(nameof(Names))]
    public void TheFirstFramesMatchTheFixture(string name)
    {
        var expected = ExampleFixture.Case(name);
        var (values, keyValues) = Replay(ExampleFixture.Example(name));

        var wanted = expected.GetProperty("values").EnumerateArray().Select(value => value.GetString()).ToList();
        var first = wanted.Zip(values).TakeWhile(pair => pair.First == pair.Second).Count();
        var wantedKeys = expected.GetProperty("keyValues").EnumerateObject()
            .ToDictionary(key => key.Name, key => key.Value.GetString());
        var keys = wantedKeys.Keys.Union(keyValues.Keys)
            .Where(key => wantedKeys.GetValueOrDefault(key) != keyValues.GetValueOrDefault(key))
            .Order(StringComparer.Ordinal);
        Assert.True(first == Frames, $"Frame {first} differs, in {string.Join(", ", keys)}.");
    }

    private static (List<string> Values, Dictionary<string, string> KeyValues) Replay(Example example)
    {
        var frames = Enumerable.Range(0, Frames).Select(i => SceneDigest.Message(example, i / 60.0)).ToList();
        var values = frames.Select(SceneDigest.Of).ToList();
        var keyValues = frames.SelectMany(frame => frame.Keys).Distinct().ToDictionary(key => key, key =>
        {
            var digest = new SceneDigest.Digest();
            foreach (var frame in frames)
            {
                digest.Add(frame.GetValueOrDefault(key) ?? SceneDigest.Absent);
            }

            return digest.Hex;
        });
        return (values, keyValues);
    }
}
