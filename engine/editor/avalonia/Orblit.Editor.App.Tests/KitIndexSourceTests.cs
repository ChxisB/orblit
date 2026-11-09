using System.Net;
using System.Text;

namespace Orblit.Editor.App.Tests;

public sealed class KitIndexSourceTests
{
    sealed class Stub(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
    {
        public List<Uri> Asked { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancel)
        {
            Asked.Add(request.RequestUri!);
            return Task.FromResult(answer(request));
        }
    }

    // A body whose bytes never come, as a server does that stops after its headers.
    sealed class Stalled : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancel = default)
        {
            await Task.Delay(Timeout.Infinite, cancel);
            return 0;
        }
    }

    static HttpResponseMessage Ok(byte[] body) => new(HttpStatusCode.OK) { Content = new ByteArrayContent(body) };

    [Fact]
    public async Task The_text_is_what_the_store_answers_with_and_the_request_goes_to_the_stores_address()
    {
        var stub = new Stub(_ => Ok(Encoding.UTF8.GetBytes("{\"formatVersion\":1,\"kits\":[]}")));

        var text = await new KitIndexSource(stub).ReadAsync(CancellationToken.None);

        Assert.Equal("{\"formatVersion\":1,\"kits\":[]}", text);
        Assert.Equal(new Uri(KitIndexSource.Address), Assert.Single(stub.Asked));
        Assert.StartsWith("https://", KitIndexSource.Address);
    }

    [Fact]
    public async Task A_non_success_answer_is_an_io_error()
    {
        var source = new KitIndexSource(new Stub(_ => new HttpResponseMessage(HttpStatusCode.NotFound)));

        await Assert.ThrowsAsync<IOException>(() => source.ReadAsync(CancellationToken.None));
    }

    [Fact]
    public async Task An_answer_one_byte_over_the_cap_is_refused_and_one_at_the_cap_is_read()
    {
        var atCap = new KitIndexSource(new Stub(_ => Ok(new byte[KitIndexSource.MaxBytes])));
        var over = new KitIndexSource(new Stub(_ => Ok(new byte[KitIndexSource.MaxBytes + 1])));

        Assert.Equal(KitIndexSource.MaxBytes, (await atCap.ReadAsync(CancellationToken.None)).Length);
        await Assert.ThrowsAsync<IOException>(() => over.ReadAsync(CancellationToken.None));
    }

    [Fact]
    public async Task A_body_that_stalls_is_an_io_error_when_the_time_is_up()
    {
        var stalled = new Stub(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new Stalled()) });
        var source = new KitIndexSource(stalled, TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAsync<IOException>(() => source.ReadAsync(CancellationToken.None)).WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task A_cancelled_read_is_an_io_error()
    {
        using var cancel = new CancellationTokenSource();
        await cancel.CancelAsync();

        await Assert.ThrowsAsync<IOException>(() => new KitIndexSource(new Stub(_ => Ok([]))).ReadAsync(cancel.Token));
    }
}
