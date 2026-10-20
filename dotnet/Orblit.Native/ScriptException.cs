namespace Orblit.Native;

/// <summary>Why a script would not load.</summary>
public sealed class ScriptException(string message) : Exception(message);
