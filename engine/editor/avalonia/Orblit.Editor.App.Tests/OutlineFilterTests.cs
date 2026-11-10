using Orblit.Editor.Bridge;

namespace Orblit.Editor.App.Tests;

public sealed class OutlineFilterTests
{
    static readonly OutlineNode s_deep = new(4, "Bulb", "Entity3D", []);
    static readonly OutlineNode s_lamp = new(1, "Lamp", "Entity3D", [s_deep]);
    static readonly OutlineNode s_sound = new(2, "Sound", "Audio", []);
    static readonly OutlineNode s_field = new(3, "Field", "Entity3D", [s_lamp, s_sound]);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_text_returns_the_same_tree(string text)
    {
        Assert.Same(s_field, OutlineFilter.Apply(s_field, text));
    }

    [Fact]
    public void No_tree_stays_no_tree()
    {
        Assert.Null(OutlineFilter.Apply(null, "lamp"));
    }

    [Fact]
    public void A_match_keeps_the_entities_above_it_and_drops_the_other_branches()
    {
        var kept = OutlineFilter.Apply(s_field, "bulb")!;

        Assert.Equal("Field", kept.Name);
        var lamp = Assert.Single(kept.Children);
        Assert.Equal("Lamp", lamp.Name);
        Assert.Equal(["Bulb"], lamp.Children.Select(child => child.Name));
    }

    [Fact]
    public void A_matching_entity_shows_without_the_children_that_do_not_match()
    {
        var kept = OutlineFilter.Apply(s_field, "lamp")!;

        var lamp = Assert.Single(kept.Children);
        Assert.Empty(lamp.Children);
    }

    [Fact]
    public void The_match_ignores_case_and_the_spaces_around_the_text()
    {
        Assert.Equal(["Sound"], OutlineFilter.Apply(s_field, "  SOUND ")!.Children.Select(child => child.Name));
    }

    [Fact]
    public void The_match_is_on_the_name_and_not_the_class()
    {
        Assert.Null(OutlineFilter.Apply(s_field, "Audio"));
    }

    [Fact]
    public void A_text_nothing_matches_gives_no_tree()
    {
        Assert.Null(OutlineFilter.Apply(s_field, "zzz"));
    }

    [Fact]
    public void The_kept_entities_keep_their_handles_and_the_source_tree_is_unchanged()
    {
        var kept = OutlineFilter.Apply(s_field, "bulb")!;

        Assert.Equal(3, kept.Handle);
        Assert.Equal(4, kept.Find(4)!.Handle);
        Assert.Equal(2, s_field.Children.Count);
        Assert.Single(s_lamp.Children);
    }
}
