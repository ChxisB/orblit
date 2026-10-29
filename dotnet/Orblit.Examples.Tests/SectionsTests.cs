namespace Orblit.Examples.Tests;

public sealed class SectionsTests
{
    [Fact]
    public void TheGroupedListHoldsEveryExampleInItsOwnOrder()
    {
        var all = ExampleFixture.All();
        var grouped = Example.BySection(all);

        Assert.Equal(
            all.OrderBy(example => example.Section).Select(example => example.Name),
            grouped.SelectMany(group => group.Examples).Select(example => example.Name));
        Assert.All(grouped, group => Assert.All(group.Examples, example => Assert.Equal(group.Section, example.Section)));
    }

    [Fact]
    public void AHeadingWithNothingUnderItIsLeftOut()
    {
        var grouped = Example.BySection([new SurfaceExample(), new BounceExample()]);

        Assert.Equal([ExampleSection.Basics, ExampleSection.Lighting], grouped.Select(group => group.Section));
    }

    [Fact]
    public void EveryHeadingButScriptingHasAnEngineExample()
    {
        var sections = Example.BySection(ExampleFixture.All()).Select(group => group.Section);

        // The scripting examples live in the gallery, beside the runtime they
        // need, so a host with only this package has nothing to put there.
        Assert.Equal(Enum.GetValues<ExampleSection>().Where(section => section != ExampleSection.Scripting), sections);
    }

    [Fact]
    public void EveryHeadingHasALabel() =>
        Assert.All(Enum.GetValues<ExampleSection>(), section => Assert.NotEmpty(section.Label()));

    [Fact]
    public void AHeadingOutsideTheListIsRefused() =>
        Assert.Throws<System.Diagnostics.UnreachableException>(() => ((ExampleSection)99).Label());
}
