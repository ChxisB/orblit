namespace Orblit.Input.Tests;

public class PadsTests
{
    private const int BtnSouth = 0x130;
    private const int BtnStart = 0x13b;
    private const int AbsX = 0x00;
    private const int AbsY = 0x01;
    private const int AbsZ = 0x02;
    private const int AbsHat0X = 0x10;
    private const int AbsHat0Y = 0x11;

    // F2: the Dart named parameters count as the options object.
    private static RecordedPad Pad(
        string path = "usb-0000:00:14.0-1/input0",
        string uniq = "",
        IEnumerable<int>? held = null,
        bool canRumble = false) => new(
            identity: new PadIdentity("Orblit Virtual Pad", vendor: 0x28de, product: 0x1205, uniq: uniq, path: path),
            capabilities: EvdevCapabilities.Parse(ev: "b", key: "3000000000000 0 0 0 0", abs: "30003"),
            ranges: new Dictionary<int, PadAxisRange>
            {
                [AbsX] = PadAxisRange.Stick,
                [AbsY] = PadAxisRange.Stick,
                [AbsZ] = PadAxisRange.Trigger,
                [AbsHat0X] = new(minimum: -1, maximum: 1),
                [AbsHat0Y] = new(minimum: -1, maximum: 1),
            },
            held: held,
            canRumble: canRumble);

    [Fact]
    public void AButtonHeldReadsAsDownOnEveryPoll()
    {
        var device = Pad();
        var pads = new Pads(new RecordedPads([device]));

        device.Button(BtnSouth, down: true);
        Assert.True(pads.Poll().First.IsDown(PadButton.South));
        Assert.True(pads.Poll().First.IsDown(PadButton.South));
    }

    [Fact]
    public void APressIsReportedForExactlyOnePoll()
    {
        var device = Pad();
        var pads = new Pads(new RecordedPads([device]));

        // Connected first. A pad is adopted when it is seen, taking what it already holds as its
        // starting point, so a press queued before the first poll is not an edge.
        pads.Poll();

        device.Button(BtnSouth, down: true);
        Assert.True(pads.Poll().First.WasPressed(PadButton.South));
        Assert.False(pads.Poll().First.WasPressed(PadButton.South));

        device.Button(BtnSouth, down: false);
        var let = pads.Poll().First;
        Assert.True(let.WasReleased(PadButton.South));
        Assert.False(let.IsDown(PadButton.South));
    }

    [Fact]
    public void AKeyRepeatIsNotANewPress()
    {
        var device = Pad();
        var pads = new Pads(new RecordedPads([device]));
        device.Button(BtnSouth, down: true);
        pads.Poll();

        device.Send(Evdev.Key, BtnSouth, 2);
        device.Send(Evdev.Syn, Evdev.SynReport, 0);
        var frame = pads.Poll();
        Assert.False(frame.First.WasPressed(PadButton.South));
        Assert.True(frame.First.IsDown(PadButton.South));
    }

    [Fact]
    public void ATapInsideOneFrameIsInvisibleToTheStateAndIsAnEvent()
    {
        var device = Pad();
        var pads = new Pads(new RecordedPads([device]));
        pads.Poll();

        device.Button(BtnSouth, down: true);
        device.Button(BtnSouth, down: false);

        var frame = pads.Poll();
        Assert.False(frame.First.IsDown(PadButton.South));
        Assert.Contains(PadButton.South, frame.Events.OfType<PadPressed>().Select(e => e.Button));
        Assert.Contains(PadButton.South, frame.Events.OfType<PadReleased>().Select(e => e.Button));
    }

    [Fact]
    public void EventsArriveInTheOrderTheyHappened()
    {
        var device = Pad();
        var pads = new Pads(new RecordedPads([device]));
        pads.Poll();

        device.Button(BtnSouth, down: true);
        device.Button(BtnStart, down: true);
        device.Button(BtnSouth, down: false);

        Assert.Equal(
            ["PadPressed(0, south)", "PadPressed(0, start)", "PadReleased(0, south)"],
            pads.Poll().Events.Select(e => e.ToString()));
    }

    [Fact]
    public void UpIsPositiveWhateverTheHardwareSays()
    {
        var device = Pad();
        var pads = new Pads(new RecordedPads([device]));
        device.Axis(AbsY, -32768);
        Assert.True(pads.Poll().First.Stick(PadStick.Left).Y > 0.9);
    }

    [Fact]
    public void ATriggerRunsNoughtToOneAndNeverNegative()
    {
        var device = Pad();
        var pads = new Pads(new RecordedPads([device]));
        device.Axis(AbsZ, 0);
        Assert.Equal(0, pads.Poll().First.Trigger(PadSide.Left));
        device.Axis(AbsZ, 255);
        Assert.Equal(1, pads.Poll().First.Trigger(PadSide.Left), 1e-6);
    }

    [Fact]
    public void ATriggerPastHalfWayIsAlsoAButton()
    {
        var device = Pad();
        var pads = new Pads(new RecordedPads([device]));
        device.Axis(AbsZ, 255);
        Assert.True(pads.Poll().First.IsDown(PadButton.LeftTrigger));
        device.Axis(AbsZ, 0);
        Assert.False(pads.Poll().First.IsDown(PadButton.LeftTrigger));
    }

    [Fact]
    public void TheRawValueIsKeptAlongsideTheShapedOne()
    {
        var device = Pad();
        var pads = new Pads(new RecordedPads([device]));
        device.Axis(AbsX, 3000);
        var state = pads.Poll().First;
        Assert.Equal(0, state.Axis(PadAxis.LeftX));
        Assert.True(state.RawAxis(PadAxis.LeftX) > 0);
    }

    [Fact]
    public void TheHatIsTheDpadAsFourButtons()
    {
        var device = Pad();
        var pads = new Pads(new RecordedPads([device]));

        device.Axis(AbsHat0Y, -1);
        var state = pads.Poll().First;
        Assert.True(state.IsDown(PadButton.DpadUp));
        Assert.Equal(1, state.Dpad.Y);

        device.Axis(AbsHat0Y, 0);
        device.Axis(AbsHat0X, 1);
        state = pads.Poll().First;
        Assert.False(state.IsDown(PadButton.DpadUp));
        Assert.True(state.IsDown(PadButton.DpadRight));
        Assert.Equal(1, state.Dpad.X);
    }

    [Fact]
    public void APadAlreadyHeldWhenTheGameStartsIsNotMissed()
    {
        var device = Pad(held: [BtnSouth]);
        var pads = new Pads(new RecordedPads([device]));
        Assert.True(pads.Poll().First.IsDown(PadButton.South));
    }

    [Fact]
    public void ArrivingAndLeavingAreEvents()
    {
        var backend = new RecordedPads();
        var pads = new Pads(backend);
        Assert.Equal(0, pads.Poll().Count);

        var device = Pad();
        backend.Attach(device);
        var arrived = pads.Poll();
        Assert.Single(arrived.Events.OfType<PadConnected>());
        Assert.Equal(1, arrived.Count);

        backend.Detach(device);
        var left = pads.Poll();
        Assert.Single(left.Events.OfType<PadDisconnected>());
        Assert.Equal(0, left.Count);
    }

    [Fact]
    public void APadThatVanishesReadsAsNeutralNotAsWhateverItHeld()
    {
        var backend = new RecordedPads();
        var device = Pad();
        backend.Attach(device);
        var pads = new Pads(backend);

        device.Button(BtnSouth, down: true);
        device.Axis(AbsX, 32767);
        var holding = pads.Poll().First;
        Assert.True(holding.IsDown(PadButton.South));
        Assert.True(holding.Stick(PadStick.Left).X > 0.9);

        backend.Detach(device);
        var gone = pads.Poll().Pad(0);
        Assert.False(gone.Connected);
        Assert.False(gone.IsDown(PadButton.South));
        Assert.Equal(0, gone.Stick(PadStick.Left).Length);
        Assert.Equal(0, gone.Trigger(PadSide.Left));
    }

    [Fact]
    public void APadPulledWhileHoldingAButtonLetsItGoFirst()
    {
        var backend = new RecordedPads();
        var device = Pad();
        backend.Attach(device);
        var pads = new Pads(backend);
        device.Button(BtnSouth, down: true);
        pads.Poll();

        backend.Detach(device);
        var events = pads.Poll().Events;

        Assert.Equal([typeof(PadReleased), typeof(PadDisconnected)], events.Select(e => e.GetType()));
        Assert.Equal(PadButton.South, ((PadReleased)events[0]).Button);
    }

    [Fact]
    public void APadThatComesBackGetsItsOwnPlayerNumberAgain()
    {
        var backend = new RecordedPads();
        var one = Pad(path: "port-a");
        var two = Pad(path: "port-b");
        backend.Attach(one);
        backend.Attach(two);
        var pads = new Pads(backend);
        pads.Poll();
        Assert.Equal("port-a", pads.Poll().Pad(0).Identity.Path);

        backend.Detach(one);
        pads.Poll();
        Assert.False(pads.Poll().Pad(0).Connected);
        Assert.Equal("port-b", pads.Poll().Pad(1).Identity.Path);

        backend.Attach(Pad(path: "port-a"));
        var back = pads.Poll();
        Assert.Equal("port-a", back.Pad(0).Identity.Path);
        Assert.Equal("port-b", back.Pad(1).Identity.Path);
    }

    [Fact]
    public void APadWithASerialNumberIsFollowedBetweenPorts()
    {
        var backend = new RecordedPads();
        var wireless = Pad(uniq: "aa:bb:cc", path: "dongle-1");
        backend.Attach(wireless);
        var pads = new Pads(backend);
        pads.Poll();
        backend.Detach(wireless);
        pads.Poll();

        backend.Attach(Pad(uniq: "aa:bb:cc", path: "dongle-2"));
        Assert.True(pads.Poll().Pad(0).Connected);
        Assert.Equal("aa:bb:cc", pads.Poll().Pad(0).Identity.Uniq);
    }

    [Fact]
    public void MorePadsThanThereAreSlotsForAreIgnoredNotShuffledIn()
    {
        var backend = new RecordedPads();
        var pads = new Pads(backend, maxPads: 2);
        foreach (var port in new[] { "a", "b", "c" })
        {
            backend.Attach(Pad(path: $"port-{port}"));
        }

        var frame = pads.Poll();
        Assert.Equal(2, frame.Count);
        Assert.Equal("port-a", frame.Pad(0).Identity.Path);
        Assert.Equal("port-b", frame.Pad(1).Identity.Path);
    }

    [Fact]
    public void StateIsAskedForAgainRatherThanCarriedOnFrom()
    {
        var device = Pad(held: [BtnStart]);
        var pads = new Pads(new RecordedPads([device]));

        device.Button(BtnSouth, down: true);
        var before = pads.Poll().First;
        Assert.True(before.IsDown(PadButton.South));
        Assert.True(before.IsDown(PadButton.Start));

        // South is let go of, and the record saying so is among what the kernel threw away. The pad
        // is now holding start alone while everything tracked up here still says both.
        device.Drift(buttons: [BtnStart]);
        device.Send(Evdev.Syn, Evdev.SynDropped, 0);
        var after = pads.Poll().First;
        Assert.False(after.IsDown(PadButton.South));
        Assert.True(after.IsDown(PadButton.Start));
    }

    [Fact]
    public void TheResyncReportsTheDifferenceAsOrdinaryEvents()
    {
        var device = Pad(held: [BtnStart]);
        var pads = new Pads(new RecordedPads([device]));
        device.Button(BtnSouth, down: true);
        pads.Poll();

        device.Drift(buttons: [BtnStart]);
        device.Send(Evdev.Syn, Evdev.SynDropped, 0);
        var frame = pads.Poll();
        Assert.Contains(PadButton.South, frame.Events.OfType<PadReleased>().Select(e => e.Button));
    }

    [Fact]
    public void NoPadsIsAStateNotAFailure()
    {
        var pads = new Pads(NoPads.Instance);
        var frame = pads.Poll();
        Assert.Equal(0, frame.Count);
        Assert.False(frame.First.Connected);
        Assert.False(frame.First.IsDown(PadButton.South));
        Assert.False(frame.Pad(99).Connected);
        Assert.False(frame.IsActive);
    }

    [Fact]
    public void FirstIsTheLowestConnectedPad()
    {
        var backend = new RecordedPads();
        var pads = new Pads(backend);
        backend.Attach(Pad(path: "a"));
        backend.Attach(Pad(path: "b"));
        pads.Poll();
        Assert.Equal(0, pads.Poll().First.Slot);
    }

    [Fact]
    public void ATruncatedReadDoesNotProduceAWrongEvent()
    {
        var device = Pad();
        var pads = new Pads(new RecordedPads([device]));
        pads.Poll();

        var whole = Evdev.Encode(Evdev.Key, BtnSouth, 1);
        device.SendBytes(whole[..12]);
        Assert.False(pads.Poll().First.IsDown(PadButton.South));

        device.SendBytes(whole[12..]);
        Assert.True(pads.Poll().First.IsDown(PadButton.South));
    }

    [Fact]
    public void ClosingLetsGoOfEverything()
    {
        var device = Pad();
        var pads = new Pads(new RecordedPads([device]));
        pads.Poll();
        pads.Close();
        Assert.False(device.IsAlive);
    }

    [Fact]
    public void RumbleGoesToThePadInTheSlotClampedToWhatAMotorTakes()
    {
        var device = Pad(canRumble: true);
        var pads = new Pads(new RecordedPads([device]));
        pads.Poll();

        Assert.True(pads.CanRumble(0));
        pads.Rumble(0, strong: 2, weak: double.NaN, duration: TimeSpan.FromMilliseconds(-5));

        Assert.Equal([(1.0, 0.0, TimeSpan.Zero)], device.Rumbles);
    }

    [Fact]
    public void RumbleSaysSoWhenThePadCannotAndDoesNothingForAnEmptySlot()
    {
        var device = Pad();
        var pads = new Pads(new RecordedPads([device]));
        pads.Poll();

        Assert.False(pads.CanRumble(0));
        Assert.False(pads.CanRumble(3));
        pads.Rumble(0, strong: 1, weak: 1, duration: TimeSpan.FromSeconds(1));
        pads.Rumble(3, strong: 1, weak: 1, duration: TimeSpan.FromSeconds(1));
        Assert.Empty(device.Rumbles);
    }
}
