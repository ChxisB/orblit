namespace Orblit.Asset;

/// <summary>
/// The files an install would replace, as the kit names them. Nothing was written when it is thrown.
/// </summary>
public sealed class KitConflictsException(IReadOnlyList<AssetId> ids)
    : Exception(
        "These files are already in the project with other contents: "
        + string.Join(", ", ids.Select(id => $"\"{KitText.Show(id.ToString())}\"")) + ".")
{
    public IReadOnlyList<AssetId> Ids { get; } = ids;
}
