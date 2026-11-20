namespace Orblit.Editor.App.Tests;

/// <summary>A clock that moves only when a test moves it. A timer that falls due fires inside <see cref="Advance"/>.</summary>
internal sealed class ManualTime : TimeProvider
{
    /// <summary>The clock starts here, in a zone with no offset, so a test can name the time it shows.</summary>
    internal static readonly DateTimeOffset Start = new(2026, 11, 20, 16, 33, 0, TimeSpan.Zero);

    readonly List<ManualTimer> _timers = [];
    TimeSpan _now;

    public override DateTimeOffset GetUtcNow() => Start + _now;

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(this, () => callback(state));
        timer.Change(dueTime, period);
        _timers.Add(timer);
        return timer;
    }

    /// <summary>The timers that will still fire.</summary>
    public int Armed => _timers.Count(timer => timer.Due != TimeSpan.MaxValue);

    // shortcut: a timer fires once and ignores its period, a repeating timer needs a test of its own first.
    public void Advance(TimeSpan by)
    {
        _now += by;
        foreach (var timer in _timers.Where(timer => timer.Due <= _now).ToArray())
        {
            timer.Dispose();
            timer.Fire();
        }
    }

    sealed class ManualTimer(ManualTime clock, Action fire) : ITimer
    {
        internal TimeSpan Due { get; private set; } = TimeSpan.MaxValue;

        internal void Fire() => fire();

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            Due = dueTime == Timeout.InfiniteTimeSpan ? TimeSpan.MaxValue : clock._now + dueTime;
            return true;
        }

        public void Dispose() => Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
