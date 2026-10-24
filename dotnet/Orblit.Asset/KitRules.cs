using System.Text.RegularExpressions;
using Orblit.Json;

namespace Orblit.Asset;

/// <summary>
/// What a kit may hold. A kit is written into a person's project by a program that trusts it, so the
/// rules refuse every name that would land somewhere a person did not look: a hidden folder, a name the
/// file system reads as something else, two names that are one file on a case insensitive disk, and a
/// name that is both a file and a folder. They also bound the count and the size, so a kit cannot fill
/// a disk.
/// </summary>
public static partial class KitRules
{
    /// <summary>The largest file a kit may hold.</summary>
    public const long FileLimit = 64L * 1024 * 1024;

    /// <summary>The most files a kit may hold.</summary>
    public const int CountLimit = 5000;

    /// <summary>The most bytes a kit may hold in all, counting a file once for each name.</summary>
    public const long TotalLimit = 1024L * 1024 * 1024;

    /// <summary>
    /// The largest kit file a release may be: the files of a kit, and room for the header, the directory
    /// and the padding between blobs.
    /// </summary>
    public const long PackLimit = TotalLimit + (16L * 1024 * 1024);

    /// <summary>The file at the root of a project that says which kits were installed.</summary>
    public const string LockName = "kits.lock";

    private const int SegmentLimit = 100;
    private const int PathLimit = 240;

    /// <summary>
    /// Extensions of a file a person could run, compile or load as code. A kit with one says so in
    /// <c>containsScripts</c>. The flag is a hint. A file with no extension can still be a program, so a
    /// person reads a kit before running anything in it.
    /// </summary>
    public static IReadOnlySet<string> ScriptExtensions { get; } = new HashSet<string>
    {
        "js", "mjs", "cjs", "jsx", "ts", "mts", "cts", "tsx", "dart", "sh", "bash", "zsh", "fish", "command",
        "bat", "cmd", "ps1", "psm1", "vbs", "wsf", "py", "pyw", "rb", "pl", "php", "lua", "scpt", "applescript",
        "gradle", "kts", "jar", "exe", "dll", "so", "dylib", "msi", "lnk",
    };

    /// <summary>Names that run code whatever their extension, in lower case.</summary>
    public static IReadOnlySet<string> ScriptNames { get; } = new HashSet<string>
    {
        "makefile", "gradlew", "mvnw", "cmakelists.txt", "package.json", "pubspec.yaml", "pubspec_overrides.yaml",
    };

    // The pattern is case insensitive for ASCII only and its dot leaves out the four line ends, so
    // the letters are spelt out and the dot is a class.
    [GeneratedRegex(
        @"\A([Cc][Oo][Nn]|[Pp][Rr][Nn]|[Aa][Uu][Xx]|[Nn][Uu][Ll]|[Cc][Oo][Mm][1-9]|[Ll][Pp][Tt][1-9])(\.[^\n\r\u2028\u2029]*)?\z",
        RegexOptions.CultureInvariant)]
    private static partial Regex Device();

    // Letters, digits and a few marks. Anything else, such as a Windows reserved character, an accent that
    // has two spellings or a bidirectional mark, would make a name read differently from the file it writes.
    [GeneratedRegex(@"\A[A-Za-z0-9_][A-Za-z0-9_.() +-]*\z", RegexOptions.CultureInvariant)]
    private static partial Regex Plain();

    /// <summary>True when <paramref name="id"/> is a file a person could run, compile or load as code.</summary>
    public static bool IsScript(AssetId id) =>
        ScriptExtensions.Contains(id.Extension) || ScriptNames.Contains(PlainText.ToLower(id.Name));

    /// <summary>True when one of <paramref name="ids"/> is a script.</summary>
    public static bool CarriesScripts(IEnumerable<AssetId> ids) => ids.Any(IsScript);

    /// <summary>Where <paramref name="id"/> lands when a kit is written under <paramref name="folder"/>. An empty folder is the project's own root.</summary>
    public static AssetId Place(AssetId id, string folder) =>
        folder.Length == 0 ? id : AssetId.Parse($"{folder}/{id}");

    /// <summary>
    /// Why the files <paramref name="ids"/> cannot be written under <paramref name="folder"/>, or null when
    /// they can. The folder is held to the rules a file name is: no hidden folder, no name a file system
    /// reads as something else. Otherwise a kit installed into <c>.git/hooks</c> would write what a person
    /// never looked at. At the root, a file that any case of <c>kits.lock</c> names would replace the
    /// record of what kits installed.
    /// </summary>
    public static string? PlaceProblem(string folder, IEnumerable<AssetId> ids)
    {
        if (folder.Length == 0)
        {
            return ids.Any(id => PlainText.ToLower(id.ToString()) == LockName)
                ? $"A file named {LockName} would replace the list of installed kits. Install the kit into a folder."
                : null;
        }

        var shown = KitText.Show(folder);
        if (AssetId.TryParse(folder) is null)
        {
            return $"\"{shown}\" is not a folder name. Use names joined by single slashes, with none at either end.";
        }

        foreach (var part in folder.Split('/'))
        {
            if (SegmentWhy(part) is { } why)
            {
                return $"\"{shown}\" has a part, \"{KitText.Show(part)}\", that {why}.";
            }
        }

        var longest = ids.Aggregate(0, (most, id) => Math.Max(most, id.ToString().Length));
        return folder.Length + 1 + longest > PathLimit
            ? $"\"{shown}\" is so long that a file in the kit would pass the limit of {PathLimit} characters."
            : null;
    }

    /// <summary>
    /// What is wrong with a kit that holds <paramref name="files"/>, each an id and its size in bytes, with the
    /// manifest left out. Empty when nothing is.
    /// </summary>
    public static List<string> Problems(KitManifest manifest, IReadOnlyDictionary<AssetId, long> files, long fileLimit = FileLimit)
    {
        var sizes = SizeProblems(files);
        if (files.Count > CountLimit)
        {
            return sizes;
        }

        var problems = new List<string>();
        if (files.Count == 0)
        {
            problems.Add("The kit holds no files.");
        }

        problems.AddRange(sizes);
        foreach (var (id, bytes) in files)
        {
            problems.AddRange(FileProblems(id, bytes, fileLimit));
        }

        problems.AddRange(CaseClashes(files.Keys));
        problems.AddRange(FolderClashes(files.Keys));
        problems.AddRange(manifest.Previews
            .Where(id => !files.ContainsKey(id))
            .Select(id => $"The preview \"{KitText.Show(id.ToString())}\" is not in the kit."));
        if (manifest.ContainsScripts != CarriesScripts(files.Keys))
        {
            problems.Add(manifest.ContainsScripts
                ? "The manifest says the kit holds scripts and it holds none."
                : "The manifest says the kit holds no scripts and it holds some.");
        }

        return problems;
    }

    private static List<string> SizeProblems(IReadOnlyDictionary<AssetId, long> files)
    {
        // A sum past 64 bits wraps and does not throw as Sum would.
        var total = files.Values.Aggregate(0L, (sum, bytes) => unchecked(sum + bytes));
        var problems = new List<string>();
        if (files.Count > CountLimit)
        {
            problems.Add($"The kit holds {files.Count} files, over the limit of {CountLimit}.");
        }

        if (total > TotalLimit)
        {
            problems.Add($"The kit holds {total} bytes in all, over the limit of {TotalLimit}.");
        }

        return problems;
    }

    private static List<string> FileProblems(AssetId id, long bytes, long limit)
    {
        var shown = KitText.Show(id.ToString());
        var problems = new List<string>();
        if (bytes > limit)
        {
            problems.Add($"\"{shown}\" is {bytes} bytes, over the limit of {limit}.");
        }

        if (id.ToString().Length > PathLimit)
        {
            problems.Add($"\"{shown}\" is longer than {PathLimit} characters.");
        }

        foreach (var segment in id.ToString().Split('/'))
        {
            if (SegmentWhy(segment) is { } why)
            {
                problems.Add($"\"{shown}\" has a part, \"{KitText.Show(segment)}\", that {why}.");
            }
        }

        return problems;
    }

    // What is wrong with one part of a path, ending a sentence that starts with the part, or null.
    private static string? SegmentWhy(string segment)
    {
        if (segment.Length > SegmentLimit)
        {
            return $"is longer than {SegmentLimit} characters";
        }

        if (segment.StartsWith('.'))
        {
            return "starts with a dot, so it would write a hidden file";
        }

        if (segment.EndsWith('.') || segment.EndsWith(' '))
        {
            return "ends with a dot or a space, which Windows drops";
        }

        if (Device().IsMatch(segment))
        {
            return "is a Windows device name";
        }

        return Plain().IsMatch(segment) ? null : "uses a character other than letters, digits, spaces and _.()+-";
    }

    private static List<string> CaseClashes(IEnumerable<AssetId> ids)
    {
        var seen = new Dictionary<string, AssetId>();
        var problems = new List<string>();
        foreach (var id in ids)
        {
            var lower = PlainText.ToLower(id.ToString());
            if (seen.TryGetValue(lower, out var other))
            {
                problems.Add($"\"{KitText.Show(other.ToString())}\" and \"{KitText.Show(id.ToString())}\" differ only in case.");
            }

            seen[lower] = id;
        }

        return problems;
    }

    // A name that is a file in one place and a folder in another cannot be written, so the kit would half install.
    private static List<string> FolderClashes(IEnumerable<AssetId> ids)
    {
        var idList = ids.ToList();
        var files = new Dictionary<string, AssetId>();
        foreach (var id in idList)
        {
            files[PlainText.ToLower(id.ToString())] = id;
        }

        var problems = new List<string>();
        foreach (var id in idList)
        {
            var parts = PlainText.ToLower(id.ToString()).Split('/');
            for (var i = 1; i < parts.Length; i++)
            {
                if (files.TryGetValue(string.Join('/', parts.Take(i)), out var file))
                {
                    problems.Add(
                        $"\"{KitText.Show(file.ToString())}\" is a file and \"{KitText.Show(id.ToString())}\" needs it to be a folder.");
                }
            }
        }

        return problems;
    }
}
