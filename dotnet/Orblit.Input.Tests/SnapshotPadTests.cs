namespace Orblit.Input.Tests;

public class SnapshotPadTests
{
    private const int South = 0x130;
    private const int AbsX = 0x00;

    private readonly HashSet<int> _held = [];
    private readonly Dictionary<int, int> _axes = [];
    private readonly SnapshotPad _pad;
    private bool _alive = true;

    public SnapshotPadTests() => _pad = new SnapshotPad(
        identity: new PadIdentity("Fake"),
        read: () => new PadSnapshot(_held.ToList(), new Dictionary<int, int>(_axes)),
        alive: () => _alive);

    [Fact]
    public void ReportsWhatChangedSinceTheLastDrainThenAReport()
    {
        _held.Add(South);
        _axes[AbsX] = 100;

        Assert.Equal(
            [new EvdevEvent(Evdev.Key, South, 1), new EvdevEvent(Evdev.Abs, AbsX, 100), new EvdevEvent(Evdev.Syn, Evdev.SynReport, 0)],
            Drained());
        Assert.Empty(Drained());

        _held.Clear();
        _axes[AbsX] = 100;
        Assert.Equal(
            [new EvdevEvent(Evdev.Key, South, 0), new EvdevEvent(Evdev.Syn, Evdev.SynReport, 0)],
            Drained());
    }

    [Fact]
    public void AskingWhereItStandsDoesNotChangeWhatTheNextDrainSays()
    {
        _held.Add(South);

        Assert.Equal([South], _pad.Resync().Buttons);
        Assert.Equal(new EvdevEvent(Evdev.Key, South, 1), Drained()[0]);
    }

    [Fact]
    public void APadThatHasGoneSaysNothing()
    {
        _held.Add(South);
        _alive = false;

        Assert.Empty(_pad.Drain());
        Assert.Empty(_pad.Resync().Buttons);
    }

    // What a snapshot pad writes, decoded again, so the test reads events rather than bytes.
    private IReadOnlyList<EvdevEvent> Drained() => new EvdevDecoder().Decode(_pad.Drain());
}
