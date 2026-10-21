using System.Collections.Concurrent;

namespace Orblit.Asset;

/// <summary>
/// Works out which settings apply to an asset, from the files around it. Four files can have a
/// say about <c>textures/wood/wall.png</c>, read in this order, each laid over the one before:
/// <c>.import.json</c>, <c>textures/.import.json</c>, <c>textures/wood/.import.json</c> and
/// <c>textures/wood/wall.png.import.json</c>.
/// </summary>
public class ImportSettingsReader(IAssetSource source)
{
    // A cook is a single pass over a project that does not change while it runs, so nothing here
    // expires. A tool that watches for edits makes a new reader.
    private readonly ConcurrentDictionary<AssetId, ImportSettings?> _read = new();

    public IAssetSource Source { get; } = source;

    /// <summary>Every settings file that can have a say about <paramref name="id"/>, outermost first.</summary>
    public static IReadOnlyList<AssetId> FilesFor(AssetId id)
    {
        var files = new List<AssetId> { ImportSettings.FolderFileFor("") };
        if (id.Directory.Length > 0)
        {
            var walked = new List<string>();
            foreach (var segment in id.Directory.Split('/'))
            {
                walked.Add(segment);
                files.Add(ImportSettings.FolderFileFor(string.Join('/', walked)));
            }
        }

        files.Add(ImportSettings.FileFor(id));
        return files;
    }

    /// <summary>What applies to <paramref name="id"/>, with each file above it laid over the last.</summary>
    public virtual async Task<ImportSettings> ForAssetAsync(AssetId id)
    {
        var settled = new ImportSettings();
        foreach (var file in FilesFor(id))
        {
            if (await FileAtAsync(file).ConfigureAwait(false) is { } found)
            {
                settled = settled.MergedWith(found);
            }
        }

        return settled;
    }

    private async Task<ImportSettings?> FileAtAsync(AssetId file)
    {
        if (_read.TryGetValue(file, out var known))
        {
            return known;
        }

        ImportSettings? settings;
        try
        {
            settings = ImportSettings.Parse(DartText.Decode(await Source.ReadAsync(file).ConfigureAwait(false)), file);
        }
        catch (AssetNotFoundException)
        {
            settings = null;
        }

        return _read[file] = settings;
    }
}
