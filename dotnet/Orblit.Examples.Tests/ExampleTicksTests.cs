using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Orblit.Parity;

namespace Orblit.Examples.Tests;

/// <summary>
/// What each example sends the renderer over its first 300 frames, against the
/// fixture. Every frame is a whole scene, so the fixture keeps FNV-1a digests:
/// one of each frame and one of each key across all frames. The bytes digested
/// are each key, then each value's type letter, length and raw bytes.
/// </summary>
public sealed class ExampleTicksTests
{
    private const int Frames = 300;

    private static readonly Fixture Golden = Fixture.Load("orblit_examples", "ticks");

    /// <summary>Names the gallery changed, against what the fixture calls them.</summary>
    private static readonly Dictionary<string, string> FixtureNames = new()
    {
        ["A scene as a control"] = "A scene as a widget",
    };

    public static TheoryData<string> Names() => [.. EngineExamples.All().Select(example => example.Name)];

    [Fact]
    public void TheFixtureIsFromCommittedSource() => Assert.DoesNotContain("+dirty", Golden.SourceCommit);

    [Theory]
    [MemberData(nameof(Names))]
    public void TheFirstFramesMatchTheFixture(string name)
    {
        var expected = Case(FixtureNames.GetValueOrDefault(name, name));
        var (values, keyValues) = Replay(EngineExamples.All().Single(example => example.Name == name));

        var wanted = expected.GetProperty("values").EnumerateArray().Select(value => value.GetString()).ToList();
        var first = wanted.Zip(values).TakeWhile(pair => pair.First == pair.Second).Count();
        var wantedKeys = expected.GetProperty("keyValues").EnumerateObject()
            .ToDictionary(key => key.Name, key => key.Value.GetString());
        var keys = wantedKeys.Keys.Union(keyValues.Keys)
            .Where(key => wantedKeys.GetValueOrDefault(key) != keyValues.GetValueOrDefault(key))
            .Order(StringComparer.Ordinal);
        Assert.True(first == Frames, $"Frame {first} differs, in {string.Join(", ", keys)}.");
    }

    private static JsonElement Case(string name) =>
        Golden.Cases.SingleOrDefault(c => c.GetProperty("name").GetString() == name) is { ValueKind: JsonValueKind.Object } found
            ? found
            : throw new InvalidDataException($"The fixture has no example called {name}.");

    private static (List<string> Values, Dictionary<string, string> KeyValues) Replay(Example example)
    {
        var camera = example.ViewPoint.ToRenderCamera();
        var frames = Enumerable.Range(0, Frames)
            .Select(i => example.Scene(camera, i / 60.0).ToMessage(i / 60.0)
                .ToDictionary(entry => entry.Key, entry => Form(entry.Value)))
            .ToList();
        var values = frames.Select(frame =>
        {
            var digest = new Digest();
            foreach (var key in frame.Keys.Order(StringComparer.Ordinal))
            {
                digest.Add(Text(key));
                digest.Add(frame[key]);
            }

            return digest.Hex;
        }).ToList();
        var keyValues = frames.SelectMany(frame => frame.Keys).Distinct().ToDictionary(key => key, key =>
        {
            var digest = new Digest();
            foreach (var frame in frames)
            {
                // An absent key is a letter no value uses, so absent and empty differ.
                digest.Add(frame.GetValueOrDefault(key) ?? Shaped('n', 0, []));
            }

            return digest.Hex;
        });
        return (values, keyValues);
    }

    /// <summary>A value's type letter, then its length, then its bytes.</summary>
    private static byte[] Form(object value) => value switch
    {
        float[] list => Shaped('f', list.Length, MemoryMarshal.AsBytes<float>(list).ToArray()),
        int[] list => Shaped('i', list.Length, MemoryMarshal.AsBytes<int>(list).ToArray()),
        long[] list => Shaped('l', list.Length, MemoryMarshal.AsBytes<long>(list).ToArray()),
        byte[] list => Shaped('b', list.Length, list),
        string[] list => Shaped('s', list.Length, [.. list.SelectMany(Text)]),
        bool flag => Shaped('z', 1, [flag ? (byte)1 : (byte)0]),
        double number => Shaped('d', 1, BitConverter.GetBytes(number)),
        string text => Shaped('t', 1, Text(text)),
        _ => throw new InvalidOperationException($"No form for {value.GetType()}."),
    };

    private static byte[] Shaped(char tag, int length, byte[] bytes) =>
        [(byte)tag, .. BitConverter.GetBytes(length), .. bytes];

    /// <summary>A string as its UTF-8 length and then its UTF-8 bytes.</summary>
    private static byte[] Text(string value)
    {
        var encoded = Encoding.UTF8.GetBytes(value);
        return [.. BitConverter.GetBytes(encoded.Length), .. encoded];
    }

    /// <summary>FNV-1a over 64 bits.</summary>
    private sealed class Digest
    {
        private ulong _hash = 0xcbf29ce484222325;

        public string Hex => _hash.ToString("x16", System.Globalization.CultureInfo.InvariantCulture);

        public void Add(byte[] data)
        {
            foreach (var b in data)
            {
                _hash = unchecked((_hash ^ b) * 0x100000001b3);
            }
        }
    }
}
