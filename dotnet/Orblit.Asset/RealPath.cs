namespace Orblit.Asset;

/// <summary>
/// Where a path really is: every link in it followed, in every folder along the way and not only in
/// the last name. <see cref="Directory.ResolveLinkTarget"/> follows the last name only, so a root
/// reached through a linked folder (every temporary directory on macOS is, <c>/var</c> being a link
/// to <c>/private/var</c>) would still look like itself.
/// </summary>
internal static class RealPath
{
    // The most links one path may pass through, as the operating systems cap it.
    private const int MaxLinks = 40;

    private static readonly char[] Separators = [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar];

    /// <summary>
    /// Throws <see cref="FileNotFoundException"/> when something along the path is missing and
    /// <see cref="IOException"/> when the links loop.
    /// </summary>
    public static string Of(string path) => new Walk(path).Run();

    private sealed class Walk
    {
        private readonly string _original;
        private readonly LinkedList<string> _pending;
        private string _current;
        private int _links;

        public Walk(string original)
        {
            _original = original;
            var full = Path.IsPathRooted(original) ? original : Path.Combine(Directory.GetCurrentDirectory(), original);
            _current = Path.GetPathRoot(full) ?? throw Missing();
            _pending = new LinkedList<string>(full[_current.Length..].Split(Separators));
        }

        public string Run()
        {
            while (_pending.First is { } head)
            {
                _pending.RemoveFirst();
                Step(head.Value);
            }

            return _current;
        }

        private void Step(string part)
        {
            if (part is "" or ".")
            {
                return;
            }

            if (part == "..")
            {
                _current = Path.GetDirectoryName(_current.TrimEnd(Path.DirectorySeparatorChar)) ?? _current;
                return;
            }

            var next = Path.Combine(_current, part);
            if (new FileInfo(next).LinkTarget is { } target)
            {
                Redirect(target);
                return;
            }

            _current = File.Exists(next) || Directory.Exists(next) ? next : throw Missing();
        }

        // A link is read where it stands, so a relative target is relative to the folder it is in.
        private void Redirect(string target)
        {
            _links = _links < MaxLinks ? _links + 1 : throw new IOException($"Too many levels of links in \"{_original}\".");
            var rest = target;
            if (Path.GetPathRoot(target) is { Length: > 0 } root)
            {
                _current = root;
                rest = target[root.Length..];
            }

            foreach (var part in rest.Split(Separators).Reverse())
            {
                _pending.AddFirst(part);
            }
        }

        private FileNotFoundException Missing() => new("There is no such path.", _original);
    }
}
