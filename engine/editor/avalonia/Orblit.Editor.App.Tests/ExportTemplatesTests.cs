using System.IO.Compression;

namespace Orblit.Editor.App.Tests;

public sealed class ExportTemplatesTests : IDisposable
{
    readonly string _folder = Path.Combine(Path.GetTempPath(), $"orblit_templates_{Guid.NewGuid():N}");

    public ExportTemplatesTests() => Directory.CreateDirectory(Path.Combine(_folder, "data"));

    public void Dispose() => Directory.Delete(_folder, true);

    string Data => Path.Combine(_folder, "data");

    string Templates => Path.Combine(Data, "export_templates");

    // A .tpz of these entries. Each holds its own name as text, and version.txt holds the version given.
    string Tpz(string version, params string[] names)
    {
        var file = Path.Combine(_folder, "templates.tpz");
        File.Delete(file);
        using var zip = ZipFile.Open(file, ZipArchiveMode.Create);
        foreach (var name in names)
        {
            using var writer = new StreamWriter(zip.CreateEntry(name).Open());
            writer.Write(name == "templates/version.txt" ? version : name);
        }
        return file;
    }

    string[] Written() => Directory.Exists(Templates)
        ? [.. Directory.GetFiles(Templates, "*", SearchOption.AllDirectories).Select(file => Path.GetRelativePath(Templates, file).Replace('\\', '/')).Order()]
        : [];

    [Fact]
    public void The_templates_land_in_a_folder_named_by_their_version()
    {
        ExportTemplates.Install(Tpz("1.0.stable.csharp\n", "templates/version.txt", "templates/macos.zip", "templates/linux_release.x86_64"), Data);

        Assert.Equal(["1.0.stable.csharp/linux_release.x86_64", "1.0.stable.csharp/macos.zip", "1.0.stable.csharp/version.txt"], Written());
        Assert.Equal("templates/macos.zip", File.ReadAllText(Path.Combine(Templates, "1.0.stable.csharp", "macos.zip")));
    }

    [Fact]
    public void Installing_a_version_again_replaces_what_its_folder_held()
    {
        ExportTemplates.Install(Tpz("1.0.stable", "templates/version.txt", "templates/old.zip"), Data);

        ExportTemplates.Install(Tpz("1.0.stable", "templates/version.txt", "templates/macos.zip"), Data);

        Assert.Equal(["1.0.stable/macos.zip", "1.0.stable/version.txt"], Written());
    }

    [Fact]
    public void A_file_with_no_version_is_refused()
    {
        var error = Assert.Throws<InvalidDataException>(() => ExportTemplates.Install(Tpz("", "templates/macos.zip"), Data));

        Assert.Equal("The file holds no templates/version.txt with a version such as 1.0.stable.", error.Message);
        Assert.Empty(Written());
    }

    [Theory]
    [InlineData("1.0")]
    [InlineData("1.0.stable/../../outside")]
    [InlineData("1..stable")]
    public void A_version_that_is_not_one_plain_folder_name_is_refused(string version)
    {
        Assert.Throws<InvalidDataException>(() => ExportTemplates.Install(Tpz(version, "templates/version.txt", "templates/macos.zip"), Data));

        Assert.Empty(Written());
        Assert.Equal(["data", "templates.tpz"], Directory.GetFileSystemEntries(_folder).Select(Path.GetFileName).Order());
    }

    [Fact]
    public void An_entry_that_climbs_out_of_its_folder_stops_the_install_before_any_file_is_written()
    {
        ExportTemplates.Install(Tpz("1.0.stable", "templates/version.txt", "templates/kept.zip"), Data);

        var tpz = Tpz("1.0.stable", "templates/version.txt", "templates/macos.zip", "templates/../../outside.txt");
        var error = Assert.Throws<InvalidDataException>(() => ExportTemplates.Install(tpz, Data));

        Assert.Equal("templates/../../outside.txt is outside the templates folder.", error.Message);
        Assert.Equal(["1.0.stable/kept.zip", "1.0.stable/version.txt"], Written());
        Assert.False(File.Exists(Path.Combine(Data, "outside.txt")));
    }

    [Fact]
    public void A_file_that_is_not_a_zip_is_refused()
    {
        var file = Path.Combine(_folder, "templates.tpz");
        File.WriteAllText(file, "not a zip");

        Assert.Throws<InvalidDataException>(() => ExportTemplates.Install(file, Data));
        Assert.Empty(Written());
    }
}
