using Orblit.Json;

namespace Orblit.Asset;

internal static class DartArgument
{
    /// <summary>
    /// What <c>ArgumentError.value(value, name, rule)</c> reads in Dart: <c>Invalid argument (name): rule: value</c>,
    /// with a string value in quotes. The C# message also ends with <c>(Parameter 'name')</c>, so a test reads the
    /// start of it.
    /// </summary>
    // shortcut: a list or a map prints as its contents, where Dart prints Instance(length:n) of '_GrowableList'. Upgrade
    // when a message has to show one.
    internal static ArgumentException Value(object? value, string name, string rule) =>
        new($"Invalid argument ({name}): {rule}: {(value is string text ? DartJson.Encode(text) : DartText.Print(value))}", name);
}
