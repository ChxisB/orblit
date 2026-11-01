using System.Net;
using Avalonia;
using Avalonia.Media.Imaging;

namespace Orblit.Ui.Avalonia.Tests;

public class WebImagesTests
{
    private static byte[] Png()
    {
        using var target = new RenderTargetBitmap(new PixelSize(2, 2));
        using var bytes = new MemoryStream();
        target.Save(bytes, PngBitmapEncoderOptions.Default);
        return bytes.ToArray();
    }

    private static (WebImages Images, Fake Server) Web(Func<HttpResponseMessage> answer)
    {
        var server = new Fake(answer);
        return (new WebImages(new HttpClient(server)), server);
    }

    private static HttpResponseMessage Ok(byte[] body) => new(HttpStatusCode.OK) { Content = new ByteArrayContent(body) };

    [Fact]
    public Task APictureAtAnHttpsAddressIsDecoded() => Headless.Run(async () =>
    {
        var (images, server) = Web(() => Ok(Png()));
        var picture = Assert.IsType<Bitmap>(await images.LoadAsync("https://example.test/a.png"));

        Assert.Equal(new PixelSize(2, 2), picture.PixelSize);
        Assert.Equal(["https://example.test/a.png"], server.Asked.Select(address => address.ToString()));
    });

    [Fact]
    public Task AnythingButHttpIsNeverFetched() => Headless.Run(async () =>
    {
        var (images, server) = Web(() => Ok(Png()));

        Assert.Null(await images.LoadAsync("file:///etc/hosts"));
        Assert.Null(await images.LoadAsync("ftp://example.test/a.png"));
        Assert.Null(await images.LoadAsync("a.png"));
        Assert.Null(await images.LoadAsync(""));
        Assert.Empty(server.Asked);
    });

    [Fact]
    public Task AMissingPictureIsNullNotAnError() => Headless.Run(async () =>
    {
        var (images, _) = Web(() => new HttpResponseMessage(HttpStatusCode.NotFound));
        Assert.Null(await images.LoadAsync("https://example.test/gone.png"));
    });

    [Fact]
    public Task BytesThatAreNotAPictureAreNullNotAnError() => Headless.Run(async () =>
    {
        var (images, _) = Web(() => Ok([1, 2, 3, 4]));
        Assert.Null(await images.LoadAsync("https://example.test/junk.png"));
    });

    [Fact]
    public Task AServerThatIsNotThereIsNullNotAnError() => Headless.Run(async () =>
    {
        var (images, _) = Web(() => throw new HttpRequestException("no route"));
        Assert.Null(await images.LoadAsync("https://example.test/a.png"));
    });

    private sealed class Fake(Func<HttpResponseMessage> answer) : HttpMessageHandler
    {
        public List<Uri> Asked { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Asked.Add(request.RequestUri!);
            return Task.FromResult(answer());
        }
    }
}
