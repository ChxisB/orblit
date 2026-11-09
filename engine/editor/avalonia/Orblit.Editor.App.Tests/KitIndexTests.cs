namespace Orblit.Editor.App.Tests;

public sealed class KitIndexTests
{
    [Fact]
    public void Rows_come_back_in_the_order_the_file_lists_them()
    {
        var kits = KitIndex.Parse("""
            {"formatVersion":1,"kits":[
              {"name":"Platformer","publisher":"ada","slug":"platformer","version":"1.2.0","summary":"A side scroller."},
              {"name":"Racing","publisher":"bo","slug":"racing","version":"0.1.0"}]}
            """);

        Assert.Equal([new KitEntry("Platformer", "ada", "platformer", "1.2.0", "A side scroller."), new KitEntry("Racing", "bo", "racing", "0.1.0", "")], kits);
    }

    [Fact]
    public void An_index_with_no_kits_is_an_empty_list()
    {
        Assert.Empty(KitIndex.Parse("""{"formatVersion":1,"kits":[]}"""));
    }

    [Fact]
    public void Keys_the_reader_does_not_know_are_ignored()
    {
        var kit = Assert.Single(KitIndex.Parse("""
            {"formatVersion":1,"generated":"today","kits":[{"name":"A","publisher":"p","slug":"a","version":"1","scripts":true}]}
            """));

        Assert.Equal("A", kit.Name);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("""{"formatVersion":1}""")]
    [InlineData("""{"formatVersion":1,"kits":{}}""")]
    [InlineData("""{"kits":[]}""")]
    [InlineData("""{"formatVersion":2,"kits":[]}""")]
    [InlineData("""{"formatVersion":0,"kits":[]}""")]
    [InlineData("""{"formatVersion":"1","kits":[]}""")]
    [InlineData("""{"formatVersion":1,"kits":[1]}""")]
    [InlineData("""{"formatVersion":1,"kits":[{"publisher":"p","slug":"a","version":"1"}]}""")]
    [InlineData("""{"formatVersion":1,"kits":[{"name":"A","slug":"a","version":"1"}]}""")]
    [InlineData("""{"formatVersion":1,"kits":[{"name":"A","publisher":"p","slug":"","version":"1"}]}""")]
    [InlineData("""{"formatVersion":1,"kits":[{"name":"A","publisher":"p","slug":"a","version":1}]}""")]
    public void A_file_the_reader_cannot_use_is_refused(string json)
    {
        Assert.Throws<FormatException>(() => KitIndex.Parse(json));
    }
}
