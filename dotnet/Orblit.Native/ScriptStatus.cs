namespace Orblit.Native;

/// <summary>What a script is doing.</summary>
/// <param name="Name">What the source file is called, without its extension.</param>
/// <param name="Source">The source file.</param>
/// <param name="Running">
/// False when the last build failed. The script stays listed either way: a
/// script that vanished from the list because it did not compile is a script
/// somebody has to remember they were writing.
/// </param>
/// <param name="Output">What the compiler last said about it.</param>
/// <param name="Revision">
/// How many times it has been built. Every build goes to a library of its own,
/// so this is also part of the file name.
/// </param>
public sealed record ScriptStatus(string Name, FileInfo Source, bool Running, string Output, int Revision = 0);
