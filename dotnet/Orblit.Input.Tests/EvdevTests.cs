namespace Orblit.Input.Tests;

public class EvdevTests
{
    // A recorded burst: the south button going down, the left stick moving, and the report that ends
    // the group, as the kernel writes it on a 64-bit machine.
    private static byte[] Recorded() =>
    [
        .. Evdev.Encode(Evdev.Key, 0x130, 1),
        .. Evdev.Encode(Evdev.Abs, 0x00, 12345),
        .. Evdev.Encode(Evdev.Syn, Evdev.SynReport, 0),
    ];

    [Fact]
    public void AWholeBufferComesOutAsTheEventsThatWentIn()
    {
        var events = new EvdevDecoder().Decode(Recorded());
        Assert.Equal(
            [new EvdevEvent(Evdev.Key, 0x130, 1), new EvdevEvent(Evdev.Abs, 0x00, 12345), new EvdevEvent(Evdev.Syn, Evdev.SynReport, 0)],
            events);
    }

    [Fact]
    public void ARecordIs24BytesOnA64BitMachine()
    {
        Assert.Equal(24, new EvdevDecoder().RecordBytes);
        Assert.Equal(72, Recorded().Length);
    }

    [Fact]
    public void A32BitKernelWrites16() => Assert.Equal(16, new EvdevDecoder(timeBytes: 8).RecordBytes);

    [Fact]
    public void ANegativeValueSurvivesTheRoundTrip()
    {
        var events = new EvdevDecoder().Decode(Evdev.Encode(Evdev.Abs, 0x01, -32768));
        Assert.Equal(-32768, Assert.Single(events).Value);
    }

    [Fact]
    public void TheFragmentIsKeptAndFinishedByTheNextRead()
    {
        var bytes = Recorded();
        var decoder = new EvdevDecoder();

        var first = decoder.Decode(bytes.AsSpan(0, 30));
        Assert.Single(first);
        Assert.True(decoder.HasPartial);

        var rest = decoder.Decode(bytes.AsSpan(30));
        Assert.Equal(2, rest.Count);
        Assert.Equal(new EvdevEvent(Evdev.Syn, Evdev.SynReport, 0), rest[^1]);
        Assert.False(decoder.HasPartial);
    }

    [Fact]
    public void ARecordSplitOneByteAtATimeStillArrives()
    {
        var decoder = new EvdevDecoder();
        var events = new List<EvdevEvent>();
        foreach (var value in Recorded())
        {
            events.AddRange(decoder.Decode([value]));
        }

        Assert.Equal(3, events.Count);
    }

    [Fact]
    public void AResetThrowsTheFragmentAway()
    {
        var decoder = new EvdevDecoder();
        decoder.Decode(Recorded().AsSpan(0, 10));
        Assert.True(decoder.HasPartial);
        decoder.Reset();
        Assert.False(decoder.HasPartial);

        var events = decoder.Decode(Evdev.Encode(Evdev.Key, 0x131, 1));
        Assert.Equal(new EvdevEvent(Evdev.Key, 0x131, 1), Assert.Single(events));
    }

    [Fact]
    public void AnEmptyReadSaysNothingAndKeepsWhatItHad()
    {
        var decoder = new EvdevDecoder();
        decoder.Decode(Recorded().AsSpan(0, 10));
        Assert.Empty(decoder.Decode([]));
        Assert.True(decoder.HasPartial);
    }

    [Fact]
    public void WordsAreReadMostSignificantFirst()
    {
        var keys = EvdevCapabilities.ParseBitmap("3000000000000 0 0 0 0");
        Assert.Contains(0x130, keys);
        Assert.Contains(0x131, keys);
        Assert.DoesNotContain(48, keys);
    }

    [Fact]
    public void TheLastWordHoldsTheLowBits()
    {
        Assert.Contains(0, EvdevCapabilities.ParseBitmap("0 1"));
        Assert.Contains(64, EvdevCapabilities.ParseBitmap("1 0"));
    }

    [Fact]
    public void TheTopBitOfAWordIsNotReadAsANegativeNumber() =>
        Assert.Contains(63, EvdevCapabilities.ParseBitmap("8000000000000000"));

    [Fact]
    public void APadIsOneThatClaimsAGamepadButton()
    {
        var pad = EvdevCapabilities.Parse(ev: "b", key: "3000000000000 0 0 0 0", abs: "3");
        Assert.True(pad.IsGamepad);
        Assert.Contains(0, pad.Axes);
        Assert.Contains(1, pad.Axes);
    }

    [Fact]
    public void AKeyboardIsNotAPad()
    {
        var keyboard = EvdevCapabilities.Parse(ev: "120013", key: "10000000000 0 0 0", abs: string.Empty);
        Assert.False(keyboard.IsGamepad);
    }

    [Fact]
    public void AnUnreadableDeviceClaimsNothing()
    {
        Assert.False(EvdevCapabilities.None.IsGamepad);
        Assert.Empty(EvdevCapabilities.ParseBitmap(string.Empty));
        Assert.Empty(EvdevCapabilities.ParseBitmap("   "));
        Assert.Empty(EvdevCapabilities.ParseBitmap("zz"));
    }
}
