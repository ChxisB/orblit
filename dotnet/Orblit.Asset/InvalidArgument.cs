using Orblit.Json;

namespace Orblit.Asset;

internal static class InvalidArgument
{
    /// <summary>
    /// The message for a value that breaks a rule: <c>Invalid argument (name): rule: value</c>,
    /// with a string value in quotes. The C# message also ends with <c>(Parameter 'name')</c>, so a test reads the
    /// start of it.
    /// </summary>
    // shortcut: a list or a map prints as its contents. Upgrade
    // when a message has to show one.
    internal static ArgumentException Value(object? value, string name, string rule) =>
        new($"Invalid argument ({name}): {rule}: {(value is string text ? JsonText.Encode(text) : TextRules.Print(value))}", name);
}
