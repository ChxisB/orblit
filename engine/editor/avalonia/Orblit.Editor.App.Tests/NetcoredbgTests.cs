namespace Orblit.Editor.App.Tests;

public sealed class NetcoredbgTests
{
    static readonly string s_a = Path.Join(Path.GetTempPath(), "a");
    static readonly string s_b = Path.Join(Path.GetTempPath(), "b");
    static readonly string s_path = s_a + Path.PathSeparator + s_b;

    static bool InB(string file) => file.StartsWith(s_b, StringComparison.Ordinal) && Path.GetFileNameWithoutExtension(file) == "netcoredbg";

    [Fact]
    public void The_setting_comes_before_the_path() =>
        Assert.Equal("/opt/dbg/netcoredbg", Netcoredbg.Find("/opt/dbg/netcoredbg", s_path, _ => true));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("/opt/missing/netcoredbg")]
    public void Without_a_setting_that_exists_the_first_folder_on_the_path_that_holds_it_wins(string? setting)
    {
        var found = Netcoredbg.Find(setting, s_path, InB);

        Assert.NotNull(found);
        Assert.True(InB(found));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void With_no_setting_and_no_path_there_is_none(string? path) =>
        Assert.Null(Netcoredbg.Find(null, path, _ => true));

    [Fact]
    public void A_path_without_it_finds_none() => Assert.Null(Netcoredbg.Find(null, s_path, _ => false));
}
