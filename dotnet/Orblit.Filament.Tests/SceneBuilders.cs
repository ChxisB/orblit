using System.Collections;
using Orblit.VectorMath;

namespace Orblit.Filament.Tests;

/// <summary>Scenes the message tests share, and a comparison of two messages value by value.</summary>
internal static class SceneBuilders
{
    internal static RenderCamera CameraAt(double x, double y, double z) =>
        new() { Position = new Vector3(x, y, z), Target = Vector3.Zero() };

    internal static RenderObject Plain(long key) =>
        new() { Key = key, Transform = Matrix4.Identity(), Colour = new Vector3(1, 1, 1) };

    internal static RenderScene Of(IReadOnlyList<RenderObject> objects, RenderCamera? camera = null) =>
        new() { Objects = objects, Camera = camera ?? CameraAt(0, 2, 8) };

    /// <summary>Six boxes in a row, which is enough for a batch.</summary>
    internal static RenderScene Row() =>
        Of(
            [
                .. Enumerable.Range(0, 6).Select(i => new RenderObject
                {
                    Key = 100 + i,
                    Transform = Matrix4.TranslationValues(i, 0, 0),
                    Colour = new Vector3(0.5, 0.4, 0.3),
                }),
            ]);

    /// <summary>A float32 cannot hold most of the doubles written into it, so packed numbers compare within a small distance.</summary>
    internal static void Near(double expected, float actual) => Assert.Equal(expected, actual, 1e-6);

    internal static void NearAll(float[] actual, params double[] expected)
    {
        Assert.Equal(expected.Length, actual.Length);
        for (var i = 0; i < expected.Length; i++)
        {
            Near(expected[i], actual[i]);
        }
    }

    internal static T Read<T>(IReadOnlyDictionary<string, object> message, string key)
        where T : class => (T)message[key];

    /// <summary>
    /// Every key but <paramref name="except"/> holds an equal value in both. Arrays compare by
    /// element, which is what a buffer handed to the renderer is.
    /// </summary>
    internal static void AssertSameBut(
        IReadOnlyDictionary<string, object> first,
        IReadOnlyDictionary<string, object> second,
        string except)
    {
        Assert.Equal(first.Keys.Order(StringComparer.Ordinal), second.Keys.Order(StringComparer.Ordinal));
        foreach (var key in first.Keys.Where(key => key != except))
        {
            Assert.True(
                StructuralComparisons.StructuralEqualityComparer.Equals(first[key], second[key]),
                $"{key} differs.");
        }
    }
}
