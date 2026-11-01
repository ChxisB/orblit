using System.Diagnostics;

namespace Orblit.Examples;

/// <summary>
/// The headings examples are grouped under, in the order somebody should meet
/// them: something drawn, then what lights it, what it is made of, what the air
/// does to it, and the big scenes last.
/// </summary>
public enum ExampleSection
{
    Basics,
    Lighting,
    Materials,
    Atmosphere,
    Effects,
    Content,
    Interface,
    Performance,
    Showcases,
}

public static class ExampleSectionExtensions
{
    /// <summary>What the heading says.</summary>
    public static string Label(this ExampleSection section) => section switch
    {
        ExampleSection.Basics => "Getting started",
        ExampleSection.Lighting => "Lighting & shadows",
        ExampleSection.Materials => "Materials & textures",
        ExampleSection.Atmosphere => "Sky & atmosphere",
        ExampleSection.Effects => "Effects",
        ExampleSection.Content => "Models & media",
        ExampleSection.Interface => "Interface",
        ExampleSection.Performance => "Performance",
        ExampleSection.Showcases => "Showcases",
        _ => throw new UnreachableException(),
    };
}
