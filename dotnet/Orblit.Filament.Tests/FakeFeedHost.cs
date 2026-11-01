namespace Orblit.Filament.Tests;

/// <summary>A feed host that records what it is asked and answers from numbers the test sets.</summary>
internal sealed class FakeFeedHost : IFeedHost
{
    private long _next = 7;

    public bool IsSupported { get; init; } = true;

    /// <summary>The number every <see cref="Create"/> answers, where the test wants a fixed one.</summary>
    public long? Answer { get; init; }

    public Exception? CreateFails { get; init; }

    public int SubmitResult { get; init; }

    public List<(int Width, int Height)> Created { get; } = [];

    public List<long> Released { get; } = [];

    public List<(long Id, nint Buffer)> Submitted { get; } = [];

    public long Create(int width, int height)
    {
        Created.Add((width, height));
        return CreateFails is null ? Answer ?? _next++ : throw CreateFails;
    }

    public void Release(long id) => Released.Add(id);

    public int Submit(long id, nint pixelBuffer)
    {
        Submitted.Add((id, pixelBuffer));
        return SubmitResult;
    }
}
