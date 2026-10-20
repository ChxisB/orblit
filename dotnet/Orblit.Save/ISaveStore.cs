namespace Orblit.Save;

/// <summary>Where saves are kept. A <see cref="SaveQueue"/> writes through it and reads through it.</summary>
public interface ISaveStore
{
    /// <summary>
    /// The save under <paramref name="key"/>, or null when there is none. Throws
    /// <see cref="FormatException"/> when what is stored is damaged.
    /// </summary>
    Task<SaveRecord?> ReadAsync(string key);

    /// <summary>Replaces the save under <paramref name="key"/>, whole or not at all.</summary>
    Task WriteAsync(string key, SaveRecord record);

    /// <summary>True when there is a save under <paramref name="key"/>.</summary>
    Task<bool> ExistsAsync(string key);

    /// <summary>Every key with a save.</summary>
    Task<IReadOnlyList<string>> KeysAsync();
}
