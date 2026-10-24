namespace Orblit.Input.Tests;

/// <summary>
/// The read path, end to end, against a gamepad the kernel invented. uinput creates a real event
/// device, the kernel delivers real records through a real device node, and <see cref="LinuxPads"/>
/// opens it, classifies it from sysfs, asks the driver for its axis ranges and reads it. Every step
/// is the same as for hardware.
/// </summary>
/// <remarks>
/// Skipped wherever the machine cannot make a device, which is most places. A container's
/// <c>/dev</c> is a tmpfs that devtmpfs never populates, so the node is created and no file shows
/// up for it. <c>tool/check_input_linux.sh</c> mounts a real devtmpfs and names it in
/// <c>ORBLIT_INPUT_DEV</c>. One class, so xunit runs these one at a time. Two virtual pads at once
/// would count each other.
/// </remarks>
public class LinuxUinputTests
{
    private const int BtnSouth = 0x130;
    private const int AbsY = 0x01;
    private const int AbsZ = 0x02;
    private const int AbsHat0Y = 0x11;

    [Fact(Skip = LinuxRig.SkipReason, SkipUnless = nameof(LinuxRig.Available), SkipType = typeof(LinuxRig))]
    public async Task AVirtualPadIsFoundAndIsThePadWeMade()
    {
        using var rig = await Rig.Start();

        var frame = await Until(rig.Pads, frame => frame.Count > 0);
        Assert.Equal(1, frame.Count);

        var state = frame.First;
        Assert.True(state.Connected);
        Assert.Equal("Orblit Virtual Pad", state.Identity.Name);
        Assert.Equal(0x28de, state.Identity.Vendor);
        Assert.Equal(0x1205, state.Identity.Product);
        Assert.Single(frame.Events.OfType<PadConnected>());
    }

    [Fact(Skip = LinuxRig.SkipReason, SkipUnless = nameof(LinuxRig.Available), SkipType = typeof(LinuxRig))]
    public async Task AButtonIsReportedThroughTheRealDeviceNode()
    {
        using var rig = await Rig.Start();
        await Until(rig.Pads, frame => frame.Count > 0);

        rig.Pad.Button(BtnSouth, down: true);
        var down = await Until(rig.Pads, frame => frame.First.IsDown(PadButton.South));
        Assert.True(down.First.IsDown(PadButton.South));

        rig.Pad.Button(BtnSouth, down: false);
        var up = await Until(rig.Pads, frame => !frame.First.IsDown(PadButton.South));
        Assert.False(up.First.IsDown(PadButton.South));
    }

    [Fact(Skip = LinuxRig.SkipReason, SkipUnless = nameof(LinuxRig.Available), SkipType = typeof(LinuxRig))]
    public async Task AStickIsReadWithUpPositive()
    {
        using var rig = await Rig.Start();
        await Until(rig.Pads, frame => frame.Count > 0);

        // evdev says positive is down and this engine says up.
        rig.Pad.Axis(AbsY, -32768);
        var pushed = await Until(rig.Pads, frame => frame.First.Stick(PadStick.Left).Y > 0.5);
        Assert.True(pushed.First.Stick(PadStick.Left).Y > 0.9);

        rig.Pad.Axis(AbsY, 0);
        var centred = await Until(rig.Pads, frame => frame.First.Stick(PadStick.Left).Length < 0.1);
        Assert.Equal(0.0, centred.First.Stick(PadStick.Left).Length);
    }

    [Fact(Skip = LinuxRig.SkipReason, SkipUnless = nameof(LinuxRig.Available), SkipType = typeof(LinuxRig))]
    public async Task ATriggerIsReadAgainstTheRangeTheDriverReported()
    {
        using var rig = await Rig.Start();
        await Until(rig.Pads, frame => frame.Count > 0);

        // Fully pulled on a trigger that is a byte wide.
        rig.Pad.Axis(AbsZ, 255);
        var pulled = await Until(rig.Pads, frame => frame.First.Trigger(PadSide.Left) > 0.5);
        Assert.Equal(1.0, pulled.First.Trigger(PadSide.Left), 1e-6);
        Assert.True(pulled.First.IsDown(PadButton.LeftTrigger));
    }

    [Fact(Skip = LinuxRig.SkipReason, SkipUnless = nameof(LinuxRig.Available), SkipType = typeof(LinuxRig))]
    public async Task TheDpadArrivesAsButtonsAndNotAsAnAxis()
    {
        using var rig = await Rig.Start();
        await Until(rig.Pads, frame => frame.Count > 0);

        rig.Pad.Axis(AbsHat0Y, -1);
        var up = await Until(rig.Pads, frame => frame.First.IsDown(PadButton.DpadUp));
        Assert.True(up.First.IsDown(PadButton.DpadUp));
        Assert.Equal(1.0, up.First.Dpad.Y);
    }

    [Fact(Skip = LinuxRig.SkipReason, SkipUnless = nameof(LinuxRig.Available), SkipType = typeof(LinuxRig))]
    public async Task UnpluggingItMidPressLeavesNothingHeld()
    {
        using var rig = await Rig.Start();
        await Until(rig.Pads, frame => frame.Count > 0);
        rig.Pad.Button(BtnSouth, down: true);
        await Until(rig.Pads, frame => frame.First.IsDown(PadButton.South));

        // The battery dies with the button down.
        rig.Pad.Dispose();

        var gone = await Until(rig.Pads, frame => frame.Count == 0);
        Assert.Equal(0, gone.Count);
        Assert.False(gone.Pad(0).IsDown(PadButton.South));
        Assert.Single(gone.Events.OfType<PadDisconnected>());
    }

    [Fact(Skip = LinuxRig.SkipReason, SkipUnless = nameof(LinuxRig.Available), SkipType = typeof(LinuxRig))]
    public async Task ASecondPadGetsTheSecondSlot()
    {
        using var one = VirtualPad.Create("Orblit Virtual Pad One");
        var pads = new Pads(new LinuxPads(LinuxRig.DeviceDirectory));
        try
        {
            await Until(pads, frame => frame.Count > 0);

            using var two = VirtualPad.Create("Orblit Virtual Pad Two");
            var both = await Until(pads, frame => frame.Count > 1);
            Assert.Equal(2, both.Count);
            Assert.True(both.Pad(0).Connected);
            Assert.True(both.Pad(1).Connected);
        }
        finally
        {
            pads.Close();
        }
    }

    // uinput delivers through the kernel, so a write and the read that sees it are not in the same
    // instant.
    private static Task Settle() => Task.Delay(TimeSpan.FromMilliseconds(120), TestContext.Current.CancellationToken);

    // Polls until the test holds or the time runs out, so a slow container does not turn into a
    // flake.
    private static async Task<PadFrame> Until(Pads pads, Func<PadFrame, bool> test)
    {
        var frame = pads.Poll();
        for (var i = 0; i < 50 && !test(frame); i++)
        {
            await Settle();
            frame = pads.Poll();
        }

        return frame;
    }

    private sealed class Rig : IDisposable
    {
        private Rig(VirtualPad pad)
        {
            Pad = pad;
            Pads = new Pads(new LinuxPads(LinuxRig.DeviceDirectory));
        }

        public VirtualPad Pad { get; }

        public Pads Pads { get; }

        public static async Task<Rig> Start()
        {
            var rig = new Rig(VirtualPad.Create());
            await Settle();
            return rig;
        }

        public void Dispose()
        {
            Pads.Close();
            Pad.Dispose();
        }
    }
}
