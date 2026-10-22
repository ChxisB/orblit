namespace Orblit.Scene.Tests;

internal static class DiffAssert
{
    /// <summary>
    /// The diff gets <paramref name="a"/> to <paramref name="b"/> and its inverse gets back. Checked
    /// on the encoded bytes, because that is the document as it is actually kept.
    /// </summary>
    public static void RoundTrips(SceneDiff diff, SceneDocument a, SceneDocument b)
    {
        Assert.Equal(b.Encode(), diff.ApplyTo(a).Encode());
        Assert.Equal(a.Encode(), diff.Inverse.ApplyTo(diff.ApplyTo(a)).Encode());
    }
}
