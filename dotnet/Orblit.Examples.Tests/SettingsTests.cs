using System.Text.Json;

namespace Orblit.Examples.Tests;

public sealed class SettingsTests
{
    public static TheoryData<string, int> Controls()
    {
        var controls = new TheoryData<string, int>();
        foreach (var example in EngineExamples.All())
        {
            for (var i = 0; i < example.Settings().Count; i++)
            {
                controls.Add(example.Name, i);
            }
        }

        return controls;
    }

    [Theory]
    [MemberData(nameof(Controls))]
    public void EachControlChangesTheSceneAndShowsItsNewValue(string name, int index)
    {
        var example = EngineExamples.All().Single(one => one.Name == name);
        var before = Message(example);

        var moved = Move(example.Settings()[index]);

        Assert.NotEqual(before, Message(example));
        Assert.Equal(moved, Shown(example.Settings()[index]));
    }

    /// <summary>The scene a second in, when a spin has had time to show.</summary>
    private static string Message(Example example) =>
        JsonSerializer.Serialize(example.Scene(example.ViewPoint.ToRenderCamera(), 1).ToMessage(1));

    /// <summary>Moves a control away from where it stands, and gives where it went.</summary>
    private static object Move(ExampleSetting setting)
    {
        switch (setting)
        {
            case Setting slider:
                var to = slider.Value == slider.Max ? slider.Min : slider.Max;
                slider.Change(to);
                return to;
            case Toggle toggle:
                toggle.Change(!toggle.Value);
                return !toggle.Value;
            case Choice choice:
                var other = choice.Options.First(option => option != choice.Selected);
                Assert.NotNull(choice.Select);
                choice.Select(other);
                return other;
            default:
                throw new InvalidOperationException($"No way to move {setting.GetType()}.");
        }
    }

    private static object Shown(ExampleSetting setting) => setting switch
    {
        Setting slider => slider.Value,
        Toggle toggle => toggle.Value,
        Choice choice => choice.Selected,
        _ => throw new InvalidOperationException($"Nothing shown for {setting.GetType()}."),
    };
}
