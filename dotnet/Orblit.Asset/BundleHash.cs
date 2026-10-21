using System.Text;

namespace Orblit.Asset;

public static class BundleHash
{
    /// <summary>
    /// A hash of a whole bundle, for asking "did this build change anything". A manifest is already the answer to
    /// "what is in here", and hashing its bytes gives one number a build can compare between runs without reading
    /// any assets. Two clean cooks of an unchanged project produce the same one, which is the claim CI checks.
    /// </summary>
    public static ContentHash Of(AssetManifest manifest) => ContentHash.Of(Encoding.UTF8.GetBytes(manifest.Encode()));
}
