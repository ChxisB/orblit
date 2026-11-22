using System.Net;
using System.Text;

namespace Orblit.Editor.App.Tests;

public sealed class TemplateMirrorTests
{
    const string Tpz = "https://github.com/ChxisB/orblit/releases/download/v1.1.0/orblit-1.1.0-export-templates.tpz";

    sealed class Stub(Func<Uri, HttpResponseMessage> answer) : HttpMessageHandler
    {
        public List<Uri> Asked { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancel)
        {
            Asked.Add(request.RequestUri!);
            return Task.FromResult(answer(request.RequestUri!));
        }
    }

    // A body that sends some bytes and then loses the connection.
    sealed class Broken : MemoryStream
    {
        public Broken() : base(Encoding.UTF8.GetBytes("part")) { }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancel = default) =>
            Position < Length ? await base.ReadAsync(buffer, cancel) : throw new HttpRequestException("The connection was reset.");
    }

    static HttpResponseMessage Ok(string body) => new(HttpStatusCode.OK) { Content = new ByteArrayContent(Encoding.UTF8.GetBytes(body)) };

    static string List(params string[] urls) => "{\"mirrors\":[" + string.Join(',', urls.Select(url => $"{{\"name\":\"m\",\"url\":\"{url}\"}}")) + "]}";

    [Theory]
    [InlineData("1.1.stable.csharp.official.0a1b2c3d4", "1.1.stable.csharp")]
    [InlineData("1.0.stable.custom_build.f231995c1", "1.0.stable")]
    [InlineData("1.2.3.beta.csharp.double.official\n", "1.2.3.beta.csharp.double")]
    [InlineData("1.1.stable.official", "1.1.stable")]
    public void The_full_version_is_the_number_the_status_and_the_module_parts(string printed, string full) =>
        Assert.Equal(full, TemplateMirror.FullVersion(printed));

    [Theory]
    [InlineData("")]
    [InlineData("stable")]
    [InlineData("1.1")]
    public void A_printed_version_with_no_number_or_status_is_an_io_error(string printed) =>
        Assert.Throws<IOException>(() => TemplateMirror.FullVersion(printed));

    [Fact]
    public async Task The_first_https_mirror_in_the_versions_list_is_downloaded_to_a_file()
    {
        var stub = new Stub(uri => uri.AbsoluteUri == Tpz ? Ok("templates") : Ok(List("http://plain.example/t.tpz", Tpz)));

        var file = await new TemplateMirror(stub).DownloadAsync("1.1.stable.csharp", CancellationToken.None);
        try
        {
            Assert.Equal("templates", File.ReadAllText(file));
            Assert.Equal([new Uri(TemplateMirror.Lists + "1.1.stable.csharp.json"), new Uri(Tpz)], stub.Asked);
            Assert.StartsWith("https://", TemplateMirror.Lists);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task A_version_with_no_list_says_none_are_published()
    {
        var mirror = new TemplateMirror(new Stub(_ => new HttpResponseMessage(HttpStatusCode.NotFound)));

        var error = await Assert.ThrowsAsync<IOException>(() => mirror.DownloadAsync("1.0.stable", CancellationToken.None));

        Assert.Equal("No export templates are published for 1.0.stable. Install a .tpz from a release instead.", error.Message);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("{\"mirrors\":[{\"url\":7}]}")]
    [InlineData("{\"mirrors\":[{\"url\":\"http://plain.example/t.tpz\"}]}")]
    [InlineData("{\"mirrors\":[]}")]
    public async Task A_list_with_no_https_mirror_is_an_io_error(string list)
    {
        var mirror = new TemplateMirror(new Stub(_ => Ok(list)));

        await Assert.ThrowsAsync<IOException>(() => mirror.DownloadAsync("1.1.stable", CancellationToken.None));
    }

    [Fact]
    public async Task A_list_past_the_limit_is_refused()
    {
        var mirror = new TemplateMirror(new Stub(_ => Ok(new string(' ', TemplateMirror.MaxListBytes + 1))));

        await Assert.ThrowsAsync<IOException>(() => mirror.DownloadAsync("1.1.stable", CancellationToken.None));
    }

    [Fact]
    public async Task A_failed_download_is_an_io_error_and_leaves_no_file()
    {
        var before = Directory.GetFiles(Path.GetTempPath(), "orblit-9.9.stable-*.tpz").Length;
        var mirror = new TemplateMirror(new Stub(uri => uri.AbsoluteUri == Tpz ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new Broken()) } : Ok(List(Tpz))));

        await Assert.ThrowsAsync<IOException>(() => mirror.DownloadAsync("9.9.stable", CancellationToken.None));

        Assert.Equal(before, Directory.GetFiles(Path.GetTempPath(), "orblit-9.9.stable-*.tpz").Length);
    }
}
