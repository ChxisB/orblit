namespace Orblit.Examples.Tests;

/// <summary>Finds a control by its label, as a reader of the panel would.</summary>
internal static class ExampleControls
{
    public static T Control<T>(this Example example, string label)
        where T : ExampleSetting =>
        example.Settings().OfType<T>().Single(control => control.Label == label);
}
