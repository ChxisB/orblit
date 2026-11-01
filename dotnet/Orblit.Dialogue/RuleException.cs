namespace Orblit.Dialogue;

/// <summary>
/// A rule broken by a value, with the name, the rule and the value in the message.
/// <see cref="DialogueDefinition.Parse"/> hands <see cref="Exception.Message"/> to the file's reader.
/// </summary>
internal sealed class RuleException(string name, string rule, object? value)
    : ArgumentException($"{name} {rule} ({value}).");
