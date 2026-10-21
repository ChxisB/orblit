namespace Orblit.Asset;

/// <summary>A release as <c>releases.json</c> lists it: the entry's own words, and what the builder read out of the kit file.</summary>
public sealed class PublishedRelease
{
    /// <summary>Throws a <see cref="FormatException"/> when <paramref name="engine"/> is not a range of versions.</summary>
    public PublishedRelease(KitRelease release, KitEngine engine, bool containsScripts)
    {
        engine.Check();
        Release = release;
        Engine = engine;
        ContainsScripts = containsScripts;
    }

    public KitRelease Release { get; }

    /// <summary>The engine range of the kit file.</summary>
    public KitEngine Engine { get; }

    public bool ContainsScripts { get; }

    internal static PublishedRelease FromJson(KitJsonObject json)
    {
        var engine = json.Section("engine");
        return json.Read(() => new PublishedRelease(
            release: KitRelease.FromJson(json),
            engine: new KitEngine(engine.Text("from"), engine.Text("to")),
            containsScripts: json.Flag("containsScripts")));
    }

    internal OrderedDictionary<string, object?> ToJson()
    {
        var json = Release.ToJson();
        json["containsScripts"] = ContainsScripts;
        json["engine"] = new OrderedDictionary<string, object?> { ["from"] = Engine.From, ["to"] = Engine.To };
        return json;
    }
}
