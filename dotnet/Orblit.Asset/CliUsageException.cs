namespace Orblit.Asset;

/// <summary>The arguments of a command line did not make sense. The command prints its usage and exits 2.</summary>
internal sealed class CliUsageException(string message) : Exception(message);
