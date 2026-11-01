namespace Orblit.Native;

/// <summary>
/// What happened when a script was compiled.
/// The output is kept whether it worked or not: a warning is worth showing on a
/// build that succeeded, and an error is the whole point of one that did not.
/// </summary>
/// <param name="Library">The library that was built, or null when it was not.</param>
/// <param name="Output">Everything the compiler said, both streams.</param>
/// <param name="Command">
/// The command that was run, so a build that fails in a way nobody expected can
/// be reproduced in a terminal rather than guessed at.
/// </param>
public sealed record BuildResult(FileInfo? Library, string Output, string Command)
{
    public bool Ok => Library is not null;
}
