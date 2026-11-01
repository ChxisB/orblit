using System.Globalization;
using System.Text.RegularExpressions;
using Orblit.Json;
using Orblit.VectorMath;

namespace Orblit.Ui;

/// <summary>
/// The words an interface shows, by key, in as many languages as were added. A table is a map from
/// key to text. A text node names its key and gives the arguments, and <see cref="Text"/> answers in
/// the language in use. A key a language lacks is looked up in its parent language and then in the
/// default. A key nobody has is answered as itself, so a missing translation shows as a name a
/// translator can search for and not a blank.
/// </summary>
public sealed partial class UiStrings
{
    private readonly Dictionary<string, Dictionary<string, string>> _tables = [];

    /// <summary>Starts in <paramref name="locale"/>, with <paramref name="fallback"/> as the language of last resort.</summary>
    /// <exception cref="ArgumentException">A tag is empty.</exception>
    public UiStrings(string locale = "en", string fallback = "en")
    {
        Fallback = TagOf(fallback);
        Locale = TagOf(locale);
    }

    /// <summary>Raised on every change of language or table, which is how a live interface redraws with no new description.</summary>
    public event Action? Changed;

    /// <summary>The language a key is looked up in last.</summary>
    public string Fallback { get; }

    /// <summary>The language in use.</summary>
    public string Locale { get; private set; }

    /// <summary>The languages that have a table, in alphabetical order.</summary>
    public IReadOnlyList<string> Locales => [.. _tables.Keys.Order(StringComparer.Ordinal)];

    /// <summary>
    /// <paramref name="tag"/> as the tables are keyed: lower case, with a hyphen, so <c>fr_CA</c>,
    /// <c>fr-CA</c> and <c>fr-ca</c> are one language.
    /// </summary>
    /// <exception cref="ArgumentException">The tag is empty.</exception>
    public static string TagOf(string tag)
    {
        var clean = PlainText.ToLower(PlainText.Trim(tag)).Replace('_', '-');
        return clean.Length == 0 ? throw new ArgumentException("The tag is empty.", nameof(tag)) : clean;
    }

    /// <summary>
    /// The plural form <paramref name="count"/> takes in <paramref name="locale"/>: one of zero, one,
    /// two, few, many and other.
    /// </summary>
    public static string PluralForm(string locale, long count) => PluralRules.Form(TagOf(locale), count);

    /// <summary>Switches the language, and tells listeners when it changed.</summary>
    public void Use(string locale)
    {
        var tag = TagOf(locale);
        if (tag == Locale)
        {
            return;
        }

        Locale = tag;
        Changed?.Invoke();
    }

    /// <summary>Drops every table, for a host that reads its files again.</summary>
    public void Clear()
    {
        _tables.Clear();
        Changed?.Invoke();
    }

    /// <summary>Adds <paramref name="table"/> to the keys <paramref name="locale"/> has, replacing any it already had.</summary>
    public void Add(string locale, IReadOnlyDictionary<string, string> table)
    {
        var tag = TagOf(locale);
        if (!_tables.TryGetValue(tag, out var own))
        {
            own = _tables[tag] = [];
        }

        foreach (var (key, text) in table)
        {
            own[key] = text;
        }

        Changed?.Invoke();
    }

    /// <summary>
    /// Adds the table in <paramref name="source"/>, one JSON object of key to text, which is what a
    /// language file holds.
    /// </summary>
    /// <exception cref="FormatException">The source is anything else.</exception>
    public void AddJson(string locale, string source)
    {
        if (JsonText.Decode(source) is not IReadOnlyDictionary<string, object?> parsed
            || parsed.Values.Any(text => text is not string))
        {
            throw new FormatException("A language file is one object of text");
        }

        Add(locale, parsed.ToDictionary(entry => entry.Key, entry => (string)entry.Value!));
    }

    /// <summary>
    /// The languages looked in for <paramref name="locale"/>, first to last: the language itself, each
    /// parent with a part of the tag dropped, and then the default and its parents.
    /// </summary>
    public IReadOnlyList<string> Chain(string locale) => [.. Ladder(TagOf(locale)).Concat(Ladder(Fallback)).Distinct()];

    /// <summary>
    /// The text for <paramref name="key"/> in the language in use, with each <c>{name}</c> in it replaced
    /// by the argument of that name. A whole <c>count</c> argument picks the plural form: the table holds
    /// <c>key.one</c>, <c>key.other</c> and so on, and <c>key</c> itself is the last resort. A name with
    /// no argument, or a null one, is left as written.
    /// </summary>
    public string Text(string key, IReadOnlyDictionary<string, object?>? args = null) => Find(key, args) ?? key;

    /// <summary>The text, or null when no language has <paramref name="key"/>, for a caller that has words of its own to show.</summary>
    public string? Find(string key, IReadOnlyDictionary<string, object?>? args = null)
    {
        var found = FindIn(key, WholeCount(args?.GetValueOrDefault("count")));
        return found is null
            ? null
            : Placeholder().Replace(
                found,
                match => (args?.GetValueOrDefault(match.Groups[1].Value) is { } value ? Shown(value) : match.Value));
    }

    /// <summary>
    /// The keys each language has no text for, for a translator's to-do list. A key counts as known when
    /// any table has it or <paramref name="used"/> names it. A language lacks it when neither its own
    /// table nor a parent language's has it, so <c>fr-ca</c> is not asked for what <c>fr</c> says. The
    /// default is checked as well, which is where a key used but written nowhere shows. Plural forms are
    /// one key, named without their last part.
    /// </summary>
    /// <remarks>A language with one of a key's forms counts as having it, whether or not it has every form it needs.</remarks>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Gaps(IEnumerable<string>? used = null)
    {
        var every = _tables.Values.SelectMany(table => table.Keys).Concat(used ?? []).Select(Stem).ToHashSet();
        var gaps = new OrderedDictionary<string, IReadOnlyList<string>>();
        foreach (var locale in Locales.Append(Fallback).Distinct())
        {
            var own = Ladder(locale).Where(_tables.ContainsKey).SelectMany(tag => _tables[tag].Keys).Select(Stem).ToHashSet();
            var lacking = every.Where(key => !own.Contains(key)).Order(StringComparer.Ordinal).ToList();
            if (lacking.Count > 0)
            {
                gaps[locale] = lacking;
            }
        }

        return gaps;
    }

    // A name is ASCII letters, digits and underscore, so the class is spelled out. \z is the end of the
    // text, where $ also matches before a final newline.
    [GeneratedRegex(@"\{([A-Za-z0-9_]+)\}", RegexOptions.CultureInvariant)]
    private static partial Regex Placeholder();

    [GeneratedRegex(@"\.(zero|one|two|few|many|other)\z", RegexOptions.CultureInvariant)]
    private static partial Regex FormSuffix();

    private static string Stem(string key) => FormSuffix().Replace(key, "", 1);

    // The tag and each shorter tag made by dropping its last part, so zh-hant-tw gives zh-hant and then zh.
    private static IEnumerable<string> Ladder(string tag)
    {
        var parts = tag.Split('-');
        return Enumerable.Range(0, parts.Length).Select(drop => string.Join('-', parts.Take(parts.Length - drop)));
    }

    // A whole number, an integer or a fraction with none. A fraction past the range saturates.
    private static long? WholeCount(object? count) => count switch
    {
        long whole => whole,
        double fraction when fraction % 1 == 0 => (long)fraction,
        _ => null,
    };

    private string? FindIn(string key, long? count)
    {
        foreach (var tag in Chain(Locale))
        {
            string[] forms = count is { } n ? [$"{key}.{PluralForm(tag, n)}", $"{key}.other", key] : [key];
            if (_tables.TryGetValue(tag, out var table) && forms.FirstOrDefault(table.ContainsKey) is { } hit)
            {
                return table[hit];
            }
        }

        return null;
    }

    // A value the way it reads in a sentence. A list or a map shows its parts without quotes.
    private static string Shown(object? value) => value switch
    {
        null => "null",
        string text => text,
        bool flag => flag ? "true" : "false",
        long whole => whole.ToString(CultureInfo.InvariantCulture),
        double fraction => Doubles.ToFileString(fraction),
        IReadOnlyList<object?> list => $"[{string.Join(", ", list.Select(Shown))}]",
        IReadOnlyDictionary<string, object?> map => $"{{{string.Join(", ", map.Select(entry => $"{entry.Key}: {Shown(entry.Value)}"))}}}",
        _ => throw new ArgumentException($"{value.GetType().Name} is not a JSON value.", nameof(value)),
    };
}
