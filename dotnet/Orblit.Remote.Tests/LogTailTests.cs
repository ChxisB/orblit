namespace Orblit.Remote.Tests;

public class LogTailTests
{
    [Fact]
    public void TailAnswersTheLastLinesInTheOrderTheyCame()
    {
        var log = new LogTail();
        log.Add("a");
        log.Add("b");
        log.Add("c");

        Assert.Equal(["b", "c"], log.Tail(2));
        Assert.Equal(["a", "b", "c"], log.Tail(10));
        Assert.Empty(log.Tail(0));
    }

    [Fact]
    public void TheOldestLineGoesWhenTheRingIsFull()
    {
        var log = new LogTail(capacity: 2);
        log.Add("a");
        log.Add("b");
        log.Add("c");

        Assert.Equal(["b", "c"], log.Tail(5));
    }

    [Fact]
    public void AnEmptyRingOrANegativeCountIsRefused()
    {
        Assert.ThrowsAny<ArgumentException>(() => new LogTail(capacity: 0));
        Assert.ThrowsAny<ArgumentException>(() => new LogTail().Tail(-1));
    }
}
