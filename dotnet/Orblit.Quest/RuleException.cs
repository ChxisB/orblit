namespace Orblit.Quest;

/// <summary>
/// A rule broken by a value: Dart's <c>ArgumentError.value(value, name, rule)</c>.
/// <see cref="QuestDefinition.Parse"/> hands <see cref="Exception.Message"/> to the file's reader.
/// </summary>
internal sealed class RuleException(string name, string rule, object? value)
    : ArgumentException($"{name} {rule} ({value}).");
