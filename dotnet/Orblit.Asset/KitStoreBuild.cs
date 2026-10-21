namespace Orblit.Asset;

/// <summary>The files to publish for one kit: its row, its releases and the previews of the latest.</summary>
internal sealed record BuiltKit(KitRow Row, KitReleases Releases, OrderedDictionary<string, byte[]> Previews);

/// <summary>One release that passed: how it is listed, the manifest it carries and its previews if it is the latest.</summary>
internal sealed record CheckedRelease(
    PublishedRelease Release, KitManifest Manifest, OrderedDictionary<string, byte[]> Previews);

/// <summary>
/// Downloads and checks releases, one at a time, keeping every problem.
/// </summary>
/// <remarks>
/// F5: each step answers what it found and also adds its reasons to <see cref="Problems"/>, so one run
/// reports every bad release and not the first.
/// </remarks>
internal sealed class KitStoreBuild(string scratch, IAssetTransport transport, TimeSpan timeout)
{
    private readonly PackFolder _folder = new(scratch);

    /// <summary>Every reason the store cannot be published, one line each.</summary>
    public List<string> Problems { get; } = [];

    /// <summary>
    /// Checks every release of <paramref name="entry"/>. Answers the files to publish for it, or null when a
    /// release failed, with the reasons in <see cref="Problems"/>.
    /// </summary>
    public async Task<BuiltKit?> KitAsync(KitEntry entry)
    {
        var passed = new List<CheckedRelease>();
        foreach (var release in entry.Releases)
        {
            if (await CheckAsync(entry, release).ConfigureAwait(false) is { } one)
            {
                passed.Add(one);
            }
        }

        if (passed.Count != entry.Releases.Count)
        {
            return null;
        }

        // The releases are newest first in both lists.
        var latest = passed[0];
        var names = latest.Previews.Keys.ToList();
        var thumbnail = names.Count == 0 ? null : $"kits/{entry.Publisher}/{entry.Slug}/{names[0]}";
        return new BuiltKit(
            new KitRow(latest.Manifest, entry.Latest.Released, thumbnail),
            new KitReleases(names, [.. passed.Select(one => one.Release)]),
            latest.Previews);
    }

    private async Task<CheckedRelease?> CheckAsync(KitEntry entry, KitRelease release)
    {
        var name = $"{entry.Publisher}/{entry.Slug} {release.Version}";
        if (!await FetchAsync(name, release).ConfigureAwait(false))
        {
            return null;
        }

        var bytes = new FilePackBytes(Path.Combine(_folder.Path, $"{release.Hash.Hex}.opack"));
        try
        {
            return await InspectAsync(name, entry, release, bytes).ConfigureAwait(false);
        }
        catch (FormatException error)
        {
            Problems.Add($"{name}: {KitText.Show(error.Message)}");
            return null;
        }
        finally
        {
            await bytes.DisposeAsync().ConfigureAwait(false);
            await _folder.RemoveAsync(release.Hash).ConfigureAwait(false);
        }
    }

    // Downloads the release into the scratch folder, which checks its hash. A wait that runs out stops the
    // build waiting, not the request.
    private async Task<bool> FetchAsync(string name, KitRelease release)
    {
        try
        {
            await _folder.DownloadAsync(release.Offer, transport, release.Origin).WaitAsync(timeout).ConfigureAwait(false);
            return true;
        }
        catch (Exception error) when (!PackDownload.IsMistake(error))
        {
            Problems.Add($"{name} could not be fetched. {KitText.Show($"{error.GetType().Name}: {error.Message}")}");
            return false;
        }
    }

    private async Task<CheckedRelease?> InspectAsync(
        string name, KitEntry entry, KitRelease release, FilePackBytes bytes)
    {
        var kit = await Kit.OpenAsync(await AssetPack.OpenAsync(bytes).ConfigureAwait(false)).ConfigureAwait(false);
        var found = Disagreements(entry, release, kit.Manifest)
            .Concat(await kit.CheckAsync().ConfigureAwait(false))
            .Concat(SizeProblem(release, await bytes.LengthAsync().ConfigureAwait(false)))
            .ToList();
        if (found.Count > 0)
        {
            Problems.AddRange(found.Select(line => $"{name}: {line}"));
            return null;
        }

        return new CheckedRelease(
            new PublishedRelease(release, kit.Manifest.Engine, kit.Manifest.ContainsScripts),
            kit.Manifest,
            release.Version == entry.Latest.Version
                ? await KitPreviews.ReadAsync(kit).ConfigureAwait(false)
                : []);
    }

    private static IEnumerable<string> Disagreements(KitEntry entry, KitRelease release, KitManifest manifest)
    {
        (string Field, string Wanted, string Found)[] fields =
        [
            ("publisher", entry.Publisher, manifest.Publisher),
            ("slug", entry.Slug, manifest.Slug),
            ("version", release.Version, manifest.Version),
        ];
        return fields
            .Where(field => field.Wanted != field.Found)
            .Select(field => $"its kit.json says {field.Field} \"{KitText.Show(field.Found)}\" and the entry says \"{field.Wanted}\".");
    }

    private static IEnumerable<string> SizeProblem(KitRelease release, long found) =>
        found == release.Bytes ? [] : [$"the file is {found} bytes and the entry says {release.Bytes}."];
}
