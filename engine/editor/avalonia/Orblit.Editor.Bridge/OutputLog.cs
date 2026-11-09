using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Text;

namespace Orblit.Editor.Bridge;

/// <summary>
/// The lines the engine and the games it runs write to their log files. The engine cannot hand a C# caller its
/// log, so each process is started with <c>--log-file</c> on a path from <see cref="NewFile"/> and this class
/// reads the file as it grows. Call <see cref="Poll"/> on the thread that shows <see cref="Lines"/>.
/// </summary>
public sealed class OutputLog : IDisposable
{
    /// <summary>The lines kept. Older ones drop off the front.</summary>
    public const int MaxLines = 5000;

    /// <summary>The most bytes one poll reads from one file, so a game that logs in a loop cannot freeze the editor.</summary>
    public const int MaxRead = 256 * 1024;

    readonly ObservableCollection<string> _lines = [];
    readonly List<LogFile> _files = [];
    readonly string _folder = Directory.CreateTempSubdirectory("orblit-output-").FullName;

    public OutputLog()
    {
        Lines = new ReadOnlyObservableCollection<string>(_lines);
    }

    /// <summary>The kept lines, oldest first. The collection raises a change for each line added and for a clear.</summary>
    public ReadOnlyObservableCollection<string> Lines { get; }

    /// <summary>
    /// A path inside this log's own folder for a process to write its log to, which the log follows from now on.
    /// </summary>
    // shortcut: every file is read until the log is disposed, so each game run adds a cheap stat to every poll. Drop a file when its process exits.
    // It registers the file and returns its path in one call because a caller needs the path to start the process (F5).
    public string NewFile()
    {
        var path = Path.Combine(_folder, $"{_files.Count}.log");
        _files.Add(new LogFile(path));
        return path;
    }

    /// <summary>Adds the complete lines each file gained since the last call.</summary>
    public void Poll()
    {
        foreach (var file in _files)
        {
            foreach (var line in file.ReadNewLines())
            {
                _lines.Add(line);
                if (_lines.Count > MaxLines)
                {
                    _lines.RemoveAt(0);
                }
            }
        }
    }

    /// <summary>Empties <see cref="Lines"/>. What the files already held is not read again.</summary>
    public void Clear() => _lines.Clear();

    public void Dispose()
    {
        try
        {
            Directory.Delete(_folder, true);
        }
        // A game that is still running holds its log open on Windows. The system's temp cleaner takes the folder then.
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
        }
    }

    // One log file and how far it has been read. The bytes of a line with no end yet wait for the rest.
    sealed class LogFile(string path)
    {
        readonly List<byte> _pending = [];
        long _offset;

        public List<string> ReadNewLines()
        {
            var lines = new List<string>();
            try
            {
                using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                // A file shorter than what was read was started over, so what it holds now is new.
                if (file.Length < _offset)
                {
                    _offset = 0;
                    _pending.Clear();
                }
                var buffer = new byte[Math.Min(file.Length - _offset, MaxRead)];
                file.Seek(_offset, SeekOrigin.Begin);
                file.ReadExactly(buffer);
                _offset += buffer.Length;
                _pending.AddRange(buffer);
            }
            // The engine has not made the file yet, or has just lost it. The next poll tries again.
            catch (IOException)
            {
                return lines;
            }

            var start = 0;
            var span = CollectionsMarshal.AsSpan(_pending);
            while (span[start..].IndexOf((byte)'\n') is var length and >= 0)
            {
                lines.Add(Encoding.UTF8.GetString(span.Slice(start, length)).TrimEnd('\r'));
                start += length + 1;
            }

            _pending.RemoveRange(0, start);
            return lines;
        }
    }
}
