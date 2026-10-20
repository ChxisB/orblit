using Orblit.VectorMath;

namespace Orblit.Terrain;

/// <summary>
/// One kind of ground a <see cref="Cover"/> can name: a pair of images and how they lie.
/// </summary>
public sealed class TerrainSet(
    string name,
    string? albedo = null,
    string? normal = null,
    double tileSize = 4,
    bool triplanar = false)
{
    /// <summary>What the set is called where people choose it: "grass", "scree".</summary>
    public string Name { get; } = name;

    /// <summary>
    /// The image whose colour is the ground's colour and whose alpha is its height, which is what
    /// lets one set show through another's gaps instead of fading into it. A path within the
    /// project.
    /// </summary>
    public string? Albedo { get; } = albedo;

    /// <summary>
    /// The image whose colour is a normal map and whose alpha is roughness. A path within the
    /// project.
    /// </summary>
    public string? Normal { get; } = normal;

    /// <summary>How many metres one repeat of the images covers.</summary>
    public double TileSize { get; } = tileSize;

    /// <summary>
    /// Whether the images are laid from three sides rather than dropped from above, so a cliff is
    /// not a smear. Costs three reads instead of one.
    /// </summary>
    public bool Triplanar { get; } = triplanar;

    public OrderedDictionary<string, object?> ToJson()
    {
        var json = new OrderedDictionary<string, object?> { ["name"] = Name };
        if (Albedo is not null)
        {
            json["albedo"] = Albedo;
        }

        if (Normal is not null)
        {
            json["normal"] = Normal;
        }

        json["tileSize"] = TileSize;
        if (Triplanar)
        {
            json["triplanar"] = true;
        }

        return json;
    }

    public static TerrainSet? FromJson(object? raw, List<string> problems)
    {
        if (JsonReads.Map(raw) is not { } map || map.GetValueOrDefault("name") is not string name)
        {
            problems.Add("A texture set with no name was left out.");
            return null;
        }

        var tileSize = JsonReads.Number(map, "tileSize");
        if (tileSize <= 0)
        {
            problems.Add($"The \"{name}\" set repeated every {Doubles.ToDartString(tileSize.Value)} m; it repeats every 4 m now.");
        }

        return new TerrainSet(
            name,
            albedo: JsonReads.Text(map, "albedo"),
            normal: JsonReads.Text(map, "normal"),
            tileSize: tileSize > 0 ? tileSize.Value : 4,
            triplanar: map.GetValueOrDefault("triplanar") is true);
    }
}
