namespace Orblit.Test;

/// <summary>An expectation about a scene, a sound or a golden file did not hold.</summary>
public sealed class ExpectationException(string message) : Exception(message);
