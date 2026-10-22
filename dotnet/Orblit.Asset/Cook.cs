using System.Collections.Concurrent;

namespace Orblit.Asset;

/// <summary>
/// Runs importers, and does not run them when the cache already has the answer. The whole point of this class is
/// the order of the steps: settings are resolved before the key is built, dependencies are found before the key is
/// built, and the key is built before anything expensive happens, because a cache that is consulted after the work
/// is done saves nothing.
/// </summary>
public sealed class Cook
{
    private readonly ConcurrentDictionary<AssetId, Lazy<Task<ContentHash>>> _hashes = new();
    private readonly Lock _callbacks = new();

    // F2: six parameters, all named at each call.
    public Cook(
        IAssetSource source,
        ICookCache cache,
        ImporterRegistry importers,
        CookTarget target,
        ImportSettingsReader? settings = null,
        int concurrency = 4)
    {
        if (concurrency < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(concurrency), concurrency, "must be at least 1");
        }

        Source = source;
        Cache = cache;
        Importers = importers;
        Target = target;
        Settings = settings ?? new ImportSettingsReader(source);
        Concurrency = concurrency;
    }

    /// <summary>Where source bytes come from.</summary>
    public IAssetSource Source { get; }

    /// <summary>Where cooked bytes go, and where they are looked for first.</summary>
    public ICookCache Cache { get; }

    public ImporterRegistry Importers { get; }

    /// <summary>
    /// The machine being cooked for. One <see cref="Cook"/> cooks for one target; cooking for several means
    /// several of these, which is what the cook command does.
    /// </summary>
    public CookTarget Target { get; }

    /// <summary>Reads <c>.import.json</c>, with its own per-run cache so a folder of a hundred sprites reads the folder's settings once.</summary>
    public ImportSettingsReader Settings { get; }

    /// <summary>
    /// How many importers may run at once. Importers are mostly native tools that use every core they are given,
    /// so the useful number here is small: it is about keeping several processes fed.
    /// </summary>
    public int Concurrency { get; }

    /// <summary>
    /// Cooks one asset, and answers with what happened rather than throwing: one unreadable texture should not
    /// end a build of a thousand assets.
    /// </summary>
    public async Task<CookResult> CookOneAsync(AssetId id)
    {
        Importer? importer = null;
        CookKey? key = null;
        try
        {
            var plan = await PlanAsync(id, found => importer = found).ConfigureAwait(false);
            if (plan is null)
            {
                return new CookResult { Id = id, Status = CookStatus.Skipped };
            }

            key = plan.Key;
            return await Cache.LookUpAsync(plan.Key).ConfigureAwait(false) is { } hit
                ? new CookResult
                {
                    Id = id,
                    Status = CookStatus.Cached,
                    Importer = plan.Importer,
                    Key = plan.Key,
                    Asset = hit,
                    Dependencies = plan.Dependencies,
                }
                : await ImportAsync(id, plan).ConfigureAwait(false);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            // Remembered against the key, so that an editor can mark the asset without running the importer that
            // broke on it. Only once there is a key: a failure before that is one anybody asking can reproduce
            // for free, because working the key out is what failed.
            if (key is not null)
            {
                await RememberAsync(key, error).ConfigureAwait(false);
            }

            return new CookResult { Id = id, Status = CookStatus.Failed, Importer = importer, Key = key, Error = error };
        }
    }

    /// <summary>
    /// Where <paramref name="id"/> stands, without cooking it. It does everything a cook does up to the cache
    /// lookup, resolve the settings, pick the importer, hash the source and everything it depends on and build
    /// the key, and then stops instead of running the importer. An editor that worked the answer out its own way
    /// would drift from the build the first time an importer changed how it resolves a setting.
    /// </summary>
    public async Task<CookState> StateOfAsync(AssetId id)
    {
        try
        {
            var plan = await PlanAsync(id).ConfigureAwait(false);
            if (plan is null)
            {
                return CookState.Ignored;
            }

            if (await Cache.LookUpAsync(plan.Key).ConfigureAwait(false) is not null)
            {
                return CookState.Cooked;
            }

            // Only once the cache has been asked for the cooked bytes, because a key that has since cooked is
            // cooked whatever was once recorded against it.
            return await Cache.LookUpFailureAsync(plan.Key).ConfigureAwait(false) is not null
                ? CookState.Failed
                : CookState.Stale;
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            // Everything that can go wrong before an importer runs is something a build would fail on, and saying
            // so now is the whole point: the editor shows it while the person is looking at the asset.
            return CookState.Failed;
        }
    }

    /// <summary>
    /// <see cref="StateOfAsync"/> for many, <see cref="Concurrency"/> at a time. A texture that forty models
    /// depend on is read once for the whole answer.
    /// </summary>
    public async Task<IReadOnlyDictionary<AssetId, CookState>> StateOfAllAsync(IEnumerable<AssetId> ids)
    {
        var pending = ids.ToList();
        var states = new CookState[pending.Count];
        await RunWorkersAsync(pending.Count, async index => states[index] = await StateOfAsync(pending[index]).ConfigureAwait(false))
            .ConfigureAwait(false);

        var answer = new Dictionary<AssetId, CookState>();
        for (var i = 0; i < pending.Count; i++)
        {
            answer[pending[i]] = states[i];
        }

        return answer;
    }

    /// <summary>
    /// Cooks all of them, <see cref="Concurrency"/> at a time, reporting each as it lands. <paramref name="onResult"/>
    /// is called in completion order, one call at a time, so a caller printing progress gets it as it happens.
    /// The returned report is in <paramref name="ids"/> order, because that is the order a person reads a summary in.
    /// </summary>
    public async Task<CookReport> CookAllAsync(IEnumerable<AssetId> ids, Action<CookResult>? onResult = null)
    {
        var pending = ids.ToList();
        var results = new CookResult?[pending.Count];
        await RunWorkersAsync(pending.Count, async index =>
        {
            var result = await CookOneAsync(pending[index]).ConfigureAwait(false);
            results[index] = result;
            lock (_callbacks)
            {
                onResult?.Invoke(result);
            }
        }).ConfigureAwait(false);

        // A worker fills every slot before the report is read.
        return new CookReport(results.Select(result => result!));
    }

    /// <summary>Runs <paramref name="work"/> for each index below <paramref name="count"/>, <see cref="Concurrency"/> at a time.</summary>
    private Task RunWorkersAsync(int count, Func<int, Task> work)
    {
        var next = -1;

        async Task Worker()
        {
            while (true)
            {
                var index = Interlocked.Increment(ref next);
                if (index >= count)
                {
                    return;
                }

                await work(index).ConfigureAwait(false);
            }
        }

        return Task.WhenAll(Enumerable.Range(0, Math.Min(Concurrency, count)).Select(_ => Worker()));
    }

    private async Task<CookResult> ImportAsync(AssetId id, Plan plan)
    {
        var result = await plan.Importer.ImportAsync(new ImportRequest
        {
            Id = id,
            Bytes = plan.Bytes,
            Settings = plan.Settings,
            Target = Target,
            Source = Source,
        }).ConfigureAwait(false);
        var stored = await Cache.StoreAsync(plan.Key, result.Outputs).ConfigureAwait(false);
        return new CookResult
        {
            Id = id,
            Status = CookStatus.Cooked,
            Importer = plan.Importer,
            Key = plan.Key,
            Asset = stored,
            Dependencies = plan.Dependencies,
            Notes = result.Notes,
        };
    }

    /// <summary>
    /// Files a failure, and does not let filing it become the failure. A cache that cannot write is a slow build,
    /// never a broken one, and a note about a failure is the last thing that should replace the failure it is about.
    /// </summary>
    private async Task RememberAsync(CookKey key, Exception error)
    {
        try
        {
            await Cache.RecordFailureAsync(key, error.Message).ConfigureAwait(false);
        }
        catch (Exception refused) when (refused is not OutOfMemoryException)
        {
            // Nothing to do and nothing worth saying: the caller is already being told what actually went wrong.
        }
    }

    /// <summary>
    /// What cooking <paramref name="id"/> would involve, short of doing it: the importer that claims it, the bytes,
    /// the resolved settings, what it depends on, and the key all of that adds up to. Null when nothing claims it.
    /// The order here is the whole contract of this class, and it lives in one place so that
    /// <see cref="CookOneAsync"/> and <see cref="StateOfAsync"/> cannot come to disagree about it.
    /// <paramref name="onImporter"/> fires as soon as the importer is known, so a caller whose plan then throws
    /// can still say which importer was trying.
    /// </summary>
    private async Task<Plan?> PlanAsync(AssetId id, Action<Importer>? onImporter = null)
    {
        var settings = await Settings.ForAssetAsync(id).ConfigureAwait(false);
        var importer = Importers.ForAsset(id, settings);
        if (importer is null)
        {
            return null;
        }

        onImporter?.Invoke(importer);

        var resolved = importer.ResolveSettings(settings);
        var bytes = await Source.ReadAsync(id).ConfigureAwait(false);
        var dependencies = await importer.DependenciesInAsync(id, bytes, resolved, Source).ConfigureAwait(false);
        var hashes = new Dictionary<AssetId, ContentHash>();
        foreach (var dependency in dependencies)
        {
            hashes[dependency] = await HashOfAsync(dependency).ConfigureAwait(false);
        }

        return new Plan
        {
            Importer = importer,
            Bytes = bytes,
            Settings = resolved,
            Dependencies = dependencies,
            Key = new CookKey(importer.Name, importer.Version, ContentHash.Of(bytes), hashes, resolved, Target.Recipe),
        };
    }

    // Hashes of source files, kept for the length of one Cook so that a texture depended on by twenty models is
    // read and hashed once. A Lazy makes concurrent workers share one read where a plain GetOrAdd could start two.
    private Task<ContentHash> HashOfAsync(AssetId id) =>
        _hashes.GetOrAdd(id, key => new Lazy<Task<ContentHash>>(() => ReadHashAsync(key))).Value;

    private async Task<ContentHash> ReadHashAsync(AssetId id) =>
        ContentHash.Of(await Source.ReadAsync(id).ConfigureAwait(false));

    /// <summary>Everything worked out about an asset before an importer runs.</summary>
    private sealed class Plan
    {
        public required Importer Importer { get; init; }

        public required byte[] Bytes { get; init; }

        public required IReadOnlyDictionary<string, object?> Settings { get; init; }

        public required IReadOnlySet<AssetId> Dependencies { get; init; }

        public required CookKey Key { get; init; }
    }
}
