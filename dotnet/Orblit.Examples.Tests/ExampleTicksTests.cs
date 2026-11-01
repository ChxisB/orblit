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
        var seeded = ExampleFixture.Seeded.GetValueOrDefault(name) ?? [];
        var (values, keyValues) = Replay(ExampleFixture.Example(name), seeded);

        // A seeded frame matches on shape only. Its keys that are not seeded
        // still match on value, every frame, in the key digests. A grown frame
        // does not match even on shape, so only its key digests are matched,
        // with the seeded keys left out.
        HashSet<string> left = ExampleFixture.Grown.Contains(name) ? seeded : [];
        var wanted = expected.GetProperty(seeded.Count == 0 ? "values" : "shapes").EnumerateArray()
            .Select(value => value.GetString()).ToList();
        var first = left.Count > 0 ? Frames : wanted.Zip(values).TakeWhile(pair => pair.First == pair.Second).Count();
        var wantedKeys = expected.GetProperty("keyValues").EnumerateObject().Where(key => !left.Contains(key.Name)).ToDictionary(key => key.Name, key =>
            (seeded.Contains(key.Name) ? expected.GetProperty("keyShapes").GetProperty(key.Name) : key.Value).GetString());
        var keys = wantedKeys.Keys.Union(keyValues.Keys.Except(left))
            .Where(key => wantedKeys.GetValueOrDefault(key) != keyValues.GetValueOrDefault(key))
            .Order(StringComparer.Ordinal).ToList();
        Assert.True(first == Frames && keys.Count == 0, $"Frame {first} differs, in {string.Join(", ", keys)}.");
    }

    private static (List<string> Values, Dictionary<string, string> KeyValues) Replay(Example example, HashSet<string> seeded)
    {
        var frames = Enumerable.Range(0, Frames).Select(i => SceneDigest.Message(example, i / 60.0)).ToList();
        Func<Dictionary<string, byte[]>, string> digestOf = seeded.Count == 0 ? SceneDigest.Of : SceneDigest.ShapeOf;
        var values = frames.Select(digestOf).ToList();
        var keyValues = frames.SelectMany(frame => frame.Keys).Distinct().ToDictionary(key => key, key =>
        {
            var digest = new SceneDigest.Digest();
            foreach (var frame in frames)
            {
                var form = frame.GetValueOrDefault(key) ?? SceneDigest.Absent;
                // The type letter and the length.
                digest.Add(seeded.Contains(key) ? form[..5] : form);
            }

            return digest.Hex;
        });
        return (values, keyValues);
    }
}
