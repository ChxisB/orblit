namespace Orblit.Asset;

/// <summary>
/// The importers a cook may choose from. Registration order decides ties: the first importer that
/// handles an asset wins, so a project that adds its own importer for <c>.png</c> ahead of the
/// built-in one gets its own.
/// </summary>
public sealed class ImporterRegistry
{
    private readonly List<Importer> _importers = [];
    private readonly Dictionary<string, Importer> _byName = [];

    public ImporterRegistry(IEnumerable<Importer>? importers = null)
    {
        foreach (var importer in importers ?? [])
        {
            Add(importer);
        }
    }

    /// <summary>The importers, in the order they will be tried.</summary>
    public IReadOnlyList<Importer> Importers => [.. _importers];

    public void Add(Importer importer)
    {
        if (_byName.TryGetValue(importer.Name, out var existing))
        {
            throw new ArgumentException(
                $"{importer.Name} is already registered, as {existing}. Two importers with one name would share cache entries while writing different bytes.",
                nameof(importer));
        }

        _byName[importer.Name] = importer;
        _importers.Add(importer);
    }

    /// <summary>The importer named by <paramref name="name"/>, or null.</summary>
    public Importer? ByName(string name) => _byName.GetValueOrDefault(name);

    /// <summary>
    /// Which importer cooks <paramref name="id"/>, honouring an <c>importer</c> named in
    /// <paramref name="settings"/> over what the extension says. Null when nothing claims the
    /// asset, which is not an error: a project holds README files too.
    /// </summary>
    public Importer? ForAsset(AssetId id, ImportSettings? settings = null)
    {
        if (settings?.Importer is { } named)
        {
            return _byName.GetValueOrDefault(named) ?? throw new ArgumentException(
                $"{named} is named by the settings for {id}, but no such importer is registered. Known importers: {string.Join(", ", _byName.Keys.Order(StringComparer.Ordinal))}.",
                nameof(settings));
        }

        return _importers.FirstOrDefault(importer => importer.CanHandle(id));
    }
}
