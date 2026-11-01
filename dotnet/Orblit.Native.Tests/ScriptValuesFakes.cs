namespace Orblit.Native.Tests;

internal sealed class MapValues(
    Dictionary<string, double>? numbers = null,
    Dictionary<string, bool>? toggles = null,
    Dictionary<string, string>? texts = null) : IScriptValues
{
    public Dictionary<string, double> Numbers { get; } = numbers ?? [];

    public Dictionary<string, bool> Toggles { get; } = toggles ?? [];

    public Dictionary<string, string> Texts { get; } = texts ?? [];

    /// <summary>Bumped by a test that changes something, the way the editor bumps it when somebody saves a data object.</summary>
    public int Revision { get; private set; }

    public void Changed() => Revision++;

    public double Number(string asset, string key, double fallback) =>
        Numbers.TryGetValue($"{asset}/{key}", out var found) ? found : fallback;

    public bool Toggle(string asset, string key, bool fallback) =>
        Toggles.TryGetValue($"{asset}/{key}", out var found) ? found : fallback;

    public string? Text(string asset, string key) => Texts.GetValueOrDefault($"{asset}/{key}");
}

/// <summary>Counts how often it is asked, so a test can show that a frame with no changes costs nothing.</summary>
internal sealed class CountingValues : IScriptValues
{
    public int Asked { get; private set; }

    public int Revision => 0;

    public double Number(string asset, string key, double fallback)
    {
        Asked++;
        return fallback;
    }

    public bool Toggle(string asset, string key, bool fallback)
    {
        Asked++;
        return fallback;
    }

    public string? Text(string asset, string key)
    {
        Asked++;
        return null;
    }
}
