using Orblit.Terrain;
using TerrainData = Orblit.Terrain.Terrain;

namespace Orblit.Asset;

/// <summary>
/// Checks a terrain at cook time and carries it into the bundle.
/// A terrain is one <c>.oterrain</c> file and a <c>.oregion</c> file for each region it lists. Neither is
/// cooked into anything smaller. A region is already written without the maps that sit at their defaults, and
/// the files are what the runtime reads. So both are carried as written, and what the cook adds is the check
/// a build otherwise makes at run time, as a hole in the ground on somebody's phone.
/// A terrain depends on every region it lists and on every image, model and material its sets and scatter
/// layers name, so one that is missing fails the terrain's cook the way a missing model fails a scene's. A
/// region file is checked against its own name. The terrain then checks each of its regions against its own
/// size, which no one region can know.
/// </summary>
public sealed class TerrainImporter : Importer
{
    /// <summary>
    /// The output of a cooked terrain. The bundle files it as <c>&lt;asset id&gt;.oterrain</c>, so
    /// <c>world.oterrain</c> becomes <c>world.oterrain.oterrain</c>.
    /// </summary>
    public const string TerrainOutput = "oterrain";

    /// <summary>The output of a cooked region, filed as <c>x0_z0.oregion.oregion</c>.</summary>
    public const string RegionOutput = "oregion";

    private static readonly IReadOnlySet<string> Claimed = new HashSet<string> { "oterrain", "oregion" };

    public override string Name => "terrain";

    public override int Version => 1;

    public override IReadOnlySet<string> Extensions => Claimed;

    public override IReadOnlyDictionary<string, object?> ResolveSettings(ImportSettings settings) =>
        new Dictionary<string, object?>();

    // On the pool so that a terrain that cannot be read faults the task, as an async method would, rather than
    // throwing at the call.
    public override Task<IReadOnlySet<AssetId>> DependenciesOfAsync(
        AssetId id, byte[] bytes, IReadOnlyDictionary<string, object?> settings) =>
        Task.Run<IReadOnlySet<AssetId>>(() => Named(id, bytes));

    // A region has nothing to await, so it runs on the pool for the same reason: a region that fails its check
    // faults the task, as it does in Dart, rather than throwing at the call.
    public override Task<ImportResult> ImportAsync(ImportRequest request) =>
        request.Id.Extension == "oterrain" ? ImportTerrainAsync(request) : Task.Run(() => ImportRegion(request));

    /// <summary>The source id of region <paramref name="key"/>, beside the terrain at <paramref name="terrain"/>.</summary>
    public static AssetId RegionFile(AssetId terrain, RegionKey key) => AssetId.Parse(Beside(terrain, key.FileName));

    private static HashSet<AssetId> Named(AssetId id, byte[] bytes)
    {
        if (id.Extension != "oterrain")
        {
            return [];
        }

        var load = TerrainIn(id, bytes);
        var found = new HashSet<AssetId>(load.Regions.Select(key => RegionFile(id, key)));
        foreach (var path in PathsIn(load.Terrain))
        {
            found.Add(AssetAt(id, path));
        }

        return found;
    }

    private static ImportResult ImportRegion(ImportRequest request)
    {
        var id = request.Id;
        var load = RegionIn(id, request.Bytes);
        var holds = load.Region.Key;
        var named = RegionKey.FromFileName(id.Name);
        if (named is not null && named != holds)
        {
            throw new ImportFailureException(
                id, $"this file is named for region {named.Value.X}, {named.Value.Z} and holds region {holds.X}, {holds.Z}");
        }

        var notes = new List<string>();
        if (named is null)
        {
            notes.Add($"{id.Name} is not named for its place, so no terrain will find it. This one is {holds.FileName}.");
        }

        notes.AddRange(load.Problems);
        return new ImportResult
        {
            Outputs = new Dictionary<string, byte[]> { [RegionOutput] = request.Bytes },
            Notes = notes,
        };
    }

    private static async Task<ImportResult> ImportTerrainAsync(ImportRequest request)
    {
        var id = request.Id;
        var load = TerrainIn(id, request.Bytes);
        var size = load.Terrain.RegionSize;
        foreach (var key in load.Regions)
        {
            var file = RegionFile(id, key);
            var region = RegionIn(file, await request.Source.ReadAsync(file).ConfigureAwait(false)).Region;
            if (region.Key != key)
            {
                throw new ImportFailureException(
                    id, $"lists region {key.X}, {key.Z}, and {file.Name} holds region {region.Key.X}, {region.Key.Z}");
            }

            if (region.Size != size)
            {
                throw new ImportFailureException(
                    id,
                    $"{file.Name} is {region.Size} texels across and this terrain's regions are {size}, "
                    + "so it cannot be put in");
            }
        }

        var count = load.Regions.Count;
        var notes = new List<string>
        {
            $"{count} region{(count == 1 ? "" : "s")} of {size} texels, {DartText.Print(load.Terrain.Spacing)} m apart",
        };
        notes.AddRange(load.Problems);
        return new ImportResult
        {
            Outputs = new Dictionary<string, byte[]> { [TerrainOutput] = request.Bytes },
            Notes = notes,
        };
    }

    private static TerrainLoad TerrainIn(AssetId id, byte[] bytes)
    {
        try
        {
            return TerrainData.Decode(DartText.Decode(bytes));
        }
        catch (TerrainFormatException error)
        {
            throw new ImportFailureException(id, error.Message, error.Message);
        }
        catch (FormatException error)
        {
            throw new ImportFailureException(id, $"this is not text: {error.Message}", error.Message);
        }
    }

    private static RegionLoad RegionIn(AssetId id, byte[] bytes)
    {
        try
        {
            return TerrainRegion.Decode(bytes);
        }
        catch (RegionFormatException error)
        {
            throw new ImportFailureException(id, error.Message, error.Message);
        }
    }

    // Everything the sets and the scatter layers name, as the paths the file wrote. Each is "a path within the
    // project".
    private static IEnumerable<string> PathsIn(TerrainData terrain)
    {
        foreach (var set in terrain.Sets)
        {
            if (set.Albedo is not null)
            {
                yield return set.Albedo;
            }

            if (set.Normal is not null)
            {
                yield return set.Normal;
            }
        }

        foreach (var layer in terrain.Scatter)
        {
            if (layer.Mesh is not null)
            {
                yield return layer.Mesh;
            }

            if (layer.Material is not null)
            {
                yield return layer.Material;
            }
        }
    }

    private static AssetId AssetAt(AssetId terrain, string path) =>
        AssetId.TryParse(path)
        ?? throw new ImportFailureException(terrain, $"names \"{path}\", which is not a path within the project");

    private static string Beside(AssetId id, string name) => id.Directory.Length == 0 ? name : $"{id.Directory}/{name}";
}
