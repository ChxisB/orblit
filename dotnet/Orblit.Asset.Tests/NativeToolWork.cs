using System.Runtime.CompilerServices;

namespace Orblit.Asset.Tests;

/// <summary>Skips a test that runs a stand-in native program, which is a shell script.</summary>
public sealed class ShellFactAttribute : FactAttribute
{
    public ShellFactAttribute([CallerFilePath] string? sourceFilePath = null, [CallerLineNumber] int sourceLineNumber = -1)
        : base(sourceFilePath, sourceLineNumber)
    {
        if (!File.Exists("/bin/sh"))
        {
            Skip = "the stand-in native program is a shell script, and there is no /bin/sh here";
        }
    }
}

/// <summary>
/// A directory for each test, and the stand-in native programs that live in it. They run as real processes
/// through the real <see cref="NativeTool"/>, because the things worth testing here (a bad exit becoming a
/// useful error, the output directory being read back) are things an in-process fake would quietly get right
/// and the real one would not.
/// </summary>
public abstract class NativeToolWork : IDisposable
{
    // fake_tool: do what the spec says, as `key=value` pairs separated by semicolons. `exit=N` leaves with
    // that code, `say=TEXT` prints TEXT to stderr, `writeArg=I` writes a file at whatever argument I says,
    // `writeInto=I:NAME` writes NAME into the directory argument I names, and `writeAfter=FLAG:NAME` writes
    // NAME inside the path FLAG names, given as `FLAG path` or `FLAG=path`, and does nothing when this run
    // was not given FLAG. Files hold `contents of <their own name>`.
    private const string FakeTool = """
        write() { mkdir -p "$(dirname "$1")"; printf 'contents of %s' "$(basename "$1")" > "$1"; }
        argument() { eval "printf '%s' \"\${$(($1 + 2))}\""; }
        for a in "$@"; do printf '%s\n' "$a"; done | { cat; printf '\n'; } >> "$record"
        code=0
        set -f
        IFS=';'
        for instruction in $spec; do
          IFS=' '
          key=${instruction%%=*}
          value=${instruction#*=}
          case $key in
            exit) code=$value ;;
            say) printf '%s\n' "$value" >&2 ;;
            writeArg) write "$(argument "$value" "$@")" ;;
            writeInto) write "$(argument "${value%%:*}" "$@")/${value#*:}" ;;
            writeAfter)
              flag=${value%%:*}
              directory=''
              found=0
              for a in "$@"; do
                if [ "$found" = 1 ]; then directory=$a; found=2; break; fi
                case $a in "$flag="*) directory=${a#"$flag="}; found=2; break ;; esac
                if [ "$a" = "$flag" ]; then found=1; fi
              done
              if [ "$found" = 1 ]; then echo "fake_tool: no argument after $flag" >&2; exit 3; fi
              if [ "$found" = 2 ]; then write "$directory/${value#*:}"; fi ;;
            *) echo "fake_tool: unknown instruction \"$key\"" >&2; exit 3 ;;
          esac
          IFS=';'
        done
        exit "$code"
        """;

    protected NativeToolWork()
    {
        Work = Directory.CreateTempSubdirectory("orblit_importers_test");
        Record = NativeWork.Inside(Work, "record.txt");
    }

    protected DirectoryInfo Work { get; }

    /// <summary>Where a stand-in program writes the arguments of each run.</summary>
    protected string Record { get; }

    public void Dispose()
    {
        Work.Delete(recursive: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>A tool that does what <paramref name="spec"/> says and records the flags it was given.</summary>
    protected NativeTool Fake(string spec) =>
        Script("tool.sh", $"record='{Record}'\nspec='{spec}'\n{FakeTool}\n", "fake");

    /// <summary>A program called <paramref name="name"/> that is the shell script <paramref name="body"/>.</summary>
    protected NativeTool Script(string name, string body, string toolName)
    {
        var path = NativeWork.Inside(Work, name);
        File.WriteAllText(path, $"#!/bin/sh\n{body}");
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        return new NativeTool(toolName, path);
    }

    /// <summary>The arguments of each run, in order.</summary>
    protected List<List<string>> Runs() =>
        File.Exists(Record)
            ? [.. File.ReadAllText(Record).Trim().Split("\n\n").Select(run => run.Split('\n').ToList())]
            : [];

    protected List<string> OnlyRun() => Runs().Single();

    protected static ImportRequest RequestFor(
        string asset, IReadOnlyDictionary<string, object?>? settings = null, CookTarget? target = null) =>
        new()
        {
            Id = AssetId.Parse(asset),
            Bytes = "source bytes"u8.ToArray(),
            Settings = settings ?? new Dictionary<string, object?>(),
            Target = target ?? CookTarget.Any,
            Source = new MemoryAssetSource(),
        };

    /// <summary>Asserts that <paramref name="run"/> holds <paramref name="wanted"/> in that order, not necessarily side by side.</summary>
    protected static void AssertInOrder(IReadOnlyList<string> run, params string[] wanted)
    {
        var at = 0;
        foreach (var argument in run)
        {
            if (at < wanted.Length && argument == wanted[at])
            {
                at++;
            }
        }

        Assert.True(at == wanted.Length, $"[{string.Join(", ", run)}] does not hold [{string.Join(", ", wanted)}] in order.");
    }
}
