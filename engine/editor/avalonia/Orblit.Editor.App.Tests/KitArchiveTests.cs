using System.IO.Compression;

namespace Orblit.Editor.App.Tests;

public sealed class KitArchiveTests : IDisposable
{
    readonly string _folder = Path.Combine(Path.GetTempPath(), $"orblit_kit_{Guid.NewGuid():N}");

    public KitArchiveTests() => Directory.CreateDirectory(Path.Combine(_folder, "project"));

    public void Dispose() => Directory.Delete(_folder, true);

    string Project => Path.Combine(_folder, "project");

    // A zip of these entries, each holding its own name as text. A name ending in a slash is a folder entry.
    string Zip(params string[] names)
    {
        var file = Path.Combine(_folder, "kit.zip");
        using var zip = ZipFile.Open(file, ZipArchiveMode.Create);
        foreach (var name in names)
        {
            var entry = zip.CreateEntry(name);
            if (!name.EndsWith('/'))
            {
                using var writer = new StreamWriter(entry.Open());
                writer.Write(name);
            }
        }
        return file;
    }

    string[] Written() => [.. Directory.GetFiles(Project, "*", SearchOption.AllDirectories).Select(file => Path.GetRelativePath(Project, file).Replace('\\', '/')).Order()];

    [Fact]
    public void Each_file_lands_at_its_path_in_the_project()
    {
        KitArchive.Install(Zip("player.oscn", "art/hero.png"), Project);

        Assert.Equal(["art/hero.png", "player.oscn"], Written());
        Assert.Equal("art/hero.png", File.ReadAllText(Path.Combine(Project, "art", "hero.png")));
    }

    [Fact]
    public void A_folder_every_entry_shares_is_taken_off()
    {
        KitArchive.Install(Zip("platformer/", "platformer/player.oscn", "platformer/art/hero.png"), Project);

        Assert.Equal(["art/hero.png", "player.oscn"], Written());
    }

    [Fact]
    public void Two_top_folders_both_stay()
    {
        KitArchive.Install(Zip("art/hero.png", "levels/one.oscn"), Project);

        Assert.Equal(["art/hero.png", "levels/one.oscn"], Written());
    }

    [Fact]
    public void An_entry_that_climbs_out_of_the_project_stops_the_install_before_any_file_is_written()
    {
        var zip = Zip("player.oscn", "../outside.txt");

        var error = Assert.Throws<InvalidDataException>(() => KitArchive.Install(zip, Project));

        Assert.Equal("../outside.txt is outside the project.", error.Message);
        Assert.Empty(Written());
        Assert.False(File.Exists(Path.Combine(_folder, "outside.txt")));
    }

    [Fact]
    public void An_entry_with_a_full_path_stops_the_install()
    {
        var error = Assert.Throws<InvalidDataException>(() => KitArchive.Install(Zip("/etc/kit.txt"), Project));

        Assert.Equal("/etc/kit.txt is outside the project.", error.Message);
    }

    [Fact]
    public void A_file_the_project_has_already_stops_the_install_and_keeps_its_text()
    {
        File.WriteAllText(Path.Combine(Project, "project.orblit"), "mine");
        var zip = Zip("art/hero.png", "project.orblit");

        var error = Assert.Throws<IOException>(() => KitArchive.Install(zip, Project));

        Assert.Equal("project.orblit is in the project already.", error.Message);
        Assert.Equal(["project.orblit"], Written());
        Assert.Equal("mine", File.ReadAllText(Path.Combine(Project, "project.orblit")));
    }

    [Fact]
    public void A_zip_of_only_folders_has_nothing_to_install()
    {
        var error = Assert.Throws<InvalidDataException>(() => KitArchive.Install(Zip("empty/"), Project));

        Assert.Equal("The kit holds no files.", error.Message);
    }

    [Fact]
    public void A_file_that_is_not_a_zip_is_refused()
    {
        var file = Path.Combine(_folder, "kit.zip");
        File.WriteAllText(file, "not a zip");

        Assert.Throws<InvalidDataException>(() => KitArchive.Install(file, Project));
        Assert.Empty(Written());
    }
}
