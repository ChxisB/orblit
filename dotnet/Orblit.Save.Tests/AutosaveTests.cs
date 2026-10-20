namespace Orblit.Save.Tests;

public sealed class AutosaveTests
{
    private static AutoSaver Make(int perTick = 10, int every = 100, int jitter = 0) =>
        new(perTick, every, jitter, new Random(7));

    // claim

    [Fact]
    public void With1000KeysNoTickClaimsMoreThanPerTick()
    {
        var auto = Make(perTick: 10, every: 200, jitter: 20);
        var keys = Enumerable.Range(0, 1000).Select(i => $"p{i}").ToList();
        var lastSeen = new Dictionary<string, long>();
        var most = 0;

        for (var tick = 0; tick < 2000; tick++)
        {
            var picked = auto.Claim(keys, tick);
            most = Math.Max(most, picked.Count);
            foreach (var key in picked)
            {
                if (lastSeen.TryGetValue(key, out var before))
                {
                    Assert.True(tick - before >= 200, key);
                }

                lastSeen[key] = tick;
            }
        }

        Assert.True(most <= 10);
        Assert.True(most == 10, "a busy tick fills its quota");
        Assert.True(lastSeen.Count == 1000, "every key was saved");
    }

    [Fact]
    public void KeysThatJoinTogetherDoNotComeDueTogether()
    {
        var auto = Make(perTick: 1000, every: 100);
        var keys = Enumerable.Range(0, 500).Select(i => $"p{i}").ToList();
        var perTick = new List<int>();

        for (var tick = 0; tick <= 100; tick++)
        {
            perTick.Add(auto.Claim(keys, tick).Count);
        }

        Assert.True(perTick.Max() < 50);
        Assert.Equal(500, perTick.Sum());
    }

    [Fact]
    public void AKeyIsClaimedAgainNoSoonerThanEveryAndByEveryPlusJitter()
    {
        var auto = Make(perTick: 1000, every: 30, jitter: 10);
        var at = new List<int>();

        for (var tick = 0; tick < 400; tick++)
        {
            if (auto.Claim(["ana"], tick).Count > 0)
            {
                at.Add(tick);
            }
        }

        Assert.True(at.Count > 5);
        var gaps = Enumerable.Range(1, at.Count - 1).Select(i => at[i] - at[i - 1]).ToList();
        foreach (var gap in gaps)
        {
            Assert.InRange(gap, 30, 40);
        }

        Assert.True(gaps.Any(gap => gap > 30), "jitter is used");
    }

    [Fact]
    public void TheLongestOverdueGoesFirstWhenTheQuotaIsShort()
    {
        var auto = Make(perTick: 1, every: 1);
        var first = auto.Claim(["a", "b", "c"], 0);
        Assert.True(first.Count == 0, "new keys wait their first turn");

        var order = new List<string>();
        for (var tick = 1; tick < 4; tick++)
        {
            order.AddRange(auto.Claim(["a", "b", "c"], tick));
        }

        Assert.Equal(["a", "b", "c"], order);
    }

    [Fact]
    public void AKeyThatLeavesTheListIsForgottenAndStartsAgain()
    {
        var auto = Make(perTick: 100, every: 50);
        var tick = 0;
        for (; tick < 60; tick++)
        {
            auto.Claim(["ana"], tick);
        }

        for (; tick < 70; tick++)
        {
            auto.Claim(["bo"], tick);
        }

        var claimedAt = -1;
        for (; tick < 200 && claimedAt < 0; tick++)
        {
            if (auto.Claim(["ana"], tick).Count > 0)
            {
                claimedAt = tick;
            }
        }

        Assert.True(claimedAt > 70);
    }

    [Fact]
    public void AKeyThatReturnsIsNotDueWhenItLeft()
    {
        var auto = Make(perTick: 100, every: 20);
        var tick = 0;
        for (; tick <= 30; tick++)
        {
            auto.Claim(["ana"], tick);
        }

        auto.Claim(["bo"], tick++);
        for (; tick < 41; tick++)
        {
            auto.Claim(["bo"], tick);
        }

        Assert.True(auto.Claim(["ana"], tick).Count == 0, "it starts again");
    }

    [Fact]
    public void ADuplicateInTheListCountsOnce()
    {
        var auto = Make(perTick: 100, every: 1);
        auto.Claim(["a", "a"], 0);

        Assert.Equal(["a"], auto.Claim(["a", "a"], 1));
    }

    [Fact]
    public void AQuotaIntervalOrJitterOutOfRangeIsRefused()
    {
        Assert.ThrowsAny<ArgumentException>(() => Make(perTick: 0));
        Assert.ThrowsAny<ArgumentException>(() => Make(every: 0));
        Assert.ThrowsAny<ArgumentException>(() => Make(jitter: -1));
    }
}
